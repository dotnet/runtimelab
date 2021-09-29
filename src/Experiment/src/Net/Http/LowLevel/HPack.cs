// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Net.Http.LowLevel
{
    internal static class HPack
    {
        public const uint StaticTableMaxIndex = 61;

        private const int DynamicTableSizeMask = 0b0001_1111;
        private const int IndexedHeaderMask = 0b0111_1111;
        public const int IncrementalIndexingMask = 0b0011_1111;
        public const int WithoutIndexingOrNeverIndexMask = 0b0000_1111;

        public static bool TryEncodeDynamicTableSizeUpdate(ulong newSize, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeInteger(0b0010_0000, DynamicTableSizeMask, newSize, buffer, out bytesWritten);

        public static bool TryEncodeIndexedHeader(ulong index, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeInteger(0b1000_0000, IndexedHeaderMask, index, buffer, out bytesWritten);

        public static byte[] EncodeIndexedHeader(ulong index)
        {
            Span<byte> tmp = stackalloc byte[2];
            bool success = TryEncodeIndexedHeader(index, tmp, out int bytesWritten);
            Debug.Assert(success == true);
            return tmp.Slice(0, bytesWritten).ToArray();
        }

        public static bool TryEncodeHeaderWithIncrementalIndexing(ulong nameIndex, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeHeader(0b0100_0000, IncrementalIndexingMask, nameIndex, value, buffer, out bytesWritten);

        public static bool TryEncodeHeaderWithIncrementalIndexing(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeHeader(0b0100_0000, IncrementalIndexingMask, name, value, buffer, out bytesWritten);

        public static bool TryEncodeHeaderWithoutIndexing(ulong nameIndex, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeHeader(0b0000_0000, WithoutIndexingOrNeverIndexMask, nameIndex, value, buffer, out bytesWritten);

        public static bool TryEncodeHeaderWithoutIndexing(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeHeader(0b0000_0000, WithoutIndexingOrNeverIndexMask, name, value, buffer, out bytesWritten);

        public static byte[] EncodeHeaderWithoutIndexing(ulong nameIndex, ReadOnlySpan<byte> value)
        {
            byte[] buffer = new byte[64 + value.Length];

            bool success = TryEncodeHeaderWithoutIndexing(nameIndex, value, buffer, out int actualLenth);
            Debug.Assert(success == true);

            return buffer.AsSpan(0, actualLenth).ToArray();
        }

        public static byte[] EncodeHeaderWithoutIndexing(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            byte[] buffer = new byte[128 + name.Length + value.Length];

            bool success = TryEncodeHeaderWithoutIndexing(name, value, buffer, out int actualLenth);
            Debug.Assert(success == true);

            return buffer.AsSpan(0, actualLenth).ToArray();
        }

        public static bool TryEncodeHeaderNeverIndexed(ulong nameIndex, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeHeader(0b0001_0000, WithoutIndexingOrNeverIndexMask, nameIndex, value, buffer, out bytesWritten);

        public static bool TryEncodeHeaderNeverIndexed(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten) =>
            TryEncodeHeader(0b0001_0000, WithoutIndexingOrNeverIndexMask, name, value, buffer, out bytesWritten);

        private static bool TryEncodeHeader(byte prefixValue, byte prefixMask, ulong nameIndex, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten)
        {
            if (TryEncodeInteger(prefixValue, prefixMask, nameIndex, buffer, out int integerLength) &&
                TryEncodeString(value, buffer.Slice(integerLength), out int valueLiteralLength))
            {
                bytesWritten = integerLength + valueLiteralLength;
                return true;
            }

            bytesWritten = 0;
            return false;
        }

        private static bool TryEncodeHeader(byte prefixValue, byte prefixMask, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten)
        {
            if (TryEncodeInteger(prefixValue, prefixMask, 0, buffer, out int integerLength) &&
                TryEncodeString(name, buffer.Slice(integerLength), out int nameLiteralLength) &&
                TryEncodeString(value, buffer.Slice(integerLength + nameLiteralLength), out int valueLiteralLength))
            {
                bytesWritten = integerLength + nameLiteralLength + valueLiteralLength;
                return true;
            }

            bytesWritten = 0;
            return false;
        }

        private static bool TryEncodeString(ReadOnlySpan<byte> value, Span<byte> buffer, out int bytesWritten)
        {
            if (TryEncodeInteger(0b0000_0000, IndexedHeaderMask, (uint)value.Length, buffer, out int integerLength))
            {
                buffer = buffer.Slice(integerLength);
                if (buffer.Length >= value.Length)
                {
                    value.CopyTo(buffer);
                    bytesWritten = integerLength + value.Length;
                    return true;
                }
            }

            bytesWritten = 0;
            return false;
        }

        private static bool TryEncodeInteger(byte prefixValue, byte prefixMask, ulong value, Span<byte> buffer, out int bytesWritten)
        {
            Debug.Assert(prefixMask != 0);

            if (value < prefixMask)
            {
                if (buffer.Length == 0)
                {
                    goto needMore;
                }

                buffer[0] = (byte)(prefixValue | value);
                bytesWritten = 1;
                return true;
            }

            if (buffer.Length < 2)
            {
                goto needMore;
            }

            value -= prefixMask;

            buffer[0] = (byte)(prefixValue | prefixMask);

            byte x = (byte)(value & 0x7F);

            value >>= 7;
            if (value == 0)
            {
                buffer[1] = x;
                bytesWritten = 2;
                return true;
            }

            buffer[1] = (byte)(x | 0x80);

            uint idx = 2;
            while (true)
            {
                if ((uint)buffer.Length == idx)
                {
                    goto needMore;
                }

                x = (byte)(value & 0x7F);
                value >>= 7;

                if (value == 0)
                {
                    buffer[(int)idx++] = x;
                    bytesWritten = (int)idx;
                    return true;
                }

                buffer[(int)idx++] = (byte)(x | 0x80);
            }

        needMore:
            bytesWritten = 0;
            return false;
        }

        public static bool TryDecodeDynamicTableSizeUpdate(byte firstByte, ReadOnlySpan<byte> buffer, out ulong newSize, out int bytesConsumed) =>
            TryDecodeInteger(DynamicTableSizeMask, firstByte, buffer, out newSize, out bytesConsumed);

        public static bool TryDecodeIndexedHeader(byte firstByte, ReadOnlySpan<byte> buffer, out ulong index, out int bytesConsumed) =>
            TryDecodeInteger(IndexedHeaderMask, firstByte, buffer, out index, out bytesConsumed);

        public static bool TryDecodeHeader(byte prefixMask, HttpHeaderFlags prefixFlags, byte firstByte, ReadOnlySpan<byte> buffer, out ulong nameIndex, out ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value, out HttpHeaderFlags flags, out int bytesConsumed)
        {
            int originalLength = buffer.Length;

            if (TryDecodeInteger(prefixMask, firstByte, buffer, out nameIndex, out int integerLength))
            {
                bool huffmanCoded;

                buffer = buffer.Slice(integerLength);

                if (nameIndex != 0)
                {
                    name = default;
                }
                else if (TryDecodeString(buffer, out huffmanCoded, out name, out int nameLength))
                {
                    buffer = buffer.Slice(nameLength);
                    if (huffmanCoded) prefixFlags |= HttpHeaderFlags.NameHuffmanCoded;
                }
                else
                {
                    goto needMore;
                }

                if (TryDecodeString(buffer, out huffmanCoded, out value, out int valueLength))
                {
                    if (huffmanCoded) prefixFlags |= HttpHeaderFlags.ValueHuffmanCoded;
                    bytesConsumed = originalLength - buffer.Length + valueLength;
                    flags = prefixFlags;
                    return true;
                }
            }

        needMore:
            nameIndex = 0;
            name = default;
            value = default;
            flags = HttpHeaderFlags.None;
            bytesConsumed = 0;
            return false;
        }

        private static bool TryDecodeString(ReadOnlySpan<byte> buffer, out bool huffmanCoded, out ReadOnlySpan<byte> value, out int bytesConsumed)
        {
            if (buffer.Length != 0)
            {
                byte firstByte = buffer[0];
                if (TryDecodeInteger(IndexedHeaderMask, firstByte, buffer, out ulong stringLength, out int integerLength) && (uint)(buffer.Length - integerLength) >= stringLength)
                {
                    huffmanCoded = (firstByte & 0x80) != 0;
                    value = buffer.Slice(integerLength, (int)(uint)stringLength);
                    bytesConsumed = integerLength + (int)(uint)stringLength;
                    return true;
                }
            }

            huffmanCoded = false;
            value = default;
            bytesConsumed = 0;
            return false;
        }

        private static bool TryDecodeInteger(byte prefixMask, byte firstByte, ReadOnlySpan<byte> buffer, out ulong value, out int bytesConsumed)
        {
            Debug.Assert(prefixMask != 0);

            ulong tmpValue = (firstByte & (uint)prefixMask);

            if (tmpValue != prefixMask)
            {
                value = tmpValue;
                bytesConsumed = 1;
                return true;
            }

            uint idx = 1;
            byte b;

            do
            {
                if ((uint)buffer.Length == idx)
                {
                    value = 0;
                    bytesConsumed = 0;
                    return false;
                }

                b = buffer[(int)idx++];
                tmpValue = (tmpValue << 7) + (b & 0x7Fu);
            }
            while ((b & 0x80) != 0);

            if (b == 0)
            {
                throw new Exception("Invalid HPACK: overlong integer.");
            }

            value = tmpValue;
            bytesConsumed = (int)idx;
            return true;
        }

        public static PreparedHeader GetStaticHeader(uint index)
        {
            Debug.Assert(index > 0);
            Debug.Assert(index <= (uint)s_entries.Length);
            return s_entries[index - 1];
        }

        private static readonly PreparedHeader[] s_entries = new[]
        {
            new PreparedHeader(PreparedHeader.PseudoAuthority, string.Empty),
            PreparedHeader.PseudoMethod.Get,
            PreparedHeader.PseudoMethod.Post,
            PreparedHeader.PseudoPath.Root,
            PreparedHeader.PseudoPath.IndexHtml,
            PreparedHeader.PseudoScheme.Http,
            PreparedHeader.PseudoScheme.Https,
            PreparedHeader.PseudoStatus.OK,
            PreparedHeader.PseudoStatus.NoContent,
            PreparedHeader.PseudoStatus.PartialContent,
            PreparedHeader.PseudoStatus.NotModified,
            PreparedHeader.PseudoStatus.BadRequest,
            PreparedHeader.PseudoStatus.NotFound,
            PreparedHeader.PseudoStatus.InternalServerError,
            new PreparedHeader(PreparedHeader.AcceptCharset, string.Empty),
            PreparedHeader.AcceptEncoding.GzipDeflate,
            new PreparedHeader(PreparedHeader.AcceptLanguage, string.Empty),
            new PreparedHeader(PreparedHeader.AcceptRanges, string.Empty),
            new PreparedHeader(PreparedHeader.Accept, string.Empty),
            new PreparedHeader(PreparedHeader.AccessControlAllowOrigin, string.Empty),
            new PreparedHeader(PreparedHeader.Age, string.Empty),
            new PreparedHeader(PreparedHeader.Allow, string.Empty),
            new PreparedHeader(PreparedHeader.Authorization, string.Empty),
            new PreparedHeader(PreparedHeader.CacheControl, string.Empty),
            new PreparedHeader(PreparedHeader.ContentDisposition, string.Empty),
            new PreparedHeader(PreparedHeader.ContentEncoding, string.Empty),
            new PreparedHeader(PreparedHeader.ContentLanguage, string.Empty),
            new PreparedHeader(PreparedHeader.ContentLength, string.Empty),
            new PreparedHeader(PreparedHeader.ContentLocation, string.Empty),
            new PreparedHeader(PreparedHeader.ContentRange, string.Empty),
            new PreparedHeader(PreparedHeader.ContentType, string.Empty),
            new PreparedHeader(PreparedHeader.Cookie, string.Empty),
            new PreparedHeader(PreparedHeader.Date, string.Empty),
            new PreparedHeader(PreparedHeader.ETag, string.Empty),
            new PreparedHeader(PreparedHeader.Expect, string.Empty),
            new PreparedHeader(PreparedHeader.Expires, string.Empty),
            new PreparedHeader(PreparedHeader.From, string.Empty),
            new PreparedHeader(PreparedHeader.Host, string.Empty),
            new PreparedHeader(PreparedHeader.IfMatch, string.Empty),
            new PreparedHeader(PreparedHeader.IfModifiedSince, string.Empty),
            new PreparedHeader(PreparedHeader.IfNoneMatch, string.Empty),
            new PreparedHeader(PreparedHeader.IfRange, string.Empty),
            new PreparedHeader(PreparedHeader.IfUnmodifiedSince, string.Empty),
            new PreparedHeader(PreparedHeader.LastModified, string.Empty),
            new PreparedHeader(PreparedHeader.Link, string.Empty),
            new PreparedHeader(PreparedHeader.Location, string.Empty),
            new PreparedHeader(PreparedHeader.MaxForwards, string.Empty),
            new PreparedHeader(PreparedHeader.ProxyAuthenticate, string.Empty),
            new PreparedHeader(PreparedHeader.ProxyAuthorization, string.Empty),
            new PreparedHeader(PreparedHeader.Range, string.Empty),
            new PreparedHeader(PreparedHeader.Referer, string.Empty),
            new PreparedHeader(PreparedHeader.Refresh, string.Empty),
            new PreparedHeader(PreparedHeader.RetryAfter, string.Empty),
            new PreparedHeader(PreparedHeader.Server, string.Empty),
            new PreparedHeader(PreparedHeader.SetCookie, string.Empty),
            new PreparedHeader(PreparedHeader.StrictTransportSecurity, string.Empty),
            new PreparedHeader(PreparedHeader.TransferEncoding, string.Empty),
            new PreparedHeader(PreparedHeader.UserAgent, string.Empty),
            new PreparedHeader(PreparedHeader.Vary, string.Empty),
            new PreparedHeader(PreparedHeader.Via, string.Empty),
            new PreparedHeader(PreparedHeader.WWWAuthenticate, string.Empty)
        };
    }
}
