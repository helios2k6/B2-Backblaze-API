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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// Represents the final result of uploading a file in multiple parts
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2UploadMultipartFileResult : IEquatable<BackblazeB2UploadMultipartFileResult>, IBackblazeB2UploadResult
    {
        #region public properties
        /// <summary>
        /// Get or set the Account ID used 
        /// </summary>
        [JsonProperty(PropertyName = "accountId")]
        public string AccountID { get; set; }

        /// <summary>
        /// The Bucket ID the file is being uploaded to
        /// </summary>
        [JsonProperty(PropertyName = "bucketId")]
        public string BucketID { get; set;}

        /// <summary>
        /// The total length, in bytes, of this file
        /// </summary>
        [JsonProperty(PropertyName = "contentLength")]
        public long ContentLength { get; set; }

        /// <summary>
        /// The SHA-1 file hashes of the individual parts of the file
        /// </summary>
        public IList<string> FileHashes { get; set; }

        /// <summary>
        /// The unique file ID of the uploaded file
        /// </summary>
        [JsonProperty(PropertyName = "fileId")]
        public string FileID { get; set; }

        /// <summary>
        /// The file name used on the B2 server
        /// </summary>
        [JsonProperty(PropertyName = "fileName")]
        public string FileName { get; set; }
        #endregion
        #region public fields
        public override int GetHashCode()
        {
            return
                BucketID.GetHashCode() ^
                FileID.GetHashCode() ^
                ContentLength.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2UploadMultipartFileResult);
        }

        public bool Equals(BackblazeB2UploadMultipartFileResult other)
        {
            return ContentLength == other.ContentLength &&
                BucketID.Equals(other.BucketID, StringComparison.Ordinal) &&
                Enumerable.SequenceEqual(FileHashes, other.FileHashes) &&
                FileID.Equals(other.FileID, StringComparison.Ordinal);
        }
        #endregion
        #region private method
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