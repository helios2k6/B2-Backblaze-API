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

using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using B2BackupUtility.PMVC.Encryption;
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

namespace B2BackupUtility.PMVC.Proxies
{
    /// <summary>
    /// This is a proxy for the remote file system on B2
    /// </summary>
    public sealed class RemoteFileSystemProxy : Proxy
    {
        #region private fields
        private FileDatabaseManifest FileDatabaseManifest => Data as FileDatabaseManifest;
        #endregion

        #region public properties
        public static string Name => "File Manifest Database Proxy";

        public static string RemoteFileDatabaseManifestName => "b2_backup_util_file_database_manifest.txt.aes.gz";
        #endregion

        #region ctor
        /// <summary>
        /// Default constructor of this proxy
        /// </summary>
        public RemoteFileSystemProxy() : base(Name, new FileDatabaseManifest { Files = new Database.File[0] })
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Adds a local file to the Remote File System
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="config">The program config</param>
        /// <param name="localFilePath">The local path to the </param>
        /// <returns></returns>
        public Database.File AddLocalFile(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config,
            string localFilePath
        )
        {
        }

        /// <summary>
        /// Deletes a file off the remote file system
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="config">The config for this program</param>
        /// <param name="file">The file to delete</param>
        /// <returns>Returns an IEnumerable of all of the results</returns>
        public IEnumerable<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>> DeleteFile(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config,
            Database.File file
        )
        {
            // Remove entry in the File Manifest
            FileDatabaseManifest.Files = FileDatabaseManifest.Files.Where(f => f.Equals(file) == false).ToArray();

            // Get the raw B2 File List
            IEnumerable<FileResult> rawB2FileList = GetRawB2Files(authorizationSession, config);
            IDictionary<string, FileResult> fileNameToFileResult = rawB2FileList.ToDictionary(k => k.FileName, v => v);

            IList<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>> deletionResults = new List<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>>();
            // Cycle through each File Shard and remove them
            foreach (string shardID in file.FileShardIDs)
            {
                if (fileNameToFileResult.TryGetValue(shardID, out FileResult fileShardToDelete))
                {
                    DeleteFileAction deleteFileAction =
                        new DeleteFileAction(authorizationSession, fileShardToDelete.FileID, fileShardToDelete.FileName);

                    deletionResults.Add(deleteFileAction.Execute());
                }
            }

            // Upload the file manifest now
            UploadFileDatabaseManifest(authorizationSession, config);

            // Return deletion results
            return deletionResults;
        }

        /// <summary>
        /// Tries to get a file by name
        /// </summary>
        /// <param name="authorizationSession"></param>
        /// <param name="config"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool TryGetFileByName(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config,
            string fileName,
            out Database.File file
        )
        {

        }

        /// <summary>
        /// Initializes this file database manifest
        /// </summary>
        public void Initialize(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        )
        {
            FileResult fileDatabaseManifestFileResult = GetRawB2Files(authorizationSession, config)
                .Where(f => f.FileName.Equals(RemoteFileDatabaseManifestName, StringComparison.Ordinal))
                .SingleOrDefault();
            if (fileDatabaseManifestFileResult == null)
            {
                return;
            }

            using (MemoryStream outputStream = new MemoryStream())
            using (DownloadFileAction manifestFileDownloadAction = new DownloadFileAction(
                authorizationSession,
                outputStream,
                fileDatabaseManifestFileResult.FileID
            ))
            {
                BackblazeB2ActionResult<BackblazeB2DownloadFileResult> manifestResultOption = manifestFileDownloadAction.Execute();
                if (manifestResultOption.HasResult)
                {
                    // Now, read string from manifest
                    outputStream.Flush();
                    Data = DeserializeManifest(outputStream.ToArray(), config.EncryptionKey, config.InitializationVector);
                }
                else
                {
                    Data = new FileDatabaseManifest
                    {
                        Files = new Database.File[0],
                    };
                }
            }
        }
        #endregion

        #region private methods
        private void UploadFileDatabaseManifest(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        )
        {
            UploadWithSingleConnectionAction uploadAction = new UploadWithSingleConnectionAction(
                authorizationSession,
                config.BucketID,
                SerializeManifest(config),
                RemoteFileDatabaseManifestName,
                CancellationToken.None
            );

            BackblazeB2ActionResult<BackblazeB2UploadFileResult> result = uploadAction.Execute();
            if (result.HasErrors)
            {
                throw new InvalidOperationException($"Could not upload file manifest {result}");
            }
        }

        private IEnumerable<FileResult> GetRawB2Files(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        )
        {
            ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileVersions(authorizationSession, config.BucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesAction.Execute();
            if (listFilesActionResult.HasErrors)
            {
                throw new InvalidOperationException($"Could not get list of files {listFilesActionResult}");
            }

            return listFilesActionResult.Result.Files;
        }

        private byte[] SerializeManifest(Config config)
        {
            using (MemoryStream serializedManifestStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(FileDatabaseManifest))))
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

        private FileDatabaseManifest DeserializeManifest(
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
