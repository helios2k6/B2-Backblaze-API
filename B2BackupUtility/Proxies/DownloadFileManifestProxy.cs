/* 
 * Copyright (c) 2023 Andrew Johnson
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
using B2BackupUtility.Utils;
using Newtonsoft.Json;
using System.IO;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// Proxy that downloads the file manifest
    /// </summary>
    public sealed class DownloadFileManifestProxy : BaseRemoteFileSystemProxy, ILogNotifier
    {
        #region public properties
        public static string Name => "Download File Manifest Proxy";
        public static string LocalFileManifestFileName => "b2_backup_util_file_database_manifest.txt";
        #endregion

        #region ctor
        public DownloadFileManifestProxy(
            BackblazeB2AuthorizationSession authorizationSession,
            Config config
        ) : base(Name, authorizationSession, config)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Downloads the file manifest to the current directory 
        /// </summary>
        public void DownloadFileManifest()
        {
            this.Debug("Downloading file manifest");
            File.WriteAllText(
                LocalFileManifestFileName,
                JsonConvert.SerializeObject(GetClonedFileDatabaseManifest())
            );

            this.Info($"Downloaded file manifest to: {Path.Combine(Directory.GetCurrentDirectory(), LocalFileManifestFileName)}");
        }
        #endregion
    }
}