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
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
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
        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly string _filePath;
        #endregion

        #region ctor
        public UploadFileAction(string filePath, string bucketID, BackblazeB2AuthorizationSession authorizationSession) : base()
        {
            _authorizationSession = authorizationSession;
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
            string sha1Hash = ComputeSHA1Hash();
            FileInfo info = new FileInfo(_filePath);
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(getUploadFileUrlResult.UploadURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", getUploadFileUrlResult.AuthorizationToken);
            webRequest.Headers.Add("X-Bz-File-Name", _filePath);
            webRequest.Headers.Add("X-Bz-Content-Sha1", sha1Hash);
            webRequest.ContentType = "b2/x-auto";
            webRequest.ContentLength = info.Length;
            using (Stream uploadStream = await webRequest.GetRequestStreamAsync())
            {
                byte[] allBytes = File.ReadAllBytes(_filePath);
                await uploadStream.WriteAsync(allBytes, 0, allBytes.Length);
                uploadStream.Close();
            }

            using (HttpWebResponse response = await webRequest.GetResponseAsync() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new UploadFileActionException(response.StatusCode);
                }
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    return DecodeUploadFileResponse(await streamReader.ReadToEndAsync());
                }
            }
        }

        private string ComputeSHA1Hash()
        {
            using (FileStream fileStream = new FileStream(_filePath, FileMode.Open))
            using (SHA1 shaHash = SHA1.Create())
            {
                byte[] hashBytes = shaHash.ComputeHash(fileStream);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private BackblazeB2UploadFileResult DecodeUploadFileResponse(string jsonPayload)
        {
            Dictionary<string, dynamic> decodedJsonPayload = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonPayload);
            return new BackblazeB2UploadFileResult
            {
                AccountID = decodedJsonPayload["accountId"],
                BucketID = decodedJsonPayload["bucketId"],
                ContentLength = decodedJsonPayload["contentLength"],
                ContentSHA1 = decodedJsonPayload["contentSha1"],
                FileID = decodedJsonPayload["fileId"],
                FileName = decodedJsonPayload["fileName"],
                UploadTimeStamp = decodedJsonPayload["uploadTimestamp"],
            };
        }

        private async Task<GetUploadFileURLResult> GetUploadURLAsync()
        {
            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL);
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.Method = "POST";

            string body = "{\"bucketId\":\"" + _bucketID + "\"}";
            byte[] encodedBody = Encoding.UTF8.GetBytes(body);
            webRequest.ContentLength = encodedBody.Length;

            using (Stream webrequestStream = await webRequest.GetRequestStreamAsync())
            {
                await webrequestStream.WriteAsync(encodedBody, 0, encodedBody.Length);
                webrequestStream.Close();
                using (HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new UploadFileActionException(response.StatusCode);
                    }

                    using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        return DecodeGetUploadURLJson(await streamReader.ReadToEndAsync());
                    }
                }
            }
        }

        private GetUploadFileURLResult DecodeGetUploadURLJson(string jsonPayload)
        {
            Dictionary<string, dynamic> decodedJsonPayload = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonPayload);
            return new GetUploadFileURLResult
            {
                AuthorizationToken = decodedJsonPayload["authorizationToken"],
                BucketID = decodedJsonPayload["bucketId"],
                UploadURL = decodedJsonPayload["uploadUrl"],
            };
        }
        #endregion
    }
}