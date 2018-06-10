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
        public static async Task UploadFileAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> remainingArgs)
        {
            string fileToUpload = CommonActions.GetArgument(remainingArgs, "--file");
            string destination = CommonActions.GetArgument(remainingArgs, "--destination");
            if (string.IsNullOrWhiteSpace(fileToUpload) || string.IsNullOrWhiteSpace(destination) || File.Exists(fileToUpload) == false)
            {
                Console.WriteLine(string.Format("Invalid arguments sent for --file ({0}) or --destination ({1})", fileToUpload, destination));
                return;
            }

            Console.WriteLine("Uploading file");
            await UploadFileImplAsync(authorizationSession, bucketID, fileToUpload, destination);
        }

        public static async Task UploadFolderAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> remainingArgs)
        {
            string folder = CommonActions.GetArgument(remainingArgs, "--folder");
            bool flatten = CommonActions.DoesOptionExist(remainingArgs, "--flatten");
            bool overrideFiles = CommonActions.DoesOptionExist(remainingArgs, "--force-override-files");
            if (Directory.Exists(folder) == false)
            {
                Console.WriteLine(string.Format("Folder does not exist: {0}", folder));
                return;
            }

            Console.WriteLine("Prefetching file list");
            // Get a list of files that are already on the server
            ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileNames(authorizationSession, bucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = await CommonActions.ExecuteActionAsync(listFilesAction, "List files");
            if (listFilesActionResult.HasErrors)
            {
                Console.WriteLine(string.Format("Unable to prefetch folder. Reason: {0}", listFilesActionResult.Errors.First().Message));
                return;
            }

            Tuple<bool, IEnumerable<string>> files = await TryGetFilesToUploadAsync(authorizationSession, bucketID, folder, flatten, overrideFiles);
            if (files.Item1 == false)
            {
                Console.WriteLine("Could not get files to upload");
                return;
            }

            // Deduplicate destination files, especially if stuff is going to be flattened
            IEnumerable<LocalFileToRemoteFileMapping> deduplicatedLocalFileToDestinationFiles = FilePathUtilities.GenerateLocalToRemotePathMapping(files.Item2, flatten);

            Console.WriteLine("Uploading folder");
            BackblazeB2AuthorizationSession currentAuthorizationSession = authorizationSession;
            foreach (LocalFileToRemoteFileMapping localFileToDestinationFile in deduplicatedLocalFileToDestinationFiles)
            {
                if (currentAuthorizationSession.SessionExpirationDate - DateTime.Now < Constants.OneHour)
                {
                    Console.WriteLine("Session is about to end in less than an hour. Renewing.");

                    // Renew the authorization
                    AuthorizeAccountAction authorizeAction = new AuthorizeAccountAction(currentAuthorizationSession.AccountID, currentAuthorizationSession.ApplicationKey);
                    BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizeActionResult = await CommonActions.ExecuteActionAsync(authorizeAction, "Re-Authorize account");
                    if (authorizeActionResult.HasErrors)
                    {
                        // Could not reauthorize. Aborting
                        return;
                    }

                    currentAuthorizationSession = authorizeActionResult.Result;
                }

                await UploadFileImplAsync(currentAuthorizationSession, bucketID, localFileToDestinationFile.LocalFilePath, localFileToDestinationFile.RemoteFilePath);
            }
        }

        private static async Task<Tuple<bool, IEnumerable<string>>> TryGetFilesToUploadAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, string folder, bool flatten, bool overrideFiles)
        {
            IEnumerable<string> filesToUpload = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
            if (overrideFiles)
            {
                Console.WriteLine("Force uploading all files that aren't name collisions and duplicates");
                return Tuple.Create(true, filesToUpload);
            }

            // Get a list of files that are already on the server
            ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileNames(authorizationSession, bucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = await CommonActions.ExecuteActionAsync(listFilesAction, "List files");
            if (listFilesActionResult.HasErrors)
            {
                Console.WriteLine(string.Format("Unable to prefetch folder. Reason: {0}", listFilesActionResult.Errors.First().Message));
                return Tuple.Create<bool, IEnumerable<string>>(false, null);
            }

            Console.WriteLine("Filtered files that are already on the server");
            BackblazeB2ListFilesResult listFiles = listFilesActionResult.Result;
            IDictionary<string, string> sha1ToFileNames = listFiles.Files.ToDictionary(e => e.ContentSha1, e => e.FileName);
            IEnumerable<string> filteredFiles = from localFile in filesToUpload
                                                let localFileSHA1 = SHA1FileHashStore.Instance.GetFileHash(localFile)
                                                let destinationFileName = FilePathUtilities.GetDestinationFileName(localFile, flatten)
                                                where !sha1ToFileNames.ContainsKey(localFileSHA1) || !sha1ToFileNames[localFileSHA1].Equals(destinationFileName, StringComparison.Ordinal)
                                                select localFile;

            return Tuple.Create(true, filteredFiles);
        }

        private static async Task UploadFileImplAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID, string file, string destination)
        {
            UploadFileUsingMultipleConnectionsAction uploadAction = new UploadFileUsingMultipleConnectionsAction(
                authorizationSession,
                file,
                destination,
                bucketID,
                Constants.FileChunkSize,
                Constants.TargetUploadConnections
            );

            Stopwatch watch = new Stopwatch();
            watch.Start();
            BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> uploadResult = await CommonActions.ExecuteActionAsync(uploadAction, "Upload File");
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
                builder.AppendFormat("Upload Speed: {0:0,0.00} bytes / second", bytesPerSecond.ToString("0,0.00", CultureInfo.InvariantCulture)).AppendLine().AppendLine();
                Console.Write(builder.ToString());
            }
        }
    }
}