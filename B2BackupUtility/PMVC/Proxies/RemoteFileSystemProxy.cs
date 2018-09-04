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
using B2BackupUtility.PMVC.Encryption;
using Functional.Maybe;
using Newtonsoft.Json;
using PureMVC.Patterns.Proxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static int DefaultUploadConnections => 20;
        private static int MinimumFileLengthForMultipleConnections => 1048576;
        private static int DefaultUploadChunkSize => 5242880; // 5 mebibytes

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
        /// Gets all files in the file database manifest
        /// </summary>
        /// <returns>All of the files in the File Database Manifest</returns>
        public IEnumerable<Database.File> GetAllFiles()
        {
            return FileDatabaseManifest.Files;
        }

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
            // Remove old file and shards if they exist
            if (TryGetFileByName(localFilePath, out Database.File oldFile))
            {
                DeleteFile(authorizationSession, config, oldFile);
            }

            FileInfo info = new FileInfo(localFilePath);
            Database.File file = new Database.File
            {
                FileLength = info.Length,
                FileName = localFilePath,
                FileShardIDs = new string[0],
                LastModified = info.LastWriteTime.ToBinary(),
                SHA1 = SHA1FileHashStore.Instance.ComputeSHA1(localFilePath),
            };
            IEnumerable<BackblazeB2ActionResult<IBackblazeB2UploadResult>> results = Enumerable.Empty<BackblazeB2ActionResult<IBackblazeB2UploadResult>>();
            foreach (FileShard fileShard in FileFactory.CreateFileShards(new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read), true))
            {
                // Update Database.File
                file.FileShardIDs = file.FileShardIDs.Append(fileShard.ID).ToArray();

                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = fileShard.Length < MinimumFileLengthForMultipleConnections
                    ? ExecuteUploadAction(
                        new UploadWithSingleConnectionAction(
                            authorizationSession,
                            config.BucketID,
                            EncryptionHelpers.EncryptBytes(fileShard.Payload, config.EncryptionKey, config.InitializationVector),
                            fileShard.ID,
                            CancellationEventRouter.GlobalCancellationToken
                        ))
                    : ExecuteUploadAction(
                        new UploadWithMultipleConnectionsAction(
                            authorizationSession,
                            new MemoryStream(EncryptionHelpers.EncryptBytes(fileShard.Payload, config.EncryptionKey, config.InitializationVector)),
                            fileShard.ID,
                            config.BucketID,
                            DefaultUploadChunkSize,
                            DefaultUploadConnections,
                            CancellationEventRouter.GlobalCancellationToken
                        ));

                results = results.Append(uploadResult);

                if (uploadResult.HasErrors)
                {
                    throw new InvalidOperationException($"Could not finish upload. Reason {uploadResult}");
                }
            }

            // Update manifest
            FileDatabaseManifest.Files = FileDatabaseManifest.Files.Append(file).ToArray();
            UploadFileDatabaseManifest(authorizationSession, config);

            return file;
        }

        /// <summary>
        /// A more efficient function for deleting all files off the server. This will delete all
        /// files on the B2 server, including those that aren't on the file database manifest and
        /// the file database manifest itself!
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="config">The program config</param>
        /// <returns>All of the deletion results</returns>
        public IEnumerable<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>> DeleteAllFiles(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        )
        {
            FileDatabaseManifest.Files = new Database.File[0];
            IEnumerable<FileResult> rawB2FileList = GetRawB2Files(authorizationSession, config);
            IList<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>> deletionResults =
                new List<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>>();

            foreach (FileResult rawB2File in rawB2FileList)
            {
                DeleteFileAction deleteFileAction = 
                    new DeleteFileAction(authorizationSession, rawB2File.FileID, rawB2File.FileName);

                deletionResults.Add(deleteFileAction.Execute());
            }

            // Do not upload the file database manifest. Make sure the server is entirely clean
            return deletionResults;
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

            IList<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>> deletionResults =
                new List<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>>();
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
        private static BackblazeB2ActionResult<IBackblazeB2UploadResult> ExecuteUploadAction<T>(BaseAction<T> action) where T : IBackblazeB2UploadResult
        {
            BackblazeB2ActionResult<T> uploadResult = action.Execute();
            BackblazeB2ActionResult<IBackblazeB2UploadResult> castedResult;
            if (uploadResult.HasResult)
            {
                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(uploadResult.Result);
            }
            else
            {
                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(
                    Maybe<IBackblazeB2UploadResult>.Nothing,
                    uploadResult.Errors
                );
            }

            return castedResult;
        }

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
