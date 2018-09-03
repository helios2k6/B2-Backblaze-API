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
using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.PMVC.Proxies;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;

namespace B2BackupUtility.PMVC.Commands
{
    public sealed class InitializeListOfFilesOnB2Command : SimpleCommand
    {
        #region public properties
        public static string CommandNotification => "Initialize List Of Files On B2";

        public static string FailedCommandNotification => "Failed To Initialize List Of Files On B2";

        public static string FinishedCommandNotification => "Finished Initializing List Of Files On B2";
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            AuthorizationSessionProxy authorizationProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            ConfigProxy configProxy = (ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name);
            ListFilesAction allFileVersionsAction = ListFilesAction.CreateListFileActionForFileVersions(
                authorizationProxy.AuthorizationSession,
                configProxy.Config.BucketID,
                true
             );

            BackblazeB2ActionResult<BackblazeB2ListFilesResult> allFileVersionsActionResult = allFileVersionsAction.Execute();
            if (allFileVersionsActionResult.HasResult)
            {
                Facade.RetrieveProxy(ListOfFilesOnB2Proxy.Name).Data = allFileVersionsActionResult.Result.Files;
                SendNotification(FinishedCommandNotification, null, null);
            }
            else
            {
                SendNotification(FailedCommandNotification, allFileVersionsActionResult, null);
            }
        }
        #endregion
    }
}
