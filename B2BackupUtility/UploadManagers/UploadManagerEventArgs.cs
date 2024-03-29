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

using B2BackblazeBridge.Core;

namespace B2BackupUtility.UploadManagers
{
    /// <summary>
    /// An event that is raised by an Upload Manager
    /// </summary>
    public sealed class UploadManagerEventArgs
    {
        /// <summary>
        /// The upload ID that was assigned to this event
        /// </summary>
        public string UploadID { get; set; }

        /// <summary>
        /// The File Shard piece number
        /// </summary>
        public long FileShardPieceNumber { get; set; }

        /// <summary>
        /// The File Shard ID that was uploaded
        /// </summary>
        public string FileShardID { get; set; }

        /// <summary>
        /// The File Shard SHA-1
        /// </summary>
        public string FileShardSHA1 { get; set; }

        /// <summary>
        /// The upload result, if there is on
        /// </summary>
        public BackblazeB2ActionResult<IBackblazeB2UploadResult> UploadResult { get; set; }

        /// <summary>
        /// A print-friendly name that describes which tier this upload is moving to
        /// </summary>
        public string NewUploadTier { get; set; }
    }
}
