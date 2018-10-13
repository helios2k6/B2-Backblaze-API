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

namespace B2BackupUtility.Database
{
    /// <summary>
    /// Creates Files and FileShards for persistent on the B2 Backblaze server
    /// </summary>
    public static class FileShardFactory
    {
        #region private fields
        private static int DefaultShardLength => 104857600; // 100 Mebibytes
        #endregion

        #region public methods
        /// <summary>
        /// Creates an IEnumerable of FileShards 
        /// </summary>
        /// <param name="byteStream">The stream of bytes to read from</param>
        /// <param name="shardLength">The desired shard length</param>
        /// <param name="disposeOfStream">Whether or not to dispose of the stream</param>
        /// <returns>An IEnumerable of FileShards</returns>
        public static IEnumerable<FileShard> CreateFileShards(Stream byteStream, int shardLength, bool disposeOfStream)
        {
            byte[] payloadBuffer = new byte[shardLength];
            long pieceNumber = 0;
            while (true)
            {
                int bytesRead = byteStream.Read(payloadBuffer, 0, shardLength);
                if (bytesRead < 1)
                {
                    break;
                }

                byte[] payload = new byte[bytesRead];
                Buffer.BlockCopy(payloadBuffer, 0, payload, 0, bytesRead);

                yield return new FileShard
                {
                    ID = Guid.NewGuid().ToString(),
                    Length = bytesRead,
                    Payload = payload,
                    PieceNumber = pieceNumber++,
                    SHA1 = SHA1FileHashStore.Instance.ComputeSHA1Hash(payload),
                };
            }

            if (disposeOfStream)
            {
                byteStream.Dispose();
            }

            yield break;
        }

        /// <summary>
        /// Creates an IEnumerable of FileShards 
        /// </summary>
        /// <param name="byteStream">The stream of bytes to read from</param>
        /// <param name="disposeOfStream">Whether or not to dispose of the stream</param>
        /// <returns>An IEnumerable of FileShards</returns>
        public static IEnumerable<FileShard> CreateFileShards(Stream byteStream, bool disposeOfStream)
        {
            return CreateFileShards(byteStream, DefaultShardLength, disposeOfStream);
        }

        /// <summary>
        /// This will create a stream of Lazy File Shards that defer the generation of a file shard with
        /// the default shard size
        /// </summary>
        /// <param name="filePath">The local file path to read from</param>
        /// <returns>An IEnumerable of File Shards that are lazily generated</returns>
        public static IEnumerable<Lazy<FileShard>> CreateLazyFileShards(string filePath)
        {
            return CreateLazyFileShards(filePath, DefaultShardLength);
        }

        /// <summary>
        /// This will create a stream of Lazy File Shards that defer the generation of a file shard
        /// </summary>
        /// <param name="filePath">The local file path to read from</param>
        /// <param name="shardLength">The length of each shard</param>
        /// <returns>An IEnumerable of File Shards that are lazily generated</returns>
        public static IEnumerable<Lazy<FileShard>> CreateLazyFileShards(string filePath, int shardLength)
        {
            long numShards = CalculateNumberOfShards(filePath, shardLength);
            IEnumerable<Lazy<FileShard>> fileShardEnumerableHead = Enumerable.Empty<Lazy<FileShard>>();
            for (long i = 0; i < numShards; i++)
            {
                fileShardEnumerableHead = fileShardEnumerableHead.Append(CreateLazyFileShard(filePath, i, shardLength));
            }

            return fileShardEnumerableHead;
        }

        private static long CalculateNumberOfShards(string filePath, int shardLength)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Length < shardLength)
            {
                return 1;
            }

            bool hasMod = fileInfo.Length % shardLength > 0;
            long numShards = fileInfo.Length / shardLength;

            if (hasMod)
            {
                numShards++;
            }

            return numShards;
        }

        private static Lazy<FileShard> CreateLazyFileShard(string filePath, long pieceNumber, int shardLength)
        {
            return new Lazy<FileShard>(() => 
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Seek into file
                    fileStream.Position = pieceNumber * shardLength;

                    byte[] payloadBuffer = new byte[shardLength];
                    int bytesRead = fileStream.Read(payloadBuffer, 0, shardLength);
                    if (bytesRead < 1)
                    {
                        return null;
                    }

                    byte[] payload = new byte[bytesRead];
                    Buffer.BlockCopy(payloadBuffer, 0, payload, 0, bytesRead);

                    return new FileShard
                    {
                        ID = Guid.NewGuid().ToString(),
                        Length = bytesRead,
                        Payload = payload,
                        PieceNumber = pieceNumber++,
                        SHA1 = SHA1FileHashStore.Instance.ComputeSHA1Hash(payload),
                    };
                }
            });
        }
        #endregion
    }
}
