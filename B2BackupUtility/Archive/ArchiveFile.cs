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

namespace B2BackupUtility.Archive
{
    /// <summary>
    /// Represents an Archive File comprised of smaller archive chunks. This is meant
    /// to be the business object that combines all of the Archive Chunks together and
    /// is not meant to be serialized. 
    /// </summary>
    public sealed class ArchiveFile : IEquatable<ArchiveFile>
    {
        #region private fields
        private readonly ArchiveChunk[] _chunks;

        private string FileName => _chunks[0].FileName;
        #endregion

        #region ctor
        public ArchiveFile(ArchiveFileManifest manifest, ArchiveChunk[] chunks)
        {
            if (chunks == null)
            {
                throw new ArgumentNullException("Chunks");
            }

            if (chunks.Length != manifest.NumChunks)
            {
                throw new ArgumentException("The number of chunks does not match the expected amount");
            }

            if (chunks.Any(c => string.Equals(c.FileName, manifest.FileName) == false))
            {
                throw new ArgumentException("Chunk file names do not match the expected name");
            }

            _chunks = chunks;
        }
        #endregion

        #region public methods
        /// <summary>
        /// Gets all of the bytes of the archive file in order
        /// </summary>
        /// <returns>An enumerable of bytes that represent the complete archive file</returns>
        public IEnumerable<byte> GetAllBytes()
        {
            return from chunk in _chunks
                   orderby chunk.ChunkNumber
                   from b in chunk.Payload
                   select b;
        }

        public override string ToString()
        {
            return $"Archive File: {FileName} with {_chunks.Length} Chunks";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ArchiveFile);
        }

        public override int GetHashCode()
        {
            return FileName.GetHashCode()^
                _chunks.Aggregate(0, (acc, e) => e.GetHashCode() ^ acc);
        }

        public bool Equals(ArchiveFile other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return string.Equals(FileName, other.FileName, StringComparison.InvariantCulture) &&
                EnumerableUtils.ScrambledEquals(_chunks, other._chunks);
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
