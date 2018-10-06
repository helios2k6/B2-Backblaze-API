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
using System;

namespace B2BackupUtility.UploadManagers
{
    internal sealed class UploadManagerAuthorizationSessionTicket : IDisposable
    {
        #region private fields
        private bool _isDisposed;

        private readonly UploadManagerAuthorizationSessionTicketCounter _ticketCounter;
        private readonly string _ticketID;
        #endregion

        #region public properties
        /// <summary>
        /// This ticket's authorization session
        /// </summary>
        public BackblazeB2AuthorizationSession AuthorizationSession { get; private set; }
        #endregion

        #region ctor
        public UploadManagerAuthorizationSessionTicket(
            BackblazeB2AuthorizationSession authorizationSession,
            UploadManagerAuthorizationSessionTicketCounter ticketCounter,
            string ticketID
        )
        {
            AuthorizationSession = authorizationSession;
            _ticketCounter = ticketCounter;
            _ticketID = ticketID;
        }
        #endregion

        #region public methods
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            if (_ticketCounter.IsDisposed)
            {
                return;
            }

            _ticketCounter.ReturnTicket(_ticketID);
        }
        #endregion
    }
}
