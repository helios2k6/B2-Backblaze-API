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

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// Represents the result of an upload action, either through multiple parts or a sinle upload
    /// </summary>
    public interface IBackblazeB2UploadResult
    {
        /// <summary>
        /// Get or set the Account ID used 
        /// </summary>
        string AccountID { get; set; }

        /// <summary>
        /// The Bucket ID the file is being uploaded to
        /// </summary>
        string BucketID { get; set; }

        /// <summary>
        /// The total length, in bytes, of this file
        /// </summary>
        long ContentLength { get; set; }

        /// <summary>
        /// The unique file ID of the uploaded file
        /// </summary>
        string FileID { get; set; }

        /// <summary>
        /// The file name used on the B2 server
        /// </summary>
        string FileName { get; set; }
    }
}
