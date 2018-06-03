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
using System.Threading.Tasks;

namespace B2BackupUtility
{
    public static class ListFilesAction
    {
        public static async Task ListFilesAsync(BackblazeB2AuthorizationSession authorizationSession, string bucketID)
        {
            Console.WriteLine("Fetching file list");
            B2BackblazeBridge.Actions.ListFilesAction action = B2BackblazeBridge.Actions.ListFilesAction.CreateListFileActionForFileNames(authorizationSession, bucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> actionResult = await CommonActions.ExecuteActionAsync(action, "List files");
            if (actionResult.HasResult)
            {
                foreach (BackblazeB2ListFilesResult.FileResult file in actionResult.Result.Files)
                {
                    Console.WriteLine(string.Format("{0} - {1}", file.FileName, file.FileID));
                }
            }
        }
    }
}
