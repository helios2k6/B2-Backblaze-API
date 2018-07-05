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

            HookUpCancellationHandler(action);

            string accountID = CommonActions.GetArgument(args, "--account-id");
            string applicationKey = CommonActions.GetArgument(args, "--application-key");
            string bucketID = CommonActions.GetArgument(args, "--bucket-id");

            if (string.IsNullOrWhiteSpace(accountID) || string.IsNullOrWhiteSpace(applicationKey) || string.IsNullOrWhiteSpace(bucketID))
            {
                Console.WriteLine("Account ID, application key, or bucket ID are empty or null.");
                PrintHelp();
                return;
            }

            ExecuteAsync(accountID, applicationKey, bucketID, action, args).Wait();
        }

        private static void HookUpCancellationHandler(Action action)
        {
            switch (action)
            {
                // Cancellation is only significant for these actions
                case Action.DOWNLOAD:
                case Action.UPLOAD:
                case Action.UPLOAD_FOLDER:
                    Console.CancelKeyPress += CancellationActions.HandleCancel;
                    break;
            }
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
                    await ListFilesActions.ListFilesAsync(authorizationSession, bucketID);
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

            foreach (string arg in args)
            {
                switch (arg)
                {
                    case "--upload-file":
                        action = Action.UPLOAD;
                        return true;
                    case "--list-files":
                        action = Action.LIST;
                        return true;
                    case "--download-file":
                        action = Action.DOWNLOAD;
                        return true;
                    case "--delete-file":
                        action = Action.DELETE;
                        return true;
                    case "--upload-folder":
                        action = Action.UPLOAD_FOLDER;
                        return true;
                }
            }

            action = Action.UNKNOWN;
            return false;
        }

        private static void PrintHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("B2 Backup Utility v1.8")
                .AppendLine("Usage: <this program> <necessary switches> <action> [options]")
                .AppendLine();

            builder.AppendLine("Necessary Switches")
                .AppendLine("--account-id")
                .AppendLine("\t\tThe account ID associated with the B2 Backblaze Account")
                .AppendLine()
                .AppendLine("--application-key")
                .AppendLine("\t\tThe secret application key that's authorized to call the B2 Backblaze API")
                .AppendLine()
                .AppendLine("--bucket-id")
                .AppendLine("\t\tThe bucket ID to modify or read from")
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
                .AppendLine("\t\tDeletes a file on the B2 Backblaze server.")
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
                .AppendLine("--connections")
                .AppendLine("\t\tThe number of connections to use when uploading the file. Default = 20")
                .AppendLine()
                .AppendLine("Upload Folder Options")
                .AppendLine("--folder")
                .AppendLine("\t\tThe folder to upload.")
                .AppendLine()
                .AppendLine("--flatten")
                .AppendLine("\t\tFlatten all folders and upload all files to a single directory on B2")
                .AppendLine()
                .AppendLine("--force-override-files")
                .AppendLine("\t\tUpload files to the server regardless if their SHA-1 hashes match")
                .AppendLine()
                .AppendLine("--connections")
                .AppendLine("\t\tThe number of connections to use when uploading the file. Default = 20")
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
