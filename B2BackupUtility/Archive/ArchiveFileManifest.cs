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
using System.Runtime.Serialization;

namespace B2BackupUtility.Archive
{
    /// <summary>
    /// Represents a manifest of all of the Archive File Chunks that comprise a single
    /// Archive File. This is meant to be serialized alongside of the Archive Chunks and 
    /// then used to reconstruct the original Archive File. 
    /// </summary>
    [Serializable]
    public sealed class ArchiveFileManifest : IEquatable<ArchiveFileManifest>, ISerializable
    {
        #region private fields
        #endregion

        #region public properties
        /// <summary>
        /// The file names of the Archive File chunks themselves (not the 
        /// name of the original file contained within the Archive)
        /// </summary>
        public string[] ArchiveChunkFileNames { get; set; }

        /// <summary>
        /// The name of the original file inside of the archive
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The number of chunks that comprise this Archive File
        /// </summary>
        public long NumChunks { get; set; }
        #endregion

        #region ctor
        #endregion

        #region public methods
        public bool Equals(ArchiveFileManifest other)
        {
            throw new NotImplementedException();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region private methods
        #endregion
    }
}
