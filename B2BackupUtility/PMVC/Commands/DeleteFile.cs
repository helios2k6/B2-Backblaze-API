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

using B2BackblazeBridge.Core;
using B2BackupUtility.Commands;
using B2BackupUtility.PMVC.Proxies;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.PMVC.Commands
{
    /// <summary>
    /// The Delete File command 
    /// </summary>
    public sealed class DeleteFile : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Delete File";

        public static string FailedCommandNotification => "Failed To Delete File";

        public static string FinishCommandNotification => "Finished Deleting File";

        public static string CommandSwitch => "--delete-file";

        public static string FileNameOption => "--file-name";

        public static CommandType CommandType => CommandType.DELETE;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            if (programArgsProxy.TryGetArgument(FileNameOption, out string fileName))
            {
                AuthorizationSessionProxy authorizationSessionProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
                ConfigProxy configProxy = (ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name);
                RemoteFileSystemProxy remoteFileSystem = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);

                if (remoteFileSystem.TryGetFileByName(fileName, out Database.File fileToDelete))
                {
                    IEnumerable<BackblazeB2ActionResult<BackblazeB2DeleteFileResult>> deletionResult = 
                        remoteFileSystem.DeleteFile(authorizationSessionProxy.AuthorizationSession, configProxy.Config, fileToDelete);

                    if (deletionResult.Any(t => t.HasErrors))
                    {
                        SendNotification(FailedCommandNotification, deletionResult, null);
                    }
                    else
                    {
                        SendNotification(FinishCommandNotification, null, null);
                    }
                }
            }
            else
            {
                SendNotification(FailedCommandNotification, null, null);
            }
        }
        #endregion
    }
}
