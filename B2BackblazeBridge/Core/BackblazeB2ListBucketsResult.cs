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

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// The response that comes back after calling B2 for a list of buckets
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2ListBucketsResult
    {
        #region inner classes
        /// <summary>
        /// Represents a single bucket that's listed 
        /// in the array of buckets
        /// </summary>
        [Serializable]
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class BucketItem
        {
            [JsonProperty(PropertyName = "accountId")]
            public string AccountID { get; set; }

            [JsonProperty(PropertyName = "bucketId")]
            public string BucketID { get; set; }

            [JsonProperty(PropertyName = "bucketName")]
            public string BucketName { get; set; }

            [JsonProperty(PropertyName = "bucketType")]
            public string BucketType { get; set; }
            
            [JsonProperty(PropertyName = "revision")]
            public long Revision { get; set; }
        }
        #endregion

        #region public properties
        /// <summary>
        /// Gets or sets the array of buckets
        /// </summary>
        [JsonProperty(PropertyName = "buckets")]
        public BucketItem[] Buckets { get; set; }
        #endregion
    }
}