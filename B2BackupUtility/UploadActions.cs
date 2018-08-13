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
        #region public methods
        public static void UploadFile(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> args)
        {
            string fileToUpload = CommonUtils.GetArgument(args, "--file");
            string destination = CommonUtils.GetArgument(args, "--destination");
            int numberOfConnections = GetNumberOfConnections(args);
            if (string.IsNullOrWhiteSpace(fileToUpload) || string.IsNullOrWhiteSpace(destination) || File.Exists(fileToUpload) == false)
            {
                Console.WriteLine(string.Format("Invalid arguments sent for --file ({0}) or --destination ({1})", fileToUpload, destination));
                return;
            }

            FileManifest fileManifest = FileManifestActions.ReadManifestFileFromServerOrReturnNewOne(authorizationSession, bucketID);

            Console.WriteLine("Uploading file");
            UploadFileImpl(authorizationSession, fileManifest, bucketID, fileToUpload, numberOfConnections);
        }

        public static void UploadFolder(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> args)
        {
            string folder = CommonUtils.GetArgument(args, "--folder");
            bool overrideFiles = CommonUtils.DoesOptionExist(args, "--force-override-files");
            int numberOfConnections = GetNumberOfConnections(args);
            if (Directory.Exists(folder) == false)
            {
                Console.WriteLine(string.Format("Folder does not exist: {0}", folder));
                return;
            }

            FileManifest fileManifest = FileManifestActions.ReadManifestFileFromServerOrReturnNewOne(authorizationSession, bucketID);
            IEnumerable<string> localFilesToUpload = GetFilesToUpload(fileManifest, folder, overrideFiles);

            Console.WriteLine("Uploading folder");
            BackblazeB2AuthorizationSession currentAuthorizationSession = authorizationSession;
            foreach (string localFile in localFilesToUpload)
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
                        CommonUtils.ExecuteAction(authorizeAction, "Re-Authorize account");
                    if (authorizeActionResult.HasErrors)
                    {
                        // Could not reauthorize. Aborting
                        return;
                    }

                    currentAuthorizationSession = authorizeActionResult.Result;
                }

                UploadFileImpl(
                    currentAuthorizationSession,
                    fileManifest,
                    bucketID,
                    localFile,
                    numberOfConnections
                );
            }
        }
        #endregion

        #region private methods
        private static IEnumerable<string> GetFilesToUpload(
            FileManifest fileManifest,
            string folder,
            bool overrideFiles
        )
        {
            IEnumerable<string> allLocalFiles = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
            if (overrideFiles)
            {
                Console.WriteLine("Force uploading all files that aren't name collisions and duplicates");
                return allLocalFiles;
            }

            Console.WriteLine("Filtered files that are already on the server");
            IDictionary<string, FileManifestEntry> originalFilePathToFileManifest = fileManifest.FileEntries.ToDictionary(t => t.OriginalFilePath, t => t);
            // If there's nothing to compare, then there's no point in iterating
            if (originalFilePathToFileManifest.Any() == false)
            {
                return allLocalFiles;
            }

            ISet<string> filesToUpload = new HashSet<string>();
            foreach (string localFile in allLocalFiles)
            {
                if (originalFilePathToFileManifest.TryGetValue(localFile, out FileManifestEntry remoteFileManifestEntry))
                {
                    // Need confirm if this is a duplicate or not
                    // 1. If the lengths and last modified dates are the same, then just assume the files are equals (do not upload)
                    // 2. If the lengths are different then the files are not the same (upload)
                    // 3. If the lengths are the same but the last modified dates are different, then we need to perform a SHA-1 check to see
                    //    if the contents are actually different (upload if SHA-1's are different)
                    FileInfo localFileInfo = new FileInfo(localFile);
                    if (localFileInfo.Length == remoteFileManifestEntry.Length)
                    {
                        // Scenario 3
                        if (localFileInfo.LastWriteTimeUtc.Equals(DateTime.FromBinary(remoteFileManifestEntry.LastModified)) == false)
                        {
                            string sha1OfLocalFile = SHA1FileHashStore.Instance.GetFileHash(localFile);
                            if (string.Equals(sha1OfLocalFile, remoteFileManifestEntry.SHA1, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                filesToUpload.Add(localFile);
                            }
                        }
                        // Scenario 1 is implied 
                    }
                    else
                    {
                        // Scenario 2
                        filesToUpload.Add(localFile);
                    }
                }
                else
                {
                    // We have never uploaded this file to the server
                    filesToUpload.Add(localFile);
                }
            }

            return filesToUpload;
        }

        private static void UploadFileImpl(
            BackblazeB2AuthorizationSession authorizationSession,
            FileManifest fileManifest,
            string bucketID,
            string file,
            int uploadConnections
        )
        {
            try
            {
                FileInfo info = new FileInfo(file);
                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = info.Length < 1024 * 1024 || uploadConnections == 1
                ? ExecuteUploadAction(new UploadFileAction(
                    authorizationSession,
                    file,
                    file,
                    bucketID
                ))
                : ExecuteUploadAction(new UploadFileUsingMultipleConnectionsAction(
                    authorizationSession,
                    file,
                    file,
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
                        DestinationFilePath = uploadResult.Result.FileName,
                        SHA1 = SHA1FileHashStore.Instance.GetFileHash(file),
                        Length = info.Length,
                        LastModified = info.LastWriteTimeUtc.ToBinary(),
                    };
                    fileManifest.Version++;
                    fileManifest.FileEntries = fileManifest.FileEntries.Append(addedFileEntry).ToArray();
                    // Update file manifest if the upload was successful
                    FileManifestActions.WriteManifestFileToServer(authorizationSession, bucketID, fileManifest);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Cancelled upload");
            }
            catch (Exception ex)
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

        private static BackblazeB2ActionResult<IBackblazeB2UploadResult> ExecuteUploadAction<T>(BaseAction<T> action) where T : IBackblazeB2UploadResult
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            BackblazeB2ActionResult<T> uploadResult = CommonUtils.ExecuteAction(action, "Upload File");
            watch.Stop();

            BackblazeB2ActionResult<IBackblazeB2UploadResult> castedResult;
            if (uploadResult.HasResult)
            {
                double bytesPerSecond = uploadResult.Result.ContentLength / ((double)watch.ElapsedTicks / Stopwatch.Frequency);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Upload Successful:");
                builder.AppendFormat("File: {0}", uploadResult.Result.FileName).AppendLine();
                builder.AppendFormat("File ID: {0}", uploadResult.Result.FileID).AppendLine();
                builder.AppendFormat("Total Content Length: {0:n0} bytes", uploadResult.Result.ContentLength).AppendLine();
                builder.AppendFormat("Upload Time: {0} seconds", (double)watch.ElapsedTicks / Stopwatch.Frequency).AppendLine();
                builder.AppendFormat("Upload Speed: {0:0,0.00} bytes / second", bytesPerSecond.ToString("0,0.00", CultureInfo.InvariantCulture)).AppendLine().AppendLine();
                Console.Write(builder.ToString());

                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(uploadResult.Result);
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
            string connections = CommonUtils.GetArgument(args, "--connections");
            int numberOfConnections = Constants.TargetUploadConnections;
            int.TryParse(connections, out numberOfConnections);

            return numberOfConnections > 0 ? numberOfConnections : Constants.TargetUploadConnections;
        }
        #endregion
    }
}
