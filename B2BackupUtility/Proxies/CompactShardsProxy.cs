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

using B2BackblazeBridge.Core;
using B2BackupUtility.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// Proxy that will compact duplicate files by redirecting their shards to a common
    /// set of shards
    /// </summary>
    public sealed class CompactShardsProxy : BaseRemoteFileSystemProxy, ILogNotifier
    {
        #region public properties
        public static string Name => "Compact Shards Proxy";
        #endregion

        #region ctor
        public CompactShardsProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion

        #region public methods
        public void CompactShards(
            BackblazeB2AuthorizationSession authorizationSession,
            bool dryRun
        )
        {
            this.Debug("Compacting file shards");
            ISet<ISet<Database.File>> fileGroupsByContents = new HashSet<ISet<Database.File>>();
            foreach (Database.File file in FileDatabaseManifestFiles)
            {
                bool foundAGroup = false;
                foreach (ISet<Database.File> auxGroup in fileGroupsByContents)
                {
                    Database.File auxFile = auxGroup.First();
                    if (file.FileLength == auxFile.FileLength &&
                        file.FileShardHashes.Length == auxFile.FileShardHashes.Length &&
                        file.SHA1.Equals(auxFile.SHA1, StringComparison.OrdinalIgnoreCase) &&
                        file.FileShardHashes.SequenceEqual(auxFile.FileShardHashes))
                    {
                        foundAGroup = true;
                        auxGroup.Add(file);
                        break;
                    }
                }

                if (foundAGroup == false)
                {
                    HashSet<Database.File> newAuxGroup = new HashSet<Database.File>
                    {
                        file,
                    };

                    fileGroupsByContents.Add(newAuxGroup);
                }
            }

            // Go through groups and rewrite the file manifest
            foreach (ISet<Database.File> fileGroup in fileGroupsByContents.Where(g => g.Count > 1))
            {
                Database.File prototypeFile = fileGroup.First();
                foreach (Database.File otherFile in fileGroup.Where(f => ReferenceEquals(f, prototypeFile) == false))
                {
                    this.Verbose($"{otherFile.FileName} is now using shards from {prototypeFile.FileName}");
                    RemoveFile(otherFile);
                    otherFile.FileShardIDs = prototypeFile.FileShardIDs;
                    AddFile(otherFile);
                }
            }

            if (dryRun == false)
            {
                while (TryUploadFileDatabaseManifest(authorizationSession) == false)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }

            this.Info("Finished compacting file shards");
        }
        #endregion
    }
}
