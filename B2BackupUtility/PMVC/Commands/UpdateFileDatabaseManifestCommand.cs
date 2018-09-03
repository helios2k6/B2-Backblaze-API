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
using B2BackupUtility.PMVC.Encryption;
using B2BackupUtility.PMVC.Proxies;
using Newtonsoft.Json;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace B2BackupUtility.PMVC.Commands
{
    /// <summary>
    /// Uploads the file database manifest to the B2 Backblaze Server
    /// </summary>
    public sealed class UploadFileDatabaseManifestCommand : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Update File Database Manifest";

        public static string FailedCommandNotification => "Failed To Update File Database Manifest";

        public static string FinishedCommandNotification => "Finished Updating File Database Manifest";
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            AuthorizationSessionProxy authorizationProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            ConfigProxy configProxy = (ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name);

            UploadWithSingleConnectionAction uploadAction = new UploadWithSingleConnectionAction(
                authorizationProxy.AuthorizationSession,
                configProxy.Config.BucketID,
                SerializeManifest(),
                FileDatabaseManifestProxy.RemoteFileDatabaseManifestName,
                CancellationToken.None
            );

            BackblazeB2ActionResult<BackblazeB2UploadFileResult> uploadResult = uploadAction.Execute();
            if (uploadResult.HasResult)
            {
                SendNotification(FinishedCommandNotification, uploadResult, null);
            }
            else
            {
                SendNotification(FailedCommandNotification, uploadResult, null);
            }
        }
        #endregion

        #region private methods
        private byte[] SerializeManifest()
        {
            ConfigProxy configProxy = (ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name);
            FileDatabaseManifestProxy fileDatabaseManifestProxy = (FileDatabaseManifestProxy)Facade.RetrieveProxy(FileDatabaseManifestProxy.Name);
            using (MemoryStream serializedManifestStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileDatabaseManifestProxy.FileDatabaseManifest))))
            using (MemoryStream compressedMemoryStream = new MemoryStream())
            {
                // It's very important that we dispose of the GZipStream before reading from the memory stream
                using (GZipStream compressionStream = new GZipStream(compressedMemoryStream, CompressionMode.Compress, true))
                {
                    serializedManifestStream.CopyTo(compressionStream);
                }

                return EncryptionHelpers.EncryptBytes(compressedMemoryStream.ToArray(), configProxy.Config.EncryptionKey, configProxy.Config.InitializationVector);
            }
        }
        #endregion
    }
}
