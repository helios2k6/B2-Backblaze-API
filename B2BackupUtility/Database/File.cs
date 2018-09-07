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

using B2BackupUtility.Utils;
using Newtonsoft.Json;
using System;

namespace B2BackupUtility.Database
{
    /// <summary>
    /// Represents a file that has been sharded into different pieces
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class File : IEquatable<File>
    {
        #region public properties
        /// <summary>
        /// The length of the file
        /// </summary>
        [JsonProperty(PropertyName = "FileLength")]
        public long FileLength { get; set; }

        /// <summary>
        /// The original file name (full path)
        /// </summary>
        [JsonProperty(PropertyName = "FileName")]
        public string FileName { get; set; }

        /// <summary>
        /// The file shard IDs that comprise this file
        /// </summary>
        [JsonProperty(PropertyName = "FileShardIDs")]
        public string[] FileShardIDs { get; set; }

        /// <summary>
        /// The last time this file was modified
        /// </summary>
        [JsonProperty(PropertyName = "LastModified")]
        public long LastModified { get; set; }

        /// <summary>
        /// The SHA-1 Hash of this file
        /// </summary>
        [JsonProperty(PropertyName = "SHA1")]
        public string SHA1 { get; set; }
        #endregion

        #region public methods
        public override string ToString()
        {
            return $"{FileName} - [{SHA1}]";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as File);
        }

        public override int GetHashCode()
        {
            return FileLength.GetHashCode() ^
                FileName?.GetHashCode() ?? 0 ^
                FileShardIDs?.GetHashCodeEnumerable() ?? 0 ^
                LastModified.GetHashCode() ^
                SHA1?.GetHashCode() ?? 0;
        }

        public bool Equals(File other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return FileLength == other.FileLength &&
                string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
                FileShardIDs.ScrambledEquals(other.FileShardIDs) &&
                LastModified == other.LastModified &&
                string.Equals(SHA1, other.SHA1, StringComparison.OrdinalIgnoreCase);
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
