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
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.Database
{
    /// <summary>
    /// Acts as the client-side database reader of files from the manifest
    /// </summary>
    public sealed class FileDatabaseReader
    {
        #region private fields
        private readonly FileDatabaseManifest _manifest;
        private readonly IDictionary<string, File> _fileNameToFileMapping;
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new FileDatabase using an existing manifest
        /// </summary>
        /// <param name="manifest">The file database manifest</param>
        public FileDatabaseReader(FileDatabaseManifest manifest)
        {
            _manifest = manifest ?? throw new ArgumentNullException("manifest");
            _fileNameToFileMapping = _manifest.Files.ToDictionary(t => t.FileName, t => t);
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public FileDatabaseReader() : this(new FileDatabaseManifest())
        {
        }
        #endregion

        #region public methods
        public bool TryGetFileShardIDs(string fileName, out IEnumerable<string> fileShardIDs)
        {
            fileShardIDs = null;
            if (_fileNameToFileMapping.TryGetValue(fileName, out File file))
            {
                fileShardIDs = file.FileShardIDs;
                return true;
            }

            return false;
        }
        #endregion
    }
}
