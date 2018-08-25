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
using B2BackupUtility.Logger;
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

        private static IDictionary<string, Command> CommandSwitchToActionMap => new Dictionary<string, Command>
        {
            { DeleteAllFilesCommand.CommandSwitch, Command.DELETE_ALL_FILES },
            { DeleteFileCommand.CommandSwitch, Command.DELETE },
            { DownloadFileCommand.CommandSwitch, Command.DOWNLOAD },
            { GetFileInfoCommand.CommandSwitch, Command.GET_FILE_INFO },
            { ListFilesCommand.CommandSwitch, Command.LIST },
            { UploadFileCommand.CommandSwitch, Command.UPLOAD },
            { UploadFolderCommand.CommandSwitch, Command.UPLOAD_FOLDER }
        };

        private static string[] NecessaryOptions => new[]
        {
            BaseCommand.ConfigOption,
        };

        private static string[] LoggerOptions => new[]
        {
            Loggers.VerboseLogOption,
            Loggers.DebugLogOption,
        };

        private static IDictionary<string, IEnumerable<string>> CommandSwitchesToOptionsMap => new Dictionary<string, IEnumerable<string>>
        {
            { DeleteAllFilesCommand.CommandSwitch, DeleteAllFilesCommand.CommandOptions },
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
            if (args.Length < 3 || WantsHelp(args))
            {
                PrintHelp();
                return;
            }

            Command command = Command.UNKNOWN;
            if (TryGetCommand(args, out command) == false)
            {
                PrintHelp();
                return;
            }

            HookUpCancellationHandler(command);
            Loggers.InitializeLogger(args);

            switch (command)
            {
                case Command.DELETE:
                    new DeleteFileCommand(args).ExecuteAction();
                    break;

                case Command.DELETE_ALL_FILES:
                    new DeleteAllFilesCommand(args).ExecuteAction();
                    break;

                case Command.DOWNLOAD:
                    new DownloadFileCommand(args).ExecuteAction();
                    break;

                case Command.GET_FILE_INFO:
                    new GetFileInfoCommand(args).ExecuteAction();
                    break;

                case Command.LIST:
                    new ListFilesCommand(args).ExecuteAction();
                    break;

                case Command.UPLOAD:
                    new UploadFileCommand(args).ExecuteAction();
                    break;

                case Command.UPLOAD_FOLDER:
                    new UploadFolderCommand(args).ExecuteAction();
                    break;

                default:
                    Console.WriteLine("Unknown action specified");
                    break;
            }
        }

        private static void HookUpCancellationHandler(Command action)
        {
            switch (action)
            {
                // Cancellation is only significant for these actions
                case Command.DOWNLOAD:
                case Command.UPLOAD:
                case Command.UPLOAD_FOLDER:
                case Command.DELETE_ALL_FILES:
                    Console.CancelKeyPress += CancellationActions.HandleCancel;
                    break;
            }
        }

        private static bool TryGetCommand(string[] args, out Command action)
        {
            foreach (string arg in args)
            {
                if (CommandSwitchToActionMap.TryGetValue(arg, out action))
                {
                    return true;
                }
            }

            action = Command.UNKNOWN;
            return false;
        }

        private static bool WantsHelp(IEnumerable<string> args)
        {
            return args.Any(e => e.Equals(HelpSwitch, StringComparison.OrdinalIgnoreCase));
        }

        private static void PrintHeader()
        {
            Console.WriteLine("B2 Backup Utility v4.3");
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

            Console.Write(builder.ToString());
        }
    }
}
