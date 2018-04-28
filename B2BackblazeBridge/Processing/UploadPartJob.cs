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

namespace B2BackblazeBridge.Processing
{
    /// <summary>
    /// Represents an upload part job
    /// </summary>
    internal sealed class UploadPartJob
    {
        /// <summary>
        /// The length of this chunk's content
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// The position of the first byte that must be read
        /// </summary>
        public long FileCursorPosition { get; set; }

        /// <summary>
        /// The file part number
        /// </summary>
        public int FilePartNumber { get; set; }

        /// <summary>
        /// The SHA1 hash of this chunk
        /// </summary>
        /// <returns></returns>
        public string SHA1 { get; set; }
    }
}