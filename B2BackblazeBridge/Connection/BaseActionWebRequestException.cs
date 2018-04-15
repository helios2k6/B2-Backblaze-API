﻿/* 
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

namespace B2BackblazeBridge.Connection
{
    /// <summary>
    /// Represents an exception thrown when a web request is sent from the BaseAction class. This exception is meant to be caught,
    /// inspected, and then another exception is meant to be rethrown with an HttpsStatusCode
    /// </summary>
    internal sealed class BaseActionWebRequestException : Exception
    {
        public BaseActionWebRequestException(HttpStatusCode statusCode) : base(string.Format("The status code {0} was returned", statusCode))
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// The HTTP status code of the exception
        /// </summary>
        public HttpStatusCode StatusCode { get; }
    }
}
