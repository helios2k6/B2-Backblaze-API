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
using System.Threading;
using B2BackblazeBridge.Core;
using B2BackblazeBridge.Processing;
using Newtonsoft.Json;

namespace B2BackblazeBridge.Actions.InternalActions
{
    internal sealed class StartLargeFileAction : BaseAction<StartLargeFileResponse>
    {
        #region private fields
        private static readonly string StartLargeFileURL = "/b2api/v1/b2_start_large_file";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly string _fileDestination;
        #endregion
        
        public StartLargeFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID,
            string fileDestination
        ) : base(CancellationToken.None)
        {
            _authorizationSession = authorizationSession;
            _bucketID = bucketID;
            _fileDestination = fileDestination;
        }

        public override BackblazeB2ActionResult<StartLargeFileResponse> Execute()
        {
            StartLargeFileRequest request = new StartLargeFileRequest
            {
                BucketID = _bucketID,
                ContentType = "b2/x-auto",
                FileName = GetSafeFileName(_fileDestination),
            };

            byte[] jsonBodyBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + StartLargeFileURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = jsonBodyBytes.Length;

            return SendWebRequestAndDeserialize(webRequest, jsonBodyBytes);
        }
    }
}