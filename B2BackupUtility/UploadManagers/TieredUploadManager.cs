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


using B2BackblazeBridge.Actions;
using B2BackblazeBridge.Core;
using B2BackupUtility.Database;
using B2BackupUtility.Encryption;
using Functional.Maybe;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace B2BackupUtility.UploadManagers
{
    /// <summary>
    /// An upload manager that breaks the uploading of data to B2 into "lanes." These lanes
    /// are used in the event that an upload fails in a specific lane
    /// </summary>
    public sealed class TieredUploadManager
    {
        #region private classes
        private sealed class UploadJob
        {
            public FileShard Shard { get; set; }
            public string UploadID { get; set; }
        }
        #endregion

        #region private fields
        private const int DefaultUploadChunkSize = 5242880; // 5 mebibytes
        private const int FastLaneUploadConnections = 20;
        private const int FastLaneUploadAttempts = 1;

        private const int MidLaneUploadConnections = 5;
        private const int MidLaneUploadAttempts = 3;

        private const int SlowLandUploadAttempts = 10;

        private const long MaxSize = 536870912; // 512 mebibytes

        private readonly UploadManagerAuthorizationSessionTicketCounter _ticketCounter;
        private readonly BlockingCollection<UploadJob> _fastLane;
        private readonly BlockingCollection<UploadJob> _midLane;
        private readonly BlockingCollection<UploadJob> _slowLane;
        private readonly List<Task> _taskList;
        private readonly Config _config;

        private volatile bool _isSealed;
        #endregion

        #region public events
        public event EventHandler<UploadManagerEventArgs> OnUploadFinished;

        public event EventHandler<UploadManagerEventArgs> OnUploadTierChanged;

        public event EventHandler<UploadManagerEventArgs> OnUploadFailed;
        #endregion

        #region ctor
        public TieredUploadManager(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            Config config
        )
        {
            _ticketCounter = new UploadManagerAuthorizationSessionTicketCounter(authorizationSessionGenerator);
            _fastLane = new BlockingCollection<UploadJob>();
            _midLane = new BlockingCollection<UploadJob>();
            _slowLane = new BlockingCollection<UploadJob>();
            _taskList = new List<Task>();
            _isSealed = false;
            _config = config;
        }
        #endregion

        #region public methods
        /// <summary>
        /// Add an upload job and return back an ID of the upload job
        /// </summary>
        /// <param name="fileShard"></param>
        /// <returns></returns>
        public string AddUploadJob(FileShard fileShard)
        {
            if (_isSealed)
            {
                throw new InvalidOperationException(
                    "The upload manager has been sealed. You can no longer add new upload jobs"
                );
            }

            UploadJob job = new UploadJob
            {
                Shard = fileShard,
                UploadID = Guid.NewGuid().ToString(),
            };
            _fastLane.Add(job);

            return job.UploadID;
        }

        /// <summary>
        /// Begin executing this upload manager. This function returns immediately
        /// </summary>
        public void Execute()
        {
            _taskList.AddRange(new[]
            {
                Task.Factory.StartNew(ExecuteFastLane),
                Task.Factory.StartNew(ExecuteMidLane),
                Task.Factory.StartNew(ExecuteSlowLane),
            });
        }

        /// <summary>
        /// This will seal the upload manager and signal that no further elements
        /// can be added to it. This also makes it possible to wait on this upload 
        /// manager until it has completed all of its uploads
        /// </summary>
        public void SealUploadManager()
        {
            _isSealed = true;
            _fastLane.CompleteAdding(); // This is the initial queue, so close it down
        }
        #endregion

        #region private methods
        private void ExecuteFastLane()
        {
            foreach (UploadJob job in _fastLane.GetConsumingEnumerable())
            {
                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = ExecuteLaneImpl(
                    _config,
                    _ticketCounter,
                    job.Shard,
                    true,
                    FastLaneUploadConnections,
                    FastLaneUploadAttempts
                );

                PostProcessJob(job, uploadResult, _midLane, "Mid Tier");
            }
        }

        private void ExecuteMidLane()
        {
            foreach (UploadJob job in _midLane.GetConsumingEnumerable())
            {
                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = ExecuteLaneImpl(
                    _config,
                    _ticketCounter,
                    job.Shard,
                    true,
                    MidLaneUploadConnections,
                    MidLaneUploadAttempts
                );

                PostProcessJob(job, uploadResult, _slowLane, "Slow Tier");
            }
        }

        private void PostProcessJob(
            UploadJob job,
            BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult,
            BlockingCollection<UploadJob> failureDestinationQueue,
            string failureDestinationQueueName
        )
        {
            if (uploadResult.HasErrors)
            {
                OnUploadTierChanged(this, new UploadManagerEventArgs
                {
                    UploadID = job.UploadID,
                    NewUploadTier = failureDestinationQueueName,
                });

                failureDestinationQueue.Add(job);
            }
            else
            {
                OnUploadFinished(this, new UploadManagerEventArgs
                {
                    UploadID = job.UploadID,
                });
            }
        }

        private void ExecuteSlowLane()
        {
            foreach (UploadJob job in _slowLane.GetConsumingEnumerable())
            {
                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = ExecuteLaneImpl(
                    _config,
                    _ticketCounter,
                    job.Shard,
                    false,
                    -1,
                    -1
                );

                if (uploadResult.HasErrors)
                {
                    OnUploadFailed(this, new UploadManagerEventArgs
                    {
                        UploadID = job.UploadID,
                    });
                }
                else
                {
                    OnUploadFinished(this, new UploadManagerEventArgs
                    {
                        UploadID = job.UploadID,
                    });
                }
            }
        }

        private static BackblazeB2ActionResult<IBackblazeB2UploadResult> ExecuteLaneImpl(
            Config config,
            UploadManagerAuthorizationSessionTicketCounter ticketCounter,
            FileShard fileShard,
            bool canUseMultipleConnections,
            int uploadConnections,
            int uploadAttempts
        )
        {
            byte[] serializedAndEncryptedBytes = EncryptionHelpers.EncryptBytes(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fileShard)),
                config.EncryptionKey,
                config.InitializationVector
            );

            using (UploadManagerAuthorizationSessionTicket authorizationTicket = ticketCounter.GetAuthorizationSessionTicket())
            {
                return fileShard.Length > DefaultUploadChunkSize && canUseMultipleConnections
                   ? ExecuteUploadAction(
                       new UploadWithSingleConnectionAction(
                           authorizationTicket.AuthorizationSession,
                           config.BucketID,
                           serializedAndEncryptedBytes,
                           fileShard.ID,
                           uploadAttempts,
                           CancellationEventRouter.GlobalCancellationToken,
                           _ => { } // There is no backoff, so we can just NoOp here
                       ))
                   : ExecuteUploadAction(
                       new UploadWithMultipleConnectionsAction(
                           authorizationTicket.AuthorizationSession,
                           new MemoryStream(serializedAndEncryptedBytes),
                           fileShard.ID,
                           config.BucketID,
                           DefaultUploadChunkSize,
                           uploadConnections,
                           uploadAttempts,
                           CancellationEventRouter.GlobalCancellationToken,
                           _ => { } // There is no backoff, so we can just NoOp here
                       ));
            }
        }

        private static BackblazeB2ActionResult<IBackblazeB2UploadResult> ExecuteUploadAction<T>(
            BaseAction<T> action
        ) where T : IBackblazeB2UploadResult
        {
            BackblazeB2ActionResult<T> uploadResult = action.Execute();
            BackblazeB2ActionResult<IBackblazeB2UploadResult> castedResult;
            if (uploadResult.HasResult)
            {
                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(uploadResult.Result);
            }
            else
            {
                castedResult = new BackblazeB2ActionResult<IBackblazeB2UploadResult>(
                    Maybe<IBackblazeB2UploadResult>.Nothing,
                    uploadResult.Errors
                );
            }

            return castedResult;
        }
        #endregion
    }
}
