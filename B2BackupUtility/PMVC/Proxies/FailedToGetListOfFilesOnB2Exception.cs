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

using B2BackblazeBridge.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace B2BackupUtility.PMVC.Proxies
{
    /// <summary>
    /// An Exception that is thrown we could not get the list of files on B2
    /// </summary>
    public sealed class FailedToGetListOfFilesOnB2Exception : Exception
    {
        #region public properties
        public IEnumerable<BackblazeB2ActionErrorDetails> BackblazeErrorDetails { get; set; }
        #endregion

        #region ctor
        public FailedToGetListOfFilesOnB2Exception()
        {
        }

        public FailedToGetListOfFilesOnB2Exception(string message) : base(message)
        {
        }

        public FailedToGetListOfFilesOnB2Exception(string message, Exception innerException) : base(message, innerException)
        {
        }
        #endregion

        #region public methods
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            foreach (BackblazeB2ActionErrorDetails errorDetails in BackblazeErrorDetails)
            {
                builder.AppendLine(errorDetails.ToString()).AppendLine();
            }

            return builder.ToString();
        }
        #endregion
    }
}