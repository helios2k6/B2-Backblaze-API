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
using System.Collections.Generic;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Selects the associated program command that was passed in
    /// (e.g. "--upload-file", "--list-files", etc...)
    /// </summary>
    public sealed class StartSelectedProgramCommand : SimpleCommand
    {
        #region private static fields
        private static IDictionary<string, CommandType> CommandSwitchToActionMap => new Dictionary<string, CommandType>
        {
            { DeleteAllFiles.CommandSwitch, CommandType.DELETE_ALL_FILES },
            { DeleteFile.CommandSwitch, CommandType.DELETE },
            { DownloadFile.CommandSwitch, CommandType.DOWNLOAD },
            { GenerateEncryptionKey.CommandSwitch, CommandType.GENERATE_ENCRYPTION_KEY },
            { ListFiles.CommandSwitch, CommandType.LIST },
            { UploadFile.CommandSwitch, CommandType.UPLOAD },
            { UploadFolder.CommandSwitch, CommandType.UPLOAD_FOLDER }
        };

        private static IDictionary<CommandType, string> CommandTypeToNotification => new Dictionary<CommandType, string>
        {
            { DeleteAllFiles.CommandType, DeleteAllFiles.CommandNotification },
            { DeleteFile.CommandType, DeleteFile.CommandNotification },
            { DownloadFile.CommandType, DownloadFile.CommandNotification },
            { GenerateEncryptionKey.CommandType, GenerateEncryptionKey.CommandNotification },
            { ListFiles.CommandType, ListFiles.CommandNotification },
            { UploadFile.CommandType, UploadFile.CommandNotification },
            { UploadFolder.CommandType, UploadFolder.CommandNotification },
        };
        #endregion

        #region public properties
        public static string CommandNotification => "Start Selected Program Command";

        public static string FailedCommandNotification => "Failed To Start Selected Program";
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            CommandType command = CommandType.UNKNOWN;
            if (TryGetCommand(programArgsProxy.ProgramArguments, out command))
            {
                InitializeModelIfNecessary(command);
                SendNotification(CommandTypeToNotification[command], null, null);
            }
            else
            {
                SendNotification(FailedCommandNotification, $"Could not find command", null);
            }
        }
        #endregion

        #region private methods
        private void InitializeModelIfNecessary(CommandType commandType)
        {
            if (commandType != CommandType.GENERATE_ENCRYPTION_KEY)
            {
                SendNotification(InitializeModel.CommandNotification, null, null);
            }
        }

        private static bool TryGetCommand(IEnumerable<string> args, out CommandType action)
        {
            foreach (string arg in args)
            {
                if (CommandSwitchToActionMap.TryGetValue(arg, out action))
                {
                    return true;
                }
            }

            action = CommandType.UNKNOWN;
            return false;
        }
        #endregion
    }
}
