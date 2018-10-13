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
using B2BackupUtility.Encryption;
using B2BackupUtility.Proxies.Exceptions;
using B2BackupUtility.UploadManagers;
using Functional.Maybe;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// The proxy that's in charge of uploading files to the remote file system
    /// </summary>
    public sealed class UploadFileProxy : BaseRemoteFileSystemProxy
    {
        #region private fields
        private static int DefaultFilesToUploadBeforeUploadingManifest => 5;
        private static int DefaultReuploadAttempts => 3;
        private static int DefaultUploadConnections => 20;
        private static int DefaultUploadChunkSize => 5242880; // 5 mebibytes
        private static int MaxConsecutiveFileManifestUploadFailures => 3;
        #endregion

        #region public properties
        public static string Name => "Upload File Proxy";

        public static string BeginUploadFile => "Begin Upload File";
        public static string FailedToUploadFile => "Failed To Upload File";
        public static string FailedToUploadFileManifest => "Failed To Upload File Manifest";
        public static string SkippedUploadFile => "Skip Uploading File";
        public static string FinishUploadFile => "Finished Uploading File";
        public static string FileTierChanged => "File Tier Changed";
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
        /// <param name="localFilePath">The local path to the </param>
        /// <param name="shouldOverride">
        /// Whether to overide old files. If false, this will not throw an exception, but
        /// instead will quietly skip that file
        /// </param>
        public void AddFolder(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            string localFolderPath,
            bool shouldOverride
        )
        {
            if (Directory.Exists(localFolderPath) == false)
            {
                throw new DirectoryNotFoundException($"Could not find directory {localFolderPath}");
            }

            UploadFiles(authorizationSessionGenerator, GetFilesToUpload(localFolderPath, shouldOverride), shouldOverride);
        }

        /// <summary>
        /// Adds a local file to the Remote File System
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="localFilePath">The local path to the </param>
        /// <param name="shouldOverride">Whether to overide old files</param>
        public void AddLocalFile(
            BackblazeB2AuthorizationSession authorizationSession,
            string localFilePath,
            bool shouldOverride
        )
        {
            string absoluteFilePath = Path.GetFullPath(localFilePath);
            if (System.IO.File.Exists(absoluteFilePath) == false)
            {
                throw new FileNotFoundException("Could not find file to upload", absoluteFilePath);
            }

            // Check to see if the file exists already
            if (TryGetFileByName(absoluteFilePath, out Database.File fileThatExists))
            {
                // If we can't override, we need to throw an exception
                if (shouldOverride == false)
                {
                    throw new FailedToUploadFileException("File already exists and we are not allowed to override it!")
                    {
                        File = absoluteFilePath,
                    };
                }

                // Remove the file from the manifest
                RemoveFile(fileThatExists);
            }

            UploadFiles(() => authorizationSession, new[] { absoluteFilePath }, shouldOverride);
        }
        #endregion

        #region private methods
        private void UploadFiles(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            IEnumerable<string> localFilePaths,
            bool shouldOverride
        )
        {
            // Do all of the sanity checks first, so we don't have to deal with aggregation exceptions
            IList<string> absoluteLocalFilePaths = new List<string>();
            foreach (string relativeLocalFilePath in localFilePaths)
            {
                string absoluteFilePath = Path.GetFullPath(relativeLocalFilePath);
                if (System.IO.File.Exists(absoluteFilePath) == false)
                {
                    throw new FileNotFoundException("Could not find file to upload", absoluteFilePath);
                }

                // Check to see if the file exists already
                if (TryGetFileByName(absoluteFilePath, out Database.File fileThatExists))
                {
                    // If we can't override, we need to throw an exception
                    if (shouldOverride == false)
                    {
                        throw new FailedToUploadFileException("File already exists and we are not allowed to override it!")
                        {
                            File = absoluteFilePath,
                        };
                    }
                }

                absoluteLocalFilePaths.Add(absoluteFilePath);
            }

            using (TieredUploadManager uploadManager = new TieredUploadManager(authorizationSessionGenerator, Config, CancellationEventRouter.GlobalCancellationToken))
            {
                IDictionary<string, ISet<string>> localFilePathToUploadIDs = new Dictionary<string, ISet<string>>();
                IDictionary<string, string> uploadIDsToLocalFilePath = new Dictionary<string, string>();
                foreach (string localFilePath in absoluteLocalFilePaths)
                {
                    localFilePathToUploadIDs[localFilePath] = new HashSet<string>();
                    foreach (Lazy<FileShard> lazyFileShard in FileShardFactory.CreateLazyFileShards(localFilePath))
                    {
                        string uploadID = uploadManager.AddLazyFileShard(lazyFileShard);

                        localFilePathToUploadIDs[localFilePath].Add(uploadID);
                        uploadIDsToLocalFilePath[uploadID] = localFilePath;
                    }
                }

                // This part is tricky because all of these event handlers will be executed on background threads. Because of this
                // we will need to lock around the SendNotification call in order to prevent any race conditions or data corruption
                // due to any state changes that occur because of it. We know the main thread is waiting on these background 
                // threads below because it's called the "Wait()" method
                object localLockObject = new object();
                IDictionary<string, ISet<UploadManagerEventArgs>> localFileToFileShardIDs = new Dictionary<string, ISet<UploadManagerEventArgs>>();
                ISet<string> localFilesThatHaveAlreadyStarted = new HashSet<string>();
                void HandleOnUploadBegin(object sender, UploadManagerEventArgs eventArgs)
                {
                    lock (localLockObject)
                    {
                        string localFile = uploadIDsToLocalFilePath[eventArgs.UploadID];
                        if (localFilesThatHaveAlreadyStarted.Contains(localFile) == false)
                        {
                            localFilesThatHaveAlreadyStarted.Add(localFile);
                            SendNotification(BeginUploadFile, localFile, null);
                        }
                    }
                }

                void HandleOnUploadFailed(object sender, UploadManagerEventArgs eventArgs)
                {
                    lock (localLockObject)
                    {
                        SendNotification(
                            FailedToUploadFile,
                            $"{uploadIDsToLocalFilePath[eventArgs.UploadID]} | {eventArgs.UploadResult.ToString()}",
                            null
                        );
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

                        ISet<string> uploadIDsForLocalFile = localFilePathToUploadIDs[localFilePath];
                        uploadIDsForLocalFile.Remove(eventArgs.UploadID);
                        if (uploadIDsForLocalFile.Any() == false)
                        {
                            SendNotification(FinishUploadFile, localFilePath, null);

                            string[] orderedShardIDs = new string[uploadEvents.Count];
                            string[] orderedShardHashes = new string[uploadEvents.Count];

                            foreach (UploadManagerEventArgs uploadEvent in uploadEvents)
                            {
                                orderedShardIDs[(int)uploadEvent.FileShardPieceNumber] = uploadEvent.FileShardID;
                                orderedShardHashes[(int)uploadEvent.FileShardPieceNumber] = uploadEvent.FileShardSHA1;
                            }

                            // Create file
                            FileInfo info = new FileInfo(localFilePath);
                            Database.File file = new Database.File
                            {
                                FileID = Guid.NewGuid().ToString(),
                                FileLength = info.Length,
                                FileName = localFilePath,
                                FileShardIDs = orderedShardIDs,
                                FileShardHashes = orderedShardHashes,
                                LastModified = info.LastWriteTimeUtc.ToBinary(),
                                SHA1 = SHA1FileHashStore.Instance.ComputeSHA1(localFilePath),
                            };

                            // Update manifest
                            if (TryGetFileByName(localFilePath, out Database.File fileThatExists))
                            {
                                // Remove old file first
                                RemoveFile(fileThatExists);
                            }

                            AddFile(file);
                            UploadFileDatabaseManifest(authorizationSessionGenerator());
                        }
                    }
                }

                void HandleOnUploadTierChanged(object sender, UploadManagerEventArgs eventArgs)
                {
                    lock (localLockObject)
                    {
                        SendNotification(
                            FileTierChanged,
                            $"{uploadIDsToLocalFilePath[eventArgs.UploadID]} -> {eventArgs.NewUploadTier}",
                            null
                        );
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
            }
        }

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

            foreach (string localFilePath in allLocalFiles)
            {
                if (TryGetFileByName(localFilePath, out Database.File remoteFileEntry))
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
        #endregion
    }
}