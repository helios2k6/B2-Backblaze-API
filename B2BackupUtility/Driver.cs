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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace B2BackupUtility
{
    /// <summary>
    /// The entry point for this utility program
    /// </summary>
    public static class Driver
    {
        #region private fields
        private const int FILE_CHUNK_SIZE = 1024 * 1024 * 5;
        private const int CONNECTIONS = 20;
        #endregion

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

            ExecuteAsync(args[0], args[1], args[2], action, args.Skip(4)).Wait();
        }

        private static async Task ExecuteAsync(string accountID, string applicationKey, string bucketID, Action action, IEnumerable<string> remainingArgs)
        {
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

            BackblazeB2AuthorizationSession authorizationSession = authorizationSessionResult.Result;
            switch (action)
            {
                case Action.DELETE:
                    await DeleteFileAsync(authorizationSession, remainingArgs);
                    break;

                case Action.DOWNLOAD:
                    await DownloadFileAsync(authorizationSession, bucketID, remainingArgs);
                    break;

                case Action.LIST:
                    await ListFilesAsync(authorizationSession, bucketID);
                    break;

                case Action.UPLOAD:
                    await UploadFileAsync(authorizationSession, bucketID, remainingArgs);
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

        private static async Task UploadFileAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> remainingArgs)
        {
            string fileToUpload = GetArgument(remainingArgs, "--file");
            string destination = GetArgument(remainingArgs, "--destination");

            if (string.IsNullOrWhiteSpace(fileToUpload) || string.IsNullOrWhiteSpace(destination) || File.Exists(fileToUpload) == false)
            {
                Console.WriteLine(string.Format("Invalid arguments sent for --file ({0}) or --destination ({1})", fileToUpload, destination));
                return;
            }

            UploadFileUsingMultipleConnectionsAction uploadAction = new UploadFileUsingMultipleConnectionsAction(
                authorizationSession,
                fileToUpload,
                destination,
                bucketID,
                FILE_CHUNK_SIZE,
                CONNECTIONS
            );

            Stopwatch watch = new Stopwatch();
            watch.Start();
            BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> uploadResult = await ExecuteActionAsync(uploadAction, "Upload File");
            watch.Stop();
            if (uploadResult.HasResult)
            {
                double bytesPerSecond = uploadResult.Result.TotalContentLength / ((double)watch.ElapsedTicks / Stopwatch.Frequency);

                BackblazeB2UploadMultipartFileResult result = uploadResult.Result;
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Upload Successful:");
                builder.AppendFormat("File: {0}", result.FileName).AppendLine();
                builder.AppendFormat("File ID: {0}", result.FileID).AppendLine();
                builder.AppendFormat("Total Content Length: {0}", result.TotalContentLength).AppendLine();
                builder.AppendFormat("Upload Time: {0} seconds", (double)watch.ElapsedTicks / Stopwatch.Frequency).AppendLine();
                builder.AppendFormat("Upload Speed: {0:0,0.00} bytes / second", bytesPerSecond.ToString("0,0.00", CultureInfo.InvariantCulture)).AppendLine();
                Console.Write(builder.ToString());
            }
        }

        private static async Task ListFilesAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID)
        {
            ListFilesAction action = ListFilesAction.CreateListFileActionForFileNames(authorizationSession, bucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> actionResult = await ExecuteActionAsync(action, "List files");
            if (actionResult.HasResult)
            {
                foreach (BackblazeB2ListFilesResult.FileResult file in actionResult.Result.Files)
                {
                    Console.WriteLine(string.Format("{0} - {1}", file.FileName, file.FileID));
                }
            }
        }

        private static async Task DownloadFileAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> remainingArgs)
        {
            string fileName = GetArgument(remainingArgs, "--file-name");
            string fileID = GetArgument(remainingArgs, "--file-id");

            if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(fileID))
            {
                Console.WriteLine("No file name or file ID could be found");
                return;
            }

            string destination = GetArgument(remainingArgs, "--destination");
            if (string.IsNullOrWhiteSpace(destination))
            {
                Console.WriteLine("No file destination provided");
                return;
            }

            if (File.Exists(destination))
            {
                Console.WriteLine("Cannot override file that exists");
                return;
            }

            DownloadFileAction downloadAction = string.IsNullOrWhiteSpace(fileName)
                ? new DownloadFileAction(authorizationSession, destination, fileID)
                : new DownloadFileAction(authorizationSession, destination, bucketID, fileName);

            Stopwatch watch = new Stopwatch();
            watch.Start();
            BackblazeB2ActionResult<BackblazeB2DownloadFileResult> result = await ExecuteActionAsync(downloadAction, "Download file");
            watch.Stop();
            if (result.HasResult)
            {
                double bytesPerSecond = result.Result.ContentLength / ((double)watch.ElapsedTicks / Stopwatch.Frequency);

                Console.WriteLine(string.Format("File successfully downloaded: {0} to {1}", result.Result.FileName, destination));
                Console.WriteLine(string.Format("Download Time: {0} seconds", (double)watch.ElapsedTicks / Stopwatch.Frequency));
                Console.WriteLine(string.Format("Download Speed: {0:0,0.00} bytes / second", bytesPerSecond.ToString("0,0.00", CultureInfo.InvariantCulture)));
            }
        }

        private static async Task DeleteFileAsync(BackblazeB2AuthorizationSession authorizationSession, IEnumerable<string> remainingArgs)
        {
            string fileName = GetArgument(remainingArgs, "--file-name");
            string fileID = GetArgument(remainingArgs, "--file-id");

            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(fileID))
            {
                Console.WriteLine("A file name and file ID must be provided");
                return;
            }

            DeleteFileAction deleteFileAction = new DeleteFileAction(authorizationSession, fileID, fileName);
            BackblazeB2ActionResult<BackblazeB2DeleteFileResult> result = await ExecuteActionAsync(deleteFileAction, "Delete file");
            if (result.HasResult)
            {
                Console.WriteLine(string.Format("File successfully deleted: {0} | {1}", result.Result.FileName, result.Result.FileID));
            }
        }

        private static async Task<BackblazeB2ActionResult<T>> ExecuteActionAsync<T>(BaseAction<T> action, string actionName)
        {
            BackblazeB2ActionResult<T> actionResult = await action.ExecuteAsync();
            if (actionResult.HasErrors)
            {
                string errorMessagesComposed = actionResult.Errors.Select(t => t.Message).Aggregate((a, b) => string.Format("{0}\n{1}", a, b));
                Console.WriteLine(string.Format("Could not execute action {0}. Errors: {1}", actionName, errorMessagesComposed));
            }

            return actionResult;
        }

        private static string GetArgument(IEnumerable<string> args, string option)
        {
            bool returnNextItem = false;
            foreach (string arg in args)
            {
                if (returnNextItem)
                {
                    return arg;
                }

                if (arg.Equals(option, StringComparison.Ordinal))
                {
                    returnNextItem = true;
                }
            }

            return null;
        }
    }
}
