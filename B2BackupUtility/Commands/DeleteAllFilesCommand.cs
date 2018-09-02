﻿/* 
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
    public sealed class DeleteAllFilesCommand : BaseDeleteCommand
    {
        #region public properties
        public static string CommandName => "Delete All Files";

        public static string CommandSwitch => "--delete-all-files";

        public static IEnumerable<string> CommandOptions => Enumerable.Empty<string>();
        #endregion

        #region ctor
        public DeleteAllFilesCommand(IEnumerable<string> rawArgs) : base(rawArgs)
        {
        }
        #endregion

        #region public methods
        public override void ExecuteAction()
        {
            LogInfo("Deleting all files and ignoring file database manifest");
            try
            {
                foreach (BackblazeB2ListFilesResult.FileResult fileResult in AllRemoteB2Files)
                {
                    CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();
                    BackblazeB2ActionResult<BackblazeB2DeleteFileResult> deletionResult = DeleteRawFile(fileResult.FileID, fileResult.FileName);

                    if (deletionResult.HasResult)
                    {
                        LogInfo($"Deleted file {fileResult.FileName} - {fileResult.FileID}");
                    }
                    else
                    {
                        LogCritical($"Could not delete file {fileResult.FileName}. Reason: {deletionResult}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogCritical("Deletion cancelled!");
            }
        }
        #endregion
    }
}
