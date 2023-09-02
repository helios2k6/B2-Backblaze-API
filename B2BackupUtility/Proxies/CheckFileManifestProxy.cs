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
using B2BackupUtility.Mediators;
using B2BackupUtility.Proxies.Exceptions;
using B2BackupUtility.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// Proxy that checks the integrity of the file shards on the B2 server
    /// </summary>
    public sealed class CheckFileManifestProxy : BaseRemoteFileSystemProxy, ILogNotifier
    {
        #region public properties
        public static string Name => "Check File Manifest Proxy";
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
            this.Info("Checking file manifest");
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
            IDictionary<string, ISet<Database.File>> shardIDToDatbaseFile = new Dictionary<string, ISet<Database.File>>();
            foreach (Database.File file in FileDatabaseManifestFiles)
            {
                foreach (string shardID in file.FileShardIDs)
                {
                    if (shardIDToDatbaseFile.TryGetValue(shardID, out ISet<Database.File> setOfFiles) == false)
                    {
                        setOfFiles = new HashSet<Database.File>();
                        shardIDToDatbaseFile[shardID] = setOfFiles;
                    }

                    setOfFiles.Add(file);
                }
            }

            IEnumerable<string> allShardIDsNotAccountedFor = from file in FileDatabaseManifestFiles
                                                             from shardID in file.FileShardIDs
                                                             where allShardIDsPresent.Contains(shardID) == false
                                                             select shardID;
            bool allShardIDsAccountedFor = true;
            foreach (string shardIDNotAccountedFor in allShardIDsNotAccountedFor)
            {
                allShardIDsAccountedFor = false;
                StringBuilder stringBuilder = new StringBuilder();
                foreach (Database.File file in shardIDToDatbaseFile[shardIDNotAccountedFor])
                {
                    stringBuilder.Append($"{file} | ");
                }
                this.Critical($"Shard ID {shardIDNotAccountedFor} for file(s): {stringBuilder.ToString()}");
            }

            if (allShardIDsAccountedFor)
            {
                this.Info("All Shard IDs accounted for");
            }
            else
            {
                this.Info("Some Shard IDs Are Not Accounted For");
            }
        }
        #endregion
    }
}
