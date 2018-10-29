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

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// Proxy that checks the integrity of the file shards on the B2 server
    /// </summary>
    public sealed class CheckFileManifestProxy : BaseRemoteFileSystemProxy
    {
        #region public properties
        public static string Name => "Check File Manifest Proxy";

        public static string BeginCheckFileManifest => "Begin Check File Manifest";
        public static string ShardIDNotAccountedFor => "Shard ID Not Accounted For";
        public static string FinishedCheckFileManifest => "Finished Check File Manifest";
        #endregion

        #region ctor
        public CheckFileManifestProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Checks the file manifest against the file shards on the server
        /// </summary>
        /// <param name="authorizationSessionGenerator">The generator for an authorization session</param>
        public void CheckFileManifest(Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator)
        {
            SendNotification(BeginCheckFileManifest, null, null);
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

            ISet<string> allShardIDsPresent = listFilesActionResult.Result.Files.Select(t => t.FileName).ToHashSet();
            IDictionary<string, Database.File> shardIDToDatbaseFile = (from file in FileDatabaseManifestFiles
                                                                       from shardID in file.FileShardIDs
                                                                       select new KeyValuePair<string, Database.File>(shardID, file)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            IEnumerable<string> allShardIDsNotAccountedFor = from file in FileDatabaseManifestFiles.AsParallel()
                                                             from shardID in file.FileShardIDs.AsParallel()
                                                             where allShardIDsPresent.Contains(shardID) == false
                                                             select shardID;
            bool allShardIDsAccountedFor = true;
            foreach (string shardIDNotAccountedFor in allShardIDsNotAccountedFor)
            {
                allShardIDsAccountedFor = false;
                SendNotification(ShardIDNotAccountedFor, $"Shard ID {shardIDNotAccountedFor} for {shardIDToDatbaseFile[shardIDNotAccountedFor]}", null);
            }

            if (allShardIDsAccountedFor)
            {
                SendNotification(FinishedCheckFileManifest, "All Shard IDs Accounted For", null);
            }
            else
            {
                SendNotification(FinishedCheckFileManifest, "Some Shard IDs Are Not Accounted For", null);
            }
        }
        #endregion
    }
}
