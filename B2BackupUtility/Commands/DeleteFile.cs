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

using System;
using B2BackupUtility.Proxies;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// The Delete File command 
    /// </summary>
    public sealed class DeleteFile : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Delete File";

        public static string CommandSwitch => "--delete-file";

        public static string FileNameOption => "--file-name";

        public static string FileIDOption => "--file-id";

        public static CommandType CommandType => CommandType.DELETE;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            AuthorizationSessionProxy authorizationSessionProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            DeleteFileProxy deleteFileProxy = (DeleteFileProxy)Facade.RetrieveProxy(DeleteFileProxy.Name);

            deleteFileProxy.DeleteFile(authorizationSessionProxy.AuthorizationSession, GetFileToDelete());
        }
        #endregion

        #region private methods
        private Database.File GetFileToDelete()
        {
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            bool hasFileID = programArgsProxy.TryGetArgument(FileIDOption, out string fileToDeleteByID);
            bool hasFileName = programArgsProxy.TryGetArgument(FileNameOption, out string fileToDeleteByName);
            if (hasFileID && hasFileName)
            {
                throw new TerminateProgramException("Specific either a file name or a file ID; not both");
            }
            
            RemoteFileSystemProxy remoteFileSystemProxy = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);
            if (hasFileID)
            {
                if (remoteFileSystemProxy.TryGetFileByID(fileToDeleteByID, out Database.File file))
                {
                    return file;
                }

                throw new TerminateProgramException($"Could not find file ID {fileToDeleteByID}");
            }

            if (hasFileName)
            {
                if (remoteFileSystemProxy.TryGetFileByName(fileToDeleteByName, out Database.File file))
                {
                    return file;
                }

                throw new TerminateProgramException($"Could not find file name {fileToDeleteByName}");
            }

            throw new InvalidOperationException("No file ID or file name provided to download");
        }
        #endregion
    }
}
