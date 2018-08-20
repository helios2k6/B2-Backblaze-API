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
using System.Linq;

namespace B2BackupUtility.Commands
{
    public abstract class BaseDeleteCommand : BaseCommand
    {
        #region ctor
        public BaseDeleteCommand(IEnumerable<string> rawArgs) : base(rawArgs)
        {
        }
        #endregion

        #region protected methods
        protected void DeleteFile(string fileID, string fileName, bool shouldUpdateManifest)
        {
            BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deleteFileActionResult = new DeleteFileAction(
                GetOrCreateAuthorizationSession(),
                fileID,
                fileName
            ).Execute();

            if (shouldUpdateManifest)
            {
                UpdateFileManifest(deleteFileActionResult);
            }
            LogDeletion(deleteFileActionResult, fileName);
        }
        #endregion

        #region private methods
        private void UpdateFileManifest(BackblazeB2ActionResult<BackblazeB2DeleteFileResult> result)
        {
            if (result.HasResult)
            {
                string fileName = result.Result.FileName;
                FileManifest.FileEntries = FileManifest.FileEntries.Where(e => e.DestinationFilePath.Equals(fileName, StringComparison.Ordinal) == false).ToArray();
                FileManifestActions.WriteManifestFileToServer(GetOrCreateAuthorizationSession(), BucketID, FileManifest);
            }
        }

        private void LogDeletion(BackblazeB2ActionResult<BackblazeB2DeleteFileResult> result, string fileName)
        {
            if (result.HasResult)
            {
                LogInfo($"Deleted file: {fileName}");
            }
            else
            {
                LogInfo($"Failed to delete file: {fileName}. {result.Errors.First().Message}");
            }
        }
        #endregion
    }
}
