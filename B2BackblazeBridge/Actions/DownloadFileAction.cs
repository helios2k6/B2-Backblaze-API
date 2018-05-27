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

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using B2BackblazeBridge.Core;
using Functional.Maybe;
using Newtonsoft.Json;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents a download file action, either by ID or by file name
    /// </summary>
    public sealed class DownloadFileAction : BaseAction<BackblazeB2DownloadFileResult>
    {
        #region inner classes
        /// <summary>
        /// The type of identifier to use when looking up the file to download
        /// </summary>
        public enum IdentifierType
        {
            /// <summary>
            /// Download by using the unique file ID
            /// </summary>
            ID,
            /// <summary>
            /// Download by using the name of the file
            /// </summary>
            Name,
        }
        #endregion

        #region private fields
        private static readonly string DownloadByIDURL = "/api/b2_download_file_by_id";

        private static readonly int BufferSize = 64 * 1024; // 64 kibibytes

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        #endregion

        #region public properties
        /// <summary>
        /// The identifier type that will be used to download the file
        /// </summary>
        public IdentifierType DownloadIdentifierType { get; }

        /// <summary>
        /// The identifier to use when downloading a file
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// The file path to download to
        /// </summary>
        public string FileDestination { get; }
        #endregion

        #region ctor
        /// <summary>
        /// Construct a DownloadFileAction with the given identifier and identifier type
        /// </summary>
        /// <param name="authorizationSession">The authorization session to use</param>
        /// <param name="fileDestination">The file path to download to</param>
        /// <param name="identifier">The identifier</param>
        /// <param name="downloadIdentifierType">The type of identifier</param>
        private DownloadFileAction(BackblazeB2AuthorizationSession authorizationSession, string fileDestination, string identifier, IdentifierType downloadIdentifierType)
        {
            if (File.Exists(fileDestination))
            {
                throw new ArgumentException(string.Format("File already exists: {0}", fileDestination));
            }

            _authorizationSession = authorizationSession;
            FileDestination = fileDestination;
            DownloadIdentifierType = downloadIdentifierType;
            Identifier = identifier;
        }

        /// <summary>
        /// Construct a DownloadFileAction with the given file ID
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="fileDestination">The file path to download to</param>
        /// <param name="fileID">The file ID to download</param>
        public DownloadFileAction(BackblazeB2AuthorizationSession authorizationSession, string fileDestination, string fileID)
            : this(authorizationSession, fileDestination, fileID, IdentifierType.ID)
        {
        }

        /// <summary>
        /// Construct a DownloadFileAction with the given Bucket Name and File Name
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="fileDestination">The file path to download to</param>
        /// <param name="bucketName">The name of the bucket the file is in</param>
        /// <param name="fileName">The name of the file</param>
        public DownloadFileAction(BackblazeB2AuthorizationSession authorizationSession, string fileDestination, string bucketName, string fileName)
            : this(authorizationSession, fileDestination, bucketName + "/" + fileName, IdentifierType.Name)
        {
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2ActionResult<BackblazeB2DownloadFileResult>> ExecuteAsync()
        {
            switch (DownloadIdentifierType)
            {
                case IdentifierType.ID:
                    return await DownloadByFileIDAsync();
                case IdentifierType.Name:
                    throw new NotImplementedException();
                default:
                    throw new InvalidOperationException();
            }
        }
        #endregion

        #region private method
        private async Task<BackblazeB2ActionResult<BackblazeB2DownloadFileResult>> DownloadByFileIDAsync()
        {
            string body = "{\"fileId\":\"" + Identifier + "\"}";
            byte[] payload = Encoding.UTF8.GetBytes(body);

            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession + DownloadByIDURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = payload.Length;

            return await ExecuteWebDownloadRequest(webRequest, payload);
        }

        private async Task<BackblazeB2ActionResult<BackblazeB2DownloadFileResult>> DownloadByFileNameAsync()
        {
            HttpWebRequest webRequest = GetHttpWebRequest(GetDownloadByFileURL());
            webRequest.Method = "GET";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);

            return await ExecuteWebDownloadRequest(webRequest, null);
        }

        /// <summary>
        /// A custom web request execution function because the one used by the BaseAction isn't sufficient 
        /// </summary>
        /// <returns>The download result</returns>
        private async Task<BackblazeB2ActionResult<BackblazeB2DownloadFileResult>> ExecuteWebDownloadRequest(
            HttpWebRequest webRequest,
            byte[] payload
        )
        {
            try
            {
                if (payload != null)
                {
                    using (Stream stream = await webRequest.GetRequestStreamAsync())
                    {
                        await stream.WriteAsync(payload, 0, payload.Length);
                    }
                }

                using (HttpWebResponse response = await webRequest.GetResponseAsync() as HttpWebResponse)
                using (BinaryReader binaryFileReader = new BinaryReader(response.GetResponseStream()))
                using (FileStream outputFileStream = new FileStream(FileDestination, FileMode.CreateNew))
                {
                    byte[] fileBuffer;
                    while (true)
                    {
                        fileBuffer = binaryFileReader.ReadBytes(BufferSize);
                        if (fileBuffer == null || fileBuffer.Length < 1)
                        {
                            break;
                        }

                        await outputFileStream.WriteAsync(fileBuffer, 0, fileBuffer.Length);
                    }

                    long timeStamp = -1;
                    long.TryParse(response.Headers.Get("X-Bz-Upload-Timestamp"), out timeStamp);

                    BackblazeB2DownloadFileResult result = new BackblazeB2DownloadFileResult
                    {
                        ContentLength = response.ContentLength,
                        ContentSha1 = response.Headers.Get("X-Bz-Content-Sha1"),
                        ContentType = response.ContentType,
                        FileName = response.Headers.Get("X-Bz-File-Name"),
                        TimeStamp = timeStamp,
                    };

                    return new BackblazeB2ActionResult<BackblazeB2DownloadFileResult>(
                        result
                    );
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = (HttpWebResponse)ex.Response;
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseJson = await reader.ReadToEndAsync();
                    return new BackblazeB2ActionResult<BackblazeB2DownloadFileResult>(
                        Maybe<BackblazeB2DownloadFileResult>.Nothing,
                        JsonConvert.DeserializeObject<BackblazeB2ActionErrorDetails>(responseJson)
                    );
                }
            }
        }

        private string GetDownloadByFileURL()
        {
            return _authorizationSession.DownloadURL + "/file/" + Identifier;
        }
        #endregion
    }
}
