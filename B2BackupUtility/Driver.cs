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

using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2BackupUtility
{
    /// <summary>
    /// The entry point for this utility program
    /// </summary>
    public static class Driver
    {
        public static void Main(string[] args)
        {
            // Just to add space between the command and the output
            Console.WriteLine();

            if (args.Length < 4 || CommonActions.DoesOptionExist(args, "--help"))
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

            ExecuteAsync(args[0], args[1], args[2], action, args.Skip(4)).Wait();
        }

        private static async Task ExecuteAsync(string accountID, string applicationKey, string bucketID, Action action, IEnumerable<string> remainingArgs)
        {
            Console.WriteLine("Authorizing account");
            AuthorizeAccountAction authorizeAccountAction = new AuthorizeAccountAction(accountID, applicationKey);
            BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizationSessionResult = await authorizeAccountAction.ExecuteAsync();
            if (authorizationSessionResult.HasErrors)
            {
                Console.WriteLine(
                    string.Format(
                        "Could not authorize account with account ID {0} and application key {1}. Error: {2}",
                        accountID,
                        applicationKey,
                        authorizationSessionResult.Errors.First().Message
                    )
                );

                return;
            }

            Console.WriteLine("Account authorized");
            Console.WriteLine();
            BackblazeB2AuthorizationSession authorizationSession = authorizationSessionResult.Result;
            switch (action)
            {
                case Action.DELETE:
                    await DeleteFileActions.DeleteFileAsync(authorizationSession, remainingArgs);
                    break;

                case Action.DOWNLOAD:
                    await DownloadFileActions.DownloadFileAsync(authorizationSession, bucketID, remainingArgs);
                    break;

                case Action.LIST:
                    await ListFilesAction.ListFilesAsync(authorizationSession, bucketID);
                    break;

                case Action.UPLOAD:
                    await UploadActions.UploadFileAsync(authorizationSession, bucketID, remainingArgs);
                    break;

                case Action.UPLOAD_FOLDER:
                    await UploadActions.UploadFolderAsync(authorizationSession, bucketID, remainingArgs);
                    break;

                case Action.UNKNOWN:
                default:
                    Console.WriteLine("Unknown action specified");
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
                case "--upload-folder":
                    action = Action.UPLOAD_FOLDER;
                    break;
                default:
                    action = Action.UNKNOWN;
                    return false;
            }

            return true;
        }

        private static void PrintHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("B2 Backup Utility v1.1")
                .AppendLine("Usage: <this program> <account ID> <application key> <bucket ID> <action> [options]")
                .AppendLine();

            builder.AppendLine("Actions")
                .AppendLine()
                .AppendLine("--upload-file")
                .AppendLine("\t\tUpload a file. Will automatically update versions of files")
                .AppendLine()
                .AppendLine("--upload-folder")
                .AppendLine("\t\tUploads a folder full of files. Will automatically recurse the folder")
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
                .AppendLine("Upload Folder Options")
                .AppendLine("--folder")
                .AppendLine("\t\tThe folder to upload.")
                .AppendLine()
                .AppendLine("--flatten")
                .AppendLine("\t\tFlatten all folders and upload all files to a single directory on B2")
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
                .AppendLine("--destination")
                .AppendLine("\t\tThe destination you want to download the file")
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
