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
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.Database
{
    /// <summary>
    /// represents the business object that handles the retrieving, adding, and
    /// removing of entries into the FileManifestDatabase
    /// </summary>
    public sealed class FileDatabaseManifestManager
    {
        #region private fields
        private readonly FileDatabaseManifest _manifest;
        private readonly IDictionary<string, File> _fileNameToFileMapping;
        #endregion

        #region public properties
        /// <summary>
        /// Gets the Files that are in this manifest
        /// </summary>
        public IEnumerable<File> Files => _manifest.Files;
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new FileDatabaseManifestManager
        /// </summary>
        /// <param name="manifest">The manifest to read from</param>
        public FileDatabaseManifestManager(FileDatabaseManifest manifest)
        {
            _manifest = manifest;
            _fileNameToFileMapping = _manifest.Files.ToDictionary(k => k.FileName, v => v);
        }
        #endregion

        #region public methods
        /// <summary>
        /// Add a File to the  manifest. This will override any previous files
        /// with the same name
        /// </summary>
        /// <param name="file">The file to add</param>
        public void AddFile(File file)
        {
            // 1. Delete the file if it exists
            DeleteFile(file);
            // 2. Add file directly to manifest
            _fileNameToFileMapping.Add(file.FileName, file);
            _manifest.Files = _manifest.Files.Append(file).ToArray();
        }

        /// <summary>
        /// Delete a file from the manifest
        /// </summary>
        /// <param name="file">The file to remove</param>
        public void DeleteFile(File file)
        {
            DeleteFile(file.FileName);
        }

        /// <summary>
        /// Delete a file from the manifest
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        public void DeleteFile(string fileName)
        {
            // 1. Remove from mapping
            if (_fileNameToFileMapping.Remove(fileName))
            {
                // 2. If we found the mapping, remove it from the database manifest
                _manifest.Files = _manifest.Files.Where(t => string.Equals(t.FileName, fileName, StringComparison.Ordinal) == false).ToArray();
            }
        }

        /// <summary>
        /// Attempts to get a File that's stored in the manifest
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <param name="outFile">The reference to write to</param>
        /// <returns>True on success. False otherwise</returns>
        public bool TryGetFile(string fileName, out File outFile)
        {
            return _fileNameToFileMapping.TryGetValue(fileName, out outFile);
        }

        /// <summary>
        /// Serializes the manifest
        /// </summary>
        /// <returns>Returns a JSON version of the manifest</returns>
        public string SerializeManifest()
        {
            return JsonConvert.SerializeObject(_manifest);
        }
        #endregion
    }
}
