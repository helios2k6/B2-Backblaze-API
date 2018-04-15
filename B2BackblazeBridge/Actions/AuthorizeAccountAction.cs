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

using B2BackblazeBridge.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    public sealed class AuthorizeAccountAction : BaseAction<BackblazeB2AuthorizationSession>
    {
        #region private fields
        private readonly string _acccountID;
        private readonly string _applicationKey;

        private static readonly string APIURL = "https://api.backblazeb2.com/b2api/v1/b2_authorize_account";
        #endregion

        #region ctor
        public AuthorizeAccountAction(string accountID, string applicationKey) : base()
        {
            _acccountID = accountID;
            _applicationKey = applicationKey;
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2AuthorizationSession> ExecuteAsync()
        {
            HttpWebRequest webRequest = GetHttpWebRequest(APIURL);
            string credentialsHeader = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(_acccountID + ":" + _applicationKey)
            );
            webRequest.Headers.Add("Authorization", "Basic " + credentialsHeader);
            using (HttpWebResponse response = await webRequest.GetResponseAsync() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new AuthorizeAccountActionException(response.StatusCode);
                }
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    return DecodePayload(await streamReader.ReadToEndAsync());
                }
            }
        }
        #endregion

        #region private methods
        private BackblazeB2AuthorizationSession DecodePayload(string jsonPayload)
        {
            Dictionary<string, dynamic> decodedJsonPayload = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonPayload);

            long absoluteMinimumPartSize = decodedJsonPayload["absoluteMinimumPartSize"];
            string apiUrl = decodedJsonPayload["apiUrl"];
            string authorizationToken = decodedJsonPayload["authorizationToken"];
            string downloadUrl = decodedJsonPayload["downloadUrl"];
            long recommendedPartSize = decodedJsonPayload["recommendedPartSize"];

            return new BackblazeB2AuthorizationSession(
                absoluteMinimumPartSize,
                _acccountID,
                apiUrl,
                _applicationKey,
                authorizationToken,
                downloadUrl,
                recommendedPartSize,
                DateTime.Now.AddDays(1.0)
            );
        }
        #endregion
    }
}
