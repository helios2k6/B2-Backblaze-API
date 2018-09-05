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

using B2BackupUtility.Logging;
using B2BackupUtility.PMVC.Commands;
using B2BackupUtility.PMVC.Proxies;
using PureMVC.Interfaces;
using PureMVC.Patterns.Mediator;
using System;
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.PMVC.Mediators
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

            // Warning messages

            // Info level messages
            { GenerateEncryptionKey.EncryptionKeyNotification, LogLevel.INFO },
            { GenerateEncryptionKey.InitializationVectorNotification, LogLevel.INFO },

            // Verbose messages

            // Debug messages
        };

        private static readonly IDictionary<LogLevel, string> LogLevelToPrefix = new Dictionary<LogLevel, string>
        {
            { LogLevel.DEBUG, "[DEBUG]" },
            { LogLevel.VERBOSE, "[VERBOSE]" },
            { LogLevel.INFO, "[INFO]" },
            { LogLevel.WARNING, "[WARNING]" },
            { LogLevel.CRITICAL, "[CRITICAL]" },
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
        public ConsoleMediator() : base(Name, null)
        {
            ProgramArgumentsProxy argumentsProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
            if (argumentsProxy.DoesOptionExist(DebugLevelSwitch))
            {
                _logLevel = LogLevel.DEBUG;
            }
            else if (argumentsProxy.DoesOptionExist(VerboseLevelSwitch))
            {
                _logLevel = LogLevel.VERBOSE;
            }
            else
            {
                _logLevel = LogLevel.INFO;
            }
        }

        #endregion

        #region public methods
        public override void HandleNotification(INotification notification)
        {
            if (NotifLogLevels.TryGetValue(notification.Name, out LogLevel logLevel) && _logLevel >= logLevel)
            {
                Console.Error.WriteLine($"{LogLevelToPrefix[logLevel]}[{DateTime.Now}] - {GetLogMessageFromNotification(notification)}");
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
                return $"{notification.Name} - {e.Message}";
            }
            else if (notification.Body is string s)
            {
                return $"{notification.Name} - {s}";
            }

            return notification.Name;
        }
        #endregion
    }
}
