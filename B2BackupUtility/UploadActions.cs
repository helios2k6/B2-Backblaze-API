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
            if (Directory.Exists(folder) == false)
            {
                Console.WriteLine(string.Format("Folder does not exist: {0}", folder));
                return;
            }

            Console.WriteLine("Uploading folder");
            BackblazeB2AuthorizationSession currentAuthorizationSession = authorizationSession;
            IEnumerable<string> files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
            IDictionary<string, ISet<string>> possiblyDuplicateFiles = new Dictionary<string, ISet<string>>();
            foreach (string file in files)
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

                string destination = flatten ? Path.GetFileName(file) : file;
                if (possiblyDuplicateFiles.ContainsKey(destination))
                {
                    Console.WriteLine("Possible duplicate file found");
                    // This file might be similar to other files we know of. Check all of the files
                    // and see if it's similar to them
                    bool isDuplicate = false;
                    foreach (string possibleDuplicate in possiblyDuplicateFiles[destination])
                    {
                        if (CommonActions.AreFilesEqual(file, possibleDuplicate))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    // Remember to add this file to the list of possible duplicates
                    possiblyDuplicateFiles[destination].Add(file);
                    if (isDuplicate)
                    {
                        Console.WriteLine(string.Format("Duplicate file found. Skipping file {0}", file));
                        continue;
                    }

                    // If we're not a duplicate, then we need to rename the destination file to something we know is unique
                    destination = string.Format("{0}_non_duplicate({1}){2}", Path.GetFileNameWithoutExtension(destination), possiblyDuplicateFiles[destination].Count, Path.GetExtension(destination));
                    Console.WriteLine(string.Format("File was not a duplicate. Renaming destination file to: {0}", destination));
                }
                else
                {
                    // Add this entry
                    HashSet<string> possibleDuplicates = new HashSet<string>
                    {
                        file,
                    };
                    possiblyDuplicateFiles[destination] = possibleDuplicates;
                }

                await UploadFileImplAsync(currentAuthorizationSession, bucketID, file, destination);
            }
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
