﻿/* 
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

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Uploades a folder to the B2 Backblaze Server
    /// </summary>
    public sealed class UploadFolder : SimpleCommand, ILogNotifier
    {
        #region public properties
        public static string CommandNotification => "Upload Folder";

        public static string CommandSwitch => "--upload-folder";

        public static string FolderOption => "--folder";

        public static string RootDestinationFolderOption => "--root-destination-folder";

        public static string OverrideOption => "--override";

        public static string DryRunOption => "--dry-run";

        public static CommandType CommandType => CommandType.UPLOAD_FOLDER;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            this.Debug(CommandNotification);
            AuthorizationSessionProxy authorizationSessionProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            ProgramArgumentsProxy programArgProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            UploadFileProxy uploadFileProxy = (UploadFileProxy)Facade.RetrieveProxy(UploadFileProxy.Name);

            if (programArgProxy.TryGetArgument(FolderOption, out string folderToUpload) == false)
            {
                throw new TerminateProgramException(
                    $"No folder provided. Did you remember to use {FolderOption}?"
                );
            }

            if (programArgProxy.TryGetArgument(RootDestinationFolderOption, out string rootDestinationFolder) == false)
            {
                throw new TerminateProgramException(
                    $"No root destination folder provided. Did you remember to use {RootDestinationFolderOption}?"
                );
            }

            uploadFileProxy.AddFolder(
                () => authorizationSessionProxy.AuthorizationSession,
                folderToUpload,
                rootDestinationFolder,
                programArgProxy.DoesOptionExist(OverrideOption),
                programArgProxy.DoesOptionExist(DryRunOption)
            );
        }
        #endregion
    }
}
