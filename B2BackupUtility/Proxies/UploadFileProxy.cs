/* 
 * Copyright (c) 2023 Andrew Johnson
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
using B2BackupUtility.Proxies.Exceptions;
using B2BackupUtility.UploadManagers;
using B2BackupUtility.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// The proxy that's in charge of uploading files to the remote file system
    /// </summary>
    public sealed class UploadFileProxy : BaseRemoteFileSystemProxy, ILogNotifier
    {
        #region public properties
        public static string Name => "Upload File Proxy";
        #endregion

        #region private fields
        private const char PathSeparator = '/';
        private readonly TimeSpan MaxTimeBetweenFileManifestUploads = TimeSpan.FromMinutes(5);
        #endregion

        #region ctor
        public UploadFileProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Uploads a local folder
        /// </summary>
        /// <param name="authorizationSessionGenerator">
        /// A function that returns an authorization session. This is required because this can be a 
        /// very long running function and this gives callers the opportunity to fetch a new 
        /// authorization session should this run over 24 hours
        /// </param>
        /// <param name="localFolderPath">The path to the local folder that contains the files to upload</param>
        /// <param name="rootDestinationFolder">The root destination folder to upload the files to</param>
        /// <param name="shouldOverride">
        /// <param name="dryRun">Specifies whether to do a dry run</param>
        /// Whether to overide old files. If false, this will not throw an exception, but
        /// instead will quietly skip that file
        /// </param>
        public void AddFolder(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            string localFolderPath,
            string rootDestinationFolder,
            bool shouldOverride,
            bool dryRun
        )
        {
            this.Debug($"Adding folder: {localFolderPath} to {rootDestinationFolder}");
            UploadFiles(
                authorizationSessionGenerator,
                GetFilesToUpload(Path.GetFullPath(localFolderPath), rootDestinationFolder, shouldOverride),
                dryRun
            );
        }

        /// <summary>
        /// Adds a local file to the Remote File System
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="localFilePath">The local path to the </param>
        /// <param name="remoteDestinationPath">The destination to upload to</param>
        /// <param name="shouldOverride">Whether to overide old files</param>
        public void AddLocalFile(
            BackblazeB2AuthorizationSession authorizationSession,
            string localFilePath,
            string remoteDestinationPath,
            bool shouldOverride
        )
        {
            this.Debug($"Adding local file: {localFilePath} to {remoteDestinationPath}");
            if (shouldOverride == false && TryGetFileByName(remoteDestinationPath, out Database.File existingFile))
            {
                throw new FailedToUploadFileException("Cannot override existing remote file");
            }

            UploadFiles(
                () => authorizationSession,
                new Dictionary<string, string> { { Path.GetFullPath(localFilePath), remoteDestinationPath } },
                false
            );
        }
        #endregion

        #region private methods
        private void UploadFiles(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            IDictionary<string, string> absoluteLocalFilePathsToDestinationFilePaths,
            bool dryRun
        )
        {
            if (dryRun)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Would have uploaded the following files");
                foreach (KeyValuePair<string, string> absoluteToDestinationPath in absoluteLocalFilePathsToDestinationFilePaths)
                {
                    builder.AppendLine($"{absoluteToDestinationPath.Key} -> {absoluteToDestinationPath.Value}");
                }
                this.Info($"DRY RUN. {builder}");
                return;
            }

            using (TieredUploadManager uploadManager = new TieredUploadManager(authorizationSessionGenerator, Config, CancellationEventRouter.GlobalCancellationToken))
            {
                IDictionary<string, ISet<string>> localFilePathToUploadIDs = new Dictionary<string, ISet<string>>();
                IDictionary<string, ISet<string>> localFilePathToFinishedUploadIDs = new Dictionary<string, ISet<string>>();
                IDictionary<string, string> uploadIDsToLocalFilePath = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> localPathToRemotePath in absoluteLocalFilePathsToDestinationFilePaths)
                {
                    string localFilePath = localPathToRemotePath.Key;
                    localFilePathToUploadIDs[localFilePath] = new HashSet<string>();
                    localFilePathToFinishedUploadIDs[localFilePath] = new HashSet<string>();
                    foreach (Lazy<FileShard> lazyFileShard in FileShardFactory.CreateLazyFileShards(localFilePath))
                    {
                        string uploadID = uploadManager.AddLazyFileShard(lazyFileShard);

                        localFilePathToUploadIDs[localFilePath].Add(uploadID);
                        uploadIDsToLocalFilePath[uploadID] = localFilePath;
                    }
                }

                // This part is tricky because all of these event handlers will be executed on background threads. Because of this,
                // we will need to lock the SendNotification call in order to prevent any race conditions or data corruption. We 
                // know the main thread is blocked on these background threads because we have called the "Wait()" method.
                object localLockObject = new object();
                IDictionary<string, ISet<UploadManagerEventArgs>> localFileToFileShardIDs = new Dictionary<string, ISet<UploadManagerEventArgs>>();
                ISet<string> localFilesThatHaveAlreadyStarted = new HashSet<string>();
                DateTime lastFileManifestUpload = DateTime.Now;
                bool uploadedManifest = true;
                void HandleOnUploadBegin(object sender, UploadManagerEventArgs eventArgs)
                {
                    lock (localLockObject)
                    {
                        string localFile = uploadIDsToLocalFilePath[eventArgs.UploadID];
                        if (localFilesThatHaveAlreadyStarted.Contains(localFile) == false)
                        {
                            localFilesThatHaveAlreadyStarted.Add(localFile);
                            this.Info($"Begin uploading file: {localFile}");
                        }
                    }
                }

                void HandleOnUploadFailed(object sender, UploadManagerEventArgs eventArgs)
                {
                    lock (localLockObject)
                    {
                        this.Critical($"Failed to upload file. {uploadIDsToLocalFilePath[eventArgs.UploadID]} | {eventArgs.UploadResult.ToString()}");
                    }
                }

                void HandleOnUploadFinished(object sender, UploadManagerEventArgs eventArgs)
                {
                    lock (localLockObject)
                    {
                        string localFilePath = uploadIDsToLocalFilePath[eventArgs.UploadID];
                        if (localFileToFileShardIDs.TryGetValue(localFilePath, out ISet<UploadManagerEventArgs> uploadEvents) == false)
                        {
                            uploadEvents = new HashSet<UploadManagerEventArgs>();
                            localFileToFileShardIDs[localFilePath] = uploadEvents;
                        }

                        uploadEvents.Add(eventArgs);
                        localFilePathToFinishedUploadIDs[localFilePath].Add(eventArgs.UploadID);

                        int currentNumberOfUploadedShards = localFilePathToFinishedUploadIDs[localFilePath].Count;
                        int totalNumberOfShards = localFilePathToUploadIDs[localFilePath].Count;
                        if (totalNumberOfShards == currentNumberOfUploadedShards)
                        {
                            this.Info($"Finished uploading file: {localFilePath}");

                            string[] orderedShardIDs = new string[uploadEvents.Count];
                            string[] orderedShardHashes = new string[uploadEvents.Count];

                            foreach (UploadManagerEventArgs uploadEvent in uploadEvents)
                            {
                                orderedShardIDs[(int)uploadEvent.FileShardPieceNumber] = uploadEvent.FileShardID;
                                orderedShardHashes[(int)uploadEvent.FileShardPieceNumber] = uploadEvent.FileShardSHA1;
                            }

                            string destinationPath = absoluteLocalFilePathsToDestinationFilePaths[localFilePath];

                            // Create file
                            FileInfo info = new FileInfo(localFilePath);
                            Database.File file = new Database.File
                            {
                                FileID = Guid.NewGuid().ToString(),
                                FileLength = info.Length,
                                FileName = destinationPath,
                                FileShardIDs = orderedShardIDs,
                                FileShardHashes = orderedShardHashes,
                                LastModified = info.LastWriteTimeUtc.ToBinary(),
                                SHA1 = SHA1FileHashStore.Instance.ComputeSHA1(localFilePath),
                            };

                            // Update manifest
                            if (TryGetFileByName(destinationPath, out Database.File fileThatExists))
                            {
                                // Remove old file first
                                RemoveFile(fileThatExists);
                            }

                            uploadedManifest = false;
                            AddFile(file);
                            if (DateTime.Now - lastFileManifestUpload >= MaxTimeBetweenFileManifestUploads)
                            {
                                if (TryUploadFileDatabaseManifest(authorizationSessionGenerator()) == false)
                                {
                                    this.Critical("Failed to upload file manifest. Going to do it on the next try");
                                    uploadedManifest = true;
                                }
                            }
                        }
                        else
                        {
                            double percentFinished = (double)currentNumberOfUploadedShards / totalNumberOfShards * 100.0;
                            this.Info($"{localFilePath} - {percentFinished:N2}% uploaded");
                        }
                    }
                }

                void HandleOnUploadTierChanged(object sender, UploadManagerEventArgs eventArgs)
                {
                    lock (localLockObject)
                    {
                        this.Warning($"File tier changed. {uploadIDsToLocalFilePath[eventArgs.UploadID]} -> {eventArgs.NewUploadTier}");
                    }
                }

                // Hook up events
                uploadManager.OnUploadBegin += HandleOnUploadBegin;
                uploadManager.OnUploadFailed += HandleOnUploadFailed;
                uploadManager.OnUploadFinished += HandleOnUploadFinished;
                uploadManager.OnUploadTierChanged += HandleOnUploadTierChanged;

                uploadManager.SealUploadManager();
                uploadManager.Execute();
                uploadManager.Wait();

                // Specifically lock this part so that there is not possibility of a rogue thread
                // firing an event while this main thread is in the middle of cleaning it up
                lock (localLockObject)
                {
                    // Unsubscribe from events
                    uploadManager.OnUploadBegin -= HandleOnUploadBegin;
                    uploadManager.OnUploadFailed -= HandleOnUploadFailed;
                    uploadManager.OnUploadFinished -= HandleOnUploadFinished;
                    uploadManager.OnUploadTierChanged -= HandleOnUploadTierChanged;
                }

                // Ensure that we've uploaded the file manifest and if not upload it again
                if (uploadedManifest == false)
                {
                    this.Verbose("Uploading file mainfest");
                    while (TryUploadFileDatabaseManifest(authorizationSessionGenerator()) == false)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                    this.Verbose("Successfully uploaded file manifest");
                }
            }
        }

        private IDictionary<string, string> GetFilesToUpload(
            string absoluteLocalFolder,
            string rootDestinationFolder,
            bool overrideFiles
        )
        {
            // Sanitize root destination folder
            rootDestinationFolder = rootDestinationFolder.Replace('\\', PathSeparator);

            string GetDestinationPath(string absoluteLocalPath)
            {
                StringBuilder pathBuilder = new StringBuilder();
                pathBuilder.Append(rootDestinationFolder);

                if (rootDestinationFolder.Last() != PathSeparator)
                {
                    pathBuilder.Append(PathSeparator);
                }

                string sanitizedLocalPath = absoluteLocalPath
                    .Replace('\\', PathSeparator)
                    .Replace("\\\\", PathSeparator.ToString(), StringComparison.Ordinal)
                    .Substring(absoluteLocalFolder.Length);

                if (sanitizedLocalPath.First() == PathSeparator)
                {
                    sanitizedLocalPath = sanitizedLocalPath.Substring(1);
                }

                pathBuilder.Append(sanitizedLocalPath);
                return pathBuilder.ToString();
            }

            IEnumerable<string> allLocalFiles = Directory.EnumerateFiles(absoluteLocalFolder, "*", SearchOption.AllDirectories);
            if (overrideFiles)
            {
                return allLocalFiles.ToDictionary(localFilePath => Path.GetFullPath(localFilePath), GetDestinationPath);
            }

            IDictionary<string, string> localPathsToRemotePaths = new Dictionary<string, string>();
            Parallel.ForEach(allLocalFiles, localFilePath =>
            {
                string predictedDestinationPath = GetDestinationPath(localFilePath);
                if (TryGetFileByName(predictedDestinationPath, out Database.File remoteFileEntry))
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
                                lock (localPathsToRemotePaths)
                                {
                                    localPathsToRemotePaths[localFilePath] = predictedDestinationPath;
                                }
                            }
                        }
                        // Scenario 1 is implied 
                    }
                    else
                    {
                        // Scenario 2
                        lock (localPathsToRemotePaths)
                        {
                            localPathsToRemotePaths[localFilePath] = predictedDestinationPath;
                        }
                    }
                }
                else
                {
                    // We have never uploaded this file to the server
                    lock (localPathsToRemotePaths)
                    {
                        localPathsToRemotePaths[localFilePath] = predictedDestinationPath;
                    }
                }
            });

            return localPathsToRemotePaths;
        }
        #endregion
    }
}