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

using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using B2BackupUtility.Encryption;
using B2BackupUtility.Proxies.Exceptions;
using Functional.Maybe;
using Newtonsoft.Json;
using PureMVC.Patterns.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// This is a proxy for the remote file system on B2
    /// </summary>
    public sealed class RemoteFileSystemProxy : BaseRemoteFileSystemProxy
    {
        #region public properties
        public static string Name => "File Manifest Database Proxy";

        /// <summary>
        /// Gets all files in the file database manifest
        /// </summary>
        public IEnumerable<Database.File> AllFiles
        {
            get { return FileDatabaseManifest.Files; }
        }
        #endregion

        #region ctor
        /// <summary>
        /// Construcs a new RemoteFileSystemProxy and initializes this by fetching the file database manifest
        /// from the server. The reference to the authorization session is not kept around as this can expire
        /// </summary>
        /// <param name="authorizationSession"
        /// >The authorization session to use to initialize this. This is is not kept around
        /// </param>
        /// <param name="config">The program config</param>
        public RemoteFileSystemProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion
    }
}
