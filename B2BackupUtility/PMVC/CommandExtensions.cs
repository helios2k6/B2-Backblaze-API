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

using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;

namespace B2BackupUtility.PMVC
{
    public static class CommandExtensions
    {
        /// <summary>
        /// Checks to see if the notification matches the expected one
        /// </summary>
        /// <param name="this">This command</param>
        /// <param name="expectedNotification">The expected notification name</param>
        /// <param name="notification">The notification itself</param>
        public static void ThrowIfNotificationDoesNotMatch(
            this ICommand @this,
            string expectedNotification,
            INotification notification
        )
        {
            if (string.Equals(expectedNotification, notification.Name, StringComparison.Ordinal) == false)
            {
                throw new InvalidOperationException("Unexpected notification reached this Command");
            }
        }

        /// <summary>
        /// Automatically casts the body as the expected type. This throws otherwise
        /// </summary>
        /// <typeparam name="T">The type of the body</typeparam>
        /// <param name="this">This command</param>
        /// <param name="notification">The notification</param>
        /// <returns>The returned body casted to the expected type</returns>
        public static T GetBody<T>(this ICommand @this, INotification notification)
        {
            return (T)notification.Body;
        }
    }
}
