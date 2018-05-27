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

using Functional.Maybe;
using System;
using System.Collections.Generic;
using System.Linq;

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// Represents a result from a Backblaze B2 Action
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public sealed class BackblazeB2ActionResult<TResult> : IEquatable<BackblazeB2ActionResult<TResult>>
    {
        #region public properties
        /// <summary>
        /// The Optional result 
        /// </summary>
        public Maybe<TResult> MaybeResult { get; }

        /// <summary>
        /// The actual result; throws on a Nothing option value
        /// </summary>
        public TResult Result { get { return MaybeResult.Value; } }

        /// <summary>
        /// The errors that occurred during this action
        /// </summary>
        public IEnumerable<BackblazeB2ActionErrorDetails> Errors { get; }

        /// <summary>
        /// Gets whether this result has an actual result
        /// </summary>
        public bool HasResult
        {
            get { return MaybeResult.IsSomething() && Errors.Any() == false; }
        }

        /// <summary>
        /// Gets whether this result has any errors
        /// </summary>
        public bool HasErrors
        {
            get { return Errors.Any(); }
        }
        #endregion

        #region ctor
        /// <summary>
        /// Constructs a new BackblazeB2ActionResult to represent a result from a call to the B2 server with a non-error 
        /// </summary>
        /// <param name="result">The result of the call</param>
        public BackblazeB2ActionResult(TResult result)
            : this(result.ToMaybe(), Enumerable.Empty<BackblazeB2ActionErrorDetails>())
        {
        }

        /// <summary>
        /// Constructs a new BackblazeB2ActionResult to represent a result from a call to the B2 server
        /// </summary>
        /// <param name="result">The result of the call</param>
        /// <param name="error">The error that the call produced</param>
        public BackblazeB2ActionResult(Maybe<TResult> result, BackblazeB2ActionErrorDetails error)
            : this(result, new[] { error })
        {
        }

        /// <summary>
        /// Constructs a new BackblazeB2ActionResult to represent a result from a call to the B2 Server
        /// </summary>
        /// <param name="result"></param>
        /// <param name="errors"></param>
        public BackblazeB2ActionResult(Maybe<TResult> result, IEnumerable<BackblazeB2ActionErrorDetails> errors)
        {
            MaybeResult = result;
            Errors = errors;
        }
        #endregion

        #region public methods
        public bool Equals(BackblazeB2ActionResult<TResult> other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            TResult result = MaybeResult.OrElseDefault();
            TResult otherResult = other.MaybeResult.OrElseDefault();

            return Equals(result, otherResult) && Enumerable.SequenceEqual(Errors, other.Errors);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2ActionResult<TResult>);
        }

        public override int GetHashCode()
        {
            TResult result = MaybeResult.OrElseDefault();

            int hashCode = 0;
            if (result != null)
            {
                hashCode ^= result.GetHashCode();
            }

            return Errors.Aggregate(hashCode, (acc, d) => d.GetHashCode() ^ acc);
        }
        #endregion

        #region private methods
        private bool EqualsPreamble(object other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;

            return true;
        }
        #endregion
    }
}
