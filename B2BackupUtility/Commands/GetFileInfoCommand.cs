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
using System.Text;

namespace B2BackupUtility.Commands
{
    public sealed class GetFileInfoCommand : BaseCommand
    {
        #region private fields
        private static string FileIDOption => "--file-id";
        #endregion

        #region public properties
        public static string ActionName => "Get File Info";

        public static string CommandSwitch => "--get-file-info";

        public static IEnumerable<string> CommandOptions => new[] { FileIDOption };
        #endregion

        #region ctor
        public GetFileInfoCommand(IEnumerable<string> rawArgs) : base(rawArgs)
        {
        }
        #endregion

        #region public methods
        public override void ExecuteAction()
        {
            bool hasFileIDOption = TryGetArgument(FileIDOption, out string fileID);
            if (string.IsNullOrWhiteSpace(fileID))
            {
                throw new InvalidOperationException("You must provide a file ID");
            }

            GetFileInfoAction getFileInfoAction = new GetFileInfoAction(GetOrCreateAuthorizationSession(), fileID);
            BackblazeB2ActionResult<BackblazeB2GetFileInfoResult> getFileInfoResultMaybe = getFileInfoAction.Execute();
            if (getFileInfoResultMaybe.HasErrors)
            {
                LogCritical($"Unable to get info on file {fileID}");
                return;
            }

            BackblazeB2GetFileInfoResult fileInfo = getFileInfoResultMaybe.Result;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("File Info")
                .AppendLine(string.Format("Bucket ID: {0}", fileInfo.BucketID))
                .AppendLine(string.Format("File ID: {0}", fileInfo.FileID))
                .AppendLine(string.Format("File Name: {0}", fileInfo.FileName))
                .AppendLine(string.Format("Content Length: {0}", fileInfo.ContentLength))
                .AppendLine(string.Format("Content SHA1: {0}", fileInfo.ContentSha1))
                .AppendLine(string.Format("Content Type; {0}", fileInfo.ContentType))
                .AppendLine(string.Format("Upload Time Stamp: {0}", fileInfo.UploadTimeStamp));

            LogInfo(builder.ToString());
        }
        #endregion
    }
}
