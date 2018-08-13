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

namespace B2BackupUtility
{
    public static class DeleteFileActions
    {
        public static void DeleteFile(BackblazeB2AuthorizationSession authorizationSession, IEnumerable<string> args)
        {
            string fileName = CommonUtils.GetArgument(args, "--file-name");
            string fileID = CommonUtils.GetArgument(args, "--file-id");
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(fileID))
            {
                Console.WriteLine("A file name and file ID must be provided");
                return;
            }

            Console.WriteLine("Deleting File");
            DeleteFileAction deleteFileAction = new DeleteFileAction(authorizationSession, fileID, fileName);
            BackblazeB2ActionResult<BackblazeB2DeleteFileResult> result = CommonUtils.ExecuteAction(deleteFileAction, "Delete file");
            if (result.HasResult)
            {
                Console.WriteLine(string.Format("File successfully deleted: {0} | {1}", result.Result.FileName, result.Result.FileID));
            }
        }

        public static void DeleteAllFiles(BackblazeB2AuthorizationSession authorizationSession, string bucketID)
        {
            Console.WriteLine(string.Format("Deleting all files in {0}", bucketID));

            ListFilesAction allFileVersionsAction = ListFilesAction.CreateListFileActionForFileVersions(authorizationSession, bucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> allFileVersionsActionResult = allFileVersionsAction.Execute();
            if (allFileVersionsActionResult.HasErrors)
            {
                Console.WriteLine(string.Format("Unable to get the list of files. {0}", allFileVersionsActionResult.ToString()));
                return;
            }

            foreach (BackblazeB2ListFilesResult.FileResult fileResult in allFileVersionsActionResult.Result.Files)
            {
                if (CancellationActions.GlobalCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    DeleteFileAction deleteFileAction = new DeleteFileAction(authorizationSession, fileResult.FileID, fileResult.FileName);
                    BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deleteFileActionResult = deleteFileAction.Execute();

                    if (deleteFileActionResult.HasResult)
                    {
                        Console.WriteLine(string.Format("File successfully deleted: {0} | {1}", fileResult.FileName, fileResult.FileID));
                       
                    }
                    else
                    {
                        Console.WriteLine(string.Format("Could not delete file: {0} - {1}", fileResult.FileName, fileResult.FileID));
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("An exception occurred while deleting file {0} - {1}", fileResult.FileName, fileResult.FileID));
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
