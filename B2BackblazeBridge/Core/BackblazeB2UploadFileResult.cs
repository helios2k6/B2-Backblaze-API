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

namespace B2BackblazeBridge.Core
{
    public sealed class BackblazeB2UploadFileResult : IEquatable<BackblazeB2UploadFileResult>
    {
        #region public properties
        public string AccountID { get; set; }

        public string BucketID { get; set; }

        public long ContentLength { get; set; }

        public string ContentSHA1 { get; set; }

        public string FileID { get; set; }

        public string FileName { get; set; }

        public long UploadTimeStamp  { get; set; }
        #endregion
        #region ctor
        #endregion
        #region public methods
        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2UploadFileResult);
        }

        public override int GetHashCode()
        {
            return AccountID.GetHashCode() ^
                BucketID.GetHashCode() ^
                ContentLength.GetHashCode() ^
                ContentSHA1.GetHashCode() ^
                FileID.GetHashCode() ^
                FileName.GetHashCode() ^
                UploadTimeStamp.GetHashCode();
        }

        public bool Equals(BackblazeB2UploadFileResult other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return 
                AccountID.Equals(other.AccountID, StringComparison.Ordinal) &&
                BucketID.Equals(other.BucketID, StringComparison.Ordinal) &&
                ContentLength == other.ContentLength &&
                ContentSHA1.Equals(other.ContentSHA1, StringComparison.Ordinal) &&
                FileID.Equals(other.FileID, StringComparison.Ordinal) &&
                FileName.Equals(other.FileName, StringComparison.Ordinal) &&
                UploadTimeStamp == other.UploadTimeStamp;
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