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
using System.Linq;

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// The response that comes back from B2 after listing all of the files
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2ListFilesResult : IEquatable<BackblazeB2ListFilesResult>
    {
        #region inner classes
        /// <summary>
        /// Specific details about a file
        /// </summary>
        [Serializable]
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class FileResult : IEquatable<FileResult>
        {
            #region public properties
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
            /// The SHA-1 hash of the file that was provided during upload. 
            /// WARNING: this is usually NULL because most large files are
            /// uploaded in pieces
            /// </summary>
            [JsonProperty(PropertyName = "contentSha1")]
            public string ContentSha1 { get; set; }

            /// <summary>
            /// The current action of the file. "Upload" for files, "folder" for folders,
            /// "start" for large files that have been started, and "hide" for files that
            /// are being hidden
            /// </summary>
            [JsonProperty(PropertyName = "action")]
            public string Action { get; set; }

            /// <summary>
            /// The UTC time that this file was uploaded
            /// </summary>
            [JsonProperty(PropertyName = "uploadTimestamp")]
            public long UploadTimeStamp { get; set; }
            #endregion

            #region public methods
            public override string ToString()
            {
                return $"{FileName} - [File ID: {FileID}][Bytes: {ContentLength}][SHA-1: {ContentSha1 ?? "NULL"}][Uploaded: {UploadTimeStamp}]";
            }

            public bool Equals(FileResult other)
            {
                if (EqualsPreamble(other) == false)
                {
                    return false;
                }

                return string.Equals(FileID, other.FileID, StringComparison.Ordinal) &&
                    string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
                    ContentLength == other.ContentLength &&
                    string.Equals(ContentType, other.ContentType, StringComparison.Ordinal) &&
                    string.Equals(ContentSha1, other.ContentSha1, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Action, other.Action, StringComparison.Ordinal) &&
                    UploadTimeStamp == other.UploadTimeStamp;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as FileResult);
            }

            public override int GetHashCode()
            {
                return FileID?.GetHashCode() ?? 0 ^
                    FileName?.GetHashCode() ?? 0 ^
                    ContentLength.GetHashCode() ^
                    ContentType?.GetHashCode() ?? 0 ^
                    ContentSha1?.GetHashCode() ?? 0 ^
                    Action?.GetHashCode() ?? 0 ^
                    UploadTimeStamp.GetHashCode();
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

        #endregion

        #region public properties
        /// <summary>
        /// List of file details
        /// </summary>
        [JsonProperty(PropertyName = "files")]
        public FileResult[] Files { get; set; }

        /// <summary>
        /// The file name to pass back next in order to iterate
        /// to the next file
        /// </summary>
        [JsonProperty(PropertyName = "nextFileName")]
        public string NextFileName { get; set; }

        /// <summary>
        /// The next file ID to use to get the next set of files. 
        /// </summary>
        [JsonProperty(PropertyName = "nextFileId")]
        public string NextFileID { get; set; }
        #endregion

        #region public methods
        public bool Equals(BackblazeB2ListFilesResult other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return string.Equals(NextFileName, other.NextFileName, StringComparison.Ordinal) &&
                string.Equals(NextFileName, other.NextFileName, StringComparison.Ordinal) &&
                Enumerable.SequenceEqual(Files, other.Files);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2ListFilesResult);
        }

        public override int GetHashCode()
        {
            return NextFileID?.GetHashCode() ?? 0 ^
                NextFileName?.GetHashCode() ?? 0 ^
                Files?.Aggregate(0, (acc, e) => acc ^ e.GetHashCode()) ?? 0;
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
