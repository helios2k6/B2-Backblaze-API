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
using PureMVC.Patterns.Proxy;
using System.IO;

namespace B2BackupUtility.Proxies
{
    public sealed class ConfigProxy : Proxy
    {
        #region public properties
        public static string Name => "Config Proxy";

        /// <summary>
        /// Gets the Config of this Proxy
        /// </summary>
        public Config Config
        {
            get { return Data as Config; }
        }
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new ConfigProxy with the given local path to the 
        /// program configuration file
        /// </summary>
        /// <param name="fileName">The local path to the program configuration file</param>
        public ConfigProxy(string fileName)
            : base(Name, JsonConvert.DeserializeObject<Config>(File.ReadAllText(fileName)))
        {
        }
        #endregion
    }
}
