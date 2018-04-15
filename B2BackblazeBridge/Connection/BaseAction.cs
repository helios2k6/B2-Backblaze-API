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
using System.Net;
using System.Threading.Tasks;
using B2BackblazeBridge.Connection;

public abstract class BaseAction<T> : IBackblazeB2Action<T>
{
    #region public methods
    public abstract Task<T> ExecuteAsync();
    #endregion

    #region protected methods
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
    /// </summary>
    /// <param name="filePath">The file path to sanitize</param>
    /// <returns>A santitized file path</returns>
    protected string GetSafeFileName(string filePath)
    {
        throw new NotImplementedException();
    }
    #endregion
}