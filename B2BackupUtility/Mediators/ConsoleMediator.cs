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
using B2BackupUtility.Proxies.Exceptions;
using PureMVC.Interfaces;
using PureMVC.Patterns.Mediator;
using System;
using System.Collections.Generic;
using System.Text;

namespace B2BackupUtility.Mediators
{
    /// <summary>
    /// Represents the mediator that will write to the console
    /// </summary>
    public sealed class ConsoleMediator : Mediator
    {
        #region private fields
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
        public static string ConsoleLogNotification => "Console Log";

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
            if (notification.Name.Equals(ConsoleLogNotification, StringComparison.Ordinal) && Enum.TryParse(notification.Type, out LogLevel logLevel) && logLevel >= _logLevel)
            {
                string messageFromNotification = GetLogMessageFromNotification(notification);
                string connector = string.IsNullOrWhiteSpace(messageFromNotification) ? string.Empty : " - ";
                Console.Error.WriteLine($"[{LogLevelToPrefix[logLevel]}][{DateTime.Now}]{connector}{messageFromNotification}");
            }
        }

        public override string[] ListNotificationInterests()
        {
            return new[] { ConsoleLogNotification };
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
