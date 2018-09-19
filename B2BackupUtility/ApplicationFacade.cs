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
using PureMVC.Interfaces;
using PureMVC.Patterns.Facade;

namespace B2BackupUtility
{
    /// <summary>
    /// The concrete application facade
    /// </summary>
    public sealed class ApplicationFacade : Facade, IFacade
    {
        #region protected methods
        protected override void InitializeController()
        {
            base.InitializeController();
            RegisterCommand(DeleteAllFiles.CommandNotification, () => new DeleteAllFiles());
            RegisterCommand(DeleteFile.CommandNotification, () => new DeleteFile());
            RegisterCommand(DownloadFile.CommandNotification, () => new DownloadFile());
            RegisterCommand(GenerateEncryptionKey.CommandNotification, () => new GenerateEncryptionKey());
            RegisterCommand(InitializeAuthorizationSession.CommandNotification, () => new InitializeAuthorizationSession());
            RegisterCommand(InitializeConfig.CommandNotification, () => new InitializeConfig());
            RegisterCommand(InitializeDownloadProxy.CommandNotification, () => new InitializeDownloadProxy());
            RegisterCommand(Commands.InitializeModel.CommandNotification, () => new InitializeModel());
            RegisterCommand(InitializeProgramArguments.CommandNotification, () => new InitializeProgramArguments());
            RegisterCommand(InitializeRemoteFileSystem.CommandNotification, () => new InitializeRemoteFileSystem());
            RegisterCommand(Commands.InitializeView.CommandNotification, () => new InitializeView());
            RegisterCommand(ListFiles.CommandNotification, () => new ListFiles());
            RegisterCommand(PruneShards.CommandNotification, () => new PruneShards());
            RegisterCommand(PrintHelp.CommandNotification, () => new PrintHelp());
            RegisterCommand(StartApplication.CommandNotification, () => new StartApplication());
            RegisterCommand(StartSelectedProgramCommand.CommandNotification, () => new StartSelectedProgramCommand());
            RegisterCommand(UploadFile.CommandNotification, () => new UploadFile());
            RegisterCommand(UploadFolder.CommandNotification, () => new UploadFolder());
        }
        #endregion
    }
}
