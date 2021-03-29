// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System
{
    // Warning: Mutable struct!
    // The purpose of this struct is to simplify buffer management.
    // It manages a sliding buffer where bytes can be added at the end and removed at the beginning.
    // [ActiveSpan/Memory] contains the current buffer contents; these bytes will be preserved
    // (copied, if necessary) on any call to EnsureAvailableBytes.
    // [AvailableSpan/Memory] contains the available bytes past the end of the current content,
    // and can be written to in order to add data to the end of the buffer.
    // Commit(byteCount) will extend the ActiveSpan by [byteCount] bytes into the AvailableSpan.
    // Discard(byteCount) will discard [byteCount] bytes as the beginning of the ActiveSpan.

    /// <summary>
    /// Identical to <see cref="ArrayBuffer"/>, but with padded address space to ensure vector operations can read past end of buffer.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal struct VectorArrayBuffer : IDisposable
    {
        // It is assumed that (due to how virtual memory works) we do
        // not need to add padding to the front of the buffer.
        private const int PrefixPaddingLength = 0;
        // Pad the back of the array with enough bytes to ensure we
        // can always safely read an Vector256 at any index within returned spans.
        private static int SuffixPaddingLength => System.Net.Http.LowLevel.Http1Connection.HeaderBufferPadding;
        private static int TotalPaddingLength => PrefixPaddingLength + SuffixPaddingLength;

        private byte[] _bytes;
        private int _activeStart;
        private int _availableStart;

        // Invariants:
        // 0 <= _activeStart <= _availableStart <= (bytes.Length - TotalPaddingLength)

        public VectorArrayBuffer(int initialSize)
        {
            _bytes = Rent(initialSize + TotalPaddingLength);
            _activeStart = 0;
            _availableStart = 0;
        }

        public void Dispose()
        {
            _activeStart = 0;
            _availableStart = 0;

            byte[] array = _bytes;

            if (array != null)
            {
                _bytes = null!;
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        public int ActiveLength => _availableStart - _activeStart;
        public Span<byte> ActiveSpan => new Span<byte>(_bytes, _activeStart + PrefixPaddingLength, ActiveLength);
        public Memory<byte> ActiveMemory => new Memory<byte>(_bytes, _activeStart + PrefixPaddingLength, ActiveLength);

        public int AvailableLength => Capacity - _availableStart;
        public Span<byte> AvailableSpan => new Span<byte>(_bytes, _availableStart + PrefixPaddingLength, AvailableLength);
        public Memory<byte> AvailableMemory => new Memory<byte>(_bytes, _availableStart + PrefixPaddingLength, AvailableLength);

        public int Capacity => _bytes.Length - TotalPaddingLength;

        private static byte[] Rent(int byteCount)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(byteCount);
            array.AsSpan().Clear();

            // zero out the prefix and suffix padding, so they won't match any compares later on.
            if(PrefixPaddingLength != 0)
            {
#pragma warning disable CS0162 // Unreachable code detected
                array.AsSpan(0, PrefixPaddingLength).Clear();
#pragma warning restore CS0162 // Unreachable code detected
            }

            array.AsSpan(array.Length - SuffixPaddingLength, SuffixPaddingLength).Clear();

            return array;
        }

        public void Discard(int byteCount)
        {
            Debug.Assert(byteCount <= ActiveLength, $"Expected {byteCount} <= {ActiveLength}");
            _activeStart += byteCount;

            if (_activeStart == _availableStart)
            {
                _activeStart = 0;
                _availableStart = 0;
            }
        }

        public void Commit(int byteCount)
        {
            Debug.Assert(byteCount <= AvailableLength);
            _availableStart += byteCount;
        }

        // Ensure at least [byteCount] bytes to write to.
        public void EnsureAvailableSpace(int byteCount)
        {
            if (byteCount > AvailableLength)
            {
                EnsureAvailableSpaceSlow(byteCount);
            }
        }

        private void EnsureAvailableSpaceSlow(int byteCount)
        {
            int totalFree = _activeStart + AvailableLength;
            if (byteCount <= totalFree)
            {
                // We can free up enough space by just shifting the bytes down, so do so.
                Buffer.BlockCopy(_bytes, _activeStart + PrefixPaddingLength, _bytes, PrefixPaddingLength, ActiveLength);
                _availableStart = ActiveLength;
                _activeStart = 0;
                Debug.Assert(byteCount <= AvailableLength);
                return;
            }

            // Double the size of the buffer until we have enough space.
            int desiredSize = ActiveLength + byteCount;
            int newSize = Capacity;
            do
            {
                newSize *= 2;
            } while (newSize < desiredSize);
            newSize += TotalPaddingLength;

            byte[] newBytes = Rent(newSize);
            byte[] oldBytes = _bytes;

            if (ActiveLength != 0)
            {
                Buffer.BlockCopy(oldBytes, _activeStart + PrefixPaddingLength, newBytes, PrefixPaddingLength, ActiveLength);
            }

            _availableStart = ActiveLength;
            _activeStart = 0;
            _bytes = newBytes;

            ArrayPool<byte>.Shared.Return(oldBytes);

            Debug.Assert(byteCount <= AvailableLength);
        }
    }
}
