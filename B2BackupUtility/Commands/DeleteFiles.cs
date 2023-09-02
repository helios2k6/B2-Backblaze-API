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

using B2BackupUtility.Proxies;
using B2BackupUtility.Utils;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.Commands
{
    public sealed class DeleteFiles : SimpleCommand, ILogNotifier
    {
        #region private fields
        public static string CommandNotification => "Delete Files";

        public static string CommandSwitch => "--delete-files";

        public static string FileIDsOption => "--file-ids";

        public static string DryRunOption => "--dry-run";

        public static CommandType CommandType => CommandType.DELETE_FILES;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            this.Debug(CommandNotification);
            AuthorizationSessionProxy authorizationProxy =
                (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            DeleteFileProxy deleteFileProxy = (DeleteFileProxy)Facade.RetrieveProxy(DeleteFileProxy.Name);
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            bool isDryRun = programArgsProxy.DoesOptionExist(DryRunOption);
            foreach (Database.File file in GetFilesToDelete())
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();
                if (isDryRun == false)
                {
                    deleteFileProxy.DeleteFile(() => authorizationProxy.AuthorizationSession, file);
                }
                else
                {
                    this.Info($"[DRY-RUN|: Would have deleted file: {file.FileName}");
                }
            }

            this.Info("Finished deleting all files");
        }
        #endregion

        #region private methods
        private IEnumerable<Database.File> GetFilesToDelete()
        {
            this.Debug("Getting files to delete");
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            IEnumerable<string> fileIDs = programArgsProxy.GetArgsUntilNextSwitch(FileIDsOption);

            if (fileIDs.Any() == false)
            {
                throw new TerminateProgramException(
                    $"No file IDs passed in. Did you use the {FileIDsOption} argument?"
                );
            }

            IList<Database.File> databaseFiles = new List<Database.File>();
            RemoteFileSystemProxy remoteFileSystemProxy = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);
            foreach (string fileID in fileIDs)
            {
                if (remoteFileSystemProxy.TryGetFileByID(fileID, out Database.File file) == false)
                {
                    throw new TerminateProgramException(
                        $"Could not find file entry for file ID {fileID}"
                    );
                }

                databaseFiles.Add(file);
            }

            return databaseFiles;
        }
        #endregion
    }
}
