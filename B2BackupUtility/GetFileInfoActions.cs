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

namespace B2BackupUtility
{
    public static class GetFileInfoActions
    {
        public static void ExecuteGetFileInfo(
            BackblazeB2AuthorizationSession authorizationSession,
            IEnumerable<string> args
        )
        {
            string fileID = CommonUtils.GetArgument(args, "--file-id");
            if (string.IsNullOrWhiteSpace(fileID))
            {
                Console.WriteLine("A file name and file ID must be provided");
                return;
            }

            GetFileInfoAction getFileInfoAction = new GetFileInfoAction(authorizationSession, fileID);
            BackblazeB2ActionResult<BackblazeB2GetFileInfoResult> getFileInfoResultMaybe = getFileInfoAction.Execute();
            if (getFileInfoResultMaybe.HasErrors)
            {
                Console.WriteLine(string.Format("Could not get file info for {0}", fileID));
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
                .AppendLine(string.Format("Upload Time Stamp: {0}", fileInfo.UploadTimeStamp))
                .AppendLine();
            
            Console.WriteLine(builder.ToString());
        }
    }
}