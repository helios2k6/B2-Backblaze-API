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
    /// <summary>
    /// An internal class that will fetch the URL to upload to
    /// </summary>
    internal sealed class GetUploadFileURLAction : BaseAction<GetUploadFileURLResponse>
    {
        #region private fields
        private static readonly string GetUploadURIURI = "/b2api/v1/b2_get_upload_url";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        #endregion

        public GetUploadFileURLAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID
        ) : base(CancellationToken.None)
        {
            _authorizationSession = authorizationSession;
            _bucketID = bucketID;
        }

        public override BackblazeB2ActionResult<GetUploadFileURLResponse> Execute()
        {
            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + GetUploadURIURI);
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.Method = "POST";

            string body = "{\"bucketId\":\"" + _bucketID + "\"}";
            byte[] encodedBody = Encoding.UTF8.GetBytes(body);
            webRequest.ContentLength = encodedBody.Length;
            return SendWebRequestAndDeserialize(webRequest, encodedBody);
        }
    }
}