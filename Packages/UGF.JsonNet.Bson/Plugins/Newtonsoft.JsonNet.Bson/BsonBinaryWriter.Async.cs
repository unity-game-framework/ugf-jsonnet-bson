﻿#define HAVE_ASYNC

#region License
// Copyright (c) 2017 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

#if HAVE_ASYNC

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Bson.Utilities;

namespace Newtonsoft.Json.Bson
{
    internal partial class BsonBinaryWriter
    {
        private readonly AsyncBinaryWriter _asyncWriter;

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_asyncWriter == null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return cancellationToken.FromCanceled();
                }

                Flush();
                return AsyncUtils.CompletedTask;
            }

            return _asyncWriter.FlushAsync(cancellationToken);
        }

        public Task WriteTokenAsync(BsonToken t, CancellationToken cancellationToken)
        {
            if (_asyncWriter == null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return cancellationToken.FromCanceled();
                }

                WriteToken(t);
                return AsyncUtils.CompletedTask;
            }

            CalculateSize(t);
            return WriteTokenInternalAsync(t, cancellationToken);
        }

        private Task WriteTokenInternalAsync(BsonToken t, CancellationToken cancellationToken)
        {
            switch (t.Type)
            {
                case BsonType.Object:
                    return WriteObjectAsync((BsonObject)t, cancellationToken);
                case BsonType.Array:
                    return WriteArrayAsync((BsonArray)t, cancellationToken);
                case BsonType.Integer:
                    return _asyncWriter.WriteAsync(Convert.ToInt32(((BsonValue)t).Value, CultureInfo.InvariantCulture), cancellationToken);
                case BsonType.Long:
                    return _asyncWriter.WriteAsync(Convert.ToInt64(((BsonValue)t).Value, CultureInfo.InvariantCulture), cancellationToken);
                case BsonType.Number:
                    return _asyncWriter.WriteAsync(Convert.ToDouble(((BsonValue)t).Value, CultureInfo.InvariantCulture), cancellationToken);
                case BsonType.String:
                    BsonString bsonString = (BsonString)t;
                    return WriteStringAsync((string)bsonString.Value, bsonString.ByteCount, bsonString.CalculatedSize - 4, cancellationToken);
                case BsonType.Boolean:
                    return _asyncWriter.WriteAsync((bool)((BsonValue)t).Value, cancellationToken);
                case BsonType.Null:
                case BsonType.Undefined:
                    return AsyncUtils.CompletedTask;
                case BsonType.Date:
                    BsonValue value = (BsonValue)t;
                    return _asyncWriter.WriteAsync(TicksFromDateObject(value.Value), cancellationToken);
                case BsonType.Binary:
                    return WriteBinaryAsync((BsonBinary)t, cancellationToken);
                case BsonType.Oid:
                    return _asyncWriter.WriteAsync((byte[])((BsonValue)t).Value, cancellationToken);
                case BsonType.Regex:
                    return WriteRegexAsync((BsonRegex)t, cancellationToken);
                default:
                    throw new ArgumentOutOfRangeException(nameof(t), "Unexpected token when writing BSON: {0}".FormatWith(CultureInfo.InvariantCulture, t.Type));
            }
        }

        private async Task WriteObjectAsync(BsonObject value, CancellationToken cancellationToken)
        {
            await _asyncWriter.WriteAsync(value.CalculatedSize, cancellationToken).ConfigureAwait(false);
            foreach (BsonProperty property in value)
            {
                await _asyncWriter.WriteAsync((byte)property.Value.Type, cancellationToken).ConfigureAwait(false);
                await WriteStringAsync((string)property.Name.Value, property.Name.ByteCount, null, cancellationToken).ConfigureAwait(false);
                BsonType propertyType = property.Value.Type;
                if (propertyType != BsonType.Null & propertyType != BsonType.Undefined)
                {
                    await WriteTokenInternalAsync(property.Value, cancellationToken).ConfigureAwait(false);
                }
            }

            await _asyncWriter.WriteAsync((byte)0, cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteArrayAsync(BsonArray value, CancellationToken cancellationToken)
        {
            await _asyncWriter.WriteAsync(value.CalculatedSize, cancellationToken).ConfigureAwait(false);
            ulong index = 0;
            foreach (BsonToken c in value)
            {
                await _asyncWriter.WriteAsync((byte)c.Type, cancellationToken).ConfigureAwait(false);
                await WriteStringAsync(index.ToString(CultureInfo.InvariantCulture), MathUtils.IntLength(index), null, cancellationToken).ConfigureAwait(false);
                BsonType type = c.Type;
                if (type != BsonType.Null & type != BsonType.Undefined)
                {
                    await WriteTokenInternalAsync(c, cancellationToken).ConfigureAwait(false);
                }
                index++;
            }

            await _asyncWriter.WriteAsync((byte)0, cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteBinaryAsync(BsonBinary value, CancellationToken cancellationToken)
        {
            byte[] data = (byte[])value.Value;
            await _asyncWriter.WriteAsync(data.Length, cancellationToken).ConfigureAwait(false);
            await _asyncWriter.WriteAsync((byte)value.BinaryType, cancellationToken).ConfigureAwait(false);
            await _asyncWriter.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteRegexAsync(BsonRegex value, CancellationToken cancellationToken)
        {
            await WriteStringAsync((string)value.Pattern.Value, value.Pattern.ByteCount, null, cancellationToken).ConfigureAwait(false);
            await WriteStringAsync((string)value.Options.Value, value.Options.ByteCount, null, cancellationToken).ConfigureAwait(false);
        }

        private Task WriteStringAsync(string s, int byteCount, int? calculatedlengthPrefix, CancellationToken cancellationToken)
        {
            if (calculatedlengthPrefix != null)
            {
                return WritePrefixedStringAsync(s, byteCount, calculatedlengthPrefix.GetValueOrDefault(), cancellationToken);
            }

            return WriteUtf8BytesAsync(s, byteCount, cancellationToken);
        }

        private async Task WritePrefixedStringAsync(string s, int byteCount, int calculatedlengthPrefix, CancellationToken cancellationToken)
        {
            await _asyncWriter.WriteAsync(calculatedlengthPrefix, cancellationToken).ConfigureAwait(false);
            await WriteUtf8BytesAsync(s, byteCount, cancellationToken).ConfigureAwait(false);
        }

        private Task WriteUtf8BytesAsync(string s, int byteCount, CancellationToken cancellationToken)
        {
            if (s == null)
            {
                return _asyncWriter.WriteAsync((byte)0, cancellationToken);
            }

            if (byteCount <= 255)
            {
                if (_largeByteBuffer == null)
                {
                    _largeByteBuffer = new byte[256];
                }
                else
                {
                    _largeByteBuffer[byteCount] = 0;
                }

                Encoding.GetBytes(s, 0, s.Length, _largeByteBuffer, 0);
                return _asyncWriter.WriteAsync(_largeByteBuffer, 0, byteCount + 1, cancellationToken);
            }

            byte[] bytes = new byte[byteCount + 1];
            Encoding.GetBytes(s, 0, s.Length, bytes, 0);
            return _asyncWriter.WriteAsync(bytes, cancellationToken);
        }
    }
}

#endif
