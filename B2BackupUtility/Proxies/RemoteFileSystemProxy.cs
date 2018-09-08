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
using Functional.Maybe;
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
    /// This is a proxy for the remote file system on B2
    /// </summary>
    public sealed class RemoteFileSystemProxy : Proxy
    {
        #region private fields
        private static int DefaultUploadConnections => 20;
        private static int MinimumFileLengthForMultipleConnections => 1048576;
        private static int DefaultUploadChunkSize => 5242880; // 5 mebibytes

        private FileDatabaseManifest FileDatabaseManifest => Data as FileDatabaseManifest;
        private readonly Config _config;
        #endregion

        #region public properties
        public static string Name => "File Manifest Database Proxy";

        // Uploads
        public static string BeginUploadFile => "Begin Upload File";
        public static string FailedToUploadFile => "Failed To Upload File";
        public static string FailedToUploadFileManifest => "Failed To Upload File Manifest";
        public static string SkippedUploadFile => "Skip Uploading File";
        public static string FinishUploadFile => "Finished Uploading File";

        // Deletions
        public static string BeginDeletingFile => "Begin Deleting File";
        public static string FailedToDeleteFile => "Failed To Delete File";
        public static string FinishedDeletingFile => "Finished Deleting File";

        public static string RemoteFileDatabaseManifestName => "b2_backup_util_file_database_manifest.txt.aes.gz";
        #endregion

        #region ctor
        /// <summary>
        /// Construcs a new RemoteFileSystemProxy and initializes this by fetching the file database manifest
        /// from the server. The reference to the authorization session is not kept around as this can expire
        /// </summary>
        /// <param name="authorizationSession"
        /// >The authorization session to use to initialize this. This is is not kept around
        /// </param>
        /// <param name="config">The program config</param>
        public RemoteFileSystemProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, null)
        {
            _config = config;
            Data = GetOrCreateFileDatabaseManifest(authorizationSession);
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
        #region adding files
        /// <summary>
        /// Uploads a local folder
        /// </summary>
        /// <param name="authorizationSessionGenerator">
        /// A function that returns an authorization session. This is required because this can be a 
        /// very long running function and this gives callers the opportunity to fetch a new 
        /// authorization session should this run over 24 hours
        /// </param>
        /// <param name="localFilePath">The local path to the </param>
        /// <param name="shouldOverride">
        /// Whether to overide old files. If false, this will not throw an exception, but
        /// instead will quietly skip that file
        /// </param>
        public void AddFolder(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            string localFolderPath,
            bool shouldOverride
        )
        {
            if (Directory.Exists(localFolderPath) == false)
            {
                throw new DirectoryNotFoundException($"Could not find directory {localFolderPath}");
            }

            foreach (string localFilePath in GetFilesToUpload(localFolderPath, shouldOverride))
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();

                try
                {
                    AddLocalFile(authorizationSessionGenerator(), localFilePath, shouldOverride);
                }
                catch (FailedToUploadFileException ex)
                {
                    // Just send a notification and move on
                    SendNotification(FailedToUploadFile, ex, null);
                }
                catch (FailedToUploadFileDatabaseManifestException ex)
                {
                    // Just send a notification here as well. Continue uploading
                    SendNotification(FailedToUploadFileManifest, ex, null);
                }
            }
        }

        /// <summary>
        /// Adds a local file to the Remote File System
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="localFilePath">The local path to the </param>
        /// <param name="shouldOverride">Whether to overide old files</param>
        public void AddLocalFile(
            BackblazeB2AuthorizationSession authorizationSession,
            string localFilePath,
            bool shouldOverride
        )
        {
            string absoluteFilePath = Path.GetFullPath(localFilePath);
            if (System.IO.File.Exists(absoluteFilePath) == false)
            {
                throw new FileNotFoundException("Could not find file to upload", absoluteFilePath);
            }

            // Remove old file and shards if they exist
            if (TryGetFileByName(absoluteFilePath, out Database.File oldFile))
            {
                if (shouldOverride)
                {
                    DeleteFile(authorizationSession, oldFile);
                }
                else
                {
                    SendNotification(SkippedUploadFile, absoluteFilePath, null);
                    return;
                }
            }

            IList<FileShard> fileShards = new List<FileShard>();
            SendNotification(BeginUploadFile, absoluteFilePath, null);
            IEnumerable<BackblazeB2ActionResult<IBackblazeB2UploadResult>> results = Enumerable.Empty<BackblazeB2ActionResult<IBackblazeB2UploadResult>>();
            foreach (FileShard fileShard in FileFactory.CreateFileShards(new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read, FileShare.Read), true))
            {
                // Update Database.File
                fileShards.Add(fileShard);

                // Serialized file shard
                byte[] serializedFileShard = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileShard));

                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = fileShard.Length < MinimumFileLengthForMultipleConnections
                    ? ExecuteUploadAction(
                        new UploadWithSingleConnectionAction(
                            authorizationSession,
                            _config.BucketID,
                            EncryptionHelpers.EncryptBytes(serializedFileShard, _config.EncryptionKey, _config.InitializationVector),
                            fileShard.ID,
                            CancellationEventRouter.GlobalCancellationToken
                        ))
                    : ExecuteUploadAction(
                        new UploadWithMultipleConnectionsAction(
                            authorizationSession,
                            new MemoryStream(EncryptionHelpers.EncryptBytes(serializedFileShard, _config.EncryptionKey, _config.InitializationVector)),
                            fileShard.ID,
                            _config.BucketID,
                            DefaultUploadChunkSize,
                            DefaultUploadConnections,
                            CancellationEventRouter.GlobalCancellationToken
                        ));

                results = results.Append(uploadResult);

                if (uploadResult.HasErrors)
                {
                    throw new FailedToUploadFileException
                    {
                        BackblazeErrorDetails = uploadResult.Errors,
                    };
                }
            }
            SendNotification(FinishUploadFile, absoluteFilePath, null);

            // Create file
            FileInfo info = new FileInfo(absoluteFilePath);
            Database.File file = new Database.File
            {
                FileLength = info.Length,
                FileName = absoluteFilePath,
                FileShardIDs = fileShards.OrderBy(s => s.PieceNumber).Select(s => s.ID).ToArray(),
                LastModified = info.LastWriteTime.ToBinary(),
                SHA1 = SHA1FileHashStore.Instance.ComputeSHA1(absoluteFilePath),
            };

            // Update manifest
            FileDatabaseManifest.Files = FileDatabaseManifest.Files.Append(file).ToArray();
            UploadFileDatabaseManifest(authorizationSession);
        }
        #endregion
        #region deleting files
        /// <summary>
        /// A more efficient function for deleting all files off the server. This will delete all
        /// files on the B2 server, including those that aren't on the file database manifest and
        /// the file database manifest itself!
        /// </summary>
        /// <param name="authorizationSessionGenerator">A generator function for the authorization session</param>
        public void DeleteAllFiles(Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator)
        {
            FileDatabaseManifest.Files = new Database.File[0];
            IEnumerable<FileResult> rawB2FileList = GetRawB2Files(authorizationSessionGenerator());
            foreach (FileResult rawB2File in rawB2FileList)
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();

                SendNotification(BeginDeletingFile, rawB2File.FileName, null);
                DeleteFileAction deleteFileAction =
                    new DeleteFileAction(authorizationSessionGenerator(), rawB2File.FileID, rawB2File.FileName);
                BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deletionResult = deleteFileAction.Execute();
                if (deletionResult.HasErrors)
                {
                    SendNotification(FailedToDeleteFile, deletionResult, null);
                }
                else
                {
                    SendNotification(FinishedDeletingFile, rawB2File.FileName, null);
                }
            }
        }

        /// <summary>
        /// Deletes a file off the remote file system
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="file">The file to delete</param>
        public void DeleteFile(
            BackblazeB2AuthorizationSession authorizationSession,
            Database.File file
        )
        {
            // Remove entry in the File Manifest
            FileDatabaseManifest.Files = FileDatabaseManifest.Files.Where(f => f.Equals(file) == false).ToArray();

            // Get the raw B2 File List
            IEnumerable<FileResult> rawB2FileList = GetRawB2Files(authorizationSession);
            IDictionary<string, FileResult> fileNameToFileResult = rawB2FileList.ToDictionary(k => k.FileName, v => v);

            SendNotification(BeginDeletingFile, file.FileName, null);
            foreach (string shardID in file.FileShardIDs)
            {
                if (fileNameToFileResult.TryGetValue(shardID, out FileResult fileShardToDelete))
                {
                    DeleteFileAction deleteFileAction =
                        new DeleteFileAction(authorizationSession, fileShardToDelete.FileID, fileShardToDelete.FileName);

                    BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deletionResult = deleteFileAction.Execute();
                    if (deletionResult.HasErrors)
                    {
                        SendNotification(FailedToDeleteFile, deletionResult, null);
                    }
                }
            }
            SendNotification(FinishedDeletingFile, file.FileName, null);

            UploadFileDatabaseManifest(authorizationSession);
        }
        #endregion
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
        public FileDatabaseManifest GetOrCreateFileDatabaseManifest(
            BackblazeB2AuthorizationSession authorizationSession
        )
        {
            FileResult fileDatabaseManifestFileResult = GetRawB2Files(authorizationSession)
                .Where(f => f.FileName.Equals(RemoteFileDatabaseManifestName, StringComparison.Ordinal))
                .SingleOrDefault();
            if (fileDatabaseManifestFileResult == null)
            {
                return new FileDatabaseManifest { Files = new Database.File[0] };
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
                    return DeserializeManifest(outputStream.ToArray(), _config.EncryptionKey, _config.InitializationVector);
                }
                else
                {
                    return new FileDatabaseManifest { Files = new Database.File[0] };
                }
            }
        }
        #endregion

        #region private methods
        private IEnumerable<string> GetFilesToUpload(
            string folder,
            bool overrideFiles
        )
        {
            IEnumerable<string> allLocalFiles = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
            if (overrideFiles)
            {
                foreach (string localFilePath in allLocalFiles)
                {
                    yield return localFilePath;
                }

                yield break;
            }

            foreach (string localFilePath in allLocalFiles)
            {
                if (TryGetFileByName(localFilePath, out Database.File remoteFileEntry))
                {
                    // Need confirm if this is a duplicate or not
                    // 1. If the lengths and last modified dates are the same, then just assume the files are equals (do not upload)
                    // 2. If the lengths are different then the files are not the same (upload)
                    // 3. If the lengths are the same but the last modified dates are different, then we need to perform a SHA-1 check to see
                    //    if the contents are actually different (upload if SHA-1's are different)
                    FileInfo localFileInfo = new FileInfo(localFilePath);
                    if (localFileInfo.Length == remoteFileEntry.FileLength)
                    {
                        // Scenario 3
                        if (localFileInfo.LastWriteTimeUtc.Equals(DateTime.FromBinary(remoteFileEntry.LastModified)) == false)
                        {
                            string sha1OfLocalFile = SHA1FileHashStore.Instance.ComputeSHA1(localFilePath);
                            if (string.Equals(sha1OfLocalFile, remoteFileEntry.SHA1, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                yield return localFilePath;
                            }
                        }
                        // Scenario 1 is implied 
                    }
                    else
                    {
                        // Scenario 2
                        yield return localFilePath;
                    }
                }
                else
                {
                    // We have never uploaded this file to the server
                    yield return localFilePath;
                }
            }
        }

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
            BackblazeB2AuthorizationSession authorizationSession
        )
        {
            UploadWithSingleConnectionAction uploadAction = new UploadWithSingleConnectionAction(
                authorizationSession,
                _config.BucketID,
                SerializeManifest(FileDatabaseManifest, _config),
                RemoteFileDatabaseManifestName,
                CancellationToken.None
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

        private IEnumerable<FileResult> GetRawB2Files(
            BackblazeB2AuthorizationSession authorizationSession
        )
        {
            ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileVersions(authorizationSession, _config.BucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesAction.Execute();
            if (listFilesActionResult.HasErrors)
            {
                throw new FailedToGetListOfFilesOnB2Exception
                {
                    BackblazeErrorDetails = listFilesActionResult.Errors,
                };
            }

            return listFilesActionResult.Result.Files;
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
