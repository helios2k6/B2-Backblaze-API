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
            PrintHeader();
            if (args.Length < 4 || CommonUtils.DoesOptionExist(args, "--help"))
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

            string accountID = CommonUtils.GetArgument(args, "--account-id");
            string applicationKey = CommonUtils.GetArgument(args, "--application-key");
            string bucketID = CommonUtils.GetArgument(args, "--bucket-id");

            if (string.IsNullOrWhiteSpace(accountID) || string.IsNullOrWhiteSpace(applicationKey) || string.IsNullOrWhiteSpace(bucketID))
            {
                Console.WriteLine("Account ID, application key, or bucket ID are empty or null.");
                PrintHelp();
                return;
            }

            Console.WriteLine("Authorizing account");
            AuthorizeAccountAction authorizeAccountAction = new AuthorizeAccountAction(accountID, applicationKey);
            BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizationSessionResult = authorizeAccountAction.Execute();
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
                    DeleteFileActions.DeleteFile(authorizationSession, args);
                    break;

                case Action.DOWNLOAD:
                    DownloadFileActions.DownloadFile(authorizationSession, bucketID, args);
                    break;

                case Action.GET_FILE_INFO:
                    GetFileInfoActions.ExecuteGetFileInfo(authorizationSession, args);
                    break;

                case Action.LIST:
                    ListFilesActions.ListFiles(authorizationSession, bucketID);
                    break;

                case Action.UPLOAD:
                    UploadActions.UploadFile(authorizationSession, bucketID, args);
                    break;

                case Action.UPLOAD_FOLDER:
                    UploadActions.UploadFolder(authorizationSession, bucketID, args);
                    break;

                case Action.UNKNOWN:
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
                    case "--get-file-info":
                        action = Action.GET_FILE_INFO;
                        return true;
                }
            }

            action = Action.UNKNOWN;
            return false;
        }

        private static void PrintHeader()
        {
            Console.WriteLine("B2 Backup Utility v3.0");
            Console.WriteLine();
        }

        private static void PrintHelp()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Usage: <this program> <necessary switches> <action> [options]")
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
                .AppendLine("--get-file-info")
                .AppendLine("\t\tGets the info about a file")
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
                .AppendLine()
                .AppendLine("Get File Info Options")
                .AppendLine("--file-id")
                .AppendLine("\t\tThe file ID to get the info about")
                .AppendLine();

            Console.Write(builder.ToString());
        }
    }
}
