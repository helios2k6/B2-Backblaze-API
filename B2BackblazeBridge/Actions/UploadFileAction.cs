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
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents uploading a single file to B2 using only a single connection
    /// </summary>
    public sealed class UploadFileAction : BaseAction<BackblazeB2UploadFileResult>
    {
        #region private classes
        private sealed class GetUploadFileURLResult
        {
            public string AuthorizationToken { get; set; }

            public string BucketID { get; set; }

            public string UploadURL { get; set; }
        }
        #endregion

        #region private fields
        private static readonly string GetUploadURIURI = "/b2api/v1/b2_get_upload_url";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly string _filePath;
        #endregion

        #region ctor
        /// <summary>
        /// Construct an UploadFileAction
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="filePath">The path to the file to upload</param>
        /// <param name="bucketID">The Bucket ID to upload to</param>
        public UploadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string filePath,
            string bucketID
        ) : base()
        {
            if (File.Exists(filePath) == false)
            {
                throw new ArgumentException("filePath");
            }

            _authorizationSession = authorizationSession ?? throw new ArgumentNullException("The authorization session object must not be null");
            _bucketID = bucketID;
            _filePath = filePath;
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2UploadFileResult> ExecuteAsync()
        {
            return await UploadFileAsync(await GetUploadURLAsync());
        }
        #endregion

        #region private methods
        private async Task<BackblazeB2UploadFileResult> UploadFileAsync(GetUploadFileURLResult getUploadFileUrlResult)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(_filePath);
                string sha1Hash = ComputeSHA1Hash(fileBytes);
                FileInfo info = new FileInfo(_filePath);
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(getUploadFileUrlResult.UploadURL);
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", getUploadFileUrlResult.AuthorizationToken);
                webRequest.Headers.Add("X-Bz-File-Name", GetSafeFileName(_filePath));
                webRequest.Headers.Add("X-Bz-Content-Sha1", sha1Hash);
                webRequest.ContentType = "b2/x-auto";
                webRequest.ContentLength = info.Length;

                return await SendWebRequestAndDeserialize<BackblazeB2UploadFileResult>(webRequest, fileBytes);
            }
            catch (BaseActionWebRequestException ex)
            {
                throw new UploadFileActionException(ex.StatusCode, ex.Details);
            }
        }

        private async Task<GetUploadFileURLResult> GetUploadURLAsync()
        {
            try
            {
                HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + GetUploadURIURI, true);
                webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
                webRequest.Method = "POST";

                string body = "{\"bucketId\":\"" + _bucketID + "\"}";
                byte[] encodedBody = Encoding.UTF8.GetBytes(body);
                webRequest.ContentLength = encodedBody.Length;
                return DecodeGetUploadURLJson(await SendWebRequestAndDeserializeAsDictionaryAsync(webRequest, encodedBody));
            }
            catch (BaseActionWebRequestException ex)
            {
                throw new UploadFileActionException(ex.StatusCode, ex.Details);
            }
        }

        private GetUploadFileURLResult DecodeGetUploadURLJson(Dictionary<string, dynamic> jsonPayload)
        {
            return new GetUploadFileURLResult
            {
                AuthorizationToken = jsonPayload["authorizationToken"],
                BucketID = jsonPayload["bucketId"],
                UploadURL = jsonPayload["uploadUrl"],
            };
        }
        #endregion
    }
}