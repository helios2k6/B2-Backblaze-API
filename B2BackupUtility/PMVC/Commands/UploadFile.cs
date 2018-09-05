﻿/* 
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

using B2BackupUtility.Commands;
using B2BackupUtility.PMVC.Proxies;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;
using System.Diagnostics;
using System.Text;

namespace B2BackupUtility.PMVC.Commands
{
    /// <summary>
    /// Upload a single file
    /// </summary>
    public sealed class UploadFile : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Upload File";

        public static string FailedCommandNotification => "Failed Uploading File";

        public static string FinishedCommandNotification => "Finished Uploading File";

        public static string CommandSwitch => "--upload-file";

        public static string FileOption => "--file";

        public static CommandType CommandType => CommandType.UPLOAD;
        #endregion

        #region public sealed classes
        public sealed class ErrorDetails
        {

        }
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            AuthorizationSessionProxy authorizationSessionProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            ConfigProxy configProxy = (ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name);
            ProgramArgumentsProxy programArgProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            RemoteFileSystemProxy fileSystemProxy = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);
            if (programArgProxy.TryGetArgument(FileOption, out string fileToUpload))
            {
                try
                {
                    Database.File file = fileSystemProxy.AddLocalFile(authorizationSessionProxy.AuthorizationSession, fileToUpload);
                    SendNotification(FinishedCommandNotification, null, null);
                }
                catch (FailedToUploadFileException ex)
                {
                    SendNotification(FailedCommandNotification, ex, null);
                }
            }
            else
            {
                SendNotification(FailedCommandNotification, "No file provided", null);
            }
        }
        #endregion
    }
}
