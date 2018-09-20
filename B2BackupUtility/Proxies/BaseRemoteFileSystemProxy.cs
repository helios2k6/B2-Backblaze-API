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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using B2BackupUtility.Encryption;
using B2BackupUtility.Proxies.Exceptions;
using Newtonsoft.Json;
using PureMVC.Patterns.Proxy;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// The base class for all remote file system proxies
    /// </summary>
    public abstract class BaseRemoteFileSystemProxy : Proxy
    {
        #region public properties
        public static string RemoteFileDatabaseManifestName => "b2_backup_util_file_database_manifest.txt.aes.gz";
        #endregion

        #region private fields
        // This field must be handled with care since it's statically shared between
        // all instances. Do not reference directly
        private static FileDatabaseManifest SharedFileDatabaseManifest = null;
        #endregion

        #region protected properties
        /// <summary>
        /// The FileDatabaseManifest for this Proxy
        /// </summary>
        protected FileDatabaseManifest FileDatabaseManifest => Data as FileDatabaseManifest;

        /// <summary>
        /// The Config set on this proxy
        /// </summary>
        protected Config Config { get; private set; }
        #endregion

        #region ctor
        /// <summary>
        /// Standard ctor that must be overridden by base classes
        /// </summary>
        public BaseRemoteFileSystemProxy(
            string proxyName,
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(proxyName, null)
        {
            Config = config;
            Data = GetOrCreateFileDatabaseManifest(authorizationSession, config);
        }
        #endregion

        #region public methods
        /// <summary>
        /// Tries to get a file by name
        /// </summary>
        /// <param name="fileName">The file name to fetch</param>
        /// <param name="file">The reference to write the result to</param>
        /// <returns>True if a result was found. False otherwise</returns>
        public bool TryGetFileByName(
            string fileName,
            out Database.File file
        )
        {
            // Do linear search since this doesn't happen often
            file = FileDatabaseManifest
                .Files
                .Where(f => f.FileName.Equals(fileName, StringComparison.Ordinal))
                .SingleOrDefault();

            return file != null;
        }
        #endregion

        #region protected methods
        protected IEnumerable<FileResult> GetRawB2FileNames(BackblazeB2AuthorizationSession authorizationSession)
        {
            ListFilesAction listFilesAction =
                ListFilesAction.CreateListFileActionForFileVersions(authorizationSession, Config.BucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesAction.Execute();
            if (listFilesActionResult.HasErrors)
            {
                throw new FailedToGetListOfFilesOnB2Exception
                {
                    BackblazeErrorDetails = listFilesActionResult.Errors,
                };
            }

            IEnumerable<FileResult> rawB2FileList = listFilesActionResult.Result.Files;
            return rawB2FileList;
        }

        protected void UploadFileDatabaseManifest(
            BackblazeB2AuthorizationSession authorizationSession
        )
        {
            UploadWithSingleConnectionAction uploadAction = new UploadWithSingleConnectionAction(
                authorizationSession,
                Config.BucketID,
                SerializeManifest(FileDatabaseManifest, Config),
                RemoteFileDatabaseManifestName,
                CancellationToken.None,
                _ => { } // NoOp for exponential backoff callback
            );

            BackblazeB2ActionResult<BackblazeB2UploadFileResult> result = uploadAction.Execute();
            if (result.HasErrors)
            {
                throw new FailedToUploadFileDatabaseManifestException
                {
                    BackblazeErrorDetails = result.Errors,
                };
            }
        }
        #endregion

        #region private methods
        /// <summary>
        /// Initializes this file database manifest
        /// </summary>
        private static FileDatabaseManifest GetOrCreateFileDatabaseManifest(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        )
        {
            if (SharedFileDatabaseManifest == null)
            {
                lock (SharedFileDatabaseManifest)
                {
                    if (SharedFileDatabaseManifest == null)
                    {
                        // Get just the file names on the server
                        ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileNames(
                            authorizationSession,
                            config.BucketID,
                            true
                        );
                        BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesAction.Execute();
                        if (listFilesActionResult.HasErrors)
                        {
                            throw new FailedToGetListOfFilesOnB2Exception
                            {
                                BackblazeErrorDetails = listFilesActionResult.Errors,
                            };
                        }
                        FileResult fileDatabaseManifestFileResult = listFilesActionResult.Result.Files
                            .Where(f => f.FileName.Equals(RemoteFileDatabaseManifestName, StringComparison.Ordinal))
                            .SingleOrDefault();
                        if (fileDatabaseManifestFileResult == null)
                        {
                            SharedFileDatabaseManifest = new FileDatabaseManifest { Files = new Database.File[0] };
                        }

                        using (MemoryStream outputStream = new MemoryStream())
                        using (DownloadFileAction manifestFileDownloadAction = new DownloadFileAction(
                            authorizationSession,
                            outputStream,
                            fileDatabaseManifestFileResult.FileID
                        ))
                        {
                            BackblazeB2ActionResult<BackblazeB2DownloadFileResult> manifestResultOption =
                                manifestFileDownloadAction.Execute();
                            if (manifestResultOption.HasResult)
                            {
                                // Now, read string from manifest
                                outputStream.Flush();
                                SharedFileDatabaseManifest = DeserializeManifest(
                                    outputStream.ToArray(),
                                    config.EncryptionKey,
                                    config.InitializationVector
                                );
                            }
                            else
                            {
                                SharedFileDatabaseManifest = new FileDatabaseManifest { Files = new Database.File[0] };
                            }
                        }
                    }
                }
            }

            return SharedFileDatabaseManifest;
        }

        private static byte[] SerializeManifest(FileDatabaseManifest fileDatabaseManifest, Config config)
        {
            using (MemoryStream serializedManifestStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileDatabaseManifest))))
            using (MemoryStream compressedMemoryStream = new MemoryStream())
            {
                // It's very important that we dispose of the GZipStream before reading from the memory stream
                using (GZipStream compressionStream = new GZipStream(compressedMemoryStream, CompressionMode.Compress, true))
                {
                    serializedManifestStream.CopyTo(compressionStream);
                }

                return EncryptionHelpers.EncryptBytes(compressedMemoryStream.ToArray(), config.EncryptionKey, config.InitializationVector);
            }
        }

        private static FileDatabaseManifest DeserializeManifest(
            byte[] encryptedBytes,
            string encryptionKey,
            string initializationVector
        )
        {
            using (MemoryStream deserializedMemoryStream = new MemoryStream())
            {
                using (MemoryStream compressedBytesStream = new MemoryStream(EncryptionHelpers.DecryptBytes(encryptedBytes, encryptionKey, initializationVector)))
                using (GZipStream decompressionStream = new GZipStream(compressedBytesStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(deserializedMemoryStream);
                }

                return JsonConvert.DeserializeObject<FileDatabaseManifest>(
                    Encoding.UTF8.GetString(deserializedMemoryStream.ToArray())
                );
            }
        }
        #endregion
    }
}