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

using B2BackupUtility.Utils;
using PureMVC.Patterns.Proxy;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// The base Proxy class for all Proxies
    /// </summary>
    public abstract class BaseProxy : Proxy, ILogNotifier
    {
        #region ctor
        /// <summary>
        /// Standard ctor for proxy .
        /// </summary>
        /// <param name="proxyName">The name of this proxy.</param>
        /// <param name="proxyType">The type of Proxy this is.</param>
        public BaseProxy(string proxyName, ProxyType proxyType) : base(proxyName)
        {
            ProxyType = proxyType;
        }
        #endregion

        #region public properties
        /// <summary>
        /// The type of Proxy this is.
        /// </summary>
        public ProxyType ProxyType { get; private set; }
        #endregion
    }
}
