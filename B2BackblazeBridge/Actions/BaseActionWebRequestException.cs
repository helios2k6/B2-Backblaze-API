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
using System.Net;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents an exception thrown when a web request is sent from the BaseAction class. This exception is meant to be caught,
    /// inspected, and then another exception is meant to be rethrown with an HttpsStatusCode
    /// </summary>
    public sealed class BaseActionWebRequestException : Exception
    {
        [Serializable]
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class ErrorDetails
        {
            [JsonProperty(PropertyName = "status")]
            public string Status { get; set; }

            [JsonProperty(PropertyName = "code")]
            public int Code { get; set; }

            [JsonProperty(PropertyName = "message")]
            public string Message { get; set; }
        }

        public BaseActionWebRequestException(HttpStatusCode statusCode, ErrorDetails details) : base(string.Format("The status code {0} was returned", statusCode))
        {
            StatusCode = statusCode;
            Details = details;
        }

        /// <summary>
        /// The HTTP status code of the exception
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// The details of this HTTP error
        /// </summary>
        public ErrorDetails Details { get; }
    }
}
