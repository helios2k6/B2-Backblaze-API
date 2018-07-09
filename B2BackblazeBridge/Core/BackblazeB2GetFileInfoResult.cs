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

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// The response that comes back from B2 after calling the Get File Info API
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2GetFileInfoResult : IEquatable<BackblazeB2GetFileInfoResult>
    {
        #region public properties
        /// <summary>
        /// Get or set the Account ID used 
        /// </summary>
        [JsonProperty(PropertyName = "accountId")]
        public string AccountID { get; set; }
        
        /// <summary>
        /// The current action of the file. "Upload" for files, "folder" for folders,
        /// "start" for large files that have been started, and "hide" for files that
        /// are being hidden
        /// </summary>
        [JsonProperty(PropertyName = "action")]
        public string Action { get; set; }

        /// <summary>
        /// The Bucket ID the file is being uploaded to
        /// </summary>
        [JsonProperty(PropertyName = "bucketId")]
        public string BucketID { get; set;}

        /// <summary>
        /// The length of the file provided during upload.
        /// </summary>
        [JsonProperty(PropertyName = "contentLength")]
        public long ContentLength { get; set; }

        /// <summary>
        /// The type of content of this file provided during upload.
        /// </summary>
        [JsonProperty(PropertyName = "contentType")]
        public string ContentType { get; set; }

        /// <summary>
        /// The SHA-1 hash of the file
        /// </summary>
        [JsonProperty(PropertyName = "contentSha1")]
        public string ContentSha1 { get; set; }

        /// <summary>
        /// The file ID of the file
        /// </summary>
        [JsonProperty(PropertyName = "fileId")]
        public string FileID { get; set; }

        /// <summary>
        /// The file name
        /// </summary>
        [JsonProperty(PropertyName = "fileName")]
        public string FileName { get; set; }

        /// <summary>
        /// The UTC time that this file was uploaded
        /// </summary>
        [JsonProperty(PropertyName = "uploadTimestamp")]
        public long UploadTimeStamp { get; set; }
        #endregion
        #region public methods
        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2GetFileInfoResult);
        }

        public override int GetHashCode()
        {
            return
                AccountID?.GetHashCode() ?? 0 ^
                Action?.GetHashCode() ?? 0 ^
                BucketID?.GetHashCode() ?? 0 ^
                ContentLength.GetHashCode() ^
                ContentType?.GetHashCode() ?? 0 ^
                ContentSha1?.GetHashCode() ?? 0 ^
                FileID?.GetHashCode() ?? 0 ^
                FileName?.GetHashCode() ?? 0 ^
                UploadTimeStamp.GetHashCode();
        }

        public bool Equals(BackblazeB2GetFileInfoResult other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return
                string.Equals(AccountID, other.AccountID, StringComparison.Ordinal) &&
                string.Equals(Action, other.Action, StringComparison.Ordinal) &&
                string.Equals(BucketID, other.BucketID, StringComparison.Ordinal) &&
                ContentLength == other.ContentLength &&
                string.Equals(ContentType, other.ContentType, StringComparison.Ordinal) &&
                string.Equals(ContentSha1, other.ContentSha1, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(FileID, other.FileID, StringComparison.Ordinal) &&
                string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
                UploadTimeStamp == other.UploadTimeStamp;
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