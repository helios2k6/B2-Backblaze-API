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

using B2BackupUtility.Proxies;
using B2BackupUtility.Proxies.Exceptions;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System.IO;

namespace B2BackupUtility.Commands
{
    public sealed class DownloadFile : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Download File";

        public static string FailedCommandNotification => "Failed To Download File";

        public static string FinishedCommandNotification => "Finished Downloading File";

        public static string CommandSwitch => "--download-file";

        public static string FileOption => "--file";

        public static string DestinationOption => "--destination";

        public static CommandType CommandType => CommandType.DOWNLOAD;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            AuthorizationSessionProxy authorizationProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            if (programArgsProxy.TryGetArgument(FileOption, out string fileToDownload))
            {
                RemoteFileSystemProxy remoteFileSystemProxy = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);
                if (remoteFileSystemProxy.TryGetFileByName(fileToDownload, out Database.File remoteFileToDownload))
                {
                    if (programArgsProxy.TryGetArgument(DestinationOption, out string localFileDestination) == false)
                    {
                        localFileDestination = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            Path.GetFileName(remoteFileToDownload.FileName)
                        );
                    }

                    DownloadFileProxy downloadFileProxy = (DownloadFileProxy)Facade.RetrieveProxy(DownloadFileProxy.Name);
                    downloadFileProxy.DownloadFile(authorizationProxy.AuthorizationSession, remoteFileToDownload, localFileDestination);
                    SendNotification(FinishedCommandNotification, $"Finished downloading file {fileToDownload}", null);
                }
                else
                {
                    throw new TerminateProgramException($"The file {fileToDownload} could not be found in the manifest");
                }
            }
            else
            {
                throw new TerminateProgramException("No file specified for download");
            }
        }
        #endregion
    }
}
