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
using B2BackblazeBridge.Exceptions;
using Functional.Maybe;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2BackupUtility
{
    public static class UploadActions
    {
        public static async Task UploadFileAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> args)
        {
            string fileToUpload = CommonActions.GetArgument(args, "--file");
            string destination = CommonActions.GetArgument(args, "--destination");

            if (string.IsNullOrWhiteSpace(fileToUpload) || string.IsNullOrWhiteSpace(destination) || File.Exists(fileToUpload) == false)
            {
                Console.WriteLine(string.Format("Invalid arguments sent for --file ({0}) or --destination ({1})", fileToUpload, destination));
                return;
            }

            FileManifest fileManifest = await FileManifestActions.ReadManifestFileFromServerOrReturnNewOneAsync(authorizationSession, bucketID);

            Console.WriteLine("Uploading file");
            await UploadFileImplAsync(authorizationSession, fileManifest, bucketID, fileToUpload, destination, GetNumberOfConnections(args));
        }

        public static async Task UploadFolderAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> args)
        {
            string folder = CommonActions.GetArgument(args, "--folder");
            bool flatten = CommonActions.DoesOptionExist(args, "--flatten");
            bool overrideFiles = CommonActions.DoesOptionExist(args, "--force-override-files");
            if (Directory.Exists(folder) == false)
            {
                Console.WriteLine(string.Format("Folder does not exist: {0}", folder));
                return;
            }

            FileManifest fileManifest = await FileManifestActions.ReadManifestFileFromServerOrReturnNewOneAsync(authorizationSession, bucketID);
            IEnumerable<string> localFilesToUpload = GetFilesToUpload(authorizationSession, fileManifest, bucketID, folder, flatten, overrideFiles);

            // Deduplicate destination files, especially if stuff is going to be flattened
            IEnumerable<LocalFileToRemoteFileMapping> deduplicatedLocalFileToDestinationFiles = FilePathUtilities.GenerateLocalToRemotePathMapping(
                localFilesToUpload,
                flatten
            );

            Console.WriteLine("Uploading folder");
            BackblazeB2AuthorizationSession currentAuthorizationSession = authorizationSession;

            foreach (LocalFileToRemoteFileMapping localFileToDestinationFile in deduplicatedLocalFileToDestinationFiles)
            {
                if (CancellationActions.GlobalCancellationToken.IsCancellationRequested)
                {
                    // If the current upload has been cancelled, just return
                    return;
                }

                if (currentAuthorizationSession.SessionExpirationDate - DateTime.Now < Constants.OneHour)
                {
                    Console.WriteLine("Session is about to end in less than an hour. Renewing.");

                    // Renew the authorization
                    AuthorizeAccountAction authorizeAction = new AuthorizeAccountAction(
                        currentAuthorizationSession.AccountID,
                        currentAuthorizationSession.ApplicationKey
                    );
                    BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizeActionResult =
                        await CommonActions.ExecuteActionAsync(authorizeAction, "Re-Authorize account");
                    if (authorizeActionResult.HasErrors)
                    {
                        // Could not reauthorize. Aborting
                        return;
                    }

                    currentAuthorizationSession = authorizeActionResult.Result;
                }

                await UploadFileImplAsync(
                    currentAuthorizationSession,
                    fileManifest,
                    bucketID,
                    localFileToDestinationFile.LocalFilePath,
                    localFileToDestinationFile.RemoteFilePath,
                    GetNumberOfConnections(args)
                );

                // Write file manifest to the server
                await FileManifestActions.WriteManifestFileToServer(authorizationSession, bucketID, fileManifest);
            }
        }

        private static IEnumerable<string> GetFilesToUpload(
            BackblazeB2AuthorizationSession authorizationSession,
            FileManifest fileManifest,
            string bucketID,
            string folder,
            bool flatten,
            bool overrideFiles
        )
        {
            IEnumerable<string> filesToUpload = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
            if (overrideFiles)
            {
                Console.WriteLine("Force uploading all files that aren't name collisions and duplicates");
                return filesToUpload;
            }

            Console.WriteLine("Filtered files that are already on the server");
            IDictionary<string, string> sha1ToFileNames = fileManifest.FileEntries.ToDictionary(f => f.SHA1, f => f.DestinationFilePath);
            IEnumerable<string> filteredFiles = from localFile in filesToUpload
                                                let localFileSHA1 = SHA1FileHashStore.Instance.GetFileHash(localFile)
                                                let destinationFileName = FilePathUtilities.GetDestinationFileName(localFile, flatten)
                                                where !sha1ToFileNames.ContainsKey(localFileSHA1) || !sha1ToFileNames[localFileSHA1].Equals(destinationFileName, StringComparison.Ordinal)
                                                select localFile;

            return filteredFiles;
        }

        private static async Task UploadFileImplAsync(
            BackblazeB2AuthorizationSession authorizationSession,
            FileManifest fileManifest,
            string bucketID,
            string file,
            string destination,
            int uploadConnections
        )
        {
            try
            {
                FileInfo info = new FileInfo(file);
                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = info.Length < 1024 * 1024
                ? await ExecuteUploadActionAsync(new UploadFileAction(
                    authorizationSession,
                    file,
                    destination,
                    bucketID
                ))
                : await ExecuteUploadActionAsync(new UploadFileUsingMultipleConnectionsAction(
                    authorizationSession,
                    file,
                    destination,
                    bucketID,
                    Constants.FileChunkSize,
                    uploadConnections,
                    CancellationActions.GlobalCancellationToken
                ));

                if (uploadResult.HasResult)
                {
                    FileManifestEntry addedFileEntry = new FileManifestEntry
                    {
                        OriginalFilePath = file,
                        DestinationFilePath = destination,
                        SHA1 = SHA1FileHashStore.Instance.GetFileHash(file),
                    };
                    fileManifest.Version++;
                    fileManifest.FileEntries = fileManifest.FileEntries.Append(addedFileEntry).ToArray();
                    // Update file manifest if the upload was successful
                    await FileManifestActions.WriteManifestFileToServer(authorizationSession, bucketID, fileManifest);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Cancelled upload");
            }
            catch (B2ContractBrokenException ex)
            {
                Console.Write(new StringBuilder()
                    .AppendFormat("An unexpected exception occurred while uploading file {0}", file)
                    .AppendLine()
                    .AppendLine("==Exception Details==")
                    .AppendLine("Message")
                    .AppendLine(ex.Message)
                    .AppendLine()
                    .AppendLine("Stack")
                    .AppendLine(ex.StackTrace)
                    .AppendLine()
                    .AppendLine("Source")
                    .AppendLine(ex.Source)
                    .AppendLine().ToString()
                );
            }
        }

        private static async Task<BackblazeB2ActionResult<IBackblazeB2UploadResult>> ExecuteUploadActionAsync<T>(BaseAction<T> action) where T : IBackblazeB2UploadResult
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            BackblazeB2ActionResult<T> uploadResult = await CommonActions.ExecuteActionAsync(action, "Upload File");
            watch.Stop();
            
            BackblazeB2ActionResult<IBackblazeB2UploadResult> castedResult;
            if (uploadResult.HasResult)
            {
                double bytesPerSecond = uploadResult.Result.ContentLength / ((double)watch.ElapsedTicks / Stopwatch.Frequency);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Upload Successful:");
                builder.AppendFormat("File: {0}", uploadResult.Result.FileName).AppendLine();
                builder.AppendFormat("File ID: {0}", uploadResult.Result.FileID).AppendLine();
                builder.AppendFormat("Total Content Length: {0}", uploadResult.Result.ContentLength).AppendLine();
                builder.AppendFormat("Upload Time: {0} seconds", (double)watch.ElapsedTicks / Stopwatch.Frequency).AppendLine();
                builder.AppendFormat("Upload Speed: {0:0,0.00} bytes / second", bytesPerSecond.ToString("0,0.00", CultureInfo.InvariantCulture)).AppendLine().AppendLine();
                Console.Write(builder.ToString());

                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(
                    (IBackblazeB2UploadResult)uploadResult.Result
                );
            }
            else
            {
                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(
                    Maybe<IBackblazeB2UploadResult>.Nothing,
                    uploadResult.Errors
                );
            }

            return castedResult;
        }

        private static int GetNumberOfConnections(IEnumerable<string> args)
        {
            string connections = CommonActions.GetArgument(args, "--connections");
            int numberOfConnections = Constants.TargetUploadConnections;
            int.TryParse(connections, out numberOfConnections);

            return numberOfConnections > 0 ? numberOfConnections : Constants.TargetUploadConnections;
        }
    }
}
