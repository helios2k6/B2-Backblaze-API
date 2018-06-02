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
using System.Linq;
using System.Text;

namespace B2BackupUtility
{
    /// <summary>
    /// The entry point for this utility program
    /// </summary>
    public static class Driver
    {
        public static void Main(string[] args)
        {
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


        }

        private static bool TryGetAction(string[] args, out Action action)
        {
            if (args.Length < 4)
            {
                action = Action.UNKNOWN;
                return false;
            }

            switch (args[3])
            {
                case "--upload-file":
                    action = Action.UPLOAD;
                    break;
                case "--list-files":
                    action = Action.LIST;
                    break;
                case "--download-file":
                    action = Action.DOWNLOAD;
                    break;
                case "--delete-file":
                    action = Action.DELETE;
                    break;
                default:
                    action = Action.UNKNOWN;
                    return false;
            }

            return true;
        }

        private static bool WantsHelp(string[] args)
        {
            return args.Any(s => s.Equals("--help", StringComparison.OrdinalIgnoreCase));
        }

        private static void PrintHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("B2 Backup Utility v1.0")
                .AppendLine("Usage: <this program> <account ID> <application key> <bucket ID> <action> [options]")
                .AppendLine();

            builder.AppendLine("Actions")
                .AppendLine()
                .AppendLine("--upload-file")
                .AppendLine("\t\tUpload a file. Will automatically update versions of files")
                .AppendLine()
                .AppendLine("--list-files")
                .AppendLine("\t\tList all of the available files on the server. Note, this action will be charged as a Class C charge")
                .AppendLine()
                .AppendLine("--download-file")
                .AppendLine("\t\tDownloads a file from the B2 Backblaze server. Note, this action will be charged as a Class B charge")
                .AppendLine()
                .AppendLine("--delete-file")
                .AppendLine()
                .AppendLine("--help")
                .AppendLine("\t\tDisplay this message")
                .AppendLine();

            builder.AppendLine("Action Details")
                .AppendLine()
                .AppendLine("Upload File Options")
                .AppendLine("--file")
                .AppendLine("\t\tThe path to the file to upload")
                .AppendLine()
                .AppendLine("--destination")
                .AppendLine("\t\tThe path to upload to")
                .AppendLine()
                .AppendLine("List Files Options")
                .AppendLine("\t\tThere aren't any. Just type in the command")
                .AppendLine()
                .AppendLine("Download File Options")
                .AppendLine("--file-name")
                .AppendLine("\t\tDownload a file by file name")
                .AppendLine()
                .AppendLine("--file-id")
                .AppendLine("\t\tDownload a file by file ID")
                .AppendLine()
                .AppendLine("Delete File Options")
                .AppendLine("--file-name")
                .AppendLine("\t\tDelete a file by file name")
                .AppendLine()
                .AppendLine("--file-id")
                .AppendLine("\t\tDelete a file by file ID")
                .AppendLine();

            Console.Write(builder.ToString());
        }
    }
}
