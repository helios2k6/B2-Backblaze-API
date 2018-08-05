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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B2BackblazeBridge.Actions
{
    /// <summary>
    /// Represents uploading a single file using multiple connections in parallel
    /// </summary>
    public sealed class UploadFileUsingMultipleConnectionsAction : BaseAction<BackblazeB2UploadMultipartFileResult>
    {
        #region private fields
        private static readonly string FinishLargeFileURL = "/b2api/v1/b2_finish_large_file";

        private static readonly int MinimumFileChunkSize = 1024 * 1024; // 1 mebibyte

        private readonly BackblazeB2AuthorizationSession _authorizationSession;

        private readonly string _bucketID;

        private readonly string _filePath;

        private readonly string _fileDestination;

        private readonly int _numberOfConnections;

        private readonly int _fileChunkSizesInBytes;
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new UploadFileUsingMultipleConnectionsActions
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="filePath">The (local) path to the file you want to upload</param>
        /// <param name="fileDestination">The remote path you want to upload to</param>
        /// <param name="bucketID">The B2 bucket you want to upload to</param>
        /// <param name="fileChunkSizesInBytes">The size (in bytes) of the file chunks you want to use when uploading</param>
        /// <param name="numberOfConnections">The number of connections to use when uploading</param>
        /// <param name="cancellationToken">The cancellation token to pass in when this upload needs to be cancelled</param>
        public UploadFileUsingMultipleConnectionsAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string filePath,
            string fileDestination,
            string bucketID,
            int fileChunkSizesInBytes,
            int numberOfConnections,
            CancellationToken cancellationToken
        ) : base(cancellationToken)
        {
            if (File.Exists(filePath) == false)
            {
                throw new ArgumentException(string.Format("{0} does not exist", filePath));
            }

            if (fileChunkSizesInBytes < MinimumFileChunkSize)
            {
                throw new ArgumentException("The file chunk sizes must be larger than 1 mebibyte");
            }

            if (numberOfConnections < 1)
            {
                throw new ArgumentException("You must specify a positive, non-zero number of connections", "numberOfConnections");
            }

            _authorizationSession = authorizationSession ?? throw new ArgumentNullException("The authorization session object must not be mull");
            _bucketID = bucketID;
            _filePath = filePath;
            _fileDestination = fileDestination;
            _fileChunkSizesInBytes = fileChunkSizesInBytes;
            _numberOfConnections = numberOfConnections;
        }

        /// <summary>
        /// Constructs a new UploadFileUsingMultipleConnectionsActions
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="filePath">The (local) path to the file you want to upload</param>
        /// <param name="fileDestination">The remote path you want to upload to</param>
        /// <param name="bucketID">The B2 bucket you want to upload to</param>
        /// <param name="fileChunkSizesInBytes">The size (in bytes) of the file chunks you want to use when uploading</param>
        /// <param name="numberOfConnections">The number of connections to use when uploading</param>
        /// <param name="cancellationToken">The cancellation token to pass in when this upload needs to be cancelled</param>
        public UploadFileUsingMultipleConnectionsAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string filePath,
            string fileDestination,
            string bucketID,
            int fileChunkSizesInBytes,
            int numberOfConnections
        ) : this(
            authorizationSession,
            filePath,
            fileDestination,
            bucketID,
            fileChunkSizesInBytes,
            numberOfConnections,
            CancellationToken.None
        )
        {
        }
        #endregion

        #region public methods
        public override BackblazeB2ActionResult<BackblazeB2UploadMultipartFileResult> Execute()
        {
            BackblazeB2ActionResult<StartLargeFileResponse> fileIDResponse = new StartLargeFileAction(
                _authorizationSession,
                _bucketID,
                _fileDestination
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
                return FinishUploadingLargeFile(fileID, ProcessAllJobs(GenerateUploadParts(), GetUploadPartURLs(fileID)));
            }
            catch (TaskCanceledException)
            {
                // Attempt to do a cancellation
                return CancelFileUpload(fileID);
            }
            catch (AggregateException ex)
            {
                // Only attempt a cancellation if we had a task cancelled exception
                if (ex.InnerExceptions.Any(e => e is TaskCanceledException))
                {
                    return CancelFileUpload(fileID);
                }

                throw ex;
            }
        }
        #endregion

        #region private methods
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

        private IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>> ProcessAllJobs(
            IEnumerable<UploadPartJob> jobs,
            IEnumerable<BackblazeB2ActionResult<GetUploadPartURLResponse>> urlResponses
        )
        {
            // If any of these have errors, then we need to return it. Only return the first one
            if (urlResponses.Any(t => t.HasErrors))
            {
                return from response in urlResponses
                       from error in response.Errors
                       select new BackblazeB2ActionResult<UploadFilePartResponse>(Maybe<UploadFilePartResponse>.Nothing, error);
            }

            IEnumerable<GetUploadPartURLResponse> urls = urlResponses.Select(t => t.MaybeResult.Value);
            Task<IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>>>[] workerArray = ConstructAndStartWorkers(jobs, urls);
            Task.WaitAll(workerArray);
            return
                from worker in workerArray
                from response in worker.Result
                select response;
        }

        private Task<IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>>>[] ConstructAndStartWorkers(IEnumerable<UploadPartJob> jobs, IEnumerable<GetUploadPartURLResponse> urls)
        {
            List<UploadPartJob> jobsList = new List<UploadPartJob>(jobs);
            List<GetUploadPartURLResponse> workerList = new List<GetUploadPartURLResponse>(urls);

            int jobCount = jobsList.Count();
            int workerCount = workerList.Count();
            Task<IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>>>[] workerArray;
            if (jobCount < workerCount)
            {
                workerArray = new Task<IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>>>[jobCount];
                for (int i = 0; i < jobCount; i++)
                {
                    workerArray[i] = ProcessJobsForURLEndpoint(workerList[i], new List<UploadPartJob>() { jobsList[i] });
                }
            }
            else
            {
                int jobsPerWorker = (int)Math.Ceiling((double)jobCount / workerCount);
                int remainingJobs = Math.Max(jobCount - (jobsPerWorker * workerCount), 0);
                workerArray = new Task<IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>>>[workerCount];
                for (int i = 0; i < workerCount - 1; i++)
                {
                    workerArray[i] = ProcessJobsForURLEndpoint(workerList[i], jobsList.Skip(i * jobsPerWorker).Take(jobsPerWorker).ToList());
                }

                // Add the remaining jobs to the last worker
                workerArray[workerCount - 1] = ProcessJobsForURLEndpoint(workerList[workerCount - 1], jobsList.Skip(jobsPerWorker * (workerCount - 1)).ToList());
            }

            return workerArray;
        }

        private Task<IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>>> ProcessJobsForURLEndpoint(GetUploadPartURLResponse urlEndpoint, IList<UploadPartJob> jobs)
        {
            return Task.Factory.StartNew<IEnumerable<BackblazeB2ActionResult<UploadFilePartResponse>>>(() =>
            {
                IList<BackblazeB2ActionResult<UploadFilePartResponse>> responses = new List<BackblazeB2ActionResult<UploadFilePartResponse>>();
                foreach (UploadPartJob job in jobs)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    // Read bytes first
                    using (FileStream stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        stream.Seek(job.FileCursorPosition, SeekOrigin.Begin);
                        byte[] fileBytes = new byte[job.ContentLength];
                        int bytesRead = stream.Read(fileBytes, 0, (int)job.ContentLength);
                        if (bytesRead != job.ContentLength)
                        {
                            throw new InvalidOperationException("The number of bytes read does not match expected content length");
                        }

                        // Then upload the bytes
                        BackblazeB2ActionResult<UploadFilePartResponse> uploadResponse = 
                            new UploadFilePartAction(
                                _authorizationSession,
                                _cancellationToken,
                                _bucketID,
                                job.FilePartNumber,
                                urlEndpoint,
                                fileBytes,
                                job.SHA1
                            ).Execute();

                        responses.Add(uploadResponse);
                    }
                }
                return responses;
            });
        }

        private IEnumerable<UploadPartJob> GenerateUploadParts()
        {
            IList<UploadPartJob> jobs = new List<UploadPartJob>();
            FileInfo fileInfo = new FileInfo(_filePath);
            long numberOfChunks = (fileInfo.Length / _fileChunkSizesInBytes); // We can't have more than 4 billion chunks per file. 
            for (long currentChunk = 0; currentChunk < numberOfChunks; currentChunk++)
            {
                long cursorPosition = currentChunk * _fileChunkSizesInBytes;
                jobs.Add(new UploadPartJob
                {
                    ContentLength = _fileChunkSizesInBytes,
                    FileCursorPosition = cursorPosition,
                    FilePartNumber = currentChunk + 1L, // File parts are 1-index based...I know, fucking stupid
                    SHA1 = ComputeSHA1HashOfChunk(cursorPosition, _fileChunkSizesInBytes),
                });
            }

            // There wasn't perfect division, which means we have to account for the last chunk
            long remainderChunk = fileInfo.Length % _fileChunkSizesInBytes;
            if (remainderChunk != 0)
            {
                long cursorPosition = numberOfChunks * _fileChunkSizesInBytes;
                jobs.Add(new UploadPartJob
                {
                    ContentLength = remainderChunk,
                    FileCursorPosition = numberOfChunks * _fileChunkSizesInBytes,
                    FilePartNumber = numberOfChunks + 1L, // File parts are 1-index based...I know, fucking stupid
                    SHA1 = ComputeSHA1HashOfChunk(cursorPosition, remainderChunk),
                });
            }

            return jobs;
        }

        private string ComputeSHA1HashOfChunk(long fileCursorPosition, long length)
        {
            using (FileStream fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA1 shaHash = SHA1.Create())
            {
                fileStream.Seek(fileCursorPosition, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                int bytesRead = fileStream.Read(buffer, 0, (int)length);
                if (bytesRead != length)
                {
                    throw new InvalidOperationException("The number of bytes read did not equal the expected number of bytes while computing the SHA1 hash");
                }

                return ComputeSHA1Hash(buffer);
            }
        }

        private IEnumerable<BackblazeB2ActionResult<GetUploadPartURLResponse>> GetUploadPartURLs(string fileID)
        {
            Task<BackblazeB2ActionResult<GetUploadPartURLResponse>>[] uploadPartURLWorkers =
                new Task<BackblazeB2ActionResult<GetUploadPartURLResponse>>[_numberOfConnections];
            for (int i = 0; i < _numberOfConnections; i++)
            {
                uploadPartURLWorkers[i] = Task.Factory.StartNew(() => new GetUploadPartURLAction(
                    _authorizationSession,
                    _bucketID,
                    fileID
                ).Execute());
            }

            Task.WaitAll(uploadPartURLWorkers);

            List<BackblazeB2ActionResult<GetUploadPartURLResponse>> uploadPartURLs =
                new List<BackblazeB2ActionResult<GetUploadPartURLResponse>>();
            for (int i = 0; i < _numberOfConnections; i++)
            {
                uploadPartURLs.Add(uploadPartURLWorkers[i].Result);
            }

            return uploadPartURLs;
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

            response.MaybeResult.Do(r =>
            {
                r.FileHashes = sha1Hashes;
                r.FileName = Uri.UnescapeDataString(r.FileName);
            });

            return response;
        }
        #endregion
    }
}