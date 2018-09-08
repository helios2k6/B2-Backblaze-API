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
using B2BackupUtility.Proxies.Exceptions;
using PureMVC.Patterns.Proxy;
using System;

namespace B2BackupUtility.Proxies
{
    /// <summary>
    /// A proxy for the authorization session for this program
    /// </summary>
    public sealed class AuthorizationSessionProxy : Proxy
    {
        #region private fields
        private static TimeSpan OneHour => TimeSpan.FromMinutes(60);

        private readonly Config _config;
        private BackblazeB2AuthorizationSession _authorizationSession;
        #endregion

        #region public properties
        public static string Name => "Authorization Session Proxy";

        /// <summary>
        /// Get the authorization session
        /// </summary>
        public BackblazeB2AuthorizationSession AuthorizationSession
        {
            get
            {
                if (_authorizationSession == null || _authorizationSession.SessionExpirationDate - DateTime.Now < OneHour)
                {
                    _authorizationSession = CreateNewAuthorizationSession();
                }
                return _authorizationSession;
            }
        }
        #endregion

        #region ctor
        /// <summary>
        /// Construcs a new Authorization Session Proxy
        /// </summary>
        /// <param name="config"></param>
        public AuthorizationSessionProxy(Config config) : base(Name, null)
        {
            _config = config;
        }
        #endregion

        #region public methods
        /// <summary>
        /// Initializes this authorization session
        /// </summary>
        /// <param name="applicationID">The application key ID</param>
        /// <param name="applicationKey">The application key</param>
        private BackblazeB2AuthorizationSession CreateNewAuthorizationSession()
        {
            AuthorizeAccountAction authorizeAccountAction =
                new AuthorizeAccountAction(_config.ApplicationKeyID, _config.ApplicationKey);
            BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizationSessionResult = authorizeAccountAction.Execute();
            if (authorizationSessionResult.HasErrors)
            {
                throw new AuthorizationException
                {
                    BackblazeErrorDetails = authorizationSessionResult.Errors,
                };
            }

            return authorizationSessionResult.Result;
        }
        #endregion
    }
}
