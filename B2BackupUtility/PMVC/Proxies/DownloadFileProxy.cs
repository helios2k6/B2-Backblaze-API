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
using System.Text;
using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using B2BackupUtility.PMVC.Encryption;
using B2BackupUtility.PMVC.Proxies.Exceptions;
using Newtonsoft.Json;
using PureMVC.Patterns.Proxy;

namespace B2BackupUtility.PMVC.Proxies
{
    /// <summary>
    /// Handles downloading and saving a file from the B2 Backblaze server
    /// </summary>
    public sealed class DownloadFileProxy : Proxy
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
            if (System.IO.File.Exists(destination))
            {
                throw new InvalidOperationException($"Cannot override file {destination}.");
            }

            IList<string> localFileShardIDPaths = new List<string>();
            // Download all file shards first
            foreach (string fileShardID in file.FileShardIDs)
            {
                string shardFilePath = Path.Combine(Directory.GetCurrentDirectory(), fileShardID);
                localFileShardIDPaths.Add(shardFilePath);
                DownloadFileAction fileShardDownload =
                    new DownloadFileAction(authorizationSession, shardFilePath, _config.BucketID);

                BackblazeB2ActionResult<BackblazeB2DownloadFileResult> downloadResult =
                    fileShardDownload.Execute();

                if (downloadResult.HasErrors)
                {
                    throw new FailedToDownloadFileException
                    {
                        BackblazeErrorDetails = downloadResult.Errors,
                    };
                }
            }

            long currentShard = 0;
            using (FileStream outputFileStream = System.IO.File.Create(destination))
            {
                // Then reconstruct the original file. The ordering of the file shards
                // is assumed to be the order we should reconstruct the 
                foreach (string fileShardPath in localFileShardIDPaths)
                {
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
                }
            }
        }
        #endregion
    }
}