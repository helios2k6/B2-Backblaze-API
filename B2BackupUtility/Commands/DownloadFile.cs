/* 
 * Copyright (c) 2023 Andrew Johnson
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
using B2BackupUtility.Utils;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;
using System.IO;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Download a file from the B2 Backblaze server
    /// </summary>
    public sealed class DownloadFile : SimpleCommand, ILogNotifier
    {
        #region public properties
        public static string CommandNotification => "Download File";

        public static string FinishedCommandNotification => "Finished Downloading File";

        public static string CommandSwitch => "--download-file";

        public static string FileNameOption => "--file-name";

        public static string FileIDOption => "--file-id";

        public static string DestinationOption => "--destination";

        public static CommandType CommandType => CommandType.DOWNLOAD_FILE;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            this.Debug(CommandNotification);
            AuthorizationSessionProxy authorizationProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            Database.File fileToDownload = GetFile();
            string localFileDestination = GetDestinationOfFile(fileToDownload);

            DownloadFileProxy downloadFileProxy = (DownloadFileProxy)Facade.RetrieveProxy(DownloadFileProxy.Name);
            downloadFileProxy.DownloadFile(authorizationProxy.AuthorizationSession, fileToDownload, localFileDestination);
            this.Info($"Finished downloading file: {fileToDownload}");
        }
        #endregion

        #region private methods
        private string GetDestinationOfFile(Database.File remoteFileToDownload)
        {
            this.Debug($"Getting destination of file {remoteFileToDownload}");
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            if (programArgsProxy.TryGetArgument(DestinationOption, out string localFileDestination) == false)
            {
                localFileDestination = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    Path.GetFileName(remoteFileToDownload.FileName)
                );
            }
            this.Debug($"Destination file is: {localFileDestination}");
            return localFileDestination;
        }

        private Database.File GetFile()
        {
            this.Debug("Getting file to download");
            ProgramArgumentsProxy programArgsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            bool hasFileID = programArgsProxy.TryGetArgument(FileIDOption, out string fileToDownloadByID);
            bool hasFileName = programArgsProxy.TryGetArgument(FileNameOption, out string fileToDownloadByName);
            if (hasFileID && hasFileName)
            {
                throw new TerminateProgramException("Specific either a file name or a file ID; not both");
            }

            RemoteFileSystemProxy remoteFileSystemProxy = (RemoteFileSystemProxy)Facade.RetrieveProxy(RemoteFileSystemProxy.Name);
            if (hasFileID)
            {
                this.Debug("Searching by File ID");
                if (remoteFileSystemProxy.TryGetFileByID(fileToDownloadByID, out Database.File file))
                {
                    this.Debug($"Found: {file}");
                    return file;
                }

                throw new TerminateProgramException($"Could not find file ID {fileToDownloadByID}");
            }

            if (hasFileName)
            {
                this.Debug("Searching by file name");
                if (remoteFileSystemProxy.TryGetFileByName(fileToDownloadByName, out Database.File file))
                {
                    this.Debug($"Found: {file}");
                    return file;
                }

                throw new TerminateProgramException($"Could not find file name {fileToDownloadByName}");
            }

            throw new InvalidOperationException("No file ID or file name provided to download");
        }
        #endregion
    }
}
