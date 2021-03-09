// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Net
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
        // Pad the back of the array with enough bytes to ensure we
        // can always safely read a Vector256 at any index within returned spans.
        // Note: it is assumed that we will always be able to roll back to a 32-byte
        // aligned address, so prefix padding is not needed.
        private static int SuffixPaddingLength => 31;

        private byte[] _bytes;
        private int _activeStart;
        private int _availableStart;

        // Invariants:
        // 0 <= _activeStart <= _availableStart <= (bytes.Length - SuffixPaddingLength)

        public VectorArrayBuffer(int initialSize)
        {
            _bytes = Rent(initialSize + SuffixPaddingLength);
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
        public Span<byte> ActiveSpan => new Span<byte>(_bytes, _activeStart, ActiveLength);
        public Memory<byte> ActiveMemory => new Memory<byte>(_bytes, _activeStart, ActiveLength);

        public int AvailableLength => Capacity - _availableStart;
        public Span<byte> AvailableSpan => new Span<byte>(_bytes, _availableStart, AvailableLength);
        public Memory<byte> AvailableMemory => new Memory<byte>(_bytes, _availableStart, AvailableLength);

        public int Capacity => _bytes.Length - SuffixPaddingLength;

        private static byte[] Rent(int byteCount)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(byteCount);

            // Zero out the suffix padding so it won't match any compares later on.
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
                Buffer.BlockCopy(_bytes, _activeStart, _bytes, 0, ActiveLength);
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
            newSize += SuffixPaddingLength;

            byte[] newBytes = Rent(newSize);
            byte[] oldBytes = _bytes;

            if (ActiveLength != 0)
            {
                Buffer.BlockCopy(oldBytes, _activeStart, newBytes, 0, ActiveLength);
            }

            _availableStart = ActiveLength;
            _activeStart = 0;
            _bytes = newBytes;

            ArrayPool<byte>.Shared.Return(oldBytes);

            Debug.Assert(byteCount <= AvailableLength);
        }
    }
}
