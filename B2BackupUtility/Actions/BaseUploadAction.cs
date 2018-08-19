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
using System.IO;
using System.Linq;
using System.Text;

namespace B2BackupUtility.Actions
{
    /// <summary>
    /// The base class for all upload actions
    /// </summary>
    public abstract class BaseUploadAction : BaseAction
    {
        #region private fields
        private static int DefaultUploadConnections => 20;
        private static int MinimumFileLengthForMultipleConnections => 1048576;

        private readonly Lazy<FileManifest> _fileManifest;
        #endregion

        #region protected class
        protected sealed class UploadInfo
        {
            public long UploadLength { get; set; }
            public long UploadTimeInTicks { get; set; }
            public BackblazeB2ActionResult<IBackblazeB2UploadResult> B2UploadResult { get; set; }
        }
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

        protected string ConnectionsOption => "--connections";

        protected FileManifest FileManifest => _fileManifest.Value;
        #endregion

        #region ctor
        public BaseUploadAction(IEnumerable<string> rawArgs) : base(rawArgs)
        {
            _fileManifest = new Lazy<FileManifest>(
                () => FileManifestActions.ReadManifestFileFromServerOrReturnNewOne(
                    GetOrCreateAuthorizationSession(),
                    BucketID
                    )
                );
        }
        #endregion

        #region protected methods
        protected UploadInfo UploadFile(
            string localFilePath,
            string remoteDestinationPath
        )
        {
            FileInfo info = new FileInfo(localFilePath);
            UploadInfo uploadInfo = info.Length < MinimumFileLengthForMultipleConnections || Connections == 1
            ? ExecuteUploadAction(new UploadWithSingleConnectionAction(
                GetOrCreateAuthorizationSession(),
                localFilePath,
                GetSafeFileName(remoteDestinationPath),
                BucketID
            ))
            : ExecuteUploadAction(new UploadWithMultipleConnectionsAction(
                GetOrCreateAuthorizationSession(),
                localFilePath,
                GetSafeFileName(remoteDestinationPath),
                BucketID,
                Constants.FileChunkSize,
                Connections,
                CancellationActions.GlobalCancellationToken
            ));

            if (uploadInfo.B2UploadResult.HasResult)
            {
                UpdateFileManifest(localFilePath, uploadInfo.B2UploadResult.Result.FileName, info);
            }

            return uploadInfo;
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

        #region private methods
        private void UpdateFileManifest(string localFilePath, string remoteDestinationFileName, FileInfo fileInfo)
        {
            FileManifestEntry addedFileEntry = new FileManifestEntry
            {
                OriginalFilePath = localFilePath,
                DestinationFilePath = remoteDestinationFileName,
                SHA1 = SHA1FileHashStore.Instance.GetFileHash(localFilePath),
                Length = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc.ToBinary(),
            };
            FileManifest.Version++;
            FileManifest.FileEntries = FileManifest.FileEntries.Append(addedFileEntry).ToArray();
            FileManifestActions.WriteManifestFileToServer(GetOrCreateAuthorizationSession(), BucketID, FileManifest);
        }

        private static UploadInfo ExecuteUploadAction<T>(BaseAction<T> action) where T : IBackblazeB2UploadResult
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            BackblazeB2ActionResult<T> uploadResult = CommonUtils.ExecuteAction(action, "Upload File");
            watch.Stop();

            long uploadLength = 0;
            BackblazeB2ActionResult<IBackblazeB2UploadResult> castedResult;
            if (uploadResult.HasResult)
            {
                uploadLength = uploadResult.Result.ContentLength;
                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(uploadResult.Result);
            }
            else
            {
                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(
                    Maybe<IBackblazeB2UploadResult>.Nothing,
                    uploadResult.Errors
                );
            }

            return new UploadInfo
            {
                B2UploadResult = castedResult,
                UploadLength = uploadLength,
                UploadTimeInTicks = watch.ElapsedTicks,
            };
        }
        #endregion
    }
}