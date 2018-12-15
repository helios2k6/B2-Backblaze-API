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

using B2BackupUtility.Proxies;
using B2BackupUtility.Utils;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// This lists the files that are currently on the server
    /// </summary>
    public sealed class ListFiles : SimpleCommand, ILogNotifier
    {
        #region public properties
        public static string CommandNotification => "List Files";

        public static string CommandSwitch => "--list-files";

        public static CommandType CommandType => CommandType.LIST;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            this.Debug(CommandNotification);
            RemoteFileSystemProxy remoteFileSystemProxy = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);
            this.Info(BuildStringFromFiles(remoteFileSystemProxy.AllFiles));
        }
        #endregion

        #region private methods
        private static string BuildStringFromFiles(IEnumerable<Database.File> files)
        {
            StringBuilder builder = new StringBuilder();
            IList<Database.File> sortedFiles = (from file in files
                                                orderby file.FileName
                                                select file).ToList();

            builder.AppendLine($"{sortedFiles.Count} files");
            builder.AppendLine($"Total size: {GetTrueUsedSpace(sortedFiles):n0} bytes");
            foreach (Database.File file in sortedFiles)
            {
                builder.AppendLine(file.ToString());
            }

            return builder.ToString();
        }

        private static long GetTrueUsedSpace(IEnumerable<Database.File> sortedFiles)
        {
            long currentSpaceTaken = 0;
            IDictionary<string, ISet<string[]>> sha1ToSetOfShardIDs = new Dictionary<string, ISet<string[]>>();
            foreach (Database.File file in sortedFiles)
            {
                if (sha1ToSetOfShardIDs.TryGetValue(file.SHA1, out ISet<string[]> setOfShardIDs) == false)
                {
                    setOfShardIDs = new HashSet<string[]>();
                    sha1ToSetOfShardIDs[file.SHA1] = setOfShardIDs;
                }

                // Check to see if the shard IDs match. If they do, don't count the file
                bool foundAMatch = false;
                foreach (string[] currentListOfShardIDs in setOfShardIDs)
                {
                    if (file.FileShardIDs.SequenceEqual(currentListOfShardIDs))
                    {
                        foundAMatch = true;
                        break;
                    }
                }

                if (foundAMatch == false)
                {
                    currentSpaceTaken += file.FileLength;
                    setOfShardIDs.Add(file.FileShardIDs);
                }
            }

            return currentSpaceTaken;
        }
        #endregion
    }
}
