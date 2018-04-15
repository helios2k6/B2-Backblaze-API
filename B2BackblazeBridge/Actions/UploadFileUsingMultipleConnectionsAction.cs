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

using B2BackblazeBridge.Connection;
using B2BackblazeBridge.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents uploading a single file using multiple connections in parallel
    /// </summary>
    public sealed class UploadFileUsingMultipleConnectionsActions : BaseAction<BackblazeB2UploadFileResult>
    {
        #region inner classes
        [Serializable]
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class StartLargeFileRequest
        {
            [JsonProperty(PropertyName = "bucketId")]
            public string BucketID { get; set;  }

            [JsonProperty(PropertyName = "fileName")]
            public string FileName { get; set; }

            [JsonProperty(PropertyName = "contentType")]
            public string ContentType { get; set; }
        }

        private sealed class GetUploadPartURLResponse
        {
            public string AuthorizationToken { get; set; }

            public string UploadURL { get; set; }
        }

        private sealed class UploadPartJob
        {
            public long ContentLength { get; set; }

            public long FileCursorPosition { get; set; }

            public int FilePartNumber { get; set; }

            public string SHA1 { get; set; }
        }
        #endregion

        #region private fields
        private static readonly string StartLargeFileURL = "/b2api/v1/b2_start_large_file";

        private static readonly string GetUploadPartURLURL = "/b2api/v1/b2_get_upload_part_url";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;

        private readonly string _bucketID;

        private readonly string _filePath;

        private readonly int _numberOfConnections;

        private readonly int _fileChunkSizesInBytes;
        #endregion

        #region ctor
        public UploadFileUsingMultipleConnectionsActions(
            BackblazeB2AuthorizationSession authorizationSession,
            string filePath,
            string bucketID,
            int fileChunkSizesInBytes,
            int numberOfConnections
        )
        {
            if (File.Exists(filePath) == false)
            {
                throw new ArgumentException(string.Format("{0} does not exist", filePath));
            }

            if (fileChunkSizesInBytes < 1048576)
            {
                throw new ArgumentException("The file chunk sizes must be larger than 1 mebibyte");
            }

            if (numberOfConnections < 1)
            {
                throw new ArgumentException("You must specify a positive, non-zero number of connections", "numberOfConnections");
            }

            _authorizationSession = authorizationSession ?? throw new ArgumentNullException("The authorization session object must not be mull");
            _bucketID = bucketID;
            _filePath = filePath;
            _fileChunkSizesInBytes = fileChunkSizesInBytes;
            _numberOfConnections = numberOfConnections;
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2UploadFileResult> ExecuteAsync()
        {
            IEnumerable<UploadPartJob> jobs = await GenerateUploadPartsAsync();
            throw new NotImplementedException();
        }
        #endregion

        #region private methods
        private async Task<IEnumerable<UploadPartJob>> GenerateUploadPartsAsync()
        {
            IList<UploadPartJob> jobs = new List<UploadPartJob>();
            FileInfo fileInfo = new FileInfo(_filePath);
            int numberOfChunks = (int)(fileInfo.Length / _fileChunkSizesInBytes); // We can't have more than 4 billion chunks per file. 
            for (int currentChunk = 0; currentChunk < numberOfChunks; currentChunk++)
            {
                long cursorPosition = currentChunk * _fileChunkSizesInBytes;
                jobs.Add(new UploadPartJob
                {
                    ContentLength = _fileChunkSizesInBytes,
                    FileCursorPosition = cursorPosition,
                    FilePartNumber = currentChunk,
                    SHA1 = await ComputeSHA1HashOfChunkAsync(cursorPosition, _fileChunkSizesInBytes),
                });
            }

            // There wasn't perfect division, which means we have to account for the last chunk
            long remainderChunk = fileInfo.Length % _fileChunkSizesInBytes;
            if (remainderChunk != 0)
            {
                long cursorPosition = numberOfChunks * _fileChunkSizesInBytes;
                jobs.Add(new UploadPartJob
                {
                    ContentLength = remainderChunk,
                    FileCursorPosition = numberOfChunks * _fileChunkSizesInBytes,
                    FilePartNumber = numberOfChunks,
                    SHA1 = await ComputeSHA1HashOfChunkAsync(cursorPosition, remainderChunk),
                });
            }

            return jobs;
        }

        private async Task<string> ComputeSHA1HashOfChunkAsync(long fileCursorPosition, long length)
        {
            using (FileStream fileStream = new FileStream(_filePath, FileMode.Open))
            using (SHA1 shaHash = SHA1.Create())
            {
                fileStream.Seek(fileCursorPosition, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                int bytesRead = await fileStream.ReadAsync(buffer, 0, (int)length);
                if (bytesRead != length)
                {
                    throw new InvalidOperationException("The number of bytes read did not equal the expected number of bytes while computing the SHA1 hash");
                }

                byte[] hashBytes = shaHash.ComputeHash(buffer);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        private async Task<long> GetFileID()
        {
            try
            {
                StartLargeFileRequest request = new StartLargeFileRequest
                {
                    BucketID = _bucketID,
                    ContentType = "b2/x-auto",
                    FileName = GetSafeFileName(_filePath),
                };

                byte[] jsonBodyBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + StartLargeFileURL);
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
                webRequest.ContentLength = jsonBodyBytes.Length;

                Dictionary<string, dynamic> jsonResponse = await SendWebRequestAsync(webRequest, jsonBodyBytes);
                return (long)jsonResponse["fileId"];
            }
            catch (BaseActionWebRequestException ex)
            {
                throw new UploadFileActionException(ex.StatusCode);
            }
        }

        private async Task<GetUploadPartURLResponse> GetUploadPartURL(long fileID)
        {
            try
            {
                byte[] jsonPayloadBytes = Encoding.UTF8.GetBytes("{\"fileId\":\"" + fileID + "\"}");
                HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + GetUploadPartURLURL);
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
                webRequest.ContentLength = jsonPayloadBytes.Length;

                Dictionary<string, dynamic> jsonResponse = await SendWebRequestAsync(webRequest, jsonPayloadBytes);
                return new GetUploadPartURLResponse
                {
                    AuthorizationToken = jsonResponse["authorizationToken"],
                    UploadURL = jsonResponse["uploadUrl"],
                };
            }
            catch (BaseActionWebRequestException ex)
            {
                throw new UploadFileActionException(ex.StatusCode);
            }
        }

        private async Task<bool> UploadFilePart(GetUploadPartURLResponse getUploadPartUrl)
        {
            try
            {

            }
            catch (BaseActionWebRequestException ex)
            {

            }
        }

        private async Task<BackblazeB2UploadFileResult> FinishUploadingLargeFile()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}