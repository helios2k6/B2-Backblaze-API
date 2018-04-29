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

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents a failure to upload a file part due to validation errors
    /// </summary>
    [System.Serializable]
    public class UploadFilePartInconsistentException : System.Exception
    {
        /// <summary>
        /// The file ID that failed to upload correctly
        /// </summary>
        public string FileID { get; }

        /// <summary>
        /// The file part that didn't upload correctly
        /// </summary>
        public int FilePart { get; }

        /// <summary>
        /// Constructs a new UploadFilePartInconsistentException
        /// </summary>
        /// <param name="fileID">The file ID that had the part that failed to upload</param>
        /// <param name="filePart">The file part that failed to upload</param>
        public UploadFilePartInconsistentException(string fileID, int filePart)
        {
            FileID = fileID;
            FilePart = filePart;
        }

        protected UploadFilePartInconsistentException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context
        ) : base(info, context)
         {
             info.AddValue("FileID", FileID);
             info.AddValue("FilePart", FilePart);
         }
    }
}