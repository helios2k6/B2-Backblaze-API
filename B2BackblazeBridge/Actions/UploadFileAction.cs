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
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents uploading a single file to B2 using only a single connection
    /// </summary>
    public sealed class UploadFileAction : BaseAction<BackblazeB2UploadFileResult>
    {
        #region private fields
        private static readonly string GetUploadURIURI = "/b2api/v1/b2_get_upload_url";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly byte[] _bytesToUpload;
        private readonly string _fileDestination;
        #endregion

        #region ctor
        /// <summary>
        /// Construct an UploadFileAction using the provided bytes
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="bytesToUpload">The bytes to upload</param>
        /// <param name="fileDestination">The remote file path to upload to</param>
        /// <param name="bucketID">The Bucket ID to upload to</param>
        public UploadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID,
            byte[] bytesToUpload,
            string fileDestination
        ) : base(CancellationToken.None)
        {
            _authorizationSession = authorizationSession ?? throw new ArgumentNullException("The authorization session object must not be null");
            _bucketID = bucketID;
            _bytesToUpload = bytesToUpload;
            _fileDestination = fileDestination;
        }

        /// <summary>
        /// Construct an UploadFileAction
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="filePath">The path to the file to upload</param>
        /// <param name="fileDestination">The remote file path to upload to</param>
        /// <param name="bucketID">The Bucket ID to upload to</param>
        public UploadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string filePath,
            string fileDestination,
            string bucketID
        ) : this(authorizationSession, bucketID, File.ReadAllBytes(filePath), fileDestination)
        {
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2ActionResult<BackblazeB2UploadFileResult>> ExecuteAsync()
        {
            return await UploadFileAsync(await GetUploadURLAsync());
        }
        #endregion

        #region private methods
        private async Task<BackblazeB2ActionResult<BackblazeB2UploadFileResult>> UploadFileAsync(BackblazeB2ActionResult<GetUploadFileURLResponse> getUploadFileUrlResult)
        {
            if (getUploadFileUrlResult.HasResult)
            {
                GetUploadFileURLResponse unwrappedResult = getUploadFileUrlResult.MaybeResult.Value;

                string sha1Hash = ComputeSHA1Hash(_bytesToUpload);
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(unwrappedResult.UploadURL);
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", unwrappedResult.AuthorizationToken);
                webRequest.Headers.Add("X-Bz-File-Name", GetSafeFileName(_fileDestination));
                webRequest.Headers.Add("X-Bz-Content-Sha1", sha1Hash);
                webRequest.ContentType = "b2/x-auto";
                webRequest.ContentLength = _bytesToUpload.Length;

                BackblazeB2ActionResult<BackblazeB2UploadFileResult> result = await SendWebRequestAndDeserializeAsync<BackblazeB2UploadFileResult>(webRequest, _bytesToUpload);
                result.MaybeResult.Do(t => t.FileName = Uri.UnescapeDataString(t.FileName));
                return result;
            }
            else
            {
                return new BackblazeB2ActionResult<BackblazeB2UploadFileResult>(Maybe<BackblazeB2UploadFileResult>.Nothing, getUploadFileUrlResult.Errors);
            }
        }

        private async Task<BackblazeB2ActionResult<GetUploadFileURLResponse>> GetUploadURLAsync()
        {
            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + GetUploadURIURI);
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.Method = "POST";

            string body = "{\"bucketId\":\"" + _bucketID + "\"}";
            byte[] encodedBody = Encoding.UTF8.GetBytes(body);
            webRequest.ContentLength = encodedBody.Length;
            return await SendWebRequestAndDeserializeAsync<GetUploadFileURLResponse>(webRequest, encodedBody);
        }
        #endregion
    }
}