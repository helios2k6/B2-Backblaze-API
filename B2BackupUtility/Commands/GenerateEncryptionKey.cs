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

using B2BackupUtility.Utils;
using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using System;
using System.Security.Cryptography;

namespace B2BackupUtility.Commands
{
    /// <summary>
    /// Generates and prints a new encryption key and IV
    /// </summary>
    public sealed class GenerateEncryptionKey : SimpleCommand, ILogNotifier
    {
        #region public properties
        public static string CommandNotification => "Generate Encryption Key";

        public static string CommandSwitch => "--generate-encryption-key";

        public static CommandType CommandType => CommandType.GENERATE_ENCRYPTION_KEY;
        #endregion

        #region public methods
        public override void Execute(INotification notification)
        {
            this.Debug(CommandNotification);
            using (Aes aes = Aes.Create())
            {
                byte[] key = aes.Key;
                byte[] iv = aes.IV;

                string keyAsString = Convert.ToBase64String(key);
                string ivAsString = Convert.ToBase64String(iv);

                // Send out notififcations that will be printed later
                this.Critical($"Encryption key: {keyAsString}");
                this.Critical($"Initialization Vector: {ivAsString}");
            }
        }
        #endregion
    }
}
