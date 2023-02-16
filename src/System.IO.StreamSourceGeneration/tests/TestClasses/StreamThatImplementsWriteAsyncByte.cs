// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.IO.StreamSourceGeneration.Tests.TestClasses
{
    [GenerateStreamBoilerplate]
    internal partial class StreamThatImplementsWriteAsyncByte : Stream
    {
        private byte[] _internalBuffer;
        public override long Position { get; set; }

        internal StreamThatImplementsWriteAsyncByte(byte[] internalBuffer)
        {
            _internalBuffer = internalBuffer;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            Array.Copy(buffer, offset, _internalBuffer, Position, count);
            Position += count;

            return Task.CompletedTask;
        }

        public override void Flush() { }
    }
}