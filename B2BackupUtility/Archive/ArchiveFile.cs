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
using System.Linq;
using System.Runtime.Serialization;

namespace B2BackupUtility.Archive
{
    /// <summary>
    /// Represents an Archive File comprised of smaller archive chunks
    /// </summary>
    [Serializable]
    public sealed class ArchiveFile : IEquatable<ArchiveFile>, ISerializable
    {
        #region private fields
        private static string FileNamePropertyName => "File Name";

        private static string ChunksPropertyName => "Chunks";
        #endregion

        #region public properties
        /// <summary>
        /// The name of the file contained within this archive
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The archive chunks that comprise this archive file
        /// </summary>
        public ArchiveChunk[] Chunks { get; set; }
        #endregion

        #region ctor
        /// <summary>
        /// Default constructor
        /// </summary>
        public ArchiveFile()
        {
        }

        /// <summary>
        /// Deserialization 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public ArchiveFile(SerializationInfo info, StreamingContext context)
        {
            FileName = info.GetString(FileNamePropertyName);
            Chunks = (ArchiveChunk[])info.GetValue(ChunksPropertyName, typeof(ArchiveChunk[]));
        }
        #endregion

        #region public methods
        /// <summary>
        /// Gets all of the bytes of the archive file in order
        /// </summary>
        /// <returns>An enumerable of bytes that represent the complete archive file</returns>
        public IEnumerable<byte> GetAllBytes()
        {
            return from chunk in Chunks
                   orderby chunk.ChunkNumber
                   from b in chunk.Payload
                   select b;
        }

        public override string ToString()
        {
            return $"Archive File: {FileName} with {Chunks?.Length ?? 0} Chunks";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ArchiveFile);
        }

        public override int GetHashCode()
        {
            return FileName?.GetHashCode() ?? 0 ^
                Chunks?.Aggregate(0, (acc, e) => e.GetHashCode() ^ acc) ?? 0;
        }

        public bool Equals(ArchiveFile other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return string.Equals(FileName, other.FileName, StringComparison.InvariantCulture) &&
                EnumerableUtils.ScrambledEquals(Chunks, other.Chunks);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(FileNamePropertyName, FileName);
            info.AddValue(ChunksPropertyName, Chunks, typeof(ArchiveChunk[]));
        }
        #endregion

        #region private methods
        private bool EqualsPreamble(object other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;

            return true;
        }
        #endregion
    }
}
