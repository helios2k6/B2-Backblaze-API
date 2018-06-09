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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using static B2BackblazeBridge.Core.BackblazeB2ListFilesResult;

namespace B2BackupUtility
{
    public static class VerifyFilesActions
    {
        public static async Task VerifyFiles(BackblazeB2AuthorizationSession authorizationSession, string bucketID, IEnumerable<string> remainingArgs)
        {
            bool flattenFiles = CommonActions.DoesOptionExist(remainingArgs, "--flatten");
            string folder = CommonActions.GetArgument(remainingArgs, "--folder");
            
            if (string.IsNullOrWhiteSpace(folder))
            {
                Console.WriteLine("Must provide a valid folder path");
                return;
            }

            if (Directory.Exists(folder) == false)
            {
                Console.WriteLine(string.Format("Folder {0} does not exist", folder));
                return;
            }

            Console.WriteLine("Verifying files");
            ListFilesAction action = ListFilesAction.CreateListFileActionForFileNames(authorizationSession, bucketID, true);
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> actionResult = await CommonActions.ExecuteActionAsync(action, "List files");
            if (actionResult.HasResult)
            {
                IEnumerable<string> allFiles = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
                if (flattenFiles)
                {
                    allFiles = allFiles.Select(e => Path.GetFileName(e)).Distinct();
                }

                ISet<string> allLocalFilePaths = new HashSet<string>(allFiles);
                ISet<string> filesThatAreNotUploaded = new HashSet<string>();
                ISet<string> filesThatDoNotMatch = new HashSet<string>();
                foreach (FileResult onlineFile in actionResult.Result.Files)
                {
                    
                }
            }
        }
    }
}