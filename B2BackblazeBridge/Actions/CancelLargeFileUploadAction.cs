/* 
 * Copyright (c) 2023 Andrew Johnson
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
using System.Net;
using System.Text;
using System.Threading;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents a request to B2 to cancel the uploading of a large file. Generally speaking, this should
    /// not be used by consumers because it's already used internally during cancellation events. 
    /// </summary>
    public sealed class CancelLargeFileUploadAction : BaseAction<BackblazeB2CancelLargeFileUploadResult>
    {
        #region private fields
        private readonly string APIURL = "/b2api/v1/b2_cancel_large_file";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _fileId;
        #endregion

        #region public ctor
        /// <summary>
        /// Construct a new cancellation request for a large file upload
        /// </summary>
        /// <param name="authorizationSession">The authorization session to use</param>
        /// <param name="fileId">The file to cancel the upload of</param>
        public CancelLargeFileUploadAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string fileId
        ) : base(CancellationToken.None)
        {
            _authorizationSession = authorizationSession;
            _fileId = fileId;
        }
        #endregion

        #region public methods
        public override BackblazeB2ActionResult<BackblazeB2CancelLargeFileUploadResult> Execute()
        {
            string getUploadUrlJsonStr = "{\"fileId\":\"" + _fileId + "\"}";
            byte[] payload = Encoding.UTF8.GetBytes(getUploadUrlJsonStr);

            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + APIURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = payload.Length;

            return SendWebRequestAndDeserialize(webRequest, payload);
        }
        #endregion
    }
}