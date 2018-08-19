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
using System;
using System.Collections.Generic;
using System.Linq;

namespace B2BackupUtility.Actions
{
    /// <summary>
    /// The base class that represents all actions
    /// </summary>
    public abstract class BaseAction
    {
        #region private fields
        private readonly IEnumerable<string> _rawArgs;
        private BackblazeB2AuthorizationSession _authorizationSession;

        private static TimeSpan OneHour => TimeSpan.FromMinutes(60);
        #endregion

        #region protected properties
        /// <summary>
        /// Get the account ID
        /// </summary>
        protected string AccountID => GetArgumentOrThrow(AccountIDOption);

        /// <summary>
        /// Get the application key
        /// </summary>
        protected string ApplicationKey => GetArgumentOrThrow(ApplicationKey);

        /// <summary>
        /// Get the bucket ID
        /// </summary>
        protected string BucketID => GetArgumentOrThrow(BucketIDOption);
        #endregion

        #region public properties
        /// <summary>
        /// The option switch for the account ID
        /// </summary>
        public string AccountIDOption => "--account-id";

        /// <summary>
        /// The option switch for the application key
        /// </summary>
        public string ApplicationKeyOption => "--application-key";

        /// <summary>
        /// The option for the bucket ID
        /// </summary>
        public string BucketIDOption => "--bucket-id";

        /// <summary>
        /// The name of this action--this is displayed in the Help center
        /// </summary>
        public abstract string ActionName { get; }

        /// <summary>
        /// The command-line switch that's used to select this action
        /// </summary>
        public abstract string ActionSwitch { get; }

        /// <summary>
        /// The different command-line help-options this class offers
        /// </summary>
        public abstract IEnumerable<string> ActionOptions { get; }
        #endregion

        #region public methods
        /// <summary>
        /// Execute this action
        /// </summary>
        public abstract void ExecuteAction();
        #endregion

        #region ctor
        public BaseAction(IEnumerable<string> rawArgs)
        {
            _rawArgs = rawArgs;
        }
        #endregion

        #region protected methods
        protected BackblazeB2AuthorizationSession GetOrCreateAuthorizationSession()
        {
            if (_authorizationSession == null || _authorizationSession.SessionExpirationDate - DateTime.Now < OneHour)
            {
                AuthorizeAccountAction authorizeAccountAction = new AuthorizeAccountAction(AccountID, ApplicationKey);
                BackblazeB2ActionResult<BackblazeB2AuthorizationSession> authorizationSessionResult = authorizeAccountAction.Execute();
                if (authorizationSessionResult.HasErrors)
                {
                    string errorMessage = authorizationSessionResult.Errors.First().Message;
                    throw new InvalidOperationException($"Could not authorize the account with Account ID: ${AccountID} and Application Key: ${ApplicationKey}. ${errorMessage}");
                }
                _authorizationSession = authorizationSessionResult.Result;
            }

            return _authorizationSession;
        }

        /// <summary>
        /// Checks to see if an option exists
        /// </summary>
        /// <param name="option">Gets whether an argument exists</param>
        /// <returns>True if an argument exists. False otherwise</returns>
        protected bool DoesOptionExist(string option)
        {
            return TryGetArgument(option, out string _);
        }

        /// <summary>
        /// Attempts to get the value for the specified option. If it doesn't exist, an
        /// exception is thrown
        /// </summary>
        /// <param name="option">The option to retrieve the value for</param>
        /// <returns>The value to the option</returns>
        protected string GetArgumentOrThrow(string option)
        {
            if (TryGetArgument(option, out string value))
            {
                return value;
            }

            throw new InvalidOperationException($"Was not able to retrieve value for option {option}");
        }

        /// <summary>
        /// This will attempt to get the value of an argument that is passed in. This cannot
        /// get multiple arguments passed in to a single options
        /// </summary>
        /// <param name="option">The option to get arguments for</param>
        /// <param name="value">The value found</param>
        /// <returns>True if an argument was found. False otherwise</returns>
        protected bool TryGetArgument(string option, out string value)
        {
            bool returnNextItem = false;
            foreach (string arg in _rawArgs)
            {
                if (returnNextItem)
                {
                    value = arg;
                    return true;
                }

                if (arg.Equals(option, StringComparison.OrdinalIgnoreCase))
                {
                    returnNextItem = true;
                }
            }

            value = null;
            return false;
        }
        #endregion
    }
}
