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

using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Represents an action to upload a folder
    /// </summary>
    public sealed class UploadFolderCommand : BaseUploadCommand
    {
        #region private fields
        private static string FolderOption => "--folder";
        private static string OverrideOption => "--override";
        #endregion

        #region public properties
        public static string CommandName => "Upload Folder";

        public static string CommandSwitch => "--upload-folder";

        public static IEnumerable<string> CommandOptions => new[] { FolderOption, OverrideOption, ConnectionsOption };
        #endregion

        #region ctor
        public UploadFolderCommand(IEnumerable<string> rawArgs) : base(rawArgs)
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
                IEnumerable<string> localFilesToUpload = GetFilesToUpload(folder, overrideFiles);
                IList<string> failedUploads = new List<string>();
                foreach (string localFilePath in localFilesToUpload)
                {
                    CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();
                    LogDebug($"Uploading {localFilePath}");
                    IEnumerable<BackblazeB2ActionResult<IBackblazeB2UploadResult>> uploadresult = UploadFile(localFilePath);
                    if (uploadresult.Any(t => t.HasErrors))
                    {
                        failedUploads.Add(localFilePath);
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
            catch (Exception ex)
            {
                LogCritical($"A critical exception occured during upload {ex.Message}");
            }
        }
        #endregion

        #region private methods
        private IEnumerable<string> GetFilesToUpload(
            string folder,
            bool overrideFiles
        )
        {
            IEnumerable<string> allLocalFiles = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
            if (overrideFiles)
            {
                foreach (string localFilePath in allLocalFiles)
                {
                    yield return localFilePath;
                }

                yield break;
            }

            IDictionary<string, Database.File> destinationToManifestEntry = FileDatabaseManifest.Files.ToDictionary(k => k.FileName, v => v);
            foreach (string localFilePath in allLocalFiles)
            {
                if (destinationToManifestEntry.TryGetValue(localFilePath, out Database.File remoteFileEntry))
                {
                    // Need confirm if this is a duplicate or not
                    // 1. If the lengths and last modified dates are the same, then just assume the files are equals (do not upload)
                    // 2. If the lengths are different then the files are not the same (upload)
                    // 3. If the lengths are the same but the last modified dates are different, then we need to perform a SHA-1 check to see
                    //    if the contents are actually different (upload if SHA-1's are different)
                    FileInfo localFileInfo = new FileInfo(localFilePath);
                    if (localFileInfo.Length == remoteFileEntry.FileLength)
                    {
                        // Scenario 3
                        if (localFileInfo.LastWriteTimeUtc.Equals(DateTime.FromBinary(remoteFileEntry.LastModified)) == false)
                        {
                            string sha1OfLocalFile = SHA1FileHashStore.Instance.ComputeSHA1(localFilePath);
                            if (string.Equals(sha1OfLocalFile, remoteFileEntry.SHA1, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                yield return localFilePath;
                            }
                        }
                        // Scenario 1 is implied 
                    }
                    else
                    {
                        // Scenario 2
                        yield return localFilePath;
                    }
                }
                else
                {
                    // We have never uploaded this file to the server
                    yield return localFilePath;
                }
            }
        }

        private IEnumerable<string> FilterAnyDuplicatesDueToFileManifest(
            IEnumerable<string> localFilePath,
            FileDatabaseManifest manifest,
            bool overrideFiles
        )
        {
            if (overrideFiles)
            {
                foreach (string file in localFilePath)
                {
                    yield return file;
                }

                yield break;
            }

            IDictionary<string, Database.File> destinationToManifestEntry = manifest.Files.ToDictionary(k => k.FileName, v => v);
            foreach (string filePath in localFilePath)
            {
                if (destinationToManifestEntry.TryGetValue(filePath, out Database.File remoteFileManifestEntry))
                {
                    // Need confirm if this is a duplicate or not
                    // 1. If the lengths and last modified dates are the same, then just assume the files are equals (do not upload)
                    // 2. If the lengths are different then the files are not the same (upload)
                    // 3. If the lengths are the same but the last modified dates are different, then we need to perform a SHA-1 check to see
                    //    if the contents are actually different (upload if SHA-1's are different)
                    FileInfo localFileInfo = new FileInfo(filePath);
                    if (localFileInfo.Length == remoteFileManifestEntry.FileLength)
                    {
                        // Scenario 3
                        if (localFileInfo.LastWriteTimeUtc.Equals(DateTime.FromBinary(remoteFileManifestEntry.LastModified)) == false)
                        {
                            string sha1OfLocalFile = SHA1FileHashStore.Instance.ComputeSHA1(filePath);
                            if (string.Equals(sha1OfLocalFile, remoteFileManifestEntry.SHA1, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                yield return filePath;
                            }
                        }
                        // Scenario 1 is implied 
                    }
                    else
                    {
                        // Scenario 2
                        yield return filePath;
                    }
                }
                else
                {
                    // We have never uploaded this file to the server
                    yield return filePath;
                }
            }
        }
        #endregion
    }
}
