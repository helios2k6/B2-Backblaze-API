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
using B2BackupUtility.PMVC.Encryption;
using B2BackupUtility.PMVC.Proxies;
using Newtonsoft.Json;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility.PMVC.Commands
{
    public sealed class InitializeFileDatabaseManifest : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Initialize File Database Manifest";

        public static string FailedCommandNotification => "Failed To Initialize File Database Manifest";

        public static string FinishCommandNotification => "Finished Initializing Fetching File Database Manifest";
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            try
            {
                ListOfFilesOnB2Proxy listOfFilesProxy = (ListOfFilesOnB2Proxy)Facade.RetrieveProxy(ListOfFilesOnB2Proxy.Name);

                // First, list the files on the server
                // Second, find the file manifest
                // Third, download the file manifest. If you cannot find it, then return an empty file
                // manifest
                FileResult fileDatabaseManifest = listOfFilesProxy.Files.Where(
                    f => f.FileName.Equals(FileDatabaseManifestProxy.RemoteFileDatabaseManifestName, StringComparison.Ordinal)
                ).SingleOrDefault();

                if (fileDatabaseManifest == null)
                {
                    // Just return a new file manifest if we can't find
                    // one on the server
                    SetFileDatabaseManifestOnProxy(new FileDatabaseManifest
                    {
                        Files = new Database.File[0],
                    });
                }

                AuthorizationSessionProxy authorizationSessionProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);

                // Download the file manifest 
                using (MemoryStream outputStream = new MemoryStream())
                using (DownloadFileAction manifestFileDownloadAction = new DownloadFileAction(
                    authorizationSessionProxy.AuthorizationSession,
                    outputStream,
                    fileDatabaseManifest.FileID
                ))
                {
                    BackblazeB2ActionResult<BackblazeB2DownloadFileResult> manifestResultOption = manifestFileDownloadAction.Execute();
                    if (manifestResultOption.HasResult)
                    {
                        // Now, read string from manifest
                        outputStream.Flush();
                        SetFileDatabaseManifestOnProxy(DeserializeManifest(outputStream.ToArray()));
                    }
                    else
                    {
                        SetFileDatabaseManifestOnProxy(new FileDatabaseManifest
                        {
                            Files = new Database.File[0],
                        });
                    }
                }

                SendNotification(FinishCommandNotification, null, null);
            }
            catch (Exception ex)
            {
                SendNotification(FailedCommandNotification, ex, null);
            }
        }
        #endregion

        #region private methods
        private FileDatabaseManifest DeserializeManifest(byte[] encryptedBytes)
        {
            ConfigProxy configProxy = (ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name);
            Config config = configProxy.Config;
            using (MemoryStream deserializedMemoryStream = new MemoryStream())
            {
                using (MemoryStream compressedBytesStream = new MemoryStream(EncryptionHelpers.DecryptBytes(encryptedBytes, config.EncryptionKey, config.InitializationVector)))
                using (GZipStream decompressionStream = new GZipStream(compressedBytesStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(deserializedMemoryStream);
                }

                return JsonConvert.DeserializeObject<FileDatabaseManifest>(
                    Encoding.UTF8.GetString(deserializedMemoryStream.ToArray())
                );
            }
        }

        private void SetFileDatabaseManifestOnProxy(FileDatabaseManifest fileDatabaseManifest)
        {
            FileDatabaseManifestProxy fileDatabaseManifestProxy =
                (FileDatabaseManifestProxy)Facade.RetrieveProxy(FileDatabaseManifestProxy.Name);
            fileDatabaseManifestProxy.Data = fileDatabaseManifest ?? throw new ArgumentNullException("File Database Manifest should not be null");
            fileDatabaseManifestProxy.RefreshLookupCache();
        }
        #endregion
    }
}
