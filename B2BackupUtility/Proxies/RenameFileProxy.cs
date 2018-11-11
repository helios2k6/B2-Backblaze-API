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

using B2BackblazeBridge.Core;
using B2BackupUtility.Proxies.Exceptions;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// Proxy for renaming a file in the file manifest
    /// </summary>
    public sealed class RenameFileProxy : BaseRemoteFileSystemProxy
    {
        #region public properties
        public static string Name => "Rename File Proxy";
        #endregion

        #region ctor
        public RenameFileProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Renames a file in the File Database Manifest
        /// </summary>
        /// <param name="file"></param>
        /// <param name="newFilePath"></param>
        public void RenameFile(
            BackblazeB2AuthorizationSession authorizationSession,
            Database.File file,
            string newFilePath
        )
        {
            // Ensure that the new file name doesn't conflict with something else
            if (TryGetFileByName(newFilePath, out Database.File _))
            {
                throw new FailedToRenameFileException($"{newFilePath} already exists");
            }

            RemoveFile(file);
            file.FileName = newFilePath;
            AddFile(file);
            while (true)
            {
                if (TryUploadFileDatabaseManifest(authorizationSession))
                {
                    return;
                }
            };
        }
        #endregion
    }
}
