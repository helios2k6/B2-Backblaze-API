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

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// List of different program commands
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        /// Check the file manifest to ensure all
        /// file shards are accounted for
        /// </summary>
        CHECK_FILE_MANIFEST,
        /// <summary>
        /// Download file command
        /// </summary>
        DOWNLOAD,
        /// <summary>
        /// Delete file command
        /// </summary>
        DELETE,
        /// <summary>
        /// Delete all files in the Bucket
        /// </summary>
        DELETE_ALL_FILES,
        /// <summary>
        /// Generate the encryption key and 
        /// initialization vector
        /// </summary>
        GENERATE_ENCRYPTION_KEY,
        /// <summary>
        /// List file command
        /// </summary>
        LIST,
        /// <summary>
        /// Prune file shards that are not accounted for
        /// </summary>
        PRUNE,
        /// <summary>
        /// Upload file command
        /// </summary>
        UPLOAD,
        /// <summary>
        /// Upload a folder of files
        /// </summary>
        UPLOAD_FOLDER,
        /// <summary>
        /// Unknown command
        /// </summary>
        UNKNOWN,
    }
}