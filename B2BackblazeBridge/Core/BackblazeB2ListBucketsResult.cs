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
using System.Linq;

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// The response that comes back after calling B2 for a list of buckets
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2ListBucketsResult : IEquatable<BackblazeB2ListBucketsResult>
    {
        #region inner classes
        /// <summary>
        /// Represents a single bucket that's listed 
        /// in the array of buckets
        /// </summary>
        [Serializable]
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class BucketItem : IEquatable<BucketItem>
        {
            #region public properties
            /// <summary>
            /// The account ID of all of the B2 buckets
            /// </summary>
            [JsonProperty(PropertyName = "accountId")]
            public string AccountID { get; set; }

            /// <summary>
            /// The bucket ID
            /// </summary>
            [JsonProperty(PropertyName = "bucketId")]
            public string BucketID { get; set; }

            /// <summary>
            /// The name of the bucket
            /// </summary>
            [JsonProperty(PropertyName = "bucketName")]
            public string BucketName { get; set; }

            /// <summary>
            /// The type of bucket. Usually, it's always private
            /// </summary>
            [JsonProperty(PropertyName = "bucketType")]
            public string BucketType { get; set; }

            /// <summary>
            /// The revision number of the bucket
            /// </summary>
            [JsonProperty(PropertyName = "revision")]
            public long Revision { get; set; }
            #endregion

            #region public methods
            public bool Equals(BucketItem other)
            {
                if (EqualsPreamble(other) == false)
                {
                    return false;
                }

                return string.Equals(AccountID, other.AccountID, StringComparison.Ordinal) &&
                    string.Equals(BucketID, other.BucketID, StringComparison.Ordinal) &&
                    string.Equals(BucketName, other.BucketName, StringComparison.Ordinal) &&
                    string.Equals(BucketType, other.BucketType, StringComparison.Ordinal) &&
                    Revision == other.Revision;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as BucketItem);
            }

            public override int GetHashCode()
            {
                return AccountID?.GetHashCode() ?? 0 ^
                    BucketID?.GetHashCode() ?? 0 ^
                    BucketName?.GetHashCode() ?? 0 ^
                    BucketType?.GetHashCode() ?? 0 ^
                    Revision.GetHashCode();
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
        /// Gets or sets the array of buckets
        /// </summary>
        [JsonProperty(PropertyName = "buckets")]
        public BucketItem[] Buckets { get; set; }
        #endregion

        #region public methods
        public bool Equals(BackblazeB2ListBucketsResult other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return Enumerable.SequenceEqual(Buckets, other.Buckets);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2ListBucketsResult);
        }

        public override int GetHashCode()
        {
            return Buckets?.Aggregate(0, (seed, e) => seed ^ e.GetHashCode()) ?? 0;
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