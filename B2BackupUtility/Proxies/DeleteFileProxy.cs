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
            FileDatabaseManifest.Files = new Database.File[0];
            IEnumerable<FileResult> rawB2FileList = GetRawB2FileNames(authorizationSessionGenerator());
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
            SendNotification(FinishedDeletingFile, file.FileName, null);

            UploadFileDatabaseManifest(authorizationSession);
        }
        #endregion
    }
}