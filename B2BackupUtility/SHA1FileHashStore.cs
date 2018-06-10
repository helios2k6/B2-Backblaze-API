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
        public static SHA1FileHashStore Instance
        {
            get { return _instance.Value; }
        }
        #endregion

        #region public methods
        public string GetFileHash(string localFilePath)
        {
            string fileSHA1;
            if (_localFileToSHA1HashMap.TryGetValue(localFilePath, out fileSHA1) == false)
            {
                fileSHA1 = ComputeSHA1Hash(localFilePath);
                _localFileToSHA1HashMap.Add(localFilePath, fileSHA1);
            }

            return fileSHA1;
        }
        #endregion
        #region private methods
        private string ComputeSHA1Hash(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                throw new InvalidOperationException("File does not exist. Cannot compute SHA-1");
            }

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (SHA1 shaHash = SHA1.Create())
            {
                byte[] hashData = shaHash.ComputeHash(stream);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashData)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
        #endregion
    }
}