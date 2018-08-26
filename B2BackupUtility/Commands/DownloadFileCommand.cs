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

namespace B2BackupUtility.Commands
{
    public sealed class DownloadFileCommand : BaseCommand
    {
        #region private fields
        private static string FileNameOption => "--file-name";

        private static string FileIDOption => "--file-id";

        private static string DestinationOption => "--destination";
        #endregion

        #region public properties
        public static string ActionName => "Download File";

        public static string CommandSwitch => "--download-file";

        public static IEnumerable<string> CommandOptions => new[] { FileNameOption, FileIDOption, DestinationOption };
        #endregion

        #region ctor
        public DownloadFileCommand(IEnumerable<string> rawArgs) : base(rawArgs)
        {
        }
        #endregion

        #region public methods
        public override void ExecuteAction()
        {
            TryGetArgument(FileNameOption, out string fileName);
            TryGetArgument(FileIDOption, out string fileID);
            if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(fileID))
            {
                throw new InvalidOperationException("A file name or file ID must be provided");
            }

            bool hasDestinationOption = TryGetArgument(DestinationOption, out string destination);
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new InvalidOperationException("A local destination must be provided");
            }

            if (File.Exists(destination))
            {
                throw new InvalidOperationException($"The file {destination} already exists. We cannot override existing files");
            }

            using (DownloadFileAction downloadAction = GetDownloadAction(fileName, fileID, destination))
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                BackblazeB2ActionResult<BackblazeB2DownloadFileResult> result = downloadAction.Execute();
                watch.Stop();
                if (result.HasResult)
                {
                    double bytesPerSecond = result.Result.ContentLength / ((double)watch.ElapsedTicks / Stopwatch.Frequency);

                    LogInfo($"File successfully downloaded: {result.Result.FileName} to {destination}");
                    LogInfo($"Download Time: {(double)watch.ElapsedTicks / Stopwatch.Frequency} seconds");
                    //LogInfo($"Download Speed: {bytesPerSecond.ToString("0,0.00", CultureInfo.InvariantCulture):0:0,0.00} bytes / second");
                    LogInfo($"Download Speed: {bytesPerSecond.ToString("0,0", CultureInfo.InvariantCulture)} bytes / second");
                }
            }
        }
        #endregion

        #region private methods
        private DownloadFileAction GetDownloadAction(
            string fileName,
            string fileID,
            string destination
         )
        {
            return string.IsNullOrWhiteSpace(fileName)
                ? new DownloadFileAction(GetOrCreateAuthorizationSession(), destination, fileID)
                : new DownloadFileAction(GetOrCreateAuthorizationSession(), destination, BucketID, fileName);
        }
        #endregion
    }
}
