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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using B2BackblazeBridge.Core;
using B2BackblazeBridge.Processing;
using Newtonsoft.Json;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents an action to list all of the files within a Bucket on B2
    /// </summary>
    public sealed class ListFilesAction : BaseAction<BackblazeB2ListFilesResult>
    {
        #region private inner classes
        private enum ListFileMethod
        {
            FILE_VERSIONS,
            FILE_NAMES,
        }
        #endregion

        #region private fields
        private const string ListFileVersionsAPIURL = "/b2api/v1/b2_list_file_versions";

        private const string ListFileNamesAPIURL = "/b2api/v1/b2_list_file_names";

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly ListFileMethod _method;
        private readonly bool _shouldFetchAllFiles;
        #endregion

        #region ctor
        private ListFilesAction(BackblazeB2AuthorizationSession authorizationSession, string bucketID, ListFileMethod method, bool shouldFetchAllFiles)
        {
            _authorizationSession = authorizationSession;
            _bucketID = bucketID;
            _method = method;
            _shouldFetchAllFiles = shouldFetchAllFiles;
        }
        #endregion

        #region public methods
        public static ListFilesAction CreateListFileActionForFileVersions(BackblazeB2AuthorizationSession authorizationSession, string bucketID, bool shouldFetchAllFiles)
        {
            return new ListFilesAction(authorizationSession, bucketID, ListFileMethod.FILE_VERSIONS, shouldFetchAllFiles);
        }

        public static ListFilesAction CreateListFileActionForFileNames(BackblazeB2AuthorizationSession authorizationSession, string bucketID, bool shouldFetchAllFiles)
        {
            return new ListFilesAction(authorizationSession, bucketID, ListFileMethod.FILE_NAMES, shouldFetchAllFiles);
        }

        public async override Task<BackblazeB2ActionResult<BackblazeB2ListFilesResult>> ExecuteAsync()
        {
            switch (_method)
            {
                case ListFileMethod.FILE_VERSIONS:
                    return await GetFileListAsync(ListFileVersionsAPIURL);
                case ListFileMethod.FILE_NAMES:
                    return await GetFileListAsync(ListFileNamesAPIURL);
            }

            throw new InvalidOperationException("Invalid List File Method was pass in");
        }
        #endregion

        #region private methods
        private async Task<BackblazeB2ActionResult<BackblazeB2ListFilesResult>> GetFileListAsync(string url)
        {
            string startFileName = null;
            BackblazeB2ActionResult<BackblazeB2ListFilesResult> currentResult = null;
            IEnumerable<BackblazeB2ListFilesResult.FileResult> fileResults = Enumerable.Empty<BackblazeB2ListFilesResult.FileResult>();
            do
            {
                currentResult = await ExecuteWebRequestImpl(url, startFileName);
                if (currentResult.HasErrors)
                {
                    // If there was an error, just return immediately
                    return currentResult;
                }

                startFileName = currentResult.Result.NextFileName;
                fileResults = fileResults.Concat(currentResult.Result.Files);
            } while (_shouldFetchAllFiles && startFileName != null);
            return new BackblazeB2ActionResult<BackblazeB2ListFilesResult>(new BackblazeB2ListFilesResult
            {
                Files = fileResults.ToArray(),
                NextFileID = currentResult.Result.NextFileID,
                NextFileName = currentResult.Result.NextFileName,
            });
        }

        private async Task<BackblazeB2ActionResult<BackblazeB2ListFilesResult>> ExecuteWebRequestImpl(string url, string startFileName)
        {
            ListFileNamesRequest request = new ListFileNamesRequest
            {
                BucketID = _bucketID,
                StartFileName = startFileName,
            };
            string body = JsonConvert.SerializeObject(request);
            byte[] payload = Encoding.UTF8.GetBytes(body);

            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + url);
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
            webRequest.ContentLength = payload.Length;

            return await SendWebRequestAndDeserializeAsync<BackblazeB2ListFilesResult>(webRequest, payload);
        }
        #endregion
    }
}