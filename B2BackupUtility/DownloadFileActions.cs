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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace B2BackupUtility
{
    public static class DownloadFileActions
    {
        public static void DownloadFile(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> args)
        {
            string fileName = CommonActions.GetArgument(args, "--file-name");
            string fileID = CommonActions.GetArgument(args, "--file-id");
            if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(fileID))
            {
                Console.WriteLine("No file name or file ID could be found");
                return;
            }

            string destination = CommonActions.GetArgument(args, "--destination");
            if (string.IsNullOrWhiteSpace(destination))
            {
                Console.WriteLine("No file destination provided");
                return;
            }

            if (File.Exists(destination))
            {
                Console.WriteLine("Cannot override file that exists");
                return;
            }

            Console.WriteLine("Downloading file");
            using (DownloadFileAction downloadAction = GetDownloadAction(authorizationSession, bucketID, fileName, fileID, destination))
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                BackblazeB2ActionResult<BackblazeB2DownloadFileResult> result = CommonActions.ExecuteAction(downloadAction, "Download file");
                watch.Stop();
                if (result.HasResult)
                {
                    double bytesPerSecond = result.Result.ContentLength / ((double)watch.ElapsedTicks / Stopwatch.Frequency);

                    Console.WriteLine(string.Format("File successfully downloaded: {0} to {1}", result.Result.FileName, destination));
                    Console.WriteLine(string.Format("Download Time: {0} seconds", (double)watch.ElapsedTicks / Stopwatch.Frequency));
                    Console.WriteLine(string.Format("Download Speed: {0:0,0.00} bytes / second", bytesPerSecond.ToString("0,0.00", CultureInfo.InvariantCulture)));
                }
            }
        }

        private static DownloadFileAction GetDownloadAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string bucketID,
            string fileName,
            string fileID,
            string destination
        )
        {
            return string.IsNullOrWhiteSpace(fileName) 
                ? new DownloadFileAction(authorizationSession, destination, fileID)
                : new DownloadFileAction(authorizationSession, destination, bucketID, fileName);
        }
    }
}
