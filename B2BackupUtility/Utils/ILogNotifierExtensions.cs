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

using B2BackupUtility.Mediators;
using System;

namespace B2BackupUtility.Utils
{
    /// <summary>
    /// Extension methods for ILogNotifier classes
    /// </summary>
    public static class ILogNotifierExtensions
    {
        #region public methods
        /// <summary>
        /// Log a message at the Warning level
        /// </summary>
        /// <param name="this">The ILogNotifier</param>
        /// <param name="message">The message to send</param>
        public static void Critical(this ILogNotifier @this, string message)
        {
            CoreNullCheck(@this);
            CoreLog(@this, message, LogLevel.CRITICAL);
        }

        /// <summary>
        /// Log a message at the Warning level
        /// </summary>
        /// <param name="this">The ILogNotifier</param>
        /// <param name="message">The message to send</param>
        public static void Warning(this ILogNotifier @this, string message)
        {
            CoreNullCheck(@this);
            CoreLog(@this, message, LogLevel.WARNING);
        }

        /// <summary>
        /// Log a message at the Info level
        /// </summary>
        /// <param name="this">The ILogNotifier</param>
        /// <param name="message">The message to send</param>
        public static void Info(this ILogNotifier @this, string message)
        {
            CoreNullCheck(@this);
            CoreLog(@this, message, LogLevel.INFO);
        }

        /// <summary>
        /// Log a message at the Verbose level
        /// </summary>
        /// <param name="this">The ILogNotifier</param>
        /// <param name="message">The message to send</param>
        public static void Verbose(this ILogNotifier @this, string message)
        {
            CoreNullCheck(@this);
            CoreLog(@this, message, LogLevel.VERBOSE);
        }

        /// <summary>
        /// Log a message at the Debug level
        /// </summary>
        /// <param name="this">The ILogNotifier</param>
        /// <param name="message">The message to send</param>
        public static void Debug(this ILogNotifier @this, string message)
        {
            CoreNullCheck(@this);
            CoreLog(@this, message, LogLevel.DEBUG);
        }
        #endregion

        #region private methods
        private static void CoreNullCheck(ILogNotifier @this)
        {
            if (@this == null)
            {
                throw new NullReferenceException("This is null");
            }
        }

        public static void CoreLog(ILogNotifier notifier, string message, LogLevel logLevel)
        {
            lock(notifier)
            {
                notifier.SendNotification(ConsoleMediator.ConsoleLogNotification, message, logLevel.ToString());
            }
        }
        #endregion
    }
}
