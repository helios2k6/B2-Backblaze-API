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

using B2BackupUtility.PMVC.Proxies;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;

namespace B2BackupUtility.PMVC.Commands
{
    /// <summary>
    /// This command initializes the config proxy
    /// </summary>
    public sealed class InitializeConfig : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Initialize Config";

        public static string FailedCommandNotification => "Failed To Initialize Config";

        public static string FinishedCommandNotification => "Finished Initializing Config";

        public static string CommandOption = "--config";
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            try
            {
                ProgramArgumentsProxy argProxy = (ProgramArgumentsProxy)Facade.RetrieveProxy(ProgramArgumentsProxy.Name);
                if (argProxy.TryGetArgument(CommandOption, out string configArgument))
                {
                    ((ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name)).Initialize(configArgument);
                }

                SendNotification(FinishedCommandNotification, null, null);
            }
            catch (Exception e)
            {
                SendNotification(FailedCommandNotification, e, null);
            }
        }
        #endregion
    }
}
