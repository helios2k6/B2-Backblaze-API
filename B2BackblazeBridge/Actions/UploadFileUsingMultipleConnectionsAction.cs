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
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    public sealed class UploadFileUsingMultipleConnectionsActions : BaseAction<BackblazeB2UploadFileResult>
    {
        #region inner classes
        private sealed class GetUploadPartURLResult
        {
            public string AuthorizationToken { get; set; }

            public string UploadURL { get; set; }
        }

        private sealed class UploadPartJob
        {
            public long ContentLength { get; set; }

            public long FileCursorPosition { get; set; }

            public int FilePartNumber { get; set; }

            public string SHA1 { get; set; }
        }
        #endregion

        #region private fields
        private readonly BackblazeB2AuthorizationSession _authorizationSession;

        private readonly string _filePath;

        private readonly int _numberOfConnections;
        #endregion

        #region ctor
        public UploadFileUsingMultipleConnectionsActions(
            BackblazeB2AuthorizationSession authorizationSession,
            string filePath,
            int numberOfConnections
        )
        {
            if (numberOfConnections < 1)
            {
                throw new ArgumentException("You must specify a positive, non-zero number of connections", "numberOfConnections");
            }
            _authorizationSession = authorizationSession;
            _filePath = filePath;
            _numberOfConnections = numberOfConnections;
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2UploadFileResult> ExecuteAsync()
        {
            
        }
        #endregion

        #region private methods
        private IEnumerable<UploadPartJob> GenerateUploadParts()
        {
            FileInfo fileInfo = new FileInfo(_filePath);
            long fileChunkLengths = fileInfo.Length / _numberOfConnections;
            long fileChunkLengthOfLastItem = fileInfo.Length % _numberOfConnections;
            for (int i = 0; i < _numberOfConnections; i++)
            {
                
            }
        }

        private async Task<long> GetFileID()
        {
            throw new NotImplementedException();
        }

        private async Task<GetUploadPartURLResult> GetUploadPartURL()
        {
            throw new NotImplementedException();
        }

        private async Task<bool> UploadFilePart()
        {
            throw new NotImplementedException();
        }

        private async Task<BackblazeB2UploadFileResult> FinishUploadingLargeFile()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}