/* 
 * Copyright (c) 2023 Andrew Johnson
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
using B2BackupUtility.Proxies.Exceptions;
using B2BackupUtility.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.Proxies
{
    public sealed class DeleteFileProxy : BaseRemoteFileSystemProxy, ILogNotifier
    {
        #region public properties
        public static string Name => "Delete File Proxy";
        #endregion

        #region ctor
        public DeleteFileProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// A more efficient function for deleting all files off the server. This will delete all
        /// files on the B2 server, including those that aren't on the file database manifest and
        /// the file database manifest itself!
        /// </summary>
        /// <param name="authorizationSessionGenerator">A generator function for the authorization session</param>
        public void DeleteAllFiles(Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator)
        {
            this.Debug("Begin deleting all files");
            RemoveAllFiles();
            IEnumerable<FileResult> rawB2FileList = GetRawB2FileNames(authorizationSessionGenerator());
            object lockObject = new object();
            Parallel.ForEach(rawB2FileList, rawB2File =>
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();
                DeleteFileAction deleteFileAction =
                    new DeleteFileAction(authorizationSessionGenerator(), rawB2File.FileID, rawB2File.FileName);
                BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deletionResult = deleteFileAction.Execute();
                if (deletionResult.HasErrors)
                {
                    lock (lockObject)
                    {
                        this.Critical($"Failed to delete file {rawB2File.FileName}. Reason: {deletionResult}");
                    }
                }
                else
                {
                    lock (lockObject)
                    {
                        this.Info($"Deleted file: {rawB2File.FileName}");
                    }
                }
            });
        }

        /// <summary>
        /// Deletes a file off the remote file system
        /// </summary>
        /// <param name="authorizationSessionGenerator">The generator function that returns an authorization session</param>
        /// <param name="file">The file to delete</param>
        public void DeleteFile(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            Database.File file
        )
        {
            this.Debug($"Begin deleting file: {file.FileName}");
            // Remove entry in the File Manifest
            RemoveFile(file);

            // Determine if another file shares the same file shards
            IEnumerable<Database.File> filesThatShareTheSameShards = from currentFile in FileDatabaseManifestFiles
                                                                     where file.FileLength == currentFile.FileLength &&
                                                                        file.FileShardIDs.Length == currentFile.FileShardIDs.Length &&
                                                                        file.SHA1.Equals(currentFile.SHA1, StringComparison.OrdinalIgnoreCase) &&
                                                                        // Even if a single shard ID is shared by another file, that's enough to count it
                                                                        // as being equal (or at least not eligible for hard-deletion)
                                                                        file.FileShardIDs.Any(t => currentFile.FileShardIDs.Contains(t))
                                                                     select currentFile;
            // If there are no files that share the same Shard IDs
            if (filesThatShareTheSameShards.Any() == false)
            {
                // Get the raw B2 File List so we can get the B2 file IDs of the file shards
                ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileNames(authorizationSessionGenerator(), Config.BucketID, true);
                BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesAction.Execute();
                if (listFilesActionResult.HasErrors)
                {
                    throw new FailedToGetListOfFilesOnB2Exception
                    {
                        BackblazeErrorDetails = listFilesActionResult.Errors,
                    };
                }
                IEnumerable<FileResult> rawB2FileList = listFilesActionResult.Result.Files;
                IDictionary<string, FileResult> fileNameToFileResult = rawB2FileList.ToDictionary(k => k.FileName, v => v);

                foreach (string shardID in file.FileShardIDs)
                {
                    if (fileNameToFileResult.TryGetValue(shardID, out FileResult fileShardToDelete))
                    {
                        DeleteFileAction deleteFileAction =
                            new DeleteFileAction(authorizationSessionGenerator(), fileShardToDelete.FileID, fileShardToDelete.FileName);

                        BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deletionResult = deleteFileAction.Execute();
                        if (deletionResult.HasErrors)
                        {
                            this.Critical($"Failed to delete file. Reason: {deletionResult}");
                        }
                    }
                }
            }
            else
            {
                this.Info($"File {file.FileName} shares file shares with another file. Will not perform a hard-delete. Removing from file manifest instead");
            }

            while (TryUploadFileDatabaseManifest(authorizationSessionGenerator()) == false)
            {
                Thread.Sleep(5);
            }

            this.Info($"Deleted file: {file.FileName}");
        }
        #endregion
    }
}