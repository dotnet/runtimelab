// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;

namespace System.IO
{

    /// <summary>
    /// Used for transforming a pinned <see cref="Span{T}"/> into a <see cref="Memory{T}"/>.
    /// </summary>
    internal sealed unsafe class UnsafeSpanWrappingMemoryOwner : MemoryManager<byte>
    {
        private byte* _ptr;
        private int _len;

        public UnsafeSpanWrappingMemoryOwner()
        {
        }

        public void SetPointer(byte* ptr, int len)
        {
            _ptr = ptr;
            _len = len;
        }

        public override Span<byte> GetSpan() =>
            new Span<byte>(_ptr, _len);

        public override MemoryHandle Pin(int elementIndex = 0) =>
            new MemoryHandle(_ptr + elementIndex);

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
