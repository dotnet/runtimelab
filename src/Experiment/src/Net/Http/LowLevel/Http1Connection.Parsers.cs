// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace System.Net.Http.LowLevel
{
    public partial class Http1Connection
    {
        /// <summary>
        /// The amount of padding, in terms of valid address space after the buffer header parsing is done from.
        /// </summary>
        public static int HeaderBufferPadding => Vector256<byte>.Count - 1;

        partial void ProcessKnownHeaders(ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReadHeadersImpl(Span<byte> buffer, IHttpHeadersSink headersSink, object? state, out int bytesConsumed)
        {
            if (Avx2.IsSupported) return ReadHeadersAvx2(buffer, headersSink, state, out bytesConsumed);
            return ReadHeadersPortable(buffer, headersSink, state, out bytesConsumed);
        }

        internal unsafe bool ReadHeadersPortable(Span<byte> buffer, IHttpHeadersSink headersSink, object? state, out int bytesConsumed)
        {
            int originalBufferLength = buffer.Length;

            while (true)
            {
                if (buffer.Length == 0) goto needMore;

                int colIdx = buffer.IndexOfAny((byte)':', (byte)'\n');
                if (colIdx == -1) goto needMore;

                if (buffer[colIdx] == '\n')
                {
                    bytesConsumed = originalBufferLength - buffer.Length + colIdx + 1;
                    return true;
                }

                ReadOnlySpan<byte> headerName = buffer.Slice(0, colIdx);

                int lfIdx = colIdx;

                // Skip OWS.
                byte ch;
                do
                {
                    if (++lfIdx == buffer.Length) goto needMore;
                    ch = buffer[lfIdx];
                } while (ch == ' ' || ch == '\t');

                Span<byte> valueStart = buffer.Slice(lfIdx);
                Span<byte> valueIter = valueStart;

                while (true)
                {
                    lfIdx = valueIter.IndexOf((byte)'\n');
                    if (lfIdx == -1 || (lfIdx + 1) == valueIter.Length) goto needMore;

                    int crLfIdx = lfIdx != 0 && valueIter[lfIdx - 1] == '\r'
                        ? lfIdx - 1
                        : lfIdx;

                    if (lfIdx + 1 >= valueIter.Length)
                        goto needMore;

                    // Check if header continues on the next line.
                    byte ht = valueIter[lfIdx + 1];
                    if (ht == '\t' || ht == ' ')
                    {
                        // Replace CRLFHT with SPSPSP and loop.
                        valueIter[crLfIdx] = (byte)' ';
                        valueIter[lfIdx] = (byte)' ';
                        valueIter[lfIdx + 1] = (byte)' ';
                        valueIter = valueIter.Slice(lfIdx + 2);
                        continue;
                    }

                    ReadOnlySpan<byte> headerValue = valueStart.Slice(0, valueStart.Length - valueIter.Length + crLfIdx);

                    headersSink.OnHeader(state, headerName, headerValue);
                    ProcessKnownHeaders(headerName, headerValue);

                    buffer = valueStart.Slice(valueStart.Length - valueIter.Length + lfIdx + 1);
                    break;
                }
            }

        needMore:
            bytesConsumed = originalBufferLength - buffer.Length;
            return false;
        }

        /// <remarks>
        /// This method REQUIRES (32-1) bytes of valid address space after <paramref name="buffer"/>.
        /// It also assumes that it can always step backwards to a 32-byte aligned address.
        /// It is built to be used with VectorArrayBuffer, which does this.
        /// </remarks>
        internal unsafe bool ReadHeadersAvx2(Span<byte> buffer, IHttpHeadersSink headersSink, object? state, out int bytesConsumed)
        {
            if (buffer.Length == 0)
            {
                bytesConsumed = 0;
                return false;
            }

            Vector256<byte> maskCol = Vector256.Create((byte)':');
            Vector256<byte> maskLF = Vector256.Create((byte)'\n');

            int nameStartIdx = 0;

            fixed (byte* vectorBegin = buffer)
            {
                // align to a 32 byte address for optimal loads.
                byte* vectorEnd = vectorBegin + buffer.Length;
                byte* vectorIter = (byte*)((nint)vectorBegin & ~(32 - 1));

                Vector256<byte> vector = Avx.LoadAlignedVector256(vectorIter);
                uint foundLF = (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, maskLF));
                uint foundCol = foundLF | (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, maskCol));

                while (true)
                {
                    int colIdx;
                    do
                    {
                        while ((colIdx = BitOperations.TrailingZeroCount(foundCol)) == Vector256<byte>.Count)
                        {
                            vectorIter += Vector256<byte>.Count;
                            if (vectorIter >= vectorEnd)
                            {
                                bytesConsumed = nameStartIdx;
                                return false;
                            }

                            // vectorIter may be past the end of buffer[^1]. This is why padding is required.

                            vector = Avx.LoadAlignedVector256(vectorIter);
                            foundLF = (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, maskLF));
                            foundCol = foundLF | (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, maskCol));
                        }

                        foundCol ^= 1u << colIdx;
                        colIdx = (int)(vectorIter - vectorBegin) + colIdx;
                    }
                    while (colIdx < nameStartIdx);

                    if (colIdx >= buffer.Length)
                    {
                        bytesConsumed = nameStartIdx;
                        return false;
                    }

                    if (buffer[colIdx] == '\n')
                    {
                        bytesConsumed = colIdx + 1;
                        return true;
                    }

                    // Skip OWS.
                    int valueStartIdx = colIdx;
                    byte ch;
                    do
                    {
                        if (++valueStartIdx == buffer.Length)
                        {
                            bytesConsumed = nameStartIdx;
                            return false;
                        }
                        ch = buffer[valueStartIdx];
                    } while (ch == ' ' || ch == '\t');

                    int crlfIdx;
                    int lfIdx;
                    while (true)
                    {
                        do
                        {
                            while ((lfIdx = BitOperations.TrailingZeroCount(foundLF)) == Vector256<byte>.Count)
                            {
                                vectorIter += Vector256<byte>.Count;
                                if (vectorIter >= vectorEnd)
                                {
                                    bytesConsumed = nameStartIdx;
                                    return false;
                                }

                                vector = Avx.LoadAlignedVector256(vectorIter);
                                foundLF = (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, maskLF));
                                foundCol = foundLF | (uint)Avx2.MoveMask(Avx2.CompareEqual(vector, maskCol));
                            }

                            uint clearMask = ~(1u << lfIdx);
                            foundLF &= clearMask;
                            foundCol &= clearMask;
                            lfIdx = (int)(vectorIter - vectorBegin) + lfIdx;
                        }
                        while (lfIdx < colIdx);

                        if (lfIdx >= buffer.Length)
                        {
                            bytesConsumed = nameStartIdx;
                            return false;
                        }

                        // Check if header continues on the next line.

                        crlfIdx = lfIdx != 0 && buffer[lfIdx - 1] == '\r'
                            ? lfIdx - 1
                            : lfIdx;

                        if (lfIdx + 1 == buffer.Length)
                        {
                            bytesConsumed = nameStartIdx;
                            return false;
                        }

                        byte ht = buffer[lfIdx + 1];
                        if (ht == '\t' || ht == ' ')
                        {
                            buffer[crlfIdx] = (byte)' ';
                            buffer[lfIdx] = (byte)' ';
                            buffer[lfIdx + 1] = (byte)' ';
                            continue;
                        }
                        break;
                    };

                    Span<byte> name = buffer[nameStartIdx..colIdx];
                    Span<byte> value = buffer[valueStartIdx..crlfIdx];

                    nameStartIdx = lfIdx + 1;
                    headersSink.OnHeader(state, name, value);
                    ProcessKnownHeaders(name, value);
                }
            }
        }
    }
}
