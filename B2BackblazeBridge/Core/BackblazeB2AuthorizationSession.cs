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
    /// Represents the context required to interact with Backblaze B2 
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2AuthorizationSession : IEquatable<BackblazeB2AuthorizationSession>
    {
        #region public properties
        /// <summary>
        /// Gets or sets the absolute minimum part size for a large file
        /// </summary>
        [JsonProperty(PropertyName = "absoluteMinimumPartSize")]
        public long AbsoluteMinimumPartSize { get; set; }
        /// <summary>
        /// Gets or sets the account ID being used with this session
        /// </summary>
        [JsonProperty(PropertyName = "accountId")]
        public string AccountID { get; set; }
        /// <summary>
        /// Gets or sets the base URL used for all API calls, except uploading and downloading files
        /// </summary>
        [JsonProperty(PropertyName = "apiUrl")]
        public string APIURL { get; set; }
        /// <summary>
        /// Gets or sets the secret application key that is being used to identify this session
        /// </summary>
        public string ApplicationKey { get; set; }
        /// <summary>
        /// Gets or sets the authorization token that was given back to us after authenticating 
        /// the session
        /// </summary>
        [JsonProperty(PropertyName = "authorizationToken")]
        public string AuthorizationToken { get; set; }
        /// <summary>
        /// Gets or sets the base URL ussed for downloading files
        /// </summary>
        [JsonProperty(PropertyName = "downloadUrl")]
        public string DownloadURL { get; set; }
        /// <summary>
        /// Gets or sets the recommended part size of a large file upload
        /// </summary>
        [JsonProperty(PropertyName = "recommendedPartSize")]
        public long RecommendedPartSize { get; set; }
        /// <summary>
        /// Gets or sets the expiration date of the session
        /// </summary>
        public DateTime SessionExpirationDate { get; set; }
        #endregion

        #region public methods
        public bool Equals(BackblazeB2AuthorizationSession other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return
                AbsoluteMinimumPartSize == other.AbsoluteMinimumPartSize &&
                AccountID.Equals(other.AccountID, StringComparison.Ordinal) &&
                APIURL.Equals(other.APIURL, StringComparison.Ordinal) &&
                ApplicationKey.Equals(other.ApplicationKey, StringComparison.Ordinal) &&
                AuthorizationToken.Equals(other.AuthorizationToken, StringComparison.Ordinal) &&
                DownloadURL.Equals(other.DownloadURL, StringComparison.Ordinal) &&
                RecommendedPartSize == other.RecommendedPartSize &&
                SessionExpirationDate.Equals(other.SessionExpirationDate);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2AuthorizationSession);
        }

        public override int GetHashCode()
        {
            return
                AbsoluteMinimumPartSize.GetHashCode() ^
                AccountID.GetHashCode() ^
                APIURL.GetHashCode() ^
                ApplicationKey.GetHashCode() ^
                AuthorizationToken.GetHashCode() ^
                DownloadURL.GetHashCode() ^
                RecommendedPartSize.GetHashCode() ^
                SessionExpirationDate.GetHashCode();
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
