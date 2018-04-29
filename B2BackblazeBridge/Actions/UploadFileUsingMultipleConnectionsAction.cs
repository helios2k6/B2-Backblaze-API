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
using B2BackblazeBridge.Processing;
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
        private static readonly string StartLargeFileURL = "/b2api/v1/b2_start_large_file";

        private static readonly string GetUploadPartURLURL = "/b2api/v1/b2_get_upload_part_url";

        private static readonly string FinishLargeFileURL = "/b2api/v1/b2_finish_large_file";

        private static readonly int MaxUploadAttempts = 10;

        private readonly BackblazeB2AuthorizationSession _authorizationSession;

        private readonly string _bucketID;

        private readonly string _filePath;

        private readonly int _numberOfConnections;

        private readonly int _fileChunkSizesInBytes;
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new UploadFileUsingMultipleConnectionsActions
        /// </summary>
        /// <param name="authorizationSession">The authorization session</param>
        /// <param name="filePath">The (local) path to the file you want to upload</param>
        /// <param name="bucketID">The B2 bucket you want to upload to</param>
        /// <param name="fileChunkSizesInBytes">The size (in bytes) of the file chunks you want to use when uploading</param>
        /// <param name="numberOfConnections">The number of connections to use when uploading</param>
        public UploadFileUsingMultipleConnectionsAction(
            BackblazeB2AuthorizationSession authorizationSession,
            string filePath,
            string bucketID,
            int fileChunkSizesInBytes,
            int numberOfConnections
        )
        {
            if (File.Exists(filePath) == false)
            {
                throw new ArgumentException(string.Format("{0} does not exist", filePath));
            }

            if (fileChunkSizesInBytes < 1048576)
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
            _fileChunkSizesInBytes = fileChunkSizesInBytes;
            _numberOfConnections = numberOfConnections;
        }
        #endregion

        #region public methods
        public async override Task<BackblazeB2UploadMultipartFileResult> ExecuteAsync()
        {
            string fileID = await GetFileIDAsync();
            IEnumerable<UploadPartJob> jobs = await GenerateUploadPartsAsync();

            IEnumerable<GetUploadPartURLResponse> urlEndpoints = GetUploadPartURLs(fileID);
            IDictionary<UploadPartJob, bool> uploadResponses = ProcessAllJobs(jobs, urlEndpoints);
            int unixTimestamp = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            return await FinishUploadingLargeFileAsync(fileID, jobs.Select(t => t.SHA1).ToList());
        }
        #endregion

        #region private methods
        private IDictionary<UploadPartJob, bool> ProcessAllJobs(
            IEnumerable<UploadPartJob> jobs,
            IEnumerable<GetUploadPartURLResponse> urls
        )
        {
            List<UploadPartJob> jobsList = new List<UploadPartJob>(jobs);
            List<GetUploadPartURLResponse> workerList = new List<GetUploadPartURLResponse>(urls);

            int jobCount = jobsList.Count();
            int workerCount = workerList.Count();
            int jobsPerWorker = jobCount / workerCount;
            int remainingJobs = jobCount % workerCount;

            // TODO: If the number of jobs per worker is 0, that means there are too many workers and not enough jobs. So we'll have to
            // think about how we want to distribute these

            Task[] workerArray = new Task[workerCount];
            for (int i = 0; i < workerCount - 1; i++)
            {
                workerArray[i] = ProcessJobs(workerList[i], jobsList.Skip(jobsPerWorker * i).Take(jobsPerWorker).ToList());
            }

            // Do something special for the last worker
            workerArray[workerCount - 1] = ProcessJobs(workerList.Last(), jobsList.Skip(jobsPerWorker * (workerCount - 1)).ToList());

            Task.WaitAll(workerArray);
            IDictionary<UploadPartJob, bool> finalResultMap = new Dictionary<UploadPartJob, bool>();

            return
                (from worker in workerArray.Cast<Task<IDictionary<UploadPartJob, bool>>>()
                 from kvp in worker.Result
                 select kvp).ToDictionary(elem => elem.Key, elem => elem.Value);
        }

        private async Task<IDictionary<UploadPartJob, bool>> ProcessJobs(GetUploadPartURLResponse url, IList<UploadPartJob> jobs)
        {
            Dictionary<UploadPartJob, bool> jobResults = new Dictionary<UploadPartJob, bool>();
            foreach (UploadPartJob job in jobs)
            {
                // Read bytes first
                using (FileStream stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
                {
                    stream.Seek(job.FileCursorPosition, SeekOrigin.Begin);
                    byte[] fileBytes = new byte[job.ContentLength];
                    int bytesRead = await stream.ReadAsync(fileBytes, 0, (int)job.ContentLength);
                    if (bytesRead != job.ContentLength)
                    {
                        throw new InvalidOperationException("The number of bytes read does not match expected content length");
                    }

                    try
                    {
                        // Then upload the bytes
                        await UploadFilePartAsync(
                            fileBytes,
                            job.SHA1,
                            job.FilePartNumber,
                            url
                        );

                        jobResults.Add(job, true);
                    }
                    catch (UploadFilePartFailureException)
                    {
                        jobResults.Add(job, false);
                    }
                }
            }

            return jobResults;
        }

        private async Task<IEnumerable<UploadPartJob>> GenerateUploadPartsAsync()
        {
            IList<UploadPartJob> jobs = new List<UploadPartJob>();
            FileInfo fileInfo = new FileInfo(_filePath);
            int numberOfChunks = (int)(fileInfo.Length / _fileChunkSizesInBytes); // We can't have more than 4 billion chunks per file. 
            for (int currentChunk = 0; currentChunk < numberOfChunks; currentChunk++)
            {
                long cursorPosition = currentChunk * _fileChunkSizesInBytes;
                jobs.Add(new UploadPartJob
                {
                    ContentLength = _fileChunkSizesInBytes,
                    FileCursorPosition = cursorPosition,
                    FilePartNumber = currentChunk + 1, // File parts are 1-index based...I know, fucking stupid
                    SHA1 = await ComputeSHA1HashOfChunkAsync(cursorPosition, _fileChunkSizesInBytes),
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
                    FilePartNumber = numberOfChunks + 1, // File parts are 1-index based...I know, fucking stupid
                    SHA1 = await ComputeSHA1HashOfChunkAsync(cursorPosition, remainderChunk),
                });
            }

            return jobs;
        }

        private async Task<string> ComputeSHA1HashOfChunkAsync(long fileCursorPosition, long length)
        {
            using (FileStream fileStream = new FileStream(_filePath, FileMode.Open))
            using (SHA1 shaHash = SHA1.Create())
            {
                fileStream.Seek(fileCursorPosition, SeekOrigin.Begin);
                byte[] buffer = new byte[length];
                int bytesRead = await fileStream.ReadAsync(buffer, 0, (int)length);
                if (bytesRead != length)
                {
                    throw new InvalidOperationException("The number of bytes read did not equal the expected number of bytes while computing the SHA1 hash");
                }

                return ComputeSHA1Hash(buffer);
            }
        }

        private async Task<string> GetFileIDAsync()
        {
            try
            {
                StartLargeFileRequest request = new StartLargeFileRequest
                {
                    BucketID = _bucketID,
                    ContentType = "b2/x-auto",
                    FileName = GetSafeFileName(_filePath),
                };

                byte[] jsonBodyBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + StartLargeFileURL, true);
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
                webRequest.ContentLength = jsonBodyBytes.Length;

                Dictionary<string, dynamic> jsonResponse = await SendWebRequestAsync(webRequest, jsonBodyBytes);
                return (string)jsonResponse["fileId"];
            }
            catch (BaseActionWebRequestException ex)
            {
                throw new UploadFileActionException(ex.StatusCode, ex.Details);
            }
        }

        private IEnumerable<GetUploadPartURLResponse> GetUploadPartURLs(string fileID)
        {
            object lock_object = new object();
            Task[] taskArray = new Task[_numberOfConnections];
            for (int i = 0; i < _numberOfConnections; i++)
            {
                taskArray[i] = GetUploadPartURLAsync(fileID);
            }

            Task.WaitAll(taskArray);

            return from t in taskArray.Cast<Task<GetUploadPartURLResponse>>()
                   select t.Result;
        }

        private async Task<GetUploadPartURLResponse> GetUploadPartURLAsync(string fileID)
        {
            try
            {
                byte[] jsonPayloadBytes = Encoding.UTF8.GetBytes("{\"fileId\":\"" + fileID + "\"}");
                HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + GetUploadPartURLURL, true);
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);
                webRequest.ContentLength = jsonPayloadBytes.Length;

                Dictionary<string, dynamic> jsonResponse = await SendWebRequestAsync(webRequest, jsonPayloadBytes);
                return new GetUploadPartURLResponse
                {
                    AuthorizationToken = jsonResponse["authorizationToken"],
                    FileID = jsonResponse["fileId"],
                    UploadURL = jsonResponse["uploadUrl"],
                };
            }
            catch (BaseActionWebRequestException ex)
            {
                throw new UploadFileActionException(ex.StatusCode, ex.Details);
            }
        }

        private async Task UploadFilePartAsync(
            byte[] fileBytes,
            string sha1Hash,
            int partNumber,
            GetUploadPartURLResponse getUploadPartUrl
        )
        {
            // Loop because we want to retry to upload the file part should it fail for
            // recoverable reasons. We will break out of this loop should the upload succeed
            // or throw an exception should we determine we can't upload to the server
            int attemptNumber = 0;
            while (true)
            {
                if (attemptNumber >= MaxUploadAttempts)
                {
                    throw new UploadFilePartFailureException();
                }

                // Sleep the current thread so that we can give the server some time to recover
                if (attemptNumber > 0)
                {
                    TimeSpan backoffSleepTime = CalculateExponentialBackoffSleepTime(attemptNumber);
                    Thread.Sleep(backoffSleepTime);
                }

                try
                {
                    HttpWebRequest webRequest = GetHttpWebRequest(getUploadPartUrl.UploadURL, true);
                    webRequest.Method = "POST";
                    webRequest.Headers.Add("Authorization", getUploadPartUrl.AuthorizationToken);
                    webRequest.Headers.Add("X-Bz-Part-Number", partNumber.ToString());
                    webRequest.Headers.Add("X-Bz-Content-Sha1", sha1Hash);
                    webRequest.ContentLength = fileBytes.Length;

                    Dictionary<string, dynamic> jsonResponse = await SendWebRequestAsync(webRequest, fileBytes);

                    // Do some verification 
                    long contentLengthResult = jsonResponse["contentLength"];
                    string sha1Result = jsonResponse["contentSha1"];
                    int partNumberResult = (int)jsonResponse["partNumber"];

                    if (
                        (contentLengthResult != fileBytes.Length ||
                        sha1Hash.Equals(sha1Hash, StringComparison.Ordinal) == false ||
                        partNumber != partNumberResult) &&
                        attemptNumber >= MaxUploadAttempts
                    )
                    {
                        throw new UploadFilePartInconsistentException(getUploadPartUrl.FileID, partNumber);
                    }

                    break;
                }
                catch (BaseActionWebRequestException ex)
                {
                    if (ex.StatusCode != HttpStatusCode.ServiceUnavailable || attemptNumber >= MaxUploadAttempts)
                    {
                        throw new UploadFilePartFailureException();
                    }
                }

                attemptNumber++;
            }
        }

        private async Task<BackblazeB2UploadMultipartFileResult> FinishUploadingLargeFileAsync(
            string fileId,
            IList<string> sha1Parts
        )
        {
            try
            {
                FinishLargeFileRequest finishLargeFileRequest = new FinishLargeFileRequest
                {
                    FileID = fileId,
                    FilePartHashes = sha1Parts,
                };
                string serializedFileRequest = JsonConvert.SerializeObject(finishLargeFileRequest);
                byte[] requestBytes = Encoding.UTF8.GetBytes(serializedFileRequest);

                HttpWebRequest webRequest = GetHttpWebRequest(_authorizationSession.APIURL + FinishLargeFileURL, true);
                webRequest.ContentLength = requestBytes.Length;
                webRequest.Method = "POST";
                webRequest.Headers.Add("Authorization", _authorizationSession.AuthorizationToken);

                Dictionary<string, dynamic> response = await SendWebRequestAsync(webRequest, requestBytes);

                return new BackblazeB2UploadMultipartFileResult
                {
                    BucketID = response["bucketId"],
                    FileHashes = sha1Parts,
                    FileID = response["fileId"],
                    FileName = response["fileName"],
                    TotalContentLength = response["contentLength"],
                };
            }
            catch (BaseActionWebRequestException ex)
            {
                throw new UploadFileActionException(ex.StatusCode, ex.Details);
            }
        }
        #endregion
    }
}