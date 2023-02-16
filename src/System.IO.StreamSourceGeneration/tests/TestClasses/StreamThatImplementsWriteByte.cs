// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration.Tests.TestClasses
{
    [GenerateStreamBoilerplate]
    internal partial class StreamThatImplementsWriteByte : Stream
    {
        private byte[] _internalBuffer;
        public override long Position { get; set; }

        internal StreamThatImplementsWriteByte(byte[] internalBuffer)
        {
            _internalBuffer = internalBuffer;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Array.Copy(buffer, offset, _internalBuffer, Position, count);
            Position += count;
        }

        public override void Flush() { }
    }
}