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


using Newtonsoft.Json;
using System;
using System.Text;

namespace B2BackblazeBridge.Core
{
    /// <summary>
    /// Represents all of the errors that occured during an Action
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class BackblazeB2ActionErrorDetails : IEquatable<BackblazeB2ActionErrorDetails>
    {
        #region public properties
        /// <summary>
        /// The status code of the HTTP Request
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public int Status { get; set; }

        /// <summary>
        /// The B2 Error Code
        /// </summary>
        [JsonProperty(PropertyName = "code")]
        public string Code { get; set; }

        /// <summary>
        /// A human readable message of what went wrong
        /// </summary>
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
        #endregion

        #region public methods
        public override string ToString()
        {
            return (new StringBuilder())
                .AppendFormat("Status: {0}", Status).AppendLine()
                .AppendFormat("Code: {0}", Code).AppendLine()
                .AppendFormat("Message: {0}", Message).AppendLine()
                .ToString();
        }

        public bool Equals(BackblazeB2ActionErrorDetails other)
        {
            return Status == other.Status &&
                string.Equals(Code, other.Code, StringComparison.Ordinal) &&
                string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BackblazeB2ActionErrorDetails);
        }

        public override int GetHashCode()
        {
            int hashCode = Status.GetHashCode();

            if (Code != null)
            {
                hashCode ^= Code.GetHashCode();
            }

            if (Message != null)
            {
                hashCode ^= Message.GetHashCode();
            }

            return hashCode;
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
