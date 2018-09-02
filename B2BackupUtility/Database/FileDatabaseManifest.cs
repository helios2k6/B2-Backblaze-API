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

using Newtonsoft.Json;
using System;
using System.Linq;

namespace B2BackupUtility.Database
{
    /// <summary>
    /// Represents a database that is distributed over several different 
    /// database shards
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class FileDatabaseManifest
    {
        #region public properties
        /// <summary>
        /// The different files that are in this database
        /// </summary>
        [JsonProperty(PropertyName = "Files")]
        public File[] Files { get; set; }
        #endregion

        #region public methods
        /// <summary>
        /// Adds a file to the file manifest
        /// </summary>
        /// <param name="file">The file to add</param>
        public void AddFile(File file)
        {
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }

            Files = Files.Append(file).ToArray();
        }
        #endregion
    }
}
