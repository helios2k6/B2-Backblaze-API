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
            string localFilePath = CommonUtils.GetArgument(args, "--file");
            string destinationRemoteFilePath = CommonUtils.GetArgument(args, "--destination");
            int numberOfConnections = GetNumberOfConnections(args);
            if (string.IsNullOrWhiteSpace(localFilePath) || File.Exists(localFilePath) == false)
            {
                Console.WriteLine(string.Format("Invalid arguments sent for --file ({0})", localFilePath));
                return;
            }

            if (string.IsNullOrWhiteSpace(destinationRemoteFilePath)) {
                // Remote file path is optional
                destinationRemoteFilePath = localFilePath;
            }

            FileManifest fileManifest = FileManifestActions.ReadManifestFileFromServerOrReturnNewOne(authorizationSession, bucketID);

            Console.WriteLine("Uploading file");
            UploadFileImpl(authorizationSession, fileManifest, bucketID, localFilePath, destinationRemoteFilePath, numberOfConnections);
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
            IList<string> failedUploads = new List<string>();

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
                        Console.WriteLine("Could not reauthorize account. Aborting");
                        return;
                    }

                    currentAuthorizationSession = authorizeActionResult.Result;
                }

                bool uploadResult = UploadFileImpl(
                    currentAuthorizationSession,
                    fileManifest,
                    bucketID,
                    localFile,
                    localFile, // TODO: Destination file path will be modifyable in the future
                    numberOfConnections
                );

                if (uploadResult == false)
                {
                    failedUploads.Add(localFile);
                }
            }

            // Cycle through and print out files we could not upload
            if (failedUploads.Any())
            {
                Console.WriteLine("Failed to upload the following files:");
                foreach (string failedUpload in failedUploads)
                {
                    Console.WriteLine(failedUpload);
                }
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
            IDictionary<string, FileManifestEntry> destinationPathsToFileManifest = fileManifest.FileEntries.ToDictionary(t => t.DestinationFilePath, t => t);
            // If there's nothing to compare, then there's no point in iterating
            if (destinationPathsToFileManifest.Any() == false)
            {
                return allLocalFiles;
            }

            ISet<string> filesToUpload = new HashSet<string>();
            foreach (string localFile in allLocalFiles)
            {
                string calculatedDestinationFilePath = GetSafeFileName(localFile);
                if (destinationPathsToFileManifest.TryGetValue(calculatedDestinationFilePath, out FileManifestEntry remoteFileManifestEntry))
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

        private static bool UploadFileImpl(
            BackblazeB2AuthorizationSession authorizationSession,
            FileManifest fileManifest,
            string bucketID,
            string localFilePath,
            string remoteDestinationPath,
            int uploadConnections
        )
        {
            try
            {
                FileInfo info = new FileInfo(localFilePath);
                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = info.Length < 1024 * 1024 || uploadConnections == 1
                ? ExecuteUploadAction(new UploadWithSingleConnectionAction(
                    authorizationSession,
                    localFilePath,
                    GetSafeFileName(remoteDestinationPath),
                    bucketID
                ))
                : ExecuteUploadAction(new UploadWithMultipleConnectionsAction(
                    authorizationSession,
                    localFilePath,
                    GetSafeFileName(remoteDestinationPath),
                    bucketID,
                    Constants.FileChunkSize,
                    uploadConnections,
                    CancellationActions.GlobalCancellationToken
                ));

                if (uploadResult.HasResult)
                {
                    FileManifestEntry addedFileEntry = new FileManifestEntry
                    {
                        OriginalFilePath = localFilePath,
                        DestinationFilePath = uploadResult.Result.FileName,
                        SHA1 = SHA1FileHashStore.Instance.GetFileHash(localFilePath),
                        Length = info.Length,
                        LastModified = info.LastWriteTimeUtc.ToBinary(),
                    };
                    fileManifest.Version++;
                    fileManifest.FileEntries = fileManifest.FileEntries.Append(addedFileEntry).ToArray();
                    // Update file manifest if the upload was successful
                    FileManifestActions.WriteManifestFileToServer(authorizationSession, bucketID, fileManifest);

                    return true;
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Cancelled upload");
            }
            catch (Exception ex)
            {
                Console.Write(new StringBuilder()
                    .AppendFormat("An unexpected exception occurred while uploading file {0}", localFilePath)
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

            return false;
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

        /// <summary>
        /// This method sanitizes the the file path so that it can be used on B2. Here are the current set of rules:
        /// 1. Max length is 1024 characters
        /// 2. The characters must be in UTF-8
        /// 3. Backslashes are not allowed
        /// 4. DEL characters (127) are not allowed
        /// 5. File names cannot start with a "/", end with a "/", or contain "//" anywhere
        /// 6. For each segment of the file path, which is the part of the string between each "/", there can only be 
        ///    250 bytes of UTF-8 characters (for multi-byte characters, that can reduce this down to less than 250 characters)
        ///
        /// The following encodings will be used to fix file names for the given rules above:
        /// 1. An exception will be thrown for file paths above 1024 characters
        /// 2. Nothing will be done to ensure UTF-8 encoding, since all strings in C# are UTF-16
        /// 3. All backslashes will be replaced with forward slashes
        /// 4. Nothing, since file paths can't have the DEL character anyways
        /// 5. The very first "/" will be replaced with an empty string. An exception will be thrown for any file path that ends with a "/" or contains a "//"
        /// 6. An exception will be thrown if any segment is longer than 250 bytes
        /// 7. If there's a Windows style drive letter (e.g. "C:\"), this will be converted to the drive letter followed by a forward slash (e.g. "c/")
        /// 
        /// Additionally, we will remove drive letters
        /// </summary>
        /// <param name="filePath">The file path to sanitize</param>
        /// <returns>A santitized file path</returns>
        private static string GetSafeFileName(string filePath)
        {
            if (filePath.Length > 1024)
            {
                throw new InvalidOperationException("The file path cannot be longer than 1024 characters");
            }

            string updatedString = filePath;

            // Convert Windows style drive letters
            if (filePath.IndexOf(":") == 1)
            {
                char driveLetter = Char.ToLowerInvariant(filePath[0]);
                updatedString = updatedString.Substring(3);
                updatedString = updatedString.Insert(0, new string(new[] { driveLetter, '/' }));
            }

            updatedString = updatedString.Replace('\\', '/');
            if (updatedString[0] == '/')
            {
                updatedString = updatedString.Substring(1);
            }

            if (updatedString[updatedString.Length - 1] == '/' || updatedString.IndexOf("//") != -1)
            {
                throw new InvalidOperationException("The file path cannot start or end with a forward slash and cannot have double forward slashes anywhere");
            }

            string[] segments = updatedString.Split('/');
            foreach (string segment in segments)
            {
                byte[] rawBytes = Encoding.UTF8.GetBytes(segment);
                if (rawBytes.Length > 250)
                {
                    throw new InvalidOperationException("No segment of the file path may be greater than 250 bytes when encoded with UTF-8");
                }
            }

            return updatedString;
        }
        #endregion
    }
}
