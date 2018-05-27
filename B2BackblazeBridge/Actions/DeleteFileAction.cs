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
using Newtonsoft.Json;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents an action to delete a file
    /// </summary>
    public sealed class DeleteFileAction : BaseAction<BackblazeB2DeleteFileResult>
    {
        #region private fields
        private readonly string APIURL = "/b2api/v1/b2_delete_file_version";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _fileId;
        private readonly string _fileName;
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new DeleteFileAction 
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="fileId">The file ID to delete</param>
        /// <param name="fileName">The file name to delete</param>
        public DeleteFileAction(BackblazeB2AuthorizationSession authorizationSession, string fileId, string fileName)
        {
            _authorizationSession = authorizationSession;
            _fileId = fileId;
            _fileName = fileName;
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>> ExecuteAsync()
        {
            DeleteFileVersionRequest deleteRequest = new DeleteFileVersionRequest
            {
                FileName = _fileName,
                FileId = _fileId,
            };
            string deleteRequestJson = JsonConvert.SerializeObject(deleteRequest);
            byte[] payload = Encoding.UTF8.GetBytes(deleteRequestJson);

            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + APIURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = payload.Length;

            return await SendWebRequestAndDeserializeAsync<BackblazeB2DeleteFileResult>(webRequest, payload);
        }
        #endregion
    }
}