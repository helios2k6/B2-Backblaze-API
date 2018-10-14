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

using System;
using System.Threading;

namespace B2BackupUtility.MemoryManagement
{
    /// <summary>
    /// Represents an object that manages a memory govener
    /// </summary>
    public sealed class SharedResourceMemoryGovernor : IDisposable
    {
        private readonly long _memoryCapacity;
        private readonly ManualResetEventSlim _signal;
        private readonly object _lockObject;

        private bool _isDisposed;
        private long _currentlyAvailableMemory;

        /// <summary>
        /// Construct a new Shared Resource Memory Generator with the max memory allocation
        /// </summary>
        /// <param name="memoryCapacity">The memory capacity of this governor</param>
        public SharedResourceMemoryGovernor(long memoryCapacity)
        {
            _lockObject = new object();
            _signal = new ManualResetEventSlim();
            _memoryCapacity = memoryCapacity;
            _currentlyAvailableMemory = memoryCapacity;
            _isDisposed = false;
        }

        /// <summary>
        /// The number of bytes to allocate
        /// </summary>
        /// <param name="bytesToAllocate">The mumber of bytes to allocate</param>
        /// <param name="cancellationToken">The cancellation token to listen on</param>
        public void AllocateMemory(long bytesToAllocate, CancellationToken cancellationToken)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("This object is disposed");
            }

            if (bytesToAllocate > _memoryCapacity)
            {
                throw new InvalidOperationException("Trying to take more tickets than this ticket counter has");
            }

            if (bytesToAllocate < 0)
            {
                throw new ArgumentException("You must provide a positive number for the number of tickets");
            }

            while (true)
            {
                lock (_lockObject)
                {
                    // Scenario 1: We have enough tickets
                    if (_currentlyAvailableMemory > bytesToAllocate)
                    {
                        _currentlyAvailableMemory -= bytesToAllocate;
                        return;
                    }

                    _signal.Reset();
                }

                // Scenario 2: We do not have enough tickets and we need to wait
                _signal.Wait(cancellationToken);
            }
        }

        /// <summary>
        /// Frees a specified number of bytes
        /// </summary>
        /// <param name="bytesToFree">The number of bytes to free</param>
        public void FreeMemory(long bytesToFree)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("This object is disposed");
            }

            if (bytesToFree < 0)
            {
                throw new ArgumentException("You must provide a positive number for the number of tickets");
            }

            lock (_lockObject)
            {
                _currentlyAvailableMemory += bytesToFree;

                _signal.Set();
            }
        }

        /// <summary>
        /// Dispose of this object
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _signal.Dispose();
        }
    }
}