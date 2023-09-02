/* 
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


using Newtonsoft.Json;
using System;
using System.Linq;
using System.Runtime.Serialization;

namespace B2BackupUtility.Database
{
    /// <summary>
    /// Represents a part of a file that fits into a database record
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class FileShard : IEquatable<FileShard>, ISerializable
    {
        #region public properties
        /// <summary>
        /// The piece number of this archive
        /// </summary>
        [JsonProperty(PropertyName = "PieceNumber")]
        public long PieceNumber { get; set; }

        /// <summary>
        /// The ID of this file chunk
        /// </summary>
        [JsonProperty(PropertyName = "ID")]
        public string ID { get; set; }

        /// <summary>
        /// The length of this chunk
        /// </summary>
        [JsonProperty(PropertyName = "Length")]
        public long Length { get; set; }

        /// <summary>
        /// The SHA1 of this chunk
        /// </summary>
        [JsonProperty(PropertyName = "SHA1")]
        public string SHA1 { get; set; }

        /// <summary>
        /// The actual payload of this chunk
        /// </summary>
        [JsonProperty(PropertyName = "Payload")]
        public byte[] Payload { get; set; }
        #endregion

        #region ctor
        /// <summary>
        /// Default constructor
        /// </summary>
        public FileShard()
        {
        }

        /// <summary>
        /// Deserialization constructor
        /// </summary>
        /// <param name="info">The serialization information</param>
        /// <param name="context">The context under which this is deserialized</param>
        public FileShard(SerializationInfo info, StreamingContext context)
        {
            PieceNumber = info.GetInt64("PieceNumber");
            ID = info.GetString("ID");
            Length = info.GetInt64("Length");
            SHA1 = info.GetString("SHA1");
            Payload = (byte[])info.GetValue("Payload", typeof(byte[]));
        }
        #endregion

        #region public methods
        public override bool Equals(object obj)
        {
            return Equals(obj as FileShard);
        }

        public override int GetHashCode()
        {
            return PieceNumber.GetHashCode() ^
                ID?.GetHashCode() ?? 0 ^
                Length.GetHashCode() ^
                SHA1?.GetHashCode() ?? 0 ^
                Payload?.Aggregate(0, (acc, e) => e.GetHashCode() ^ acc) ?? 0;
        }

        public bool Equals(FileShard other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return PieceNumber == other.PieceNumber &&
                string.Equals(ID, other.ID, StringComparison.InvariantCulture) &&
                Length == other.Length &&
                string.Equals(SHA1, other.SHA1, StringComparison.InvariantCultureIgnoreCase) &&
                Enumerable.SequenceEqual(Payload, other.Payload);
        }

        public override string ToString()
        {
            return $"Archive Chunk: {PieceNumber} - {ID} - {SHA1}";
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("PieceNumber", PieceNumber);
            info.AddValue("ID", ID);
            info.AddValue("Length", Length);
            info.AddValue("SHA1", SHA1);
            info.AddValue("Payload", Payload, typeof(byte[]));
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
