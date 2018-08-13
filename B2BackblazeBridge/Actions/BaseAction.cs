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
using B2BackblazeBridge.Exceptions;
using Functional.Maybe;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// The base class for all Actions that can be taken against B2 Backblaze
    /// </summary>
    public abstract class BaseAction<T> : IBackblazeB2Action<T>
    {
        #region private classes
        private sealed class RawHttpCallResult
        {
            public Maybe<string> SuccessResult { get; set; }

            public Maybe<string> ErrorResult { get; set; }
        }
        #endregion

        #region fields
        private readonly Random _random;

        protected readonly CancellationToken _cancellationToken;
        #endregion

        #region protected ctor
        protected BaseAction(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _random = new Random();
        }
        #endregion

        #region public methods
        public abstract BackblazeB2ActionResult<T> Execute();
        #endregion

        #region protected methods
        /// <summary>
        /// Create an HTTP Web Request with the given URL and, optionally, set the 
        /// content type to JSON
        /// </summary>
        /// <param name="apiUrl">The URL to use</param>
        /// <param name="setToJson"Whether to set the content type to JSON></param>
        /// <returns>A newly constructed HTTP Web Request</returns>
        protected HttpWebRequest GetHttpWebRequest(string apiUrl)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(apiUrl);
            webRequest.ContentType = "application/json; charset=utf-8";

            return webRequest;
        }

        /// <summary>
        /// This method sanitizes the the file path so that it can be used on B2. Here are the current set of rules:
        /// 1. Max length is 1024 characters
        /// 2. The characters must be in UTF-8
        /// 3. Backslashes are not allowed
        /// 4. DEL characters (127) are not allowed
        /// 5. File names cannot start with a "/", end with a "/", or contain "//" anywhere
        /// 6. For each segment of the file path, which is the part of the string between each "/", there can only be 
        ///    250 bytes of UTF-8 characters (for multi-byte characters, that can reduce this down to less than 250 characters)
        ///
        /// The following encodings will be used to fix file names for the given rules above:
        /// 1. An exception will be thrown for file paths above 1024 characters
        /// 2. Nothing will be done to ensure UTF-8 encoding, since all strings in C# are UTF-16
        /// 3. All backslashes will be replaced with forward slashes
        /// 4. Nothing, since file paths can't have the DEL character anyways
        /// 5. The very first "/" will be replaced with an empty string. An exception will be thrown for any file path that ends with a "/" or contains a "//"
        /// 6. An exception will be thrown if any segment is longer than 250 bytes
        /// 7. If there's a Windows style drive letter (e.g. "C:\"), this will be converted to the drive letter followed by a forward slash (e.g. "c/")
        /// 
        /// Additionally, we will remove drive letters
        /// </summary>
        /// <param name="filePath">The file path to sanitize</param>
        /// <returns>A santitized file path</returns>
        protected string GetSafeFileName(string filePath)
        {
            if (filePath.Length > 1024)
            {
                throw new InvalidOperationException("The file path cannot be longer than 1024 characters");
            }

            string updatedString = filePath;

            // Convert Windows style drive letters
            if (filePath.IndexOf(":") == 1)
            {
                char driveLetter = Char.ToLowerInvariant(filePath[0]);
                updatedString = updatedString.Substring(3);
                updatedString = updatedString.Insert(0, new string(new[] { driveLetter, '/' }));
            }

            updatedString = updatedString.Replace('\\', '/');
            if (updatedString[0] == '/')
            {
                updatedString = updatedString.Substring(1);
            }

            if (updatedString[updatedString.Length - 1] == '/' || updatedString.IndexOf("//") != -1)
            {
                throw new InvalidOperationException("The file path cannot start or end with a forward slash and cannot have double forward slashes anywhere");
            }

            string[] segments = updatedString.Split('/');
            foreach (string segment in segments)
            {
                byte[] rawBytes = Encoding.UTF8.GetBytes(segment);
                if (rawBytes.Length > 250)
                {
                    throw new InvalidOperationException("No segment of the file path may be greater than 250 bytes when encoded with UTF-8");
                }
            }

            return Uri.EscapeDataString(updatedString);
        }

        /// <summary>
        /// Computes the SHA1 hash of the given set of bytes
        /// </summary>
        /// <returns>A string representing the SHA1 hash</returns>
        protected string ComputeSHA1Hash(byte[] data)
        {
            using (SHA1 shaHash = SHA1.Create())
            {
                byte[] hashData = shaHash.ComputeHash(data, 0, data.Length);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashData)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Calculate the amount of time to suspend the current thread 
        /// </summary>
        /// <param name="attemptNumber"></param>
        /// <returns></returns>
        protected TimeSpan CalculateExponentialBackoffSleepTime(int attemptNumber)
        {
            if (attemptNumber == 1)
            {
                return TimeSpan.FromSeconds(_random.Next(5, 10));
            }

            if (attemptNumber == 2)
            {
                switch (_random.Next(0, 4)) {
                    case 0:
                        return TimeSpan.FromSeconds(10);
                    case 1:
                        return TimeSpan.FromSeconds(15);
                    case 2:
                        return TimeSpan.FromSeconds(30);
                    default:
                        return TimeSpan.FromSeconds(60);
                }
            }

            return TimeSpan.FromSeconds(_random.Next(15, 60));
        }

        /// <summary>
        /// Sends a web request with the optional payload and attempts to deserialize the result. Otherwise, it deserializes the error
        /// </summary>
        /// <typeparam name="TResult">The type of result</typeparam>
        /// <param name="webRequest">The web request</param>
        /// <param name="payload">The payload to send</param>
        /// <returns>An action result</returns>
        protected BackblazeB2ActionResult<T> SendWebRequestAndDeserialize(
            HttpWebRequest webRequest,
            byte[] payload
        )
        {
            if (webRequest == null)
            {
                throw new ArgumentNullException("webRequest");
            }

            try
            {
                if (payload != null)
                {
                    using (Stream stream = webRequest.GetRequestStream())
                    {
                        stream.Write(payload, 0, payload.Length);
                    }
                }

                using (HttpWebResponse response = webRequest.GetResponse() as HttpWebResponse)
                {
                    return new BackblazeB2ActionResult<T>(
                        HandleSuccessfulWebRequest(response)
                    );
                }
            }
            catch (WebException ex)
            {
                return new BackblazeB2ActionResult<T>(HandleErrorWebRequest(ex));
            }
            catch (Exception ex)
            {
                throw new B2ContractBrokenException(
                    "An exception occurred while attempting to talk to the B2 API",
                    ex
                );
            }
        }

        /// <summary>
        /// A overridable method that handles successful web requests to the B2 Backblaze API
        /// </summary>
        /// <param name="response">The response that was returned from the APIq</param>
        /// <returns></returns>
        protected virtual T HandleSuccessfulWebRequest(HttpWebResponse response)
        {
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
        }

        /// <summary>
        /// An overridable method that handles web requests that fail
        /// </summary>
        /// <param name="ex">The WebException that occurs during the request</param>
        /// <returns>A BackblazeB2ActionErrorDetails Request</returns>
        protected virtual BackblazeB2ActionErrorDetails HandleErrorWebRequest(WebException ex)
        {
            HttpWebResponse response = (HttpWebResponse)ex.Response;
            if (response != null)
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return JsonConvert.DeserializeObject<BackblazeB2ActionErrorDetails>(reader.ReadToEnd());
                }
            }
            else
            {
                return new BackblazeB2ActionErrorDetails
                {
                    Status = (int)ex.Status,
                    Code = "Unknown B2 Error",
                    Message = ex.Message,
                };
            }
        }
        #endregion
    }
}