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
using System.Linq;

namespace B2BackupUtility.Logging
{
    public static class Loggers
    {
        #region private fields
        private static readonly object Lock = new object();

        private static bool HasInitializedLogger = false;

        private static Logger InternalLoggerInstance = null;
        #endregion

        #region public methods
        public static void InitializeLogger(IEnumerable<string> args)
        {
            lock(Lock)
            {
                if (HasInitializedLogger)
                {
                    throw new InvalidOperationException("You cannot initialize the logger twice");
                }

                HasInitializedLogger = true;

                bool hasVerbose = args.Any(e => e.Equals(VerboseLogOption, StringComparison.OrdinalIgnoreCase));
                bool hasDebug = args.Any(e => e.Equals(DebugLogOption, StringComparison.OrdinalIgnoreCase));

                LogLevel logLevel = LogLevel.INFO;
                if (hasDebug)
                {
                    logLevel = LogLevel.DEBUG;
                }
                else if (hasVerbose)
                {
                    logLevel = LogLevel.VERBOSE;
                }

                InternalLoggerInstance = new Logger(logLevel, new[] { new ConsoleLogSink() });
            }
        }
        #endregion

        #region public properties
        public static string VerboseLogOption => "--verbose";

        public static string DebugLogOption => "--debug";

        public static Logger Logger
        {
            get
            {
                lock(Lock)
                {
                    if (HasInitializedLogger == false)
                    {
                        throw new InvalidOperationException("Logger instance has not been initialized yet");
                    }

                    return InternalLoggerInstance;
                }
            }
        }
        #endregion
    }
}
