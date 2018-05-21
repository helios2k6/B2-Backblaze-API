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

using System.Threading.Tasks;
using B2BackblazeBridge.Core;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents a download file action, either by ID or by file name
    /// </summary>
    public sealed class DownloadFileAction : BaseAction<BackblazeB2DownloadFileResult>
    {
        #region inner classes
        /// <summary>
        /// The type of identifier to use when looking up the file to download
        /// </summary>
        public enum IdentifierType
        {
            /// <summary>
            /// Download by using the unique file ID
            /// </summary>
            ID,
            /// <summary>
            /// Download by using the name of the file
            /// </summary>
            Name,
        }
        #endregion

        #region private fields
        private static readonly string DownloadURL = "/api/b2_download_file_by_id";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        #endregion

        #region public properties
        /// <summary>
        /// The identifier type that will be used to download the file
        /// </summary>
        public IdentifierType DownloadIdentifierType { get; }

        /// <summary>
        /// The identifier to use when downloading a file
        /// </summary>
        public string Identifier { get; }
        #endregion

        #region ctor
        /// <summary>
        /// Construct a DownloadFileAction with the given identifier and identifier type
        /// </summary>
        /// <param name="authorizationSession">The authorization session to use</param>
        /// <param name="identifier">The identifier</param>
        /// <param name="downloadIdentifierType">The type of identifier</param>
        public DownloadFileAction(BackblazeB2AuthorizationSession authorizationSession, string identifier, IdentifierType downloadIdentifierType)
        {
            DownloadIdentifierType = downloadIdentifierType;
            Identifier = identifier;
        }
        #endregion

        #region public methods
        public override Task<BackblazeB2ActionResult<BackblazeB2DownloadFileResult>> ExecuteAsync()
        {
            throw new System.NotImplementedException();
        }
        #endregion
    }
}
