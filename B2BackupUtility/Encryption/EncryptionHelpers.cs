/* 
 * Copyright (c) 2023 Andrew Johnson
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
using System.IO;
using System.Security.Cryptography;

namespace B2BackupUtility.Encryption
{
    public static class EncryptionHelpers
    {
        private static int DefaultBufferSize => 104857600;

        /// <summary>
        /// Encryptes a series of bytes using the encryption key and IV in the Config
        /// </summary>
        /// <param name="bytes">The bytes to encrypt</param>
        /// <param name="encryptionKey">The secret key</param>
        /// <param name="initializationVector">The initialization vector</param>
        /// <returns>The encrypted bytes</returns>
        public static byte[] EncryptBytes(byte[] bytes, string encryptionKey, string initializationVector)
        {
            using Aes aesAlg = Aes.Create();
            aesAlg.Key = Convert.FromBase64String(encryptionKey);
            aesAlg.IV = Convert.FromBase64String(initializationVector);

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msEncrypt = new MemoryStream();
            using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            // It's important we dispose of the BinaryWriter before attempting to read from the 
            // memory stream with the encrypted bytes
            using (BinaryWriter swEncrypt = new BinaryWriter(csEncrypt))
            {
                //Write all data to the stream.
                swEncrypt.Write(bytes);
            }
            return msEncrypt.ToArray();
        }

        /// <summary>
        /// Decryptes a series of bytes using the encryption key and IV in the Config
        /// </summary>
        /// <param name="bytes">The bytes to decrypt</param>
        /// <returns>The decrypted bytes</returns>
        public static byte[] DecryptBytes(byte[] bytes, string encryptionKey, string initializationVector)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Convert.FromBase64String(encryptionKey);
                aesAlg.IV = Convert.FromBase64String(initializationVector);

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using MemoryStream msDecrypt = new MemoryStream(bytes);
                using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using BinaryReader srDecrypt = new BinaryReader(csDecrypt);
                byte[] buffer = new byte[DefaultBufferSize];
                List<byte> returnValues = new List<byte>();
                while (true)
                {
                    int bytesRead = srDecrypt.Read(buffer, 0, DefaultBufferSize);
                    if (bytesRead < 1)
                    {
                        break;
                    }

                    for (int i = 0; i < bytesRead; i++)
                    {
                        returnValues.Add(buffer[i]);
                    }
                }

                return returnValues.ToArray();
            }
        }
    }
}
