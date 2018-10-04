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
using B2BackupUtility.Database;
using System;
using System.Collections.Concurrent;

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
        }
        #endregion

        #region private fields
        private const int FastLaneUploadConnections = 20;
        private const int FastLaneUploadAttempts = 1;

        private const int MidLaneUploadConnections = 5;
        private const int MidLaneUploadAttempts = 3;

        private const int SlowLandUploadAttempts = 10;

        private readonly Func<BackblazeB2AuthorizationSession> _authorizationSessionGenerator;
        private readonly BlockingCollection<UploadJob> _fastLane;
        private readonly BlockingCollection<UploadJob> _midLane;
        private readonly BlockingCollection<UploadJob> _slowLane;
        #endregion

        #region public events
        public event EventHandler<UploadManagerEventArgs> OnUploadFinished;

        public event EventHandler<UploadManagerEventArgs> OnUploadTierChanged;

        public event EventHandler<UploadManagerEventArgs> OnUploadFailed;
        #endregion

        #region ctor
        public TieredUploadManager(
            Func<BackblazeB2AuthorizationSession> authorizationSessionGenerator
        )
        {
            _authorizationSessionGenerator = authorizationSessionGenerator;
            _fastLane = new BlockingCollection<UploadJob>();
            _midLane = new BlockingCollection<UploadJob>();
            _slowLane = new BlockingCollection<UploadJob>();
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
            throw new NotImplementedException();
        }
        #endregion

        #region private methods
        private void ExecuteFastLane()
        {
        }

        private void ExecuteMidLane()
        {
        }

        private void ExecuteSlowLane()
        {
        }

        private void ExecuteLaneImpl()
        {
        }
        #endregion
    }
}
