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

using System.Net;
using System.Text;
using System.Threading.Tasks;
using B2BackblazeBridge.Core;

namespace B2BackblazeBridge.Actions
{
    public sealed class ListBucketsAction : BaseAction<BackblazeB2ListBucketsResult>
    {
        #region private fields
        private readonly string APIURL = "/b2api/v1/b2_list_buckets";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        #endregion

        #region ctor
        public ListBucketsAction(BackblazeB2AuthorizationSession authorizationSession)
        {
            _authorizationSession = authorizationSession;
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2ActionResult<BackblazeB2ListBucketsResult>> ExecuteAsync()
        {
            string body = "{\"accountId\":\"" + _authorizationSession.AccountID + "\"}";
            byte[] payload = Encoding.UTF8.GetBytes(body);

            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + APIURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = payload.Length;

            return await SendWebRequestAndDeserializeAsync<BackblazeB2ListBucketsResult>(webRequest, payload);
        }
        #endregion
    }
}