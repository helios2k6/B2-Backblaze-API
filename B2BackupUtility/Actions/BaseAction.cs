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

using System.Collections.Generic;

namespace B2BackupUtility.Actions
{
    /// <summary>
    /// The base class that represents all actions
    /// </summary>
    public abstract class BaseAction
    {
        #region public properties
        /// <summary>
        /// The name of this action--this is displayed in the Help center
        /// </summary>
        public abstract string ActionName { get; }

        /// <summary>
        /// The command-line switch that's used to select this action
        /// </summary>
        public abstract string ActionSwitch { get; }

        /// <summary>
        /// The different command-line help-options this class offers
        /// </summary>
        public abstract IEnumerable<string> HelpOptions { get; }
        #endregion

        #region public methods
        /// <summary>
        /// Execute this action
        /// </summary>
        public abstract void ExecuteAction();

        
        #endregion

        #region protected methods
        #endregion
    }
}
