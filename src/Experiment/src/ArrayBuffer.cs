// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System
{
    /// <summary>
    /// A buffer used to assist with parsing/serialization when sending and receiving data.
    /// </summary>
    /// <remarks>
    /// This is a mutable buffer. Copying and disposing twice will corrupt array pool.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    internal struct ArrayBuffer : IDisposable
    {
        private byte[] _bytes;
        private int _activeStart;
        private int _availableStart;

        // Invariants:
        // 0 <= _activeStart <= _availableStart <= bytes.Length

        /// <summary>
        /// Instantiates a new <see cref="ArrayBuffer"/>.
        /// </summary>
        /// <param name="initialSize">The initial size of the buffer.</param>
        public ArrayBuffer(int initialSize)
        {
            _bytes = ArrayPool<byte>.Shared.Rent(initialSize);
            _activeStart = 0;
            _availableStart = 0;
        }

        /// <inheritdoc/>
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

        /// <summary>
        /// The number of bytes committed to the buffer.
        /// </summary>
        public int ActiveLength => _availableStart - _activeStart;

        /// <summary>
        /// The bytes committed to the buffer.
        /// </summary>
        public Span<byte> ActiveSpan => new Span<byte>(_bytes, _activeStart, _availableStart - _activeStart);

        /// <summary>
        /// The bytes committed to the buffer.
        /// </summary>
        public Memory<byte> ActiveMemory => new Memory<byte>(_bytes, _activeStart, _availableStart - _activeStart);

        /// <summary>
        /// The number of free bytes available in the buffer.
        /// </summary>
        public int AvailableLength => _bytes.Length - _availableStart;

        /// <summary>
        /// Free bytes in the buffer.
        /// </summary>
        public Span<byte> AvailableSpan => new Span<byte>(_bytes, _availableStart, AvailableLength);

        /// <summary>
        /// Free bytes in the buffer.
        /// </summary>
        public Memory<byte> AvailableMemory => new Memory<byte>(_bytes, _availableStart, _bytes.Length - _availableStart);

        /// <summary>
        /// The total capacity of the buffer.
        /// </summary>
        public int Capacity => _bytes.Length;

        /// <summary>
        /// Discards a number of active bytes.
        /// </summary>
        /// <param name="byteCount">The number of bytes to discard.</param>
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

        /// <summary>
        /// Commits a number of bytes from Available to Active.
        /// </summary>
        /// <param name="byteCount">The number of bytes to commit.</param>
        public void Commit(int byteCount)
        {
            Debug.Assert(byteCount <= AvailableLength);
            _availableStart += byteCount;
        }

        /// <summary>
        /// Ensures <see cref="AvailableLength"/> is at least <paramref name="byteCount"/>.
        /// </summary>
        /// <param name="byteCount">The minimum number of bytes to make available.</param>
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
            int newSize = _bytes.Length;
            do
            {
                newSize *= 2;
            } while (newSize < desiredSize);

            byte[] newBytes = ArrayPool<byte>.Shared.Rent(newSize);
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
