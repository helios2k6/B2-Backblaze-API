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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace B2BackupUtility
{
    public static class FileManifestActions
    {
        private static readonly string RemoteFileManifestName = "b2_backup_util_file_manifest.txt.gz";

        private static readonly Random RandomNumberGenerator = new Random();

        public static FileManifest ReadManifestFileFromServerOrReturnNewOne(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID
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

            BackblazeB2ActionResult<BackblazeB2ListFilesResult> listFilesActionResult = listFilesActions.Execute();

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
                BackblazeB2ActionResult<BackblazeB2DownloadFileResult> manifestResultOption = manifestFileDownloadAction.Execute();
                if (manifestResultOption.HasResult)
                {
                    // Now, read string from manifest
                    outputStream.Flush();
                    return GetDecompressedDeserializedManifest(outputStream.ToArray());
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

        public static void WriteManifestFileToServer(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID,
            FileManifest manifest
        )
        {
            UploadFileAction uploadAction = new UploadFileAction(
                authorizationSession,
                bucketID,
                GetCompressedSerializedBytes(manifest),
                RemoteFileManifestName
            );

            BackblazeB2ActionResult<BackblazeB2UploadFileResult> uploadResultOption = uploadAction.Execute();
            if (uploadResultOption.HasErrors)
            {
                Console.WriteLine(string.Format("There was an error uploading the File Manifest to the server: {0}", uploadResultOption.ToString()));
            }
        }

        private static byte[] GetCompressedSerializedBytes(FileManifest manifest)
        {
            using (MemoryStream serializedManifestStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest))))
            using (MemoryStream compressedMemoryStream = new MemoryStream())
            {
                // It's very important that we dispose of the GZipStream before reading from the memory stream
                using (GZipStream compressionStream = new GZipStream(compressedMemoryStream, CompressionMode.Compress, true))
                {
                    serializedManifestStream.CopyTo(compressionStream);
                }

                return compressedMemoryStream.ToArray();
            }
        }

        private static FileManifest GetDecompressedDeserializedManifest(byte[] compressedBytes)
        {
            using (MemoryStream deserializedMemoryStream = new MemoryStream())
            {
                using (MemoryStream compressedBytesStream = new MemoryStream(compressedBytes))
                using (GZipStream decompressionStream = new GZipStream(compressedBytesStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(deserializedMemoryStream);
                }

                return JsonConvert.DeserializeObject<FileManifest>(
                    Encoding.UTF8.GetString(
                        deserializedMemoryStream.ToArray()
                    )
                );
            }
        }
    }
}
