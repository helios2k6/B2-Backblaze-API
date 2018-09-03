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

using PureMVC.Patterns.Proxy;
using System.Collections.Generic;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.PMVC.Proxies
{
    /// <summary>
    /// Contains all of the files that are on the B2 server as returned by the 
    /// ListFilesAction
    /// </summary>
    public sealed class ListOfFilesOnB2Proxy : Proxy
    {
        #region public properties
        public static string Name => "List Of Files On B2 Proxy";

        public IEnumerable<FileResult> Files => Data as IEnumerable<FileResult>;
        #endregion

        #region ctor
        /// <summary>
        /// Default ctor
        /// </summary>
        public ListOfFilesOnB2Proxy() : base (Name, null)
        {
        }
        #endregion

        #region public methods
        #endregion
    }
}
