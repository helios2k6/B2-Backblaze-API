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

using B2BackblazeBridge.Actions.InternalActions;
using B2BackblazeBridge.Core;
using B2BackblazeBridge.Processing;
using Functional.Maybe;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents uploading a single file using multiple connections in parallel
    /// </summary>
    public sealed class UploadWithMultipleConnectionsAction : BaseAction<BackblazeB2UploadMultipartFileResult>, IDisposable
    {
        #region private fields
        private static readonly string FinishLargeFileURL = "/b2api/v1/b2_finish_large_file";
        private static readonly int MinimumFileChunkSize = 1048576; // 1 mebibyte
        private static readonly int MaxMemoryAllowed = 268435456; // 258 mebibytes

        private readonly BackblazeB2AuthorizationSession _authorizationSession;
        private readonly string _bucketID;
        private readonly string _remoteFilePath;
        private readonly int _numberOfConnections;
        private readonly int _fileChunkSizesInBytes;
        private readonly int _maxUploadAttempts;
        private readonly Stream _dataStream;
        private readonly BlockingCollection<ProducerUploadJob> _jobStream;
        private readonly Action<TimeSpan> _exponentialBackoffCallback;

        private bool disposedValue = false;
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new UploadFileUsingMultipleConnectionsActions
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="dataStream">The stream to read from when uploading</param>
        /// <param name="remoteFilePath">The remote path you want to upload to</param>
        /// <param name="bucketID">The B2 bucket you want to upload to</param>
        /// <param name="fileChunkSizesInBytes">The size (in bytes) of the file chunks you want to use when uploading</param>
        /// <param name="numberOfConnections">The number of connections to use when uploading</param>
        /// <param name="maxUploadAttempts">The maximum number of times to attempt to upload a file chunk</param>
        /// <param name="cancellationToken">The cancellation token to pass in when this upload needs to be cancelled</param>
        /// <param name="exponentialBackoffCallback">A callback to invoke when this upload uses exponential backoff</param>
        public UploadWithMultipleConnectionsAction(
            BackblazeB2AuthorizationSession authorizationSession,
            Stream dataStream,
            string remoteFilePath,
            string bucketID,
            int fileChunkSizesInBytes,
            int numberOfConnections,
            int maxUploadAttempts,
            CancellationToken cancellationToken,
            Action<TimeSpan> exponentialBackoffCallback
        ) : base(cancellationToken)
        {
            if (fileChunkSizesInBytes < MinimumFileChunkSize)
            {
                throw new ArgumentException("The file chunk sizes must be larger than 1 mebibyte");
            }

            if (numberOfConnections < 1)
            {
                throw new ArgumentException("You must specify a positive, non-zero number of connections", "numberOfConnections");
            }

            ValidateRawPath(remoteFilePath);

            _authorizationSession = authorizationSession ?? throw new ArgumentNullException("The authorization session object must not be mull");
            _bucketID = bucketID;
            _dataStream = dataStream;
            _remoteFilePath = remoteFilePath;
            _fileChunkSizesInBytes = fileChunkSizesInBytes;
            _numberOfConnections = numberOfConnections;
            _maxUploadAttempts = maxUploadAttempts;
            _jobStream = new BlockingCollection<ProducerUploadJob>(MaxMemoryAllowed / _fileChunkSizesInBytes);
            _exponentialBackoffCallback = exponentialBackoffCallback;
        }
        #endregion

        #region public methods
        public override BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> Execute()
        {
            BackblazeB2ActionResult<StartLargeFileResponse> fileIDResponse = new StartLargeFileAction(
                _authorizationSession,
                _bucketID,
                _remoteFilePath
            ).Execute();
            if (fileIDResponse.HasErrors)
            {
                return new BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult>(
                    fileIDResponse.Errors
                );
            }

            string fileID = fileIDResponse.MaybeResult.Value.FileID;
            try
            {
                ConcurrentBag<BackblazeB2ActionResult<UploadFilePartResponse>> uploadResponses = new ConcurrentBag<BackblazeB2ActionResult<UploadFilePartResponse>>();

                Task producerTask = Task.Factory.StartNew(StartProducerLoop);
                Task[] consumerLoops = new Task[_numberOfConnections];
                for (int connection = 0; connection < _numberOfConnections; connection++)
                {
                    consumerLoops[connection] = Task.Factory.StartNew(() => StartConsumeLoop(fileID, uploadResponses));
                }

                Task.WaitAll(consumerLoops);
                producerTask.Wait();

                // This will handle the scenario when there's an error
                return FinishUploadingLargeFile(fileID, uploadResponses);
            }
            catch (OperationCanceledException)
            {
                // Attempt to do a cancellation
                return CancelFileUpload(fileID);
            }
            catch (AggregateException ex)
            {
                // Only attempt a cancellation if we had a task cancelled exception
                if (ex.InnerExceptions.Any(e => e is OperationCanceledException))
                {
                    return CancelFileUpload(fileID);
                }

                throw ex;
            }
            catch (Exception ex)
            {
                // In the event of any other exception, cancel the upload and then rethrow
                CancelFileUpload(fileID);
                throw ex;
            }
        }

        public void Dispose()
        {
            if (!disposedValue)
            {
                _dataStream.Dispose();
                _jobStream.Dispose();
                disposedValue = true;
            }
        }
        #endregion

        #region private methods
        private void StartProducerLoop()
        {
            try
            {
                long currentChunk = 0;
                byte[] localBuffer = new byte[_fileChunkSizesInBytes];
                while (true)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    int amountRead = _dataStream.Read(localBuffer, 0, _fileChunkSizesInBytes);
                    if (amountRead > 0)
                    {
                        // Truncate buffer as necessary
                        byte[] truncatedBuffer = new byte[amountRead];
                        Buffer.BlockCopy(localBuffer, 0, truncatedBuffer, 0, amountRead);

                        // Inner-try spin loop
                        while (true)
                        {
                            // Check the cancellation token again. We don't want to spin forever
                            _cancellationToken.ThrowIfCancellationRequested();

                            bool didAdd = _jobStream.TryAdd(new ProducerUploadJob
                            {
                                Buffer = truncatedBuffer,
                                ContentLength = amountRead,
                                FilePartNumber = currentChunk + 1L, // File parts are 1-index based...I know, fucking stupid
                                SHA1 = ComputeSHA1Hash(truncatedBuffer),
                            }, TimeSpan.FromSeconds(4));

                            if (didAdd)
                            {
                                break;
                            }
                            else
                            {
                                // Otherwise, sleep for some amount of time
                                Thread.Sleep(TimeSpan.FromSeconds(5));
                            }
                        }

                        currentChunk++;
                    }
                    else
                    {
                        return;
                    }
                }
            }
            finally
            {
                // We finished reading everything or we can't read anything because something went wrong
                _jobStream.CompleteAdding();
            }
        }

        private void StartConsumeLoop(string fileID, ConcurrentBag<BackblazeB2ActionResult<UploadFilePartResponse>> responses)
        {
            BackblazeB2ActionResult<GetUploadPartURLResponse> uploadPartURLResponse = new GetUploadPartURLAction(
                _authorizationSession,
                _bucketID,
                fileID
            ).Execute();


            if (uploadPartURLResponse.HasErrors)
            {
                // Could not create the URL endpoint. Don't bother sending back the error since we can't really do anything about it
                return;
            }

            foreach (ProducerUploadJob job in _jobStream.GetConsumingEnumerable(_cancellationToken))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                responses.Add(new UploadFilePartAction(
                    _authorizationSession,
                    _cancellationToken,
                    _bucketID,
                    job.FilePartNumber,
                    _maxUploadAttempts,
                    uploadPartURLResponse.Result,
                    job.Buffer,
                    job.SHA1,
                    _exponentialBackoffCallback
                ).Execute());
            }
        }

        private BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> CancelFileUpload(string fileID)
        {
            CancelLargeFileUploadAction cancelAction = new CancelLargeFileUploadAction(_authorizationSession, fileID);
            BackblazeB2ActionResult<BackblazeB2CancelLargeFileUploadResult> cancelResult = cancelAction.Execute();
            if (cancelResult.HasErrors)
            {
                return new BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult>(
                    Maybe<BackblazeB2UploadMultipartFileResult>.Nothing,
                    cancelResult.Errors
                );
            }

            return new BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult>(
                Maybe<BackblazeB2UploadMultipartFileResult>.Nothing,
                new BackblazeB2ActionErrorDetails
                {
                    Status = -1,
                    Code = "MULTIPART_UPLOAD_CANCELLED",
                    Message = "The user cancalled the upload",
                }
            );
        }

        private BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> FinishUploadingLargeFile(
            string fileId,
            IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>> uploadResponses
        )
        {
            if (uploadResponses.Any(t => t.HasErrors))
            {
                IEnumerable<BackblazeB2ActionErrorDetails> errors = from uploadResponse in uploadResponses
                                                                    where uploadResponse.HasErrors
                                                                    from error in uploadResponse.Errors
                                                                    select error;

                return new BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult>(Maybe<BackblazeB2UploadMultipartFileResult>.Nothing, errors);
            }

            IList<string> sha1Hashes = (from uploadResponse in uploadResponses
                                        let uploadResponseValue = uploadResponse.MaybeResult.Value
                                        orderby uploadResponseValue.PartNumber ascending
                                        select uploadResponseValue.ContentSHA1).ToList();

            FinishLargeFileRequest finishLargeFileRequest = new FinishLargeFileRequest
            {
                FileID = fileId,
                FilePartHashes = sha1Hashes,
            };
            string serializedFileRequest = JsonConvert.SerializeObject(finishLargeFileRequest);
            byte[] requestBytes = Encoding.UTF8.GetBytes(serializedFileRequest);

            HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + FinishLargeFileURL);
            webRequest.ContentLength = requestBytes.Length;
            webRequest.Method = "POST";
            webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);

            BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> response =
                SendWebRequestAndDeserialize(webRequest, requestBytes);

            response.MaybeResult.Do(r => r.FileHashes = sha1Hashes);
            return response;
        }
        #endregion
    }
}