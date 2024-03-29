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

using B2BackupUtility.Mediators;
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
    /// Prints the help text
    /// </summary>
    public sealed class PrintHelp : SimpleCommand, ILogNotifier
    {
        #region public properties
        public static string CommandNotification => "Print Help";

        public static string CommandSwitch => "--print-help";
        #endregion

        #region private properties
        private static string HeaderText => "B2 Backup Utility v9.0";

        private static string InstructionText => "Usage: <this program> <necessary switches> <action> [options]";

        private static IEnumerable<string> NecessaryOptions => new[]
        {
            ConfigProxy.ConfigArgument,
        };

        private static IEnumerable<string> LoggerOptions => new[]
        {
            ConsoleMediator.VerboseLevelSwitch,
            ConsoleMediator.DebugLevelSwitch,
        };

        private static IDictionary<string, IEnumerable<string>> CommandSwitchesToOptionsMap => new Dictionary<string, IEnumerable<string>>
        {
            { CheckFileManifest.CommandSwitch, Enumerable.Empty<string>() },
            { CompactShards.CommandSwitch, new[] { CompactShards.DryRunOption } },
            { DeleteAllFiles.CommandSwitch, Enumerable.Empty<string>() },
            { DeleteFile.CommandSwitch, new[] { DeleteFile.FileNameOption, DeleteFile.FileIDOption } },
            { DeleteFiles.CommandSwitch, new[] { DeleteFiles.DryRunOption, DeleteFiles.FileIDsOption } },
            { DownloadFile.CommandSwitch, new[] { DownloadFile.FileNameOption, DownloadFile.FileIDOption, DownloadFile.DestinationOption } },
            { DownloadFileManifest.CommandSwitch, Enumerable.Empty<string>() },
            { DownloadFiles.CommandSwitch, new[] { DownloadFiles.FileIDsOption }},
            { GenerateEncryptionKey.CommandSwitch, Enumerable.Empty<string>() },
            { ListFiles.CommandSwitch, Enumerable.Empty<string>() },
            { RenameFile.CommandSwitch, new[] { RenameFile.FileIDOption, RenameFile.NewFileNameOption } },
            { PruneShards.CommandSwitch, Enumerable.Empty<string>() },
            { CommandSwitch, Enumerable.Empty<string>() },
            { UploadFile.CommandSwitch, new[] { UploadFile.FileOption, UploadFile.OverrideOption, UploadFile.DestinationOption } },
            { UploadFolder.CommandSwitch, new[] { UploadFolder.DryRunOption, UploadFolder.FolderOption, UploadFolder.OverrideOption, UploadFolder.RootDestinationFolderOption } },
        };
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            this.Debug(CommandNotification);
            StringBuilder builder = new StringBuilder();
            builder
                .AppendLine()
                .AppendLine(HeaderText)
                .AppendLine(InstructionText)
                .AppendLine();

            builder.AppendLine("Necessary Options");
            foreach (string option in NecessaryOptions)
            {
                builder.AppendLine(option);
            }
            builder.AppendLine();

            builder.AppendLine("Logger Options");
            foreach (string option in LoggerOptions)
            {
                builder.AppendLine(option);
            }
            builder.AppendLine();

            builder.AppendLine("Commands");
            foreach (KeyValuePair<string, IEnumerable<string>> commandToOptions in CommandSwitchesToOptionsMap)
            {
                builder.AppendLine(commandToOptions.Key);
                foreach (string option in commandToOptions.Value)
                {
                    builder.AppendLine($"\t{option}");
                }
                builder.AppendLine();
            }

            this.Critical(builder.ToString());
        }
        #endregion
    }
}
