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

using System;

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// Represents the result of a downloaded file from B2 Backblaze
    /// </summary>
    public sealed class BackblazeB2DownloadFileResult : IEquatable<BackblazeB2DownloadFileResult>
    {
        #region public properties
        /// <summary>
        /// The length of the file that was downloaded
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// The content type of the file
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// The file name
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The file's SHA1
        /// </summary>
        public string ContentSha1 { get; set; }

        /// <summary>
        /// The timestamp from when the file upload began
        /// </summary>
        public long TimeStamp { get; set; }
        #endregion

        #region public methods
        public bool Equals(BackblazeB2DownloadFileResult other)
        {
            return ContentLength == other.ContentLength &&
                string.Equals(ContentType, other.ContentType, StringComparison.Ordinal) &&
                string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
                string.Equals(ContentSha1, other.ContentSha1, StringComparison.Ordinal) &&
                TimeStamp == other.TimeStamp;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2DownloadFileResult);
        }

        public override int GetHashCode()
        {
            return ContentLength.GetHashCode() ^
                ContentType?.GetHashCode() ?? 0 ^
                FileName?.GetHashCode() ?? 0 ^
                ContentSha1?.GetHashCode() ?? 0 ^
                TimeStamp.GetHashCode();
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
