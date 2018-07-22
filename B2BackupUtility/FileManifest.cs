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
using System.Linq;
using Newtonsoft.Json;

namespace B2BackupUtility
{
    /// <summary>
    /// Represents a manifest of files that is used to keep track of
    /// the files that have been uploaded to B2
    /// </summary>
    /// <typeparam name="FileManifest"></typeparam>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class FileManifest : IEquatable<FileManifest>
    {
        #region public properties
        /// <summary>
        /// The ID of this file manifest
        /// </summary>
        [JsonProperty(PropertyName = "ID")]
        public long ID { get; set; }

        /// <summary>
        /// The version of this file manifest
        /// </summary>
        [JsonProperty(PropertyName = "Version")]
        public long Version { get; set; }

        /// <summary>
        /// The file entries of this manifest
        /// </summary>
        [JsonProperty(PropertyName = "FileEntries")]
        public FileManifestEntry[] FileEntries { get; set; }
        #endregion
        #region public methods
        public override bool Equals(object obj)
        {
            return Equals(obj as FileManifest);
        }
        
        public override int GetHashCode()
        {
            return ID.GetHashCode() ^
                Version.GetHashCode() ^
                FileEntries?.Aggregate(0, (acc, e) => acc ^ e.GetHashCode()) ?? 0;
        }

        public bool Equals(FileManifest other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            if (ID != other.ID || Version != other.Version)
            {
                return false;
            }

            if (FileEntries == null && other.FileEntries == null)
            {
                return true;
            }

            if (FileEntries == null || other.FileEntries == null)
            {
                // Due to the above statement, we know that at least one
                // of these references must be not-null, so we can safely 
                // return false here
                return false;
            }

            // We now know that both of the FileEntries are not null
            return FileEntries.ScrambledEquals(other.FileEntries);
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