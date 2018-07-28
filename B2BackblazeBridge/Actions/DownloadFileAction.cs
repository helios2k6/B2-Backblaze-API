﻿/* 
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
using Functional.Maybe;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents a download file action, either by ID or by file name
    /// </summary>
    public sealed class DownloadFileAction : BaseAction<BackblazeB2DownloadFileResult>, IDisposable
    {
        #region inner classes
        /// <summary>
        /// The type of identifier to use when looking up the file to download
        /// </summary>
        private enum IdentifierType
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
        private static readonly string DownloadByIDURL = "/b2api/v1/b2_download_file_by_id";

        private static readonly int BufferSize = 64 * 1024; // 64 kibibytes

        private bool disposedValue;

        private readonly BackblazeB2AuthorizationSession _authorizationSession;

        private readonly Stream _outputStream;

        private readonly IdentifierType _downloadIdentifierType;

        private readonly string _identifier;
        #endregion

        #region ctor
        /// <summary>
        /// Construct a DownloadFileAction with the given identifier and identifier type
        /// </summary>
        /// <param name="authorizationSession">The authorization session to use</param>
        /// <param name="outputStream">The output stream of the download file</param>
        /// <param name="identifier">The identifier</param>
        /// <param name="downloadIdentifierType">The type of identifier</param>
        private DownloadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            Stream outputStream,
            string identifier,
            IdentifierType downloadIdentifierType
        ) : base(CancellationToken.None)
        {
            disposedValue = false;
            _authorizationSession = authorizationSession;
            _outputStream = outputStream;
            _downloadIdentifierType = downloadIdentifierType;
            _identifier = identifier;
        }

        /// <summary>
        /// Construct a new DownloadFileAction
        /// </summary>
        /// <param name="authorizationSession">The authorization session to use</param>
        /// <param name="fileDestination">The file destination for the downloaded file</param>
        /// <param name="identifier">The identifier</param>
        /// <param name="downloadIdentifierType">The type of identifier</param>
        private DownloadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string fileDestination,
            string identifier,
            IdentifierType downloadIdentifierType
        ) : this(authorizationSession, new FileStream(fileDestination, FileMode.CreateNew), identifier, downloadIdentifierType)
        {
        }

        /// <summary>
        /// Construct a DownloadFileAction with the given file ID
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="fileDestination">The file path to download to</param>
        /// <param name="fileID">The file ID to download</param>
        public DownloadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string fileDestination,
            string fileID
        )
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
        public DownloadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string fileDestination,
            string bucketName,
            string fileName
        )
            : this(authorizationSession, fileDestination, bucketName + "/" + fileName, IdentifierType.Name)
        {
        }


        /// <summary>
        /// Construct a DownloadFileAction with the given file ID
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="fileDestination">The file path to download to</param>
        /// <param name="fileID">The file ID to download</param>
        public DownloadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            Stream outputStream,
            string fileID
        )
            : this(authorizationSession, outputStream, fileID, IdentifierType.ID)
        {
        }

        /// <summary>
        /// Construct a DownloadFileAction with the given Bucket Name and File Name and output to the
        /// specified stream
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="fileDestination">The file path to download to</param>
        /// <param name="bucketName">The name of the bucket the file is in</param>
        /// <param name="fileName">The name of the file</param>
        public DownloadFileAction(
            BackblazeB2AuthorizationSession authorizationSession,
            Stream outputStream,
            string bucketName,
            string fileName
        )
            : this(authorizationSession, outputStream, bucketName + "/" + fileName, IdentifierType.Name)
        {
        }
        #endregion

        #region public methods
        public override BackblazeB2ActionResult<BackblazeB2DownloadFileResult> Execute()
        {
            switch (_downloadIdentifierType)
            {
                case IdentifierType.ID:
                    return DownloadByFileID();
                case IdentifierType.Name:
                    return DownloadByFileName();
                default:
                    throw new InvalidOperationException();
            }
        }
        #endregion

        #region private method
        private BackblazeB2ActionResult<BackblazeB2DownloadFileResult> DownloadByFileID()
        {
            string body = "{\"fileId\":\"" + _identifier + "\"}";
            byte[] payload = Encoding.UTF8.GetBytes(body);

            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.DownloadURL + DownloadByIDURL);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = payload.Length;

            return ExecuteWebDownloadRequest(webRequest, payload);
        }

        private BackblazeB2ActionResult<BackblazeB2DownloadFileResult> DownloadByFileName()
        {
            HttpWebRequest webRequest = GetHttpWebRequest(GetDownloadByFileURL());
            webRequest.Method = "GET";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);

            return ExecuteWebDownloadRequest(webRequest, null);
        }

        /// <summary>
        /// A custom web request execution function because the one used by the BaseAction isn't sufficient 
        /// </summary>
        /// <returns>The download result</returns>
        private BackblazeB2ActionResult<BackblazeB2DownloadFileResult> ExecuteWebDownloadRequest(
            HttpWebRequest webRequest,
            byte[] payload
        )
        {
            try
            {
                if (payload != null)
                {
                    using (Stream stream = webRequest.GetRequestStream())
                    {
                        stream.Write(payload, 0, payload.Length);
                    }
                }

                using (HttpWebResponse response = webRequest.GetResponse() as HttpWebResponse)
                using (BinaryReader binaryFileReader = new BinaryReader(response.GetResponseStream()))
                {
                    byte[] fileBuffer;
                    while (true)
                    {
                        fileBuffer = binaryFileReader.ReadBytes(BufferSize);
                        if (fileBuffer == null || fileBuffer.Length < 1)
                        {
                            break;
                        }

                        _outputStream.Write(fileBuffer, 0, fileBuffer.Length);
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
                    string responseJson = reader.ReadToEnd();
                    return new BackblazeB2ActionResult<BackblazeB2DownloadFileResult>(
                        Maybe<BackblazeB2DownloadFileResult>.Nothing,
                        JsonConvert.DeserializeObject<BackblazeB2ActionErrorDetails>(responseJson)
                    );
                }
            }
        }

        private string GetDownloadByFileURL()
        {
            return _authorizationSession.DownloadURL + "/file/" + _identifier;
        }

        #region IDisposable Support
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _outputStream.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
        #endregion
    }
}
