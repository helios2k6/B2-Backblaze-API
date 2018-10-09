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
using System.Collections.Generic;
using System.Collections.Immutable;

namespace B2BackupUtility.MemoryManagement
{
    /// <summary>
    /// A ledger that tracks individual service level memory allocations and reservations
    /// </summary>
    public sealed class MultinodeMemoryManagementSystem : IDisposable
    {
        private static Lazy<MultinodeMemoryManagementSystem> SingletonInstanceLazy = new Lazy<MultinodeMemoryManagementSystem>(() => new MultinodeMemoryManagementSystem());

        private bool _isDisposed;
        private ImmutableDictionary<string, SharedResourceMemoryGovernor> _memoryCapacityLedger;

        /// <summary>
        /// The singleton instance of this multinode memory managment system
        /// </summary>
        public static MultinodeMemoryManagementSystem Instance => SingletonInstanceLazy.Value;

        private MultinodeMemoryManagementSystem()
        {
            _isDisposed = false;
            _memoryCapacityLedger = ImmutableDictionary<string, SharedResourceMemoryGovernor>.Empty;
        }

        /// <summary>
        /// Adds a service to the memory ledger that will be used to govern its memory usage
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="byteCapacity">The number of bytes to reserve</param>
        public void AddService(string serviceName, long byteCapacity)
        {
            if (_memoryCapacityLedger.ContainsKey(serviceName))
            {
                throw new InvalidOperationException("Cannot add a service twice!");
            }

            _memoryCapacityLedger = _memoryCapacityLedger.Add(serviceName, new SharedResourceMemoryGovernor(byteCapacity));
        }

        /// <summary>
        /// Retrieves the memory govenernor using the service name
        /// </summary>
        /// <param name="serviceName">The service name to </param>
        /// <returns>The memory governor. Throws if it does not exist</returns>
        public SharedResourceMemoryGovernor GetMemoryGovernor(string serviceName)
        {
            return _memoryCapacityLedger[serviceName];
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // Dispose of all of the memory governors
            foreach (KeyValuePair<string, SharedResourceMemoryGovernor> memoryLedgerEntries in _memoryCapacityLedger)
            {
                memoryLedgerEntries.Value.Dispose();
            }
        }
    }
}