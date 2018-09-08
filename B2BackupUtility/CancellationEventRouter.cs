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

namespace B2BackupUtility
{
    /// <summary>
    /// Provides a static central routing point for all cancellation actions
    /// </summary>
    public static class CancellationEventRouter
    {
        public static string CancellationEvent => "Cancellation Event";

        public static string ImmediateCancellationEvent => "Immediate Cancellation Event";

        private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        private static int CancellationRequestCounter = 0;

        public static void HandleCancel(object sender, ConsoleCancelEventArgs e)
        {
            int incrementedValue = Interlocked.Increment(ref CancellationRequestCounter);
            if (incrementedValue == 1)
            {
                // Prevent the console from shutting down the first time
                e.Cancel = true;
                PureMVC.Patterns.Facade.Facade.GetInstance(() => new ApplicationFacade()).SendNotification(CancellationEvent, null, null);
            }
            else
            {
                PureMVC.Patterns.Facade.Facade.GetInstance(() => new ApplicationFacade()).SendNotification(ImmediateCancellationEvent, null, null);
            }
            
            CancellationTokenSource.Cancel();
        }

        public static CancellationToken GlobalCancellationToken
        {
            get { return CancellationTokenSource.Token; }
        }
    }
}