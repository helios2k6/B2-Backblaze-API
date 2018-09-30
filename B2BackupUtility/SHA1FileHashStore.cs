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
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace B2BackupUtility
{
    public sealed class SHA1FileHashStore
    {
        #region private fields & ctor
        private static readonly Lazy<SHA1FileHashStore> _instance = new Lazy<SHA1FileHashStore>(() => new SHA1FileHashStore());

        private readonly IDictionary<string, string> _localFileToSHA1HashMap = new Dictionary<string, string>();

        private SHA1FileHashStore()
        {
        }
        #endregion

        #region public properties
        public static SHA1FileHashStore Instance => _instance.Value;
        #endregion

        #region public methods
        /// <summary>
        /// Gets the SHA1 using the path to a local file
        /// </summary>
        /// <param name="localFilePath">The path to the local file</param>
        /// <returns>A string representing the SHA1</returns>
        public string ComputeSHA1(string localFilePath)
        {
            if (File.Exists(localFilePath) == false)
            {
                throw new InvalidOperationException("File does not exist. Cannot compute SHA-1");
            }

            if (_localFileToSHA1HashMap.TryGetValue(localFilePath, out string fileSHA1) == false)
            {
                fileSHA1 = ComputeSHA1(new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                _localFileToSHA1HashMap.Add(localFilePath, fileSHA1);
            }

            return fileSHA1;
        }

        /// <summary>
        /// Computes the SHA1 of bytes in a stream
        /// </summary>
        /// <param name="byteStream">The stream to read from</param>
        /// <remarks>This function will dispose of the stream</remarks>
        /// <returns>A string representing the SHA1</returns>
        public string ComputeSHA1(Stream byteStream)
        {
            using (byteStream)
            using (SHA1 shaHash = SHA1.Create())
            {
                byte[] hashData = shaHash.ComputeHash(byteStream);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashData)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Computes the SHA1 of raw bytes
        /// </summary>
        /// <param name="rawBytes">The raw bytes to read from</param>
        /// <returns>A string representing the SHA1</returns>
        public string ComputeSHA1Hash(byte[] rawBytes)
        {
            return ComputeSHA1(new MemoryStream(rawBytes));
        }
        #endregion
    }
}