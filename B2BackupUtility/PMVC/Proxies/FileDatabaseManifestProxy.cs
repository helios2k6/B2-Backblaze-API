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

using B2BackupUtility.Database;
using PureMVC.Patterns.Proxy;
using System.Collections.Generic;

namespace B2BackupUtility.PMVC.Proxies
{
    /// <summary>
    /// This is a proxy for the File Manifest Database
    /// </summary>
    public sealed class FileDatabaseManifestProxy : Proxy
    {
        #region private fields
        private readonly IDictionary<string, File> _fileNameToFileMapping;
        #endregion

        #region public properties
        public static string Name => "File Manifest Database Proxy";

        public static string RemoteFileDatabaseManifestName => "b2_backup_util_file_database_manifest.txt.aes.gz";

        /// <summary>
        /// Gets the file database manifest
        /// </summary>
        public FileDatabaseManifest FileDatabaseManifest
        {
            get { return Data as FileDatabaseManifest; }
        }
        #endregion

        #region ctor
        public FileDatabaseManifestProxy() : base(Name, null)
        {
            _fileNameToFileMapping = new Dictionary<string, File>();
        }
        #endregion

        #region public methods
        /// <summary>
        /// Add a file to the manifest
        /// </summary>
        /// <param name="file"></param>
        public void AddFile(File file)
        {
        }

        /// <summary>
        /// Delete a file from the manifest
        /// </summary>
        /// <param name="file"></param>
        public void DeleteFile(File file)
        {
        }

        /// <summary>
        /// Remove a file from the manifest
        /// </summary>
        /// <param name="file"></param>
        public void DeleteFile(string file)
        {
        }

        /// <summary>
        /// Clears all entries in the lookup cache and remaps everything. 
        /// You should only do this if the FileDatabaseManifest was modified
        /// directly and not through this class' methods
        /// </summary>
        public void RefreshLookupCache()
        {
            if (FileDatabaseManifest == null)
            {
                return;
            }

            _fileNameToFileMapping.Clear();
            foreach (File file in FileDatabaseManifest.Files)
            {
                _fileNameToFileMapping.Add(file.FileName, file);
            }
        }
        #endregion

        #region private methods
        #endregion
    }
}
