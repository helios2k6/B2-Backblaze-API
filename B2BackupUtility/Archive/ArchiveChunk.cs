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

using System;
using System.Linq;
using System.Runtime.Serialization;

namespace B2BackupUtility.Archive
{
    /// <summary>
    /// Represents a single archive chunk that can be combined with other archive chunks
    /// to generate the original Archive
    /// </summary>
    [Serializable]
    public sealed class ArchiveChunk : IEquatable<ArchiveChunk>, ISerializable
    {
        #region private fields
        private static string ChunkNumberPropertyName => "Chunk Number";

        private static string FileNamePropertyName => "File Name";

        private static string LengthPropertyName => "Length";

        private static string SHA1PropertyName => "SHA1";

        private static string PayloadPropertyName => "Payload";
        #endregion

        #region public properties
        /// <summary>
        /// The piece number of this archive
        /// </summary>
        public long ChunkNumber { get; set; }

        /// <summary>
        /// The file name of the original file
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// The length of this chunk
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// The SHA1 of this chunk
        /// </summary>
        public string SHA1 { get; set; }

        /// <summary>
        /// The actual payload of this chunk
        /// </summary>
        public byte[] Payload { get; set; }
        #endregion

        #region ctor
        /// <summary>
        /// Default constructor
        /// </summary>
        public ArchiveChunk()
        {
        }

        /// <summary>
        /// Deserialization constructor
        /// </summary>
        /// <param name="info">The serialization information</param>
        /// <param name="context">The context under which this is deserialized</param>
        public ArchiveChunk(SerializationInfo info, StreamingContext context)
        {
            ChunkNumber = info.GetInt64(ChunkNumberPropertyName);
            FileName = info.GetString(FileNamePropertyName);
            Length = info.GetInt64(LengthPropertyName);
            SHA1 = info.GetString(SHA1PropertyName);
            Payload = (byte[])info.GetValue(PayloadPropertyName, typeof(byte[]));
        }
        #endregion

        #region public methods
        public override bool Equals(object obj)
        {
            return Equals(obj as ArchiveChunk);
        }

        public override int GetHashCode()
        {
            return ChunkNumber.GetHashCode() ^
                FileName?.GetHashCode() ?? 0 ^
                Length.GetHashCode() ^
                SHA1?.GetHashCode() ?? 0 ^
                Payload?.Aggregate(0, (acc, e) => e.GetHashCode() ^ acc) ?? 0;
        }

        public bool Equals(ArchiveChunk other)
        {
            if (EqualsPreamble(other) == false)
            {
                return false;
            }

            return ChunkNumber == other.ChunkNumber &&
                string.Equals(FileName, other.FileName, StringComparison.InvariantCulture) &&
                Length == other.Length &&
                string.Equals(SHA1, other.SHA1, StringComparison.InvariantCultureIgnoreCase) &&
                Enumerable.SequenceEqual(Payload, other.Payload);
        }

        public override string ToString()
        {
            return $"Archive Chunk: {ChunkNumber} - {FileName} - {SHA1}";
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(ChunkNumberPropertyName, ChunkNumber);
            info.AddValue(FileNamePropertyName, FileName);
            info.AddValue(LengthPropertyName, Length);
            info.AddValue(SHA1PropertyName, SHA1);
            info.AddValue(PayloadPropertyName, Payload, typeof(byte[]));
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
