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
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Renames a file in the file database manifest
    /// </summary>
    public sealed class RenameFile : SimpleCommand
    {
        #region private fields
        #endregion

        #region public properties
        public static string CommandNotification => "Rename File";

        public static string FinishedCommandNotification => "Finished Renaming File";

        public static string CommandSwitch => "--rename-file";

        public static string FileIDOption => "--file-id";

        public static string NewFileNameOption => "--new-file-name";

        public static CommandType CommandType => CommandType.RENAME_FILE;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            Database.File file = GetFile();
            string newFileName = GetNewFileName();
            AuthorizationSessionProxy authorizationProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            RenameFileProxy renameFileProxy = (RenameFileProxy)Facade.RetrieveProxy(RenameFileProxy.Name);
            renameFileProxy.RenameFile(authorizationProxy.AuthorizationSession, file, newFileName);
        }
        #endregion

        #region private methods
        private Database.File GetFile()
        {
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            if (programArgsProxy.TryGetArgument(FileIDOption, out string fileToRename) == false)
            {
                throw new TerminateProgramException("You must provide a file ID to rename");
            }

            RemoteFileSystemProxy remoteFileSystemProxy = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);
            if (remoteFileSystemProxy.TryGetFileByID(fileToRename, out Database.File file) == false)
            {
                throw new TerminateProgramException("Could not retrieve the file by ID");
            }

            return file;
        }

        private string GetNewFileName()
        {
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            if (programArgsProxy.TryGetArgument(NewFileNameOption, out string newFilePath) == false)
            {
                throw new TerminateProgramException("You must provide a name to rename the file to");
            }

            return newFilePath;
        }
        #endregion
    }
}
