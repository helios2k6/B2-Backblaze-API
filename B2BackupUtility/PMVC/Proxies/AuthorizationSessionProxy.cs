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
using PureMVC.Patterns.Proxy;

namespace B2BackupUtility.PMVC.Proxies
{
    public sealed class AuthorizationSessionProxy : Proxy
    {
        #region public properties
        public static string Name => "Authorization Session Proxy";

        /// <summary>
        /// Get the authorization session
        /// </summary>
        public BackblazeB2AuthorizationSession AuthorizationSession => Data as BackblazeB2AuthorizationSession;
        #endregion

        #region ctor
        public AuthorizationSessionProxy() : base(Name, null)
        {
        }
        #endregion

        #region public methods
        /// <summary>
        /// Initializes this authorization session
        /// </summary>
        /// <param name="applicationID">The application key ID</param>
        /// <param name="applicationKey">The application key</param>
        public BackblazeB2ActionResult<BackblazeB2AuthorizationSession> Initialize(Config config)
        {
            AuthorizeAccountAction authorizeAccountAction =
                new AuthorizeAccountAction(config.ApplicationKeyID, config.ApplicationKey);
            BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizationSessionResult = authorizeAccountAction.Execute();
            if (authorizationSessionResult.HasResult)
            {
                Data = authorizationSessionResult.Result;
            }
            return authorizationSessionResult;
        }
        #endregion
    }
}
