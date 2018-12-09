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
using B2BackupUtility.Proxies.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.Proxies
{
    public sealed class DeleteFileProxy : BaseRemoteFileSystemProxy
    {
        #region public properties
        public static string Name => "Delete File Proxy";

        public static string BeginDeletingFile => "Begin Deleting File";
        public static string FailedToDeleteFile => "Failed To Delete File";
        public static string FinishedDeletingFile => "Finished Deleting File";
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
            RemoveAllFiles();
            IEnumerable<FileResult> rawB2FileList = GetRawB2FileNames(authorizationSessionGenerator());
            object lockObject = new object();
            Parallel.ForEach(rawB2FileList, rawB2File =>
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();

                lock (lockObject)
                {
                    SendNotification(BeginDeletingFile, rawB2File.FileName, null);
                }

                DeleteFileAction deleteFileAction =
                    new DeleteFileAction(authorizationSessionGenerator(), rawB2File.FileID, rawB2File.FileName);
                BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deletionResult = deleteFileAction.Execute();
                if (deletionResult.HasErrors)
                {
                    lock (lockObject)
                    {
                        SendNotification(FailedToDeleteFile, deletionResult, null);
                    }
                }
                else
                {
                    lock (lockObject)
                    {
                        SendNotification(FinishedDeletingFile, rawB2File.FileName, null);
                    }
                }
            });
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
                ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileNames(authorizationSession, Config.BucketID, true);
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
            }

            while (TryUploadFileDatabaseManifest(authorizationSession) == false)
            {
                Thread.Sleep(5);
            }

            SendNotification(FinishedDeletingFile, file.FileName, null);
        }
        #endregion
    }
}