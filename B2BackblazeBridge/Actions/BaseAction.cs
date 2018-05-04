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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static B2BackblazeBridge.Actions.BaseActionWebRequestException;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// The base class for all Actions that can be taken against B2 Backblaze
    /// </summary>
    public abstract class BaseAction<T> : IBackblazeB2Action<T>
    {
        #region private fields
        private static readonly int TicksPerMicrosecond = 10;

        private readonly Random _random = new Random();
        #endregion

        #region public methods
        public abstract Task<T> ExecuteAsync();
        #endregion

        #region protected methods
        /// <summary>
        /// Create an HTTP Web Request with the given URL and, optionally, set the 
        /// content type to JSON
        /// </summary>
        /// <param name="apiUrl">The URL to use</param>
        /// <param name="setToJson"Whether to set the content type to JSON></param>
        /// <returns>A newly constructed HTTP Web Request</returns>
        protected HttpWebRequest GetHttpWebRequest(string apiUrl, bool setToJson)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(apiUrl);
            if (setToJson)
            {
                webRequest.ContentType = "application/json; charset=utf-8";
            }

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
            // Drive letters cannot be more than 2 letters, so this is always true
            if (filePath.IndexOf(":") == 1)
            {
                updatedString = updatedString.Substring(2);
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
        /// Sends an HTTP request with no payload
        /// </summary>
        /// <param name="webRequest">The web request</param>
        /// <returns>A decoded JSON response</returns>
        protected async Task<Dictionary<string, dynamic>> SendWebRequestAsync(HttpWebRequest webRequest)
        {
            using (HttpWebResponse response = await webRequest.GetResponseAsync() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    await HandleNonHttp200ErrorCodeAsync(response);
                }
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    string jsonResponse = await streamReader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonResponse);
                }
            }
        }

        /// <summary>
        /// Sends an HTTP request with the given payload
        /// </summary>
        /// <param name="webRequest"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        protected async Task<Dictionary<string, dynamic>> SendWebRequestAsync(HttpWebRequest webRequest, byte[] payload)
        {
            try
            {
                using (Stream stream = await webRequest.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(payload, 0, payload.Length);
                }

                using (HttpWebResponse response = await webRequest.GetResponseAsync() as HttpWebResponse)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        await HandleNonHttp200ErrorCodeAsync(response);
                    }
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string responseJson = await reader.ReadToEndAsync();
                        return JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(responseJson);
                    }
                }
            }
            catch (WebException ex)
            {
                await HandleNonHttp200ErrorCodeAsync(ex.Response as HttpWebResponse);
            }

            throw new InvalidOperationException("Impossible code path reached. This should never happen");
        }

        private async Task HandleNonHttp200ErrorCodeAsync(HttpWebResponse response)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                // Handle the case when we actually have an OK code
                return;
            }

            // Decode JSON error structure and rethrow
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                string responseJson = await reader.ReadToEndAsync();
                ErrorDetails errorDetails = JsonConvert.DeserializeObject<ErrorDetails>(responseJson);
                throw new BaseActionWebRequestException(response.StatusCode, errorDetails);
            }
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
                return new TimeSpan(_random.Next(0, 52) * TicksPerMicrosecond);
            }

            if (attemptNumber == 2)
            {
                int timeFrameSelection = _random.Next(0, 4);

                if (timeFrameSelection == 0)
                {
                    return new TimeSpan(0);
                }

                if (timeFrameSelection == 1)
                {
                    return new TimeSpan(52 * TicksPerMicrosecond);
                }

                if (timeFrameSelection == 2)
                {
                    return new TimeSpan(103 * TicksPerMicrosecond);
                }

                return new TimeSpan(154 * TicksPerMicrosecond);
            }

            return new TimeSpan(_random.Next(0, int.MaxValue) * TicksPerMicrosecond);
        }
        #endregion
    }
}