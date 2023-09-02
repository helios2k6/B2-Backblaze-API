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

namespace B2BackupUtility.Commands
{
    public sealed class CleanUpUnfinishedUploads : SimpleCommand, ILogNotifier
    {
        #region public properties
        public static string CommandNotification => "Clean Up Unfinished Uploads";

        public static string CommandSwitch => "--clean-up-unfinished-uploads";

        public static CommandType CommandType => CommandType.CLEAN_UP_UNFINISHED_UPLOADS;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            this.Debug(CommandNotification);
            AuthorizationSessionProxy authorizationSessionProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            CleanUpUnfinishedUploadsProxy cleanUpUnfinishedUploadsProxy = (CleanUpUnfinishedUploadsProxy)Facade.RetrieveProxy(CleanUpUnfinishedUploadsProxy.Name);
            cleanUpUnfinishedUploadsProxy.CleanUpUnfinishedUploads(() => authorizationSessionProxy.AuthorizationSession);
        }
        #endregion
    }
}
