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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace B2BackupUtility.Actions
{
    /// <summary>
    /// Represents an action to upload a folder
    /// </summary>
    public sealed class UploadFolderAction : BaseUploadAction
    {
        #region private fields
        private static string FolderOption => "--folder";
        private static string OverrideOption => "--override";
        private static string FlattenOption => "--flatten (not supported yet)";
        #endregion

        #region public properties
        public override string ActionName => "Upload Folder";

        public override string ActionSwitch => "--upload-folder";

        public override IEnumerable<string> ActionOptions => new List<string> { FolderOption, OverrideOption, FlattenOption, ConnectionsOption };
        #endregion

        #region ctor
        public UploadFolderAction(IEnumerable<string> rawArgs) : base(rawArgs)
        {
        }
        #endregion

        #region public methods
        public override void ExecuteAction()
        {
            bool hasFolderOption = TryGetArgument(FolderOption, out string folder);
            bool overrideFiles = DoesOptionExist(OverrideOption);
            if (hasFolderOption == false || string.IsNullOrWhiteSpace(folder))
            {
                throw new InvalidOperationException("No folder was provided");
            }

            if (Directory.Exists(folder) == false)
            {
                throw new DirectoryNotFoundException($"Could not find directory {folder}");
            }

            try
            {
                IEnumerable<string> localFilesToUpload = GetFilesToUpload(FileManifest, folder, overrideFiles);
                IList<string> failedUploads = new List<string>();
                foreach (string localFile in localFilesToUpload)
                {
                    CancellationActions.GlobalCancellationToken.ThrowIfCancellationRequested();

                    // TODO: We need to allow destination folders to be specified
                    UploadInfo uploadInfo = UploadFile(localFile, localFile);
                    if (uploadInfo.B2UploadResult.HasErrors)
                    {
                        LogCritical($"Failed to upload {localFile}. {uploadInfo.B2UploadResult.ToString()}");
                    }
                }

                if (failedUploads.Any())
                {
                    LogInfo("Failed to upload the following files:");
                    foreach (string failedUpload in failedUploads)
                    {
                        LogInfo($"{failedUpload}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogCritical("Upload cancelled!");
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
                return allLocalFiles;
            }

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
        #endregion
    }
}