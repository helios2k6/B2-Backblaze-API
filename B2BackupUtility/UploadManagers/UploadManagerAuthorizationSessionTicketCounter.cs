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
using System.Collections.Generic;
using System.Threading;

namespace B2BackupUtility.UploadManagers
{
    /// <summary>
    /// Manages the handing out of authorization sessions
    /// </summary>
    internal sealed class UploadManagerAuthorizationSessionTicketCounter : IDisposable
    {
        #region private fields
        private BackblazeB2AuthorizationSession _currentAuthorizationSession;
        private readonly HashSet<string> _outstandingTicketIDs;
        private readonly ManualResetEventSlim _manualResetEvent;
        private readonly Func<BackblazeB2AuthorizationSession> _authorizationSessionGenerator;
        private readonly object _lockObject;
        #endregion

        #region public properties
        public bool IsDisposed { get; private set; }
        #endregion

        #region ctor
        public UploadManagerAuthorizationSessionTicketCounter(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator
        )
        {
            _authorizationSessionGenerator = authorizationSessionGenerator;
            IsDisposed = false;
            _outstandingTicketIDs = new HashSet<string>();
            _manualResetEvent = new ManualResetEventSlim();
            _currentAuthorizationSession = null;
            _lockObject = new object();
        }
        #endregion

        #region public methods
        public UploadManagerAuthorizationSessionTicket GetAuthorizationSessionTicket()
        {
            // This will require us to wait, probably multiple times, so we need to loop until we get something
            while (true)
            {
                CancellationEventRouter.GlobalCancellationToken.ThrowIfCancellationRequested();
                lock (_lockObject)
                {
                    BackblazeB2AuthorizationSession newAuthorizationSession = _authorizationSessionGenerator();
                    string ticketID = Guid.NewGuid().ToString();

                    // Scenario 1: Nothing has changed with the authorization session. Return this authorization session and
                    // increase the current outstanding ticket count
                    if (newAuthorizationSession.Equals(_currentAuthorizationSession))
                    {
                        _outstandingTicketIDs.Add(ticketID);
                        return new UploadManagerAuthorizationSessionTicket(newAuthorizationSession, this, ticketID);
                    }

                    // Scenario 2: Nobody else is using the old authorization session. Go ahead and set it and return it
                    if (_outstandingTicketIDs.Count == 0)
                    {
                        _currentAuthorizationSession = newAuthorizationSession;
                        _outstandingTicketIDs.Add(ticketID);
                        return new UploadManagerAuthorizationSessionTicket(newAuthorizationSession, this, ticketID);
                    }

                    // Scenario 3: We need to wait until everyone returns their tickets. 
                    // Reset this so that we know when to wait up
                    _manualResetEvent.Reset();
                }

                // Exit the lock and wait until someone has signaled to us 
                _manualResetEvent.Wait();
            }
        }

        public void ReturnTicket(string ticketID)
        {
            lock (_lockObject)
            {
                // Return the ticket
                if (_outstandingTicketIDs.Remove(ticketID) == false)
                {
                    throw new InvalidOperationException("Tried to remove unknown ticket ID");
                }

                // Alert other threads that they should check for a new authorization session
                _manualResetEvent.Set();
            }
        }

        #region IDisposable Support
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }
            IsDisposed = true;

            _manualResetEvent.Dispose();
        }
        #endregion
        #endregion
    }
}
