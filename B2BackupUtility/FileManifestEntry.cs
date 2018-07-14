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
using Newtonsoft.Json;

namespace B2BackupUtility
{
    /// <summary>
    /// Represents an entry in the FileManifest database object
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class FileManifestEntry : IEquatable<FileManifestEntry>
    {
        #region public properties
        /// <summary>
        /// The original file path that was uploaded. This path
        /// might not exist anymore
        /// </summary>
        [JsonProperty(PropertyName = "OriginalFilePath")]
        public string OriginalFilePath { get; set; }

        /// <summary>
        /// The B2 file path
        /// </summary>
        [JsonProperty(PropertyName = "DestinationFilePath")]
        public string DestinationFilePath { get; set; }

        /// <summary>
        /// The SHA-1 Hash of this file
        /// </summary>
        [JsonProperty(PropertyName = "SHA1")]
        public string SHA1 { get; set; }
        #endregion

        #region public methods
        public override bool Equals(object obj)
        {
            return Equals(obj as FileManifestEntry);
        }
        
        public override int GetHashCode()
        {
            return OriginalFilePath?.GetHashCode() ?? 0 ^
                DestinationFilePath?.GetHashCode() ?? 0 ^
                SHA1?.GetHashCode() ?? 0;
        }

        public bool Equals(FileManifestEntry other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return string.Equals(OriginalFilePath, other.OriginalFilePath, StringComparison.Ordinal) &&
                string.Equals(DestinationFilePath, other.DestinationFilePath, StringComparison.Ordinal) &&
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