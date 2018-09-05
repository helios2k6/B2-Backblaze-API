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
using System;
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.PMVC.Proxies
{
    /// <summary>
    /// Contains the program arguements to the program
    /// </summary>
    public sealed class ProgramArgumentsProxy : Proxy
    {
        #region public properties
        public static string Name => "Program Arguments Proxy";

        public IEnumerable<string> ProgramArguments => Data as IEnumerable<string>;
        #endregion

        #region ctor
        /// <summary>
        /// Constructs this Program Arguments Proxy with the given program args
        /// </summary>
        /// <param name="programArgs"></param>
        public ProgramArgumentsProxy(IEnumerable<string> programArgs) : base(Name, programArgs)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Checks to see if an option exists
        /// </summary>
        /// <param name="option">Gets whether an argument exists</param>
        /// <returns>True if an argument exists. False otherwise</returns>
        public bool DoesOptionExist(string option)
        {
            return ProgramArguments.Any(t => t.Equals(option, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// This will attempt to get the value of an argument that is passed in. This cannot
        /// get multiple arguments passed in to a single options
        /// </summary>
        /// <param name="option">The option to get arguments for</param>
        /// <param name="value">The value found</param>
        /// <returns>True if an argument was found. False otherwise</returns>
        public bool TryGetArgument(string option, out string value)
        {
            bool returnNextItem = false;
            foreach (string arg in ProgramArguments)
            {
                if (returnNextItem)
                {
                    value = arg;
                    return true;
                }

                if (arg.Equals(option, StringComparison.OrdinalIgnoreCase))
                {
                    returnNextItem = true;
                }
            }

            value = null;
            return false;
        }
        #endregion
    }
}
