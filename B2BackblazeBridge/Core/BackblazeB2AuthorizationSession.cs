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
    /// <summary>
    /// Represents the context required to interact with Backblaze B2 
    /// </summary>
    public sealed class BackblazeB2AuthorizationSession : IEquatable<BackblazeB2AuthorizationSession>
    {
        #region public properties
        /// <summary>
        /// Gets the absolute minimum part size for a large file
        /// </summary>
        public long AbsoluteMinimumPartSize { get; }
        /// <summary>
        /// Gets the account ID being used with this session
        /// </summary>
        public string AccountID { get; }
        /// <summary>
        /// The base URL used for all API calls, except uploading and downloading files
        /// </summary>
        public string APIURL { get; }
        /// <summary>
        /// Gets the secret application key that is being used to identify this session
        /// </summary>
        public string ApplicationKey { get; }
        /// <summary>
        /// Gets the authorization token that was given back to us after authenticating 
        /// the session
        /// </summary>
        public string AuthorizationToken { get; }
        /// <summary>
        /// Gets the base URL ussed for downloading files
        /// </summary>
        public string DownloadURL { get; }
        /// <summary>
        /// Gets the recommended part size of a large file upload
        /// </summary>
        public long RecommendedPartSize { get; }
        /// <summary>
        /// The expiration date of the session
        /// </summary>
        public DateTime SessionExpirationDate { get; }
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new BackblazeB2AuthorizationSession with the given parameters
        /// </summary>
        /// <param name="absoluteMinimumPartSize">The minimum part size for uploads</param>
        /// <param name="accountID">The account ID for this session context</param>
        /// <param name="apiUrl">The API URL to use when making requests</param>
        /// <param name="applicationKey">The application key used for this session context</param>
        /// <param name="authorizationToken">The authorization token used in this context</param>
        /// <param name="downloadUrl">The base URL for downloading files</param>
        /// <param name="recommendedPartSize">The recommended part size for files</param>
        /// <param name="sessionExpirationDate">The expiration date of this session</param>
        public BackblazeB2AuthorizationSession(
            long absoluteMinimumPartSize,
            string accountID,
            string apiUrl,
            string applicationKey,
            string authorizationToken,
            string downloadUrl,
            long recommendedPartSize,
            DateTime sessionExpirationDate
        )
        {
            AbsoluteMinimumPartSize = absoluteMinimumPartSize;
            AccountID = accountID;
            APIURL = apiUrl;
            ApplicationKey = applicationKey;
            AuthorizationToken = authorizationToken;
            DownloadURL = downloadUrl;
            RecommendedPartSize = recommendedPartSize;
            SessionExpirationDate = sessionExpirationDate;
        }
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
