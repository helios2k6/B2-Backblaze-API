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

using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Forcably terminates this application after sending a notification to log the reason why
    /// </summary>
    public sealed class TerminateProgramImmediately : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Terminate Program Immediately";

        public static string LogProgramTerminationMessage => "Termination Reason Message";

        public static int ExitCode => -1;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            // Assume that the body of the notification contains the reason why this was terminated
            SendNotification(LogProgramTerminationMessage, notification, null);

            // Exit this process immediately and cease processing other notifications
            Environment.Exit(ExitCode);
        }
        #endregion
    }
}
