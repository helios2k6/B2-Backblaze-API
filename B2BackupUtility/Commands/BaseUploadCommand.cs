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
using B2BackupUtility.Database;
using Functional.Maybe;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// The base class for all upload actions
    /// </summary>
    public abstract class BaseUploadCommand : BaseCommand
    {
        #region private fields
        private static int DefaultUploadConnections => 20;
        private static int MinimumFileLengthForMultipleConnections => 1048576;
        private static int DefaultUploadChunkSize => 5242880; // 5 mebibytes
        #endregion

        #region protected properties
        protected int Connections
        {
            get
            {
                if (TryGetArgument(ConnectionsOption, out string rawConnectionsOption))
                {
                    return int.TryParse(rawConnectionsOption, out int numberOfConnections)
                        ? numberOfConnections
                        : DefaultUploadConnections;
                }

                return DefaultUploadConnections;
            }
        }

        protected static string ConnectionsOption => "--connections";
        #endregion

        #region ctor
        public BaseUploadCommand(IEnumerable<string> rawArgs) : base(rawArgs)
        {
        }
        #endregion

        #region protected methods
        protected IEnumerable<BackblazeB2ActionResult<IBackblazeB2UploadResult>> UploadFile(string localFilePath)
        {
            FileInfo info = new FileInfo(localFilePath);
            Database.File file = new Database.File
            {
                FileLength = info.Length,
                FileName = localFilePath,
                FileShardIDs = new string[0],
                LastModified = info.LastWriteTime.ToBinary(),
                SHA1 = SHA1FileHashStore.Instance.ComputeSHA1(localFilePath),
            };

            IEnumerable<BackblazeB2ActionResult<IBackblazeB2UploadResult>> results = Enumerable.Empty<BackblazeB2ActionResult<IBackblazeB2UploadResult>>();
            Stopwatch stopWatch = Stopwatch.StartNew();
            foreach (FileShard fileShard in FileFactory.CreateFileShards(new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read), true))
            {
                // Update Database.File
                file.FileShardIDs = file.FileShardIDs.Append(fileShard.ID).ToArray();

                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = fileShard.Length < MinimumFileLengthForMultipleConnections
                    ? ExecuteUploadAction(
                        new UploadWithSingleConnectionAction(
                            GetOrCreateAuthorizationSession(),
                            BucketID,
                            fileShard.Payload,
                            GetSafeFileName(fileShard.ID),
                            CancellationEventRouter.GlobalCancellationToken
                        ))
                    : ExecuteUploadAction(
                        new UploadWithMultipleConnectionsAction(
                            GetOrCreateAuthorizationSession(),
                            new MemoryStream(fileShard.Payload),
                            GetSafeFileName(fileShard.ID),
                            BucketID,
                            DefaultUploadChunkSize,
                            Connections,
                            CancellationEventRouter.GlobalCancellationToken
                        ));

                results = results.Append(uploadResult);

                if (uploadResult.HasErrors)
                {
                    LogCritical($"Error uploading File Shard for File {localFilePath}. Reason: {uploadResult}");
                    break;
                }
            }
            stopWatch.Stop();

            if (results.All(t => t.HasResult))
            {
                // Update file manifest
                FileDatabaseManifest.AddFile(file);
                UploadFileDatabaseManifest();

                // Print upload statistics
                PrintUploadResult(localFilePath, info.Length, stopWatch.ElapsedTicks);
            }

            return results;
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
        protected static string GetSafeFileName(string filePath)
        {
            if (filePath.Length > 1024)
            {
                throw new InvalidOperationException("The file path cannot be longer than 1024 characters");
            }

            string updatedString = filePath;

            // Convert Windows style drive letters
            if (filePath.IndexOf(":") == 1)
            {
                char driveLetter = char.ToLowerInvariant(filePath[0]);

                // Sometimes, windows will return two backslashes
                int subStringCutOff = filePath[3] == '\\'
                    ? 4
                    : 3;

                updatedString = updatedString.Substring(subStringCutOff);
                updatedString = updatedString.Insert(0, new string(new[] { driveLetter, '/' }));
            }

            updatedString = updatedString.Replace('\\', '/');
            if (updatedString[0] == '/')
            {
                updatedString = updatedString.Substring(1);
            }

            return updatedString;
        }
        #endregion

        #region private methods
        private static BackblazeB2ActionResult<IBackblazeB2UploadResult> ExecuteUploadAction<T>(BaseAction<T> action) where T : IBackblazeB2UploadResult
        {
            BackblazeB2ActionResult<T> uploadResult = action.Execute();
            BackblazeB2ActionResult<IBackblazeB2UploadResult> castedResult;
            if (uploadResult.HasResult)
            {
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

        private void PrintUploadResult(string fileName, long contentLength, long uploadTimeInTicks)
        {
            double bytesPerSecond = contentLength / ((double)uploadTimeInTicks / Stopwatch.Frequency);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"File: {fileName}");
            builder.AppendLine($"Total Content Length: {contentLength:0:n0} bytes");
            builder.AppendLine($"Upload Time: {(double)uploadTimeInTicks / Stopwatch.Frequency} seconds");
            builder.AppendLine($"Upload Speed: {bytesPerSecond:0,0.00} bytes / second");

            LogInfo($"Uploaded File {fileName}");
            LogVerbose(builder.ToString());
        }
        #endregion
    }
}