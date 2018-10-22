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

using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using B2BackupUtility.Encryption;
using B2BackupUtility.Proxies.Exceptions;
using Newtonsoft.Json;
using PureMVC.Patterns.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
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
        public static long CurrentDatabaseDataFormatVersion => 1;
        #endregion

        #region private fields and properties
        private static int MaxAttemptsToUploadFileManifest => 3;

        // This field must be handled with care since it's statically shared between
        // all instances. Do not reference directly
        private static readonly object SharedFileDatabaseManifestLock = new object();
        private static FileDatabaseManifest SharedFileDatabaseManifest = null;

        private FileDatabaseManifest FileDatabaseManifest => (Data as FileDatabaseManifest);
        #endregion

        #region protected properties
        /// <summary>
        /// The files in the FileDatabaseManifest for this proxy
        /// </summary>
        protected IEnumerable<Database.File> FileDatabaseManifestFiles => FileDatabaseManifest.Files;

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
            file = FileDatabaseManifestFiles
                .Where(f => f.FileName.Equals(fileName, StringComparison.Ordinal))
                .SingleOrDefault();

            return file != null;
        }

        /// <summary>
        /// Tries to get a file by its ID
        /// </summary>
        /// <param name="fileID">The file ID to retrieve</param>
        /// <param name="file">The reference to write to</param>
        /// <returns>True if the result was found. False otherwise</returns>
        public bool TryGetFileByID(
            string fileID,
            out Database.File file
        )
        {
            // Linear search since this doesn't happen often
            file = FileDatabaseManifestFiles
                .Where(f => f.FileID.Equals(fileID, StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();

            return file != null;
        }
        #endregion

        #region protected methods
        /// <summary>
        /// Removes all files from the FileDatabaseManifest
        /// </summary>
        protected void RemoveAllFiles()
        {
            lock (SharedFileDatabaseManifestLock)
            {
                FileDatabaseManifest.Files = new Database.File[0];
            }
        }

        /// <summary>
        /// Removes a file from the FileDatabaseManifest in a thread-safe manner
        /// </summary>
        /// <param name="file">The file to remove</param>
        protected void RemoveFile(Database.File file)
        {
            lock (SharedFileDatabaseManifestLock)
            {
                FileDatabaseManifest.Files = FileDatabaseManifest.Files.Where(t => t.Equals(file) == false).ToArray();
            }
        }

        /// <summary>
        /// Adds a file to the FileDatabaseManifest in a thread-safe manner. It does not check to see
        /// if the file already exists
        /// </summary>
        /// <param name="file">The file to add</param>
        protected void AddFile(Database.File file)
        {
            lock (SharedFileDatabaseManifestLock)
            {
                FileDatabaseManifest.Files = FileDatabaseManifest.Files.Append(file).ToArray();
            }
        }

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

        protected bool TryUploadFileDatabaseManifest(
            BackblazeB2AuthorizationSession authorizationSession
        )
        {
            try
            {
                UploadFileDatabaseManifest(authorizationSession);
            }
            catch (FailedToUploadFileDatabaseManifestException)
            {
                return false;
            }

            return true;
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
                MaxAttemptsToUploadFileManifest,
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

        /// <summary>
        /// Clones the current file database manifest and returns it
        /// </summary>
        /// <returns></returns>
        protected FileDatabaseManifest GetClonedFileDatabaseManifest()
        {
            return DeserializeManifest(SerializeManifest(FileDatabaseManifest, Config), Config);
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
                lock (SharedFileDatabaseManifestLock)
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
                            SharedFileDatabaseManifest = new FileDatabaseManifest
                            {
                                DataFormatVersionNumber = CurrentDatabaseDataFormatVersion,
                                Files = new Database.File[0]
                            };
                        }
                        else
                        {
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
                                        config
                                    );
                                }
                                else
                                {
                                    SharedFileDatabaseManifest = new FileDatabaseManifest
                                    {
                                        DataFormatVersionNumber = CurrentDatabaseDataFormatVersion,
                                        Files = new Database.File[0]
                                    };
                                }
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
            Config config
        )
        {
            using (MemoryStream deserializedMemoryStream = new MemoryStream())
            {
                using (MemoryStream compressedBytesStream = new MemoryStream(EncryptionHelpers.DecryptBytes(encryptedBytes, config.EncryptionKey, config.InitializationVector)))
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