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
using System.Net;
using System.Text;
using System.Threading;

namespace B2BackblazeBridge.Actions.InternalActions
{
    internal sealed class GetUploadPartURLAction : BaseAction<GetUploadPartURLResponse>
    {
        #region private fields
        private static readonly string GetUploadPartURLURL = "/b2api/v1/b2_get_upload_part_url";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly string _fileID;
        #endregion

        #region ctor
        public GetUploadPartURLAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID,
            string fileID
        ) : base(CancellationToken.None)
        {
            _authorizationSession = authorizationSession;
            _bucketID = bucketID;
            _fileID = fileID;
        }
        #endregion

        #region public methods
        public override BackblazeB2ActionResult<GetUploadPartURLResponse> Execute()
        {
            byte[] jsonPayloadBytes = Encoding.UTF8.GetBytes("{\"fileId\":\"" + _fileID + "\"}");
            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + GetUploadPartURLURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = jsonPayloadBytes.Length;

            return SendWebRequestAndDeserialize(webRequest, jsonPayloadBytes);
        }
        #endregion
    }
}