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
using System.Threading.Tasks;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// Proxy that prunes file shards that are unaccounted for
    /// </summary>
    public sealed class PruneFileShardProxy : BaseRemoteFileSystemProxy
    {
        #region public properties
        public static string Name => "Prune File Shard Proxy";

        public static string BeginPruneFile => "Begin Prune File";
        public static string FailedToPruneFile => "Failed To Prune File";
        public static string FinishedPruningFile => "Finished Pruning File";
        #endregion

        #region ctor
        public PruneFileShardProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Prunes any shards on the server that are not accounted for in the database manifest
        /// </summary>
        /// <param name="authorizationSessionGenerator">The generator for an authorization session</param>
        public void PruneShards(Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator)
        {
            // Get just the file names on the server
            ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileNames(authorizationSessionGenerator(), Config.BucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesAction.Execute();
            if (listFilesActionResult.HasErrors)
            {
                throw new FailedToGetListOfFilesOnB2Exception
                {
                    BackblazeErrorDetails = listFilesActionResult.Errors,
                };
            }

            object lockObject = new object();
            IDictionary<string, FileResult> fileNameToFileResultMap = listFilesActionResult.Result.Files.ToDictionary(k => k.FileName, v => v);
            ISet<string> allDatabaseFileShardIds = FileDatabaseManifestFiles.SelectMany(t => t.FileShardIDs).ToHashSet();
            ISet<string> allRawFileNamesOnServer = fileNameToFileResultMap.Keys.ToHashSet();
            ISet<string> allFilesNotAccountedFor = allRawFileNamesOnServer.Except(allDatabaseFileShardIds).Where(t => t.Equals(RemoteFileDatabaseManifestName, StringComparison.OrdinalIgnoreCase) == false).ToHashSet();
            Parallel.ForEach(allFilesNotAccountedFor, fileNameNotAccountedFor =>
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();

                SendNotification(BeginPruneFile, fileNameNotAccountedFor, null);
                FileResult fileNotAccountedFor = fileNameToFileResultMap[fileNameNotAccountedFor];
                DeleteFileAction deleteFileAction =
                    new DeleteFileAction(authorizationSessionGenerator(), fileNotAccountedFor.FileID, fileNotAccountedFor.FileName);

                BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deletionResult = deleteFileAction.Execute();
                if (deletionResult.HasErrors)
                {
                    lock (lockObject)
                    {
                        SendNotification(FailedToPruneFile, deletionResult, null);
                    }
                }
                else
                {
                    lock (lockObject)
                    {
                        SendNotification(FinishedPruningFile, fileNameNotAccountedFor, null);
                    }
                }
            });
        }
        #endregion
    }
}