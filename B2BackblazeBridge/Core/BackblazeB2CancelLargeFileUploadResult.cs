/* 
 * Copyright (c) 2023 Andrew Johnson
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

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// The result returned from a cancellation request
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2CancelLargeFileUploadResult : IEquatable<BackblazeB2CancelLargeFileUploadResult>
    {
        #region public properties
        /// <summary>
        /// The account the bucket is in
        /// </summary>
        [JsonProperty(PropertyName = "accountId")]
        public string AccountID { get; set; }

        /// <summary>
        /// The bucket the file resides in
        /// </summary>
        [JsonProperty(PropertyName = "bucketId")]
        public string BucketID { get; set; }

        /// <summary>
        /// The file ID that has been cancelled
        /// </summary>
        [JsonProperty(PropertyName = "fileId")]
        public string FileID { get; set; }

        /// <summary>
        /// The file name that has been cancelled
        /// </summary>
        [JsonProperty(PropertyName = "fileName")]
        public string FileName { get; set; }
        #endregion

        #region public methods
        public bool Equals(BackblazeB2CancelLargeFileUploadResult other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return string.Equals(AccountID, other.AccountID, StringComparison.Ordinal) &&
                string.Equals(BucketID, other.BucketID, StringComparison.Ordinal) &&
                string.Equals(FileID, other.FileID, StringComparison.Ordinal) &&
                string.Equals(FileName, other.FileName, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return AccountID?.GetHashCode() ?? 0 ^
                BucketID?.GetHashCode() ?? 0 ^
                FileID?.GetHashCode() ?? 0 ^
                FileName?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2CancelLargeFileUploadResult);
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