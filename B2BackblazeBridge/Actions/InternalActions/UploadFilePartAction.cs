/* 
 * Copyright (c) 2015 Andrew Johnson
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of 
 * this software and associated documentation files (the "Software"), to deal in 
 * the Software without restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the
 * Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN
 * AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using B2BackblazeBridge.Core;
using B2BackblazeBridge.Processing;
using Functional.Maybe;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace B2BackblazeBridge.Actions.InternalActions
{
    internal sealed class UploadFilePartAction : BaseAction<UploadFilePartResponse>
    {
        #region private fields
        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly long _filePartNumber;
        private readonly int _maxUploadAttempts;
        private readonly GetUploadPartURLResponse _getUploadPartUrl;
        private readonly byte[] _rawBytes;
        private readonly string _sha1;
        private readonly Action<TimeSpan> _exponentialBackoffCallback;
        #endregion

        #region ctor
        public UploadFilePartAction(
            BackblazeB2AuthorizationSession authorizationSession,
            CancellationToken cancellationToken,
            string bucketID,
            long filePart,
            int maxUploadAttempts,
            GetUploadPartURLResponse getUploadPartUrl,
            byte[] rawBytes,
            string sha1,
            Action<TimeSpan> exponentialBackoffCallback
        ) : base(cancellationToken)
        {
            _authorizationSession = authorizationSession;
            _bucketID = bucketID;
            _filePartNumber = filePart;
            _maxUploadAttempts = maxUploadAttempts;
            _getUploadPartUrl = getUploadPartUrl;
            _rawBytes = rawBytes;
            _sha1 = sha1;
            _exponentialBackoffCallback = exponentialBackoffCallback;
        }
        #endregion

        #region public methods
        public override BackblazeB2ActionResult<UploadFilePartResponse> Execute()
        {
            // Loop because we want to retry to upload the file part should it fail for
            // recoverable reasons. We will break out of this loop should the upload succeed
            // or throw an exception should we determine we can't upload to the server
            int attemptNumber = 0;
            while (true)
            {
                // Throw an exception is we are being interrupted
                CancellationToken.ThrowIfCancellationRequested();

                // Sleep the current thread so that we can give the server some time to recover
                if (attemptNumber > 0)
                {
                    TimeSpan backoffSleepTime = CalculateExponentialBackoffSleepTime(attemptNumber);
                    _exponentialBackoffCallback(backoffSleepTime);
                    Thread.Sleep(backoffSleepTime);
                }

                HttpWebRequest webRequest = GetHttpWebRequest(_getUploadPartUrl.UploadURL);
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", _getUploadPartUrl.AuthorizationToken);
                webRequest.Headers.Add("X-Bz-Part-Number", _filePartNumber.ToString());
                webRequest.Headers.Add("X-Bz-Content-Sha1", _sha1);
                webRequest.ContentLength = _rawBytes.Length;

                attemptNumber++;

                BackblazeB2ActionResult<UploadFilePartResponse> uploadResponse = SendWebRequestAndDeserialize(webRequest, _rawBytes);
                if (uploadResponse.HasResult)
                {
                    // Verify result
                    UploadFilePartResponse unwrappedResponse = uploadResponse.MaybeResult.Value;
                    if (
                        unwrappedResponse.ContentLength != _rawBytes.Length ||
                        unwrappedResponse.ContentSHA1.Equals(_sha1, StringComparison.Ordinal) == false ||
                        unwrappedResponse.PartNumber != _filePartNumber
                    )
                    {
                        return new BackblazeB2ActionResult<UploadFilePartResponse>(
                            Maybe<UploadFilePartResponse>.Nothing,
                            new BackblazeB2ActionErrorDetails
                            {
                                Code = "CUSTOM_ERROR",
                                Message = string.Format("File part number {0} uploaded successfully but could not be verified", _filePartNumber),
                                Status = -1,
                            }
                        );
                    }
                    else
                    {
                        return uploadResponse;
                    }
                }
                else if (uploadResponse.Errors.Any(e => e.Status >= 500 && e.Status < 600) && attemptNumber < _maxUploadAttempts)
                {
                    continue;
                }
                else
                {
                    return uploadResponse;
                }
            }
        }
        #endregion
    }
}