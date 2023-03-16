// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.IO.StreamSourceGeneration.Tests.TestClasses
{
    [GenerateStreamBoilerplate]
    internal partial class StreamThatImplementsWriteAsyncMemory : Stream
    {
        private byte[] _internalBuffer;
        public override long Position { get; set; }

        internal StreamThatImplementsWriteAsyncMemory(byte[] internalBuffer)
        {
            _internalBuffer = internalBuffer;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            buffer.CopyTo(_internalBuffer.AsMemory((int)Position));
            Position += buffer.Length;

            return ValueTask.CompletedTask;
        }
    }
}