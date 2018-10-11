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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// The proxy that's in charge of uploading files to the remote file system
    /// </summary>
    public sealed class UploadFileProxy : BaseRemoteFileSystemProxy
    {
        #region private fields
        private const int DefaultUploadAttempts = 1;

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
        public static string ExponentialBackoffInitiated => "Exponential Backoff Initiated";
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

            IList<string> filesFailedToUpload = new List<string>();
            int consecuitiveFileManifestFailures = 0;
            foreach (string localFilePath in GetFilesToUpload(localFolderPath, shouldOverride))
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();

                try
                {
                    AddLocalFile(authorizationSessionGenerator(), localFilePath, shouldOverride);
                    // If we got here, then we know we uploaded the file manifest successfully
                    consecuitiveFileManifestFailures = 0;
                }
                catch (FailedToUploadFileException ex)
                {
                    filesFailedToUpload.Add(localFilePath);
                    SendNotification(FailedToUploadFile, ex, null);
                }
                catch (FailedToUploadFileDatabaseManifestException ex)
                {
                    consecuitiveFileManifestFailures++;
                    if (consecuitiveFileManifestFailures > MaxConsecutiveFileManifestUploadFailures)
                    {
                        // We will only tolerate a certain amount of failed consecutive file manifest
                        // failures to minimize the number of files we would have to reupload
                        throw ex;
                    }

                    // We'll try to upload this in the next upload. Worst case
                    SendNotification(FailedToUploadFileManifest, ex, null);
                }
            }

            // Attempt to upload files that we couldn't do before. Since this is a second chance, we are going
            // to be very stringent on the requirements. The file manifest must upload every single time and any
            // failure to upload a given file will just be passed over
            if (filesFailedToUpload.Any())
            {
                foreach (string localFilePath in filesFailedToUpload)
                {
                    try
                    {
                        AddLocalFile(authorizationSessionGenerator(), localFilePath, shouldOverride, DefaultReuploadAttempts);
                    }
                    catch (FailedToUploadFileException ex)
                    {
                        SendNotification(FailedToUploadFile, ex, null);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a local file to the Remote File System
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="localFilePath">The local path to the </param>
        /// <param name="shouldOverride">Whether to overide old files</param>
        /// <param name="maxUploadAttempts">Max upload attempts</param>
        public void AddLocalFile(
            BackblazeB2AuthorizationSession authorizationSession,
            string localFilePath,
            bool shouldOverride,
            int maxUploadAttempts = DefaultUploadAttempts
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

            IList<string> fileShardIDs = new List<string>();
            SendNotification(BeginUploadFile, absoluteFilePath, null);
            foreach (FileShard fileShard in FileShardFactory.CreateFileShards(new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read, FileShare.Read), true))
            {
                // Update Database.File
                fileShardIDs.Add(fileShard.ID);

                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = fileShard.Length < DefaultUploadChunkSize
                    ? ExecuteUploadAction(
                        new UploadWithSingleConnectionAction(
                            authorizationSession,
                            Config.BucketID,
                            EncryptionHelpers.EncryptBytes(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileShard)), Config.EncryptionKey, Config.InitializationVector),
                            fileShard.ID,
                            DefaultUploadAttempts,
                            CancellationEventRouter.GlobalCancellationToken,
                            t => SendNotificationAboutExponentialBackoff(t, absoluteFilePath, fileShard.ID)
                        ))
                    : ExecuteUploadAction(
                        new UploadWithMultipleConnectionsAction(
                            authorizationSession,
                            new MemoryStream(
                                EncryptionHelpers.EncryptBytes(
                                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileShard)),
                                    Config.EncryptionKey,
                                    Config.InitializationVector
                                )
                            ),
                            fileShard.ID,
                            Config.BucketID,
                            DefaultUploadChunkSize,
                            DefaultUploadConnections,
                            DefaultUploadAttempts,
                            CancellationEventRouter.GlobalCancellationToken,
                            t => SendNotificationAboutExponentialBackoff(t, absoluteFilePath, fileShard.ID)
                        ));

                if (uploadResult.HasErrors)
                {
                    throw new FailedToUploadFileException(uploadResult.ToString())
                    {
                        BackblazeErrorDetails = uploadResult.Errors,
                        File = absoluteFilePath,
                        FileShardID = fileShard.ID,
                    };
                }
            }
            SendNotification(FinishUploadFile, absoluteFilePath, null);

            // Create file
            FileInfo info = new FileInfo(absoluteFilePath);
            Database.File file = new Database.File
            {
                FileID = Guid.NewGuid().ToString(),
                FileLength = info.Length,
                FileName = absoluteFilePath,
                FileShardIDs = fileShardIDs.ToArray(),
                LastModified = info.LastWriteTimeUtc.ToBinary(),
                SHA1 = SHA1FileHashStore.Instance.ComputeSHA1(absoluteFilePath),
            };

            // Update manifest
            AddFile(file);
            UploadFileDatabaseManifest(authorizationSession);
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

            object localLockObject = new object();
            using (TieredUploadManager uploadManager = new TieredUploadManager(authorizationSessionGenerator, Config, CancellationEventRouter.GlobalCancellationToken))
            {
                EventHandler<UploadManagerEventArgs> handleOnUploadFailed = (sender, eventArgs) =>
                {

                };

                EventHandler<UploadManagerEventArgs> handleOnUploadFinished = (sender, eventArgs) =>
                {

                };

                EventHandler<UploadManagerEventArgs> handleOnUploadTierChanged = (sender, eventArgs) =>
                {

                };

                // Hook up events
                uploadManager.OnUploadFailed += handleOnUploadFailed;
                uploadManager.OnUploadFinished += handleOnUploadFinished;
                uploadManager.OnUploadTierChanged += handleOnUploadTierChanged;

                Parallel.ForEach(absoluteLocalFilePaths, (absoluteLocalFilePath, _, __) => 
                {
                    
                });

                // Unsubscribe from events
                uploadManager.OnUploadFailed -= handleOnUploadFailed;
                uploadManager.OnUploadFinished -= handleOnUploadFinished;
                uploadManager.OnUploadTierChanged -= handleOnUploadTierChanged;
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

        private void SendNotificationAboutExponentialBackoff(TimeSpan backoff, string fileName, string fileShardID)
        {
            SendNotification(
                ExponentialBackoffInitiated,
                $"Exponential backoff initiated for file {fileName} shard {fileShardID} for {backoff}",
                null
            );
        }

        private static BackblazeB2ActionResult<IBackblazeB2UploadResult> ExecuteUploadAction<T>(
            BaseAction<T> action
        ) where T : IBackblazeB2UploadResult
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
        #endregion
    }
}