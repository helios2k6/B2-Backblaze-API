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

using B2BackblazeBridge.Core;
using B2BackupUtility.Commands;
using B2BackupUtility.Proxies;
using B2BackupUtility.Proxies.Exceptions;
using PureMVC.Interfaces;
using PureMVC.Patterns.Mediator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace B2BackupUtility.Mediators
{
    /// <summary>
    /// Represents the mediator that will write to the console
    /// </summary>
    public sealed class ConsoleMediator : Mediator
    {
        #region private fields
        private static readonly IDictionary<string, LogLevel> NotifLogLevels = new Dictionary<string, LogLevel>
        {
            // Critical messsages
            { CancellationEventRouter.CancellationEvent, LogLevel.CRITICAL },
            { CancellationEventRouter.ImmediateCancellationEvent, LogLevel.CRITICAL },
            { PrintHelp.HelpStringNotification, LogLevel.CRITICAL },
            { StartSelectedProgram.FailedCommandNotification, LogLevel.CRITICAL },

            // Warning messages
            { DeleteFileProxy.FailedToDeleteFile, LogLevel.WARNING},
            { UploadFileProxy.FailedToUploadFile, LogLevel.WARNING },
            { UploadFileProxy.FailedToUploadFileManifest, LogLevel.WARNING },
            { PruneFileShardProxy.FailedToPruneFile, LogLevel.WARNING },

            // Info level messages
            { CheckFileManifestProxy.BeginCheckFileManifest, LogLevel.INFO },
            { CheckFileManifestProxy.ShardIDNotAccountedFor, LogLevel.INFO },
            { CheckFileManifestProxy.FinishedCheckFileManifest, LogLevel.INFO },
            { DeleteFileProxy.FinishedDeletingFile, LogLevel.INFO },
            { DownloadFile.FinishedCommandNotification, LogLevel.INFO },
            { GenerateEncryptionKey.EncryptionKeyNotification, LogLevel.INFO },
            { GenerateEncryptionKey.InitializationVectorNotification, LogLevel.INFO },
            { ListFiles.AllFilesListNotification, LogLevel.INFO },
            { PruneFileShardProxy.FinishedPruningFile, LogLevel.INFO },
            { UploadFileProxy.BeginUploadFile, LogLevel.INFO },
            { UploadFileProxy.FinishUploadFile, LogLevel.INFO },
            { UploadFileProxy.FileTierChanged, LogLevel.INFO },
            { UploadFileProxy.SkippedUploadFile, LogLevel.INFO },
            { UploadFileProxy.UploadProgress, LogLevel.INFO },
            { UploadFileProxy.UploadingFileManifest, LogLevel.INFO },

            // Verbose messages
            { DeleteFileProxy.BeginDeletingFile, LogLevel.VERBOSE },

            // Debug messages
            { CheckFileManifest.CommandNotification, LogLevel.DEBUG },
            { DeleteAllFiles.CommandNotification, LogLevel.DEBUG },
            { DeleteFile.CommandNotification, LogLevel.DEBUG },
            { DownloadFile.CommandNotification, LogLevel.DEBUG },
            { GenerateEncryptionKey.CommandNotification, LogLevel.DEBUG },
            { InitializeAuthorizationSession.CommandNotification, LogLevel.DEBUG },
            { InitializeConfig.CommandNotification, LogLevel.DEBUG },
            { InitializeDownloadProxy.CommandNotification, LogLevel.DEBUG },
            { InitializeModel.CommandNotification, LogLevel.DEBUG },
            { InitializeRemoteFileSystem.CommandNotification, LogLevel.DEBUG },
            { ListFiles.CommandNotification, LogLevel.DEBUG },
            { PrintHelp.CommandNotification, LogLevel.DEBUG },
            { PruneFileShardProxy.BeginPruneFile, LogLevel.DEBUG },
            { StartSelectedProgram.CommandNotification, LogLevel.DEBUG },
            { UploadFile.CommandNotification, LogLevel.DEBUG },
            { UploadFolder.CommandNotification, LogLevel.DEBUG },
        };

        private static readonly IDictionary<LogLevel, string> LogLevelToPrefix = new Dictionary<LogLevel, string>
        {
            { LogLevel.DEBUG, "DEBUG" },
            { LogLevel.VERBOSE, "VERBOSE" },
            { LogLevel.INFO, "INFO" },
            { LogLevel.WARNING, "WARNING" },
            { LogLevel.CRITICAL, "CRITICAL" },
        };
        #endregion

        #region public properties
        public static string Name => "Console Mediator";

        public static string DebugLevelSwitch => "--debug";

        public static string VerboseLevelSwitch => "--verbose";

        private readonly LogLevel _logLevel;
        #endregion

        #region ctor
        /// <summary>
        /// Default constructor
        /// </summary>
        public ConsoleMediator(LogLevel logLevel) : base(Name, null)
        {
            _logLevel = logLevel;
        }

        #endregion

        #region public methods
        public override void HandleNotification(INotification notification)
        {
            if (NotifLogLevels.TryGetValue(notification.Name, out LogLevel logLevel) && _logLevel <= logLevel)
            {
                string messageFromNotification = GetLogMessageFromNotification(notification);
                string connector = string.IsNullOrWhiteSpace(messageFromNotification) ? string.Empty : " - ";
                Console.Error.WriteLine($"[{LogLevelToPrefix[logLevel]}][{DateTime.Now}][{notification.Name}]{connector}{messageFromNotification}");
            }
        }

        public override string[] ListNotificationInterests()
        {
            return NotifLogLevels.Keys.ToArray();
        }
        #endregion

        #region private methods
        private static string GetLogMessageFromNotification(INotification notification)
        {
            if (notification.Body is Exception e)
            {
                if (e is IExceptionHasB2BackblazeDetails d)
                {
                    return $"{PrintErrorDetails(d)}";
                }

                return $"{e.Message}";
            }
            else if (notification.Body is string s)
            {
                return $"{s}";
            }
            else if (notification.Body != null)
            {
                return $"{notification.Body.ToString()}";
            }

            return string.Empty;
        }

        private static string PrintErrorDetails(IExceptionHasB2BackblazeDetails errorDetails)
        {
            StringBuilder builder = new StringBuilder();
            foreach (BackblazeB2ActionErrorDetails error in errorDetails.BackblazeErrorDetails)
            {
                builder.AppendLine(error.ToString()).AppendLine();
            }

            return builder.ToString();
        }
        #endregion
    }
}
