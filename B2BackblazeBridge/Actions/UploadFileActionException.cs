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
using System.Net;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// This exception is thrown whenever uploading a file fails
    /// </summary>
    public sealed class UploadFileActionException : BaseActionWebRequestException
    {
        #region ctor
        /// <summary>
        /// Construct a new UploadFileActionException
        /// </summary>
        /// <param name="statusCode">The HTTP status code that caused this exception</param>
        /// <param name="details">The details of the error given back by B2 Backblaze</param>
        public UploadFileActionException(HttpStatusCode statusCode, ErrorDetails details) : base(statusCode, details)
        {
        }
        #endregion
    }
}
