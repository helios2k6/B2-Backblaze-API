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
using B2BackupUtility.PMVC.Proxies;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;

namespace B2BackupUtility.PMVC.Commands
{
    /// <summary>
    /// Initializes the authorization session
    /// </summary>
    public sealed class InitializeAuthorizationSession : SimpleCommand
    {
        #region private static properties
        private static TimeSpan OneHour => TimeSpan.FromMinutes(60);
        #endregion

        #region public properties
        public static string CommandNotification => "Initialize Authorization Session";

        public static string FailedCommandNotification => "Failed To Authorize Session";

        public static string FinishedCommandNotification => "Finished Initializing Authorization Session";
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            // Check the current authorization session
            AuthorizationSessionProxy authorizationProxy = (AuthorizationSessionProxy)Facade.RetrieveProxy(AuthorizationSessionProxy.Name);
            if (authorizationProxy.AuthorizationSession == null || authorizationProxy.AuthorizationSession.SessionExpirationDate - DateTime.Now < OneHour)
            {
                ConfigProxy configProxy = (ConfigProxy)Facade.RetrieveProxy(ConfigProxy.Name);
                BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizationSessionResult =  authorizationProxy.Initialize(configProxy.Config);
                if (authorizationSessionResult.HasErrors)
                {
                    SendNotification(FailedCommandNotification, authorizationSessionResult, null);
                    return;
                }

                authorizationProxy.Data = authorizationSessionResult.Result;
            }

            SendNotification(FinishedCommandNotification, null, null);
        }
        #endregion
    }
}
