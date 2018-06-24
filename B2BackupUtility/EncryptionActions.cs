﻿/* 
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

using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace B2BackupUtility
{
    public static class EncryptionActions
    {
        private static readonly int ByteBufferSize = 4096; // Default page size of 4 kibibytes

        public async static Task<byte[]> EncryptFileAsync(string file, byte[] key, byte[] initializationVector)
        {
            using (Aes aesAlgorithm = Aes.Create())
            {
                // Don't use the default Key or IV
                aesAlgorithm.Key = key;
                aesAlgorithm.IV = initializationVector;

                ICryptoTransform encryptor = aesAlgorithm.CreateEncryptor(key, initializationVector);
                using (MemoryStream encryptedMemoryStream = new MemoryStream())
                using (CryptoStream cryptoStream = new CryptoStream(encryptedMemoryStream, encryptor, CryptoStreamMode.Write))
                using (BinaryWriter binaryCryptoStreamWriter = new BinaryWriter(cryptoStream))
                using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[ByteBufferSize];
                    while (true)
                    {
                        int readBytes = await fileStream.ReadAsync(buffer, 0, ByteBufferSize);
                        if (readBytes < 1)
                        {
                            break;
                        }

                        binaryCryptoStreamWriter.Write(buffer, 0, readBytes);
                    }

                    return encryptedMemoryStream.ToArray();
                }
            }
        }
    }
}
