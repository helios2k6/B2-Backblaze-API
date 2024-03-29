﻿/* 
 * Copyright (c) 2023 Andrew Johnson
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
using B2BackupUtility.MemoryManagement;
using Functional.Maybe;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B2BackupUtility.UploadManagers
{
    /// <summary>
    /// An upload manager that breaks the uploading of data to B2 into "lanes." These lanes
    /// are used in the event that an upload fails in a specific lane
    /// </summary>
    public sealed class TieredUploadManager : IDisposable
    {
        #region private classes
        private sealed class UploadJob
        {
            public Lazy<FileShard> LazyShard { get; set; }
            public string UploadID { get; set; }
        }
        #endregion

        #region private fields
        private const string MemoryServiceName = "Tiered Upload Manager";

        private const int DefaultFastLaneCount = 5;
        private const int DefaultMidLaneCount = 3;
        private const int DefaultSlowLaneCount = 1;

        private const int DefaultUploadChunkSize = 5242880; // 5 mebibytes
        private const int DefaultFastLaneUploadConnections = 20;
        private const int DefaultFastLaneUploadAttempts = 1;

        private const int DefaultMidLaneUploadConnections = 5;
        private const int DefaultMidLaneUploadAttempts = 3;

        private const long DefaultMaxMemoryAllowed = 6442450944; // 6 gibibytes

        private readonly Func<BackblazeB2AuthorizationSession> _authorizationSessionGenerator;
        private readonly BlockingCollection<UploadJob> _fastLane;
        private readonly BlockingCollection<UploadJob> _midLane;
        private readonly BlockingCollection<UploadJob> _slowLane;
        private readonly CancellationToken _cancellationToken;
        private readonly List<Task> _taskList;
        private readonly Config _config;

        private int _fastLaneCount;
        private int _midLaneCount;
        private int _slowLaneCount;
        private bool _isSealed;
        private bool _isDisposed;
        #endregion

        #region public events
        public event EventHandler<UploadManagerEventArgs> OnUploadBegin;
        public event EventHandler<UploadManagerEventArgs> OnUploadFinished;
        public event EventHandler<UploadManagerEventArgs> OnUploadTierChanged;
        public event EventHandler<UploadManagerEventArgs> OnUploadFailed;
        #endregion

        #region public properties
        /// <summary>
        /// The number of fast lanes
        /// </summary>
        public int FastLaneCount
        {
            get => _fastLaneCount;
            set
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException("This object has been disposed");
                }

                if (value <= 0)
                {
                    throw new ArgumentException("The number of fast lanes must be greater than 0");
                }

                _fastLaneCount = value;
            }
        }

        /// <summary>
        /// The number of mid lanes
        /// </summary>
        public int MidLaneCount
        {
            get => _midLaneCount;
            set
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException("This object has been disposed");
                }

                if (value <= 0)
                {
                    throw new ArgumentException("The number of mid lanes must be greater than 0");
                }

                _midLaneCount = value;
            }
        }

        /// <summary>
        /// The number of slow lanes
        /// </summary>
        public int SlowLaneCount
        {
            get => _slowLaneCount;
            set
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException("This object has been disposed");
                }

                if (value <= 0)
                {
                    throw new ArgumentException("The number of slow lanes must be greater than 0");
                }

                _slowLaneCount = value;
            }
        }
        #endregion

        #region ctor
        public TieredUploadManager(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            Config config,
            CancellationToken cancellationToken
        )
        {
            _authorizationSessionGenerator = authorizationSessionGenerator;
            _fastLane = new BlockingCollection<UploadJob>();
            _midLane = new BlockingCollection<UploadJob>();
            _slowLane = new BlockingCollection<UploadJob>();
            _cancellationToken = cancellationToken;
            _taskList = new List<Task>();
            _isSealed = false;
            _config = config;

            FastLaneCount = DefaultFastLaneCount;
            MidLaneCount = DefaultMidLaneCount;
            SlowLaneCount = DefaultSlowLaneCount;

            MultinodeMemoryManagementSystem.Instance.AddService(MemoryServiceName, DefaultMaxMemoryAllowed);
        }
        #endregion

        #region public methods
        /// <summary>
        /// Add an upload job and return back an ID of the upload job
        /// </summary>
        /// <param name="lazyFileShard">The file shard to upload</param>
        /// <returns></returns>
        public string AddLazyFileShard(Lazy<FileShard> lazyFileShard)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("This object has been disposed");
            }

            if (_isSealed)
            {
                throw new InvalidOperationException(
                    "The upload manager has been sealed. You can no longer add new upload jobs"
                );
            }

            UploadJob job = new UploadJob
            {
                LazyShard = lazyFileShard,
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
            if (_isDisposed)
            {
                throw new ObjectDisposedException("This object has been disposed");
            }

            // Start fast lanes
            Task[] fastLaneTasks = new Task[FastLaneCount];
            for (int i = 0; i < FastLaneCount; i++)
            {
                fastLaneTasks[i] = Task.Factory.StartNew(ExecuteFastLane);
            }
            _taskList.Add(Task.WhenAll(fastLaneTasks).ContinueWith(_ =>
            {
                _midLane.CompleteAdding();
            }));

            // Start mid lanes
            Task[] midLaneTasks = new Task[MidLaneCount];
            for (int i = 0; i < MidLaneCount; i++)
            {
                midLaneTasks[i] = Task.Factory.StartNew(ExecuteMidLane);
            }
            _taskList.Add(Task.WhenAll(midLaneTasks).ContinueWith(_ =>
            {
                _slowLane.CompleteAdding();
            }));

            // Start slow lanes
            for (int i = 0; i < SlowLaneCount; i++)
            {
                _taskList.Add(Task.Factory.StartNew(ExecuteSlowLane));
            }
        }

        /// <summary>
        /// This will seal the upload manager and signal that no further elements
        /// can be added to it. This also makes it possible to wait on this upload 
        /// manager until it has completed all of its uploads
        /// </summary>
        public void SealUploadManager()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("This object has been disposed");
            }

            _isSealed = true;
            _fastLane.CompleteAdding(); // This is the initial queue, so close it down
        }

        /// <summary>
        /// Waits until all uploads are finished
        /// </summary>
        public void Wait()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("This object has been disposed");
            }

            Task.WaitAll(_taskList.ToArray());
        }

        /// <summary>
        /// Disposes of this upload manager
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isSealed = true;
            _fastLane.Dispose();
            _midLane.Dispose();
            _slowLane.Dispose();

            foreach (Task t in _taskList)
            {
                t.Dispose();
            }
        }
        #endregion

        #region private methods
        private void ExecuteFastLane()
        {
            foreach (UploadJob job in _fastLane.GetConsumingEnumerable(_cancellationToken))
            {
                OnUploadBegin(this, new UploadManagerEventArgs
                {
                    UploadID = job.UploadID,
                });

                FileShard fileShard = job.LazyShard.Value;
                MultinodeMemoryManagementSystem
                    .Instance
                    .GetMemoryGovernor(MemoryServiceName)
                    .AllocateMemory(fileShard.Length, _cancellationToken);

                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = ExecuteLaneImpl(
                    _config,
                    _authorizationSessionGenerator,
                    fileShard,
                    _cancellationToken,
                    true,
                    DefaultFastLaneUploadConnections,
                    DefaultFastLaneUploadAttempts
                );

                PostProcessJob(job, uploadResult, _midLane, "Mid Tier");
            }
        }

        private void ExecuteMidLane()
        {
            foreach (UploadJob job in _midLane.GetConsumingEnumerable(_cancellationToken))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = ExecuteLaneImpl(
                    _config,
                    _authorizationSessionGenerator,
                    job.LazyShard.Value,
                    _cancellationToken,
                    true,
                    DefaultMidLaneUploadConnections,
                    DefaultMidLaneUploadAttempts
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
                MultinodeMemoryManagementSystem
                    .Instance
                    .GetMemoryGovernor(MemoryServiceName)
                    .FreeMemory(job.LazyShard.Value.Length);

                OnUploadFinished(this, new UploadManagerEventArgs
                {
                    FileShardID = job.LazyShard.Value.ID,
                    FileShardPieceNumber = job.LazyShard.Value.PieceNumber,
                    FileShardSHA1 = job.LazyShard.Value.SHA1,
                    UploadID = job.UploadID,
                    UploadResult = uploadResult,
                });
            }
        }

        private void ExecuteSlowLane()
        {
            foreach (UploadJob job in _slowLane.GetConsumingEnumerable(_cancellationToken))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                FileShard fileShard = job.LazyShard.Value;
                BackblazeB2ActionResult<IBackblazeB2UploadResult> uploadResult = ExecuteLaneImpl(
                    _config,
                    _authorizationSessionGenerator,
                    fileShard,
                    _cancellationToken,
                    false,
                    -1,
                    -1
                );

                if (uploadResult.HasErrors)
                {
                    OnUploadFailed(this, new UploadManagerEventArgs
                    {
                        UploadID = job.UploadID,
                        UploadResult = uploadResult,
                    });
                }
                else
                {
                    OnUploadFinished(this, new UploadManagerEventArgs
                    {
                        FileShardID = fileShard.ID,
                        FileShardPieceNumber = fileShard.PieceNumber,
                        FileShardSHA1 = fileShard.SHA1,
                        UploadID = job.UploadID,
                        UploadResult = uploadResult,
                    });
                }

                // It doesn't matter if the job succeeds or fails
                MultinodeMemoryManagementSystem
                    .Instance
                    .GetMemoryGovernor(MemoryServiceName)
                    .FreeMemory(fileShard.Length);
            }
        }

        private static BackblazeB2ActionResult<IBackblazeB2UploadResult> ExecuteLaneImpl(
            Config config,
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator,
            FileShard fileShard,
            CancellationToken cancellationToken,
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

            using (MemoryResourceContext serializedAndEncryptedBytesMemoryContext = MultinodeMemoryManagementSystem
                .Instance
                .GetMemoryGovernor(MemoryServiceName)
                .AllocateMemoryWithContext(serializedAndEncryptedBytes.Length, cancellationToken))
            {
                BackblazeB2ActionResult<IBackblazeB2UploadResult> result = null;
                if (fileShard.Length > DefaultUploadChunkSize && canUseMultipleConnections)
                {
                    // Reallocate another chunk of memory since the multiconnection action will allocate
                    // more memory internally
                    using (MemoryResourceContext uploadWithMultipleConnectionsActionContext = MultinodeMemoryManagementSystem
                        .Instance
                        .GetMemoryGovernor(MemoryServiceName)
                        .AllocateMemoryWithContext(serializedAndEncryptedBytes.Length, cancellationToken))
                    {
                        result = ExecuteUploadAction(
                            new UploadWithMultipleConnectionsAction(
                                authorizationSessionGenerator(),
                                new MemoryStream(serializedAndEncryptedBytes),
                                fileShard.ID,
                                config.BucketID,
                                DefaultUploadChunkSize,
                                uploadConnections,
                                uploadAttempts,
                                cancellationToken,
                                _ => { } // There is no backoff, so we can just NoOp here
                            ));
                    }
                }
                else
                {
                    result = ExecuteUploadAction(
                       new UploadWithSingleConnectionAction(
                           authorizationSessionGenerator(),
                           config.BucketID,
                           serializedAndEncryptedBytes,
                           fileShard.ID,
                           uploadAttempts,
                           cancellationToken,
                           _ => { } // There is no backoff, so we can just NoOp here
                       ));
                }

                return result;
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
