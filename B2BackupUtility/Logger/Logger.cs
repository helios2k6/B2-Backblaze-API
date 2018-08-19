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

using System.Collections.Generic;

namespace B2BackupUtility.Logger
{
    /// <summary>
    /// A generic logging class
    /// </summary>
    public sealed class Logger
    {
        #region private fields
        private readonly LogLevel _logLevel;

        private readonly IEnumerable<ILogSink> _logSinks;
        #endregion

        #region ctor
        public Logger(LogLevel logLevel, IEnumerable<ILogSink> logSinks)
        {
            _logLevel = logLevel;
            _logSinks = logSinks;
        }
        #endregion

        #region public methods
        public void Log(LogLevel level, string message)
        {
            if (level >= _logLevel)
            {
                foreach (ILogSink sink in _logSinks)
                {
                    sink.LogRawMessage($"{GetLevelPrefix(level)}: ${message}");
                }
            }
        }
        #endregion

        #region private methods
        private string GetLevelPrefix(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.CRITICAL:
                    return "[CRITICAL]";
                case LogLevel.DEBUG:
                    return "[DEBUG]";
                case LogLevel.INFO:
                    return "[INFO]";
                case LogLevel.VERBOSE:
                    return "[VERBOSE]";
                case LogLevel.WARNING:
                    return "[WARNING]";
            }

            return "[UNKNOWN]";
        }
        #endregion
    }
}
