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

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Represents an action to upload a file
    /// </summary>
    public sealed class UploadFileCommand : BaseUploadCommand
    {
        #region private fields
        private static string FileOption => "--file";

        private static string DestinationOption => "--destination";
        #endregion

        #region public properties
        public static string ActionName => "Upload File";

        public static string CommandSwitch => "--upload-file";

        public static IEnumerable<string> CommandOptions => new[] { FileOption, DestinationOption, ConnectionsOption };
        #endregion

        #region ctor
        public UploadFileCommand(IEnumerable<string> rawArgs) : base(rawArgs)
        {
        }
        #endregion

        #region public methods
        public override void ExecuteAction()
        {
            bool hasFileOption = TryGetArgument(FileOption, out string localFilePath);
            if (hasFileOption == false)
            {
                throw new InvalidOperationException("You must have a file to upload");
            }

            if (string.IsNullOrWhiteSpace(localFilePath) || File.Exists(localFilePath) == false)
            {
                throw new FileNotFoundException($"Cannot find file path: {localFilePath}");
            }

            bool hasDestinationOption = TryGetArgument(DestinationOption, out string destinationRemoteFilePath);
            if (hasDestinationOption == false || string.IsNullOrWhiteSpace(destinationRemoteFilePath))
            {
                // Remote file path is optional
                destinationRemoteFilePath = localFilePath;
            }

            UploadFile(localFilePath, destinationRemoteFilePath);
        }
        #endregion
    }
}