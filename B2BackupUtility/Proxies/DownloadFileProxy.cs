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
using B2BackupUtility.Utils;
using Newtonsoft.Json;
using PureMVC.Patterns.Proxy;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// Handles downloading and saving a file from the B2 Backblaze server
    /// </summary>
    public sealed class DownloadFileProxy : Proxy, ILogNotifier
    {
        #region private fields
        private readonly IDictionary<Database.File, string> _fileToLocalFilePath;
        private readonly Config _config;
        #endregion

        #region public properties
        public static string Name => "Download File Proxy";
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new Download File Proxy
        /// </summary>
        /// <param name="config">The program config</param>
        public DownloadFileProxy(Config config) : base(Name, null)
        {
            _fileToLocalFilePath = new Dictionary<Database.File, string>();
            _config = config;
        }
        #endregion

        #region public methods
        /// <summary>
        /// Downloads a file from the B2 Backblaze server, throwing an exception if 
        /// this fails
        /// </summary>
        /// <param name="file"></param>
        /// <param name="destination"></param>
        public void DownloadFile(
            BackblazeB2AuthorizationSession authorizationSession,
            Database.File file,
            string destination
        )
        {
            this.Verbose($"Downloading file: {file.FileName}");
            if (System.IO.File.Exists(destination))
            {
                throw new InvalidOperationException($"Cannot override file {destination}.");
            }

            IDictionary<string, FileResult> fileNameToFileResultMap = GetShardIDToFileResultMapping(authorizationSession);

            ConcurrentBag<Tuple<string, long>> localFileShardIDPathsAndIndices = new ConcurrentBag<Tuple<string, long>>();
            long currentShardsDownloaded = 0;
            Parallel.ForEach(
                file.FileShardIDs,
                new ParallelOptions { MaxDegreeOfParallelism = 3 },
                (fileShardID, loopState, currentShardIndex) =>
            {
                if (loopState.ShouldExitCurrentIteration || loopState.IsExceptional || loopState.IsStopped)
                {
                    return;
                }

                string shardFilePath = GetShardIDFilePath(fileShardID);
                localFileShardIDPathsAndIndices.Add(Tuple.Create(shardFilePath, currentShardIndex));
                if (fileNameToFileResultMap.TryGetValue(fileShardID, out FileResult b2FileShard))
                {
                    if (TryDownloadFileShard(authorizationSession, shardFilePath, fileShardID, b2FileShard))
                    {
                        long totalDownloaded = Interlocked.Increment(ref currentShardsDownloaded);
                        this.Info($"{file.FileName} download progress: {totalDownloaded} / {file.FileShardIDs.Length} downloaded");
                    }
                    else
                    {
                        loopState.Stop();
                    }
                }
                else
                {
                    loopState.Stop();
                    throw new FailedToDownloadFileException($"Could not find the file shard: {fileShardID}");
                }
            });

            ReconstructFile(destination, localFileShardIDPathsAndIndices);
        }
        #endregion
        #region private methods
        private static string GetShardIDFilePath(string fileShardID)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), fileShardID);
        }

        private bool TryDownloadFileShard(
            BackblazeB2AuthorizationSession authorizationSession,
            string fileShardID,
            string filePathDestination,
            FileResult remoteFile
        )
        {
            using (DownloadFileAction fileShardDownload =
                new DownloadFileAction(authorizationSession, filePathDestination, remoteFile.FileID))
            {
                BackblazeB2ActionResult<BackblazeB2DownloadFileResult> downloadResult = fileShardDownload.Execute();
                if (downloadResult.HasErrors)
                {
                    this.Critical($"Exception occurred during downloading a file shard: {downloadResult}. Retrying later.");
                    return false;
                }
            }
            this.Verbose($"Downloaded shard {fileShardID}");
            return true;
        }

        private IDictionary<string, FileResult> GetShardIDToFileResultMapping(
            BackblazeB2AuthorizationSession authorizationSession
        )
        {
            ListFilesAction listFilesAction = ListFilesAction.CreateListFileActionForFileNames(
                authorizationSession,
                _config.BucketID,
                true
            );
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesAction.Execute();
            if (listFilesActionResult.HasErrors)
            {
                throw new FailedToGetListOfFilesOnB2Exception
                {
                    BackblazeErrorDetails = listFilesActionResult.Errors,
                };
            }

            return listFilesActionResult.Result.Files.ToDictionary(k => k.FileName, v => v);
        }

        private void ReconstructFile(
            string destination,
            IEnumerable<Tuple<string, long>> localFileShardIDPathsAndIndices
        )
        {
            long currentShard = 0;
            using (FileStream outputFileStream = System.IO.File.Create(destination))
            {
                this.Info("Piecing file together");
                foreach (string fileShardPath in localFileShardIDPathsAndIndices.OrderBy(t => t.Item2).Select(t => t.Item1))
                {
                    this.Verbose($"Adding shard: {currentShard}");
                    // Load the bytes into memory
                    byte[] decryptedBytes = EncryptionHelpers.DecryptBytes(
                        System.IO.File.ReadAllBytes(fileShardPath),
                        _config.EncryptionKey,
                        _config.InitializationVector
                    );

                    // Load the string
                    FileShard deserializedFileShard =
                        JsonConvert.DeserializeObject<FileShard>(
                            Encoding.UTF8.GetString(decryptedBytes, 0, decryptedBytes.Length)
                        );

                    // Assert that the current shard equals the one we expect
                    if (deserializedFileShard.PieceNumber != currentShard)
                    {
                        throw new InvalidOperationException("The file shard IDs are out of order!");
                    }

                    outputFileStream.Write(deserializedFileShard.Payload, 0, deserializedFileShard.Payload.Length);
                    currentShard++;

                    System.IO.File.Delete(fileShardPath);
                }
            }
        }
        #endregion
    }
}