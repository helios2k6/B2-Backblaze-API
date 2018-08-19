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

using B2BackupUtility.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace B2BackupUtility
{
    /// <summary>
    /// The entry point for this utility program
    /// </summary>
    public static class Driver
    {
        private static string HelpSwitch => "--help";

        private static IDictionary<string, Action> CommandSwitchToActionMap => new Dictionary<string, Action>
        {
            { DeleteAllFilesCommand.CommandSwitch, Action.DELETE_ALL_FILES },
            { DeleteFileCommand.CommandSwitch, Action.DELETE },
            { DownloadFileCommand.CommandSwitch, Action.DOWNLOAD },
            { GetFileInfoCommand.CommandSwitch, Action.GET_FILE_INFO },
            { ListFilesCommand.CommandSwitch, Action.LIST },
            { UploadFileCommand.CommandSwitch, Action.UPLOAD },
            { UploadFolderCommand.CommandSwitch, Action.UPLOAD_FOLDER }
        };

        private static string[] NecessaryOptions => new[]
        {
            BaseCommand.AccountIDOption,
            BaseCommand.ApplicationKeyOption,
            BaseCommand.BucketIDOption,
        };

        private static IDictionary<string, IEnumerable<string>> CommandSwitchesToOptionsMap => new Dictionary<string, IEnumerable<string>>
        {
            { DeleteAllFilesCommand.CommandSwitch, new string[] { } },
            { DeleteFileCommand.CommandSwitch, DeleteFileCommand.CommandOptions },
            { DownloadFileCommand.CommandSwitch, DownloadFileCommand.CommandOptions },
            { GetFileInfoCommand.CommandSwitch, GetFileInfoCommand.CommandOptions },
            { ListFilesCommand.CommandSwitch, new string[] { } },
            { UploadFileCommand.CommandSwitch, UploadFileCommand.CommandOptions },
            { UploadFolderCommand.CommandSwitch, UploadFolderCommand.CommandOptions },
            { HelpSwitch, new string[] { } },
        };

        public static void Main(string[] args)
        {
            PrintHeader();
            if (args.Length < 4 || WantsHelp(args))
            {
                PrintHelp();
                return;
            }

            Action action = Action.UNKNOWN;
            if (TryGetAction(args, out action) == false)
            {
                PrintHelp();
                return;
            }

            HookUpCancellationHandler(action);

            switch (action)
            {
                case Action.DELETE:
                    new DeleteFileCommand(args).ExecuteAction();
                    break;

                case Action.DELETE_ALL_FILES:
                    new DeleteAllFilesCommand(args).ExecuteAction();
                    break;

                case Action.DOWNLOAD:
                    new DownloadFileCommand(args).ExecuteAction();
                    break;

                case Action.GET_FILE_INFO:
                    new GetFileInfoCommand(args).ExecuteAction();
                    break;

                case Action.LIST:
                    new ListFilesCommand(args).ExecuteAction();
                    break;

                case Action.UPLOAD:
                    new UploadFileCommand(args).ExecuteAction();
                    break;

                case Action.UPLOAD_FOLDER:
                    new UploadFolderCommand(args).ExecuteAction();
                    break;

                default:
                    Console.WriteLine("Unknown action specified");
                    break;
            }
        }

        private static void HookUpCancellationHandler(Action action)
        {
            switch (action)
            {
                // Cancellation is only significant for these actions
                case Action.DOWNLOAD:
                case Action.UPLOAD:
                case Action.UPLOAD_FOLDER:
                case Action.DELETE_ALL_FILES:
                    Console.CancelKeyPress += CancellationActions.HandleCancel;
                    break;
            }
        }

        private static bool TryGetAction(string[] args, out Action action)
        {
            if (args.Length < 4)
            {
                action = Action.UNKNOWN;
                return false;
            }

            foreach (string arg in args)
            {
                if (CommandSwitchToActionMap.TryGetValue(arg, out action))
                {
                    return true;
                }
            }

            action = Action.UNKNOWN;
            return false;
        }

        private static bool WantsHelp(IEnumerable<string> args)
        {
            return args.Any(e => e.Equals(HelpSwitch, StringComparison.OrdinalIgnoreCase));
        }

        private static void PrintHeader()
        {
            Console.WriteLine("B2 Backup Utility v4.0");
            Console.WriteLine();
        }

        private static void PrintHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Usage: <this program> <necessary switches> <action> [options]")
                .AppendLine();

            builder.AppendLine("Necessary Options");
            foreach (string option in NecessaryOptions)
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

            Console.Write(builder.ToString());
        }
    }
}
