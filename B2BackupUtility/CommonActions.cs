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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace B2BackupUtility
{
    public static class CommonActions
    {
        public static bool AreFilesEqual(string fileA, string fileB)
        {
            if (File.Exists(fileA) == false || File.Exists(fileB) == false)
            {
                return false;
            }

            FileInfo fileAInfo = new FileInfo(fileA);
            FileInfo fileBInfo = new FileInfo(fileB);
            if (fileAInfo.Length != fileBInfo.Length)
            {
                return false;
            }

            using (FileStream fileAStream = new FileStream(fileA, FileMode.Open, FileAccess.Read))
            using (FileStream fileBStream = new FileStream(fileB, FileMode.Open, FileAccess.Read))
            {
                int fileAByte = fileAStream.ReadByte();
                int fileBByte = fileBStream.ReadByte();
                while (fileAByte != -1 && fileBByte != -1)
                {
                    if (fileAByte != fileBByte)
                    {
                        return false;
                    }

                    fileAByte = fileAStream.ReadByte();
                    fileBByte = fileBStream.ReadByte();
                }
            }

            return true;
        }

        public static async Task<BackblazeB2ActionResult<T>> ExecuteActionAsync<T>(BaseAction<T> action, string actionName)
        {
            BackblazeB2ActionResult<T> actionResult = await action.ExecuteAsync();
            if (actionResult.HasErrors)
            {
                string errorMessagesComposed = actionResult.Errors.Select(t => t.Message).Aggregate((a, b) => string.Format("{0}\n{1}", a, b));
                Console.WriteLine(string.Format("Could not execute action {0}. Errors: {1}", actionName, errorMessagesComposed));
            }

            return actionResult;
        }

        public static bool DoesOptionExist(IEnumerable<string> args, string option)
        {
            return args.Any(t => t.Equals(option, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetArgument(IEnumerable<string> args, string option)
        {
            bool returnNextItem = false;
            foreach (string arg in args)
            {
                if (returnNextItem)
                {
                    return arg;
                }

                if (arg.Equals(option, StringComparison.OrdinalIgnoreCase))
                {
                    returnNextItem = true;
                }
            }

            return null;
        }
    }
}
