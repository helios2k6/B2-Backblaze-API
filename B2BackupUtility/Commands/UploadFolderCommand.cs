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
        private static string FlattenOption => "--flatten";
        #endregion

        #region public properties
        public static string ActionName => "Upload Folder";

        public static string CommandSwitch => "--upload-folder";

        public static IEnumerable<string> CommandOptions => new[] { FolderOption, OverrideOption, FlattenOption, ConnectionsOption };
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
            bool flatten = DoesOptionExist(FlattenOption);
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
                IEnumerable<LocalToRemoteFileMapping> localToRemoteFileMappings = GetFilesToUpload(folder, overrideFiles, flatten);
                IList<LocalToRemoteFileMapping> failedUploads = new List<LocalToRemoteFileMapping>();
                foreach (LocalToRemoteFileMapping mapping in localToRemoteFileMappings)
                {
                    CancellationActions.GlobalCancellationToken.ThrowIfCancellationRequested();
                    LogDebug($"Uploading {mapping.LocalFilePath}");
                    UploadInfo uploadInfo = UploadFile(mapping.LocalFilePath, mapping.RemoteDestinationPath);
                    if (uploadInfo.B2UploadResult.HasErrors)
                    {
                        failedUploads.Add(mapping);
                    }
                }

                if (failedUploads.Any())
                {
                    LogInfo("Failed to upload the following files:");
                    foreach (LocalToRemoteFileMapping failedUpload in failedUploads)
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
        private IEnumerable<LocalToRemoteFileMapping> GetFilesToUpload(
            string folder,
            bool overrideFiles,
            bool flatten
        )
        {
            return FilterAnyDuplicatesDueToFileManifest(FilterAnyDuplicatesDueToRemoteFilePath(
                GetAllLocalToRemoteFileMappings(folder, flatten)),
                FileManifest,
                overrideFiles
            );
        }

        private IEnumerable<LocalToRemoteFileMapping> GetAllLocalToRemoteFileMappings(string folder, bool flatten)
        {
            IList<string> allLocalFiles = new List<string>(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories));
            foreach (string localFile in allLocalFiles)
            {
                yield return new LocalToRemoteFileMapping
                {
                    LocalFilePath = localFile,
                    RemoteDestinationPath = GetDestinationPath(localFile, flatten),
                };
            }
        }

        private IEnumerable<LocalToRemoteFileMapping> FilterAnyDuplicatesDueToRemoteFilePath(IEnumerable<LocalToRemoteFileMapping> mappings)
        {
            IDictionary<string, int> remoteDuplicateCounter = new Dictionary<string, int>();
            foreach (LocalToRemoteFileMapping mapping in mappings)
            {
                if (remoteDuplicateCounter.TryGetValue(mapping.RemoteDestinationPath, out int counter) == false)
                {
                    counter = 0;
                    remoteDuplicateCounter.Add(mapping.RemoteDestinationPath, 0);
                }

                remoteDuplicateCounter[mapping.RemoteDestinationPath] = counter + 1;
            }

            foreach (LocalToRemoteFileMapping mapping in mappings)
            {
                if (remoteDuplicateCounter[mapping.RemoteDestinationPath] < 2)
                {
                    yield return mapping;
                }
                else
                {
                    LogInfo($"Skipping {mapping.LocalFilePath}. This file would have been a duplicate on the server");
                }
            }
        }

        private IEnumerable<LocalToRemoteFileMapping> FilterAnyDuplicatesDueToFileManifest(
            IEnumerable<LocalToRemoteFileMapping> mappings,
            FileManifest manifest,
            bool overrideFiles
        )
        {
            if (overrideFiles)
            {
                foreach (LocalToRemoteFileMapping mapping in mappings)
                {
                    yield return mapping;
                }

                yield break;
            }

            IDictionary<string, FileManifestEntry> destinationToManifestEntry = manifest.FileEntries.ToDictionary(k => k.DestinationFilePath, v => v);
            foreach (LocalToRemoteFileMapping mapping in mappings)
            {
                if (destinationToManifestEntry.TryGetValue(mapping.RemoteDestinationPath, out FileManifestEntry remoteFileManifestEntry))
                {
                    // Need confirm if this is a duplicate or not
                    // 1. If the lengths and last modified dates are the same, then just assume the files are equals (do not upload)
                    // 2. If the lengths are different then the files are not the same (upload)
                    // 3. If the lengths are the same but the last modified dates are different, then we need to perform a SHA-1 check to see
                    //    if the contents are actually different (upload if SHA-1's are different)
                    FileInfo localFileInfo = new FileInfo(mapping.LocalFilePath);
                    if (localFileInfo.Length == remoteFileManifestEntry.Length)
                    {
                        // Scenario 3
                        if (localFileInfo.LastWriteTimeUtc.Equals(DateTime.FromBinary(remoteFileManifestEntry.LastModified)) == false)
                        {
                            string sha1OfLocalFile = SHA1FileHashStore.Instance.GetFileHash(mapping.LocalFilePath);
                            if (string.Equals(sha1OfLocalFile, remoteFileManifestEntry.SHA1, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                yield return mapping;
                            }
                        }
                        // Scenario 1 is implied 
                    }
                    else
                    {
                        // Scenario 2
                        yield return mapping;
                    }
                }
                else
                {
                    // We have never uploaded this file to the server
                    yield return mapping;
                }
            }
        }

        private static string GetDestinationPath(string localFilePath, bool flatten)
        {
            return GetSafeFileName(flatten ? Path.GetFileName(localFilePath) : localFilePath);
        }
        #endregion
    }
}
