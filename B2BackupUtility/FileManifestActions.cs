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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2BackupUtility
{
    public static class FileManifestActions
    {
        private static readonly string RemoteFileManifestName = "b2_backup_util_file_manifest.txt";

        private static readonly Random RandomNumberGenerator = new Random();

        public async static Task<FileManifest> ReadManifestFileFromServerOrReturnNewOneAsync(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID,
            IEnumerable<string> args
        )
        {
            // First, list the files on the server
            // Second, find the file manifest
            // Third, download the file manifest. If you cannot find it, then return an empty file
            // manifest
            ListFilesAction listFilesActions = ListFilesAction.CreateListFileActionForFileNames(
                authorizationSession,
                bucketID,
                true
            );

            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = await listFilesActions.ExecuteAsync();

            // If we have issues listing the files, we probably have bigger problems. Going to throw an exception instead
            if (listFilesActionResult.HasErrors)
            {
                throw new InvalidOperationException("We couldn't list the files on the B2 server. Crashing immediately");
            }

            // Search for the file manifest
            BackblazeB2ListFilesResult filesResult = listFilesActionResult.Result;
            BackblazeB2ListFilesResult.FileResult manifestFile = filesResult.Files.Where(f => f.FileName.Equals(RemoteFileManifestName, StringComparison.Ordinal)).SingleOrDefault();
            if (manifestFile == null)
            {
                // Just return a new file manifest if we can't find
                // one on the server
                return new FileManifest
                {
                    ID = RandomNumberGenerator.Next(),
                    Version = 0,
                    FileEntries = new FileManifestEntry[0],
                };
            }

            // Download the file manifest 
            using (MemoryStream outputStream = new MemoryStream())
            using (DownloadFileAction manifestFileDownloadAction = new DownloadFileAction(authorizationSession, outputStream, manifestFile.FileID))
            {
                BackblazeB2ActionResult<BackblazeB2DownloadFileResult> manifestResultOption = await manifestFileDownloadAction.ExecuteAsync();
                if (manifestResultOption.HasResult)
                {
                    // Now, read string from manifest
                    await outputStream.FlushAsync();
                    outputStream.Position = 0;
                    using (StreamReader outputStreamReader = new StreamReader(outputStream))
                    {
                        string fileManifestString = await outputStreamReader.ReadToEndAsync();
                        return JsonConvert.DeserializeObject<FileManifest>(fileManifestString);
                    }
                }
                else
                {
                    return new FileManifest
                    {
                        ID = RandomNumberGenerator.Next(),
                        Version = 0,
                        FileEntries = new FileManifestEntry[0],
                    };
                }
            }
        }

        public async static Task WriteManifestFileToServer(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID,
            FileManifest manifest
        )
        {
            string serializedManifest = JsonConvert.SerializeObject(manifest);
            byte[] encodedBytes = Encoding.UTF8.GetBytes(serializedManifest);
            UploadFileAction uploadAction = new UploadFileAction(
                authorizationSession,
                bucketID,
                encodedBytes,
                RemoteFileManifestName
            );

            BackblazeB2ActionResult<BackblazeB2UploadFileResult> uploadResultOption = await uploadAction.ExecuteAsync();
            if (uploadResultOption.HasErrors)
            {
                Console.WriteLine(string.Format("There was an error uploading the File Manifest to the server: {0}", uploadResultOption.ToString()));
            }
        }
    }
}
