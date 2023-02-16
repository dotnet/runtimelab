// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration.Tests.TestClasses
{
    [GenerateStreamBoilerplate]
    internal partial class StreamThatImplementsWriteSpan : Stream
    {
        private byte[] _internalBuffer;
        public override long Position { get; set; }

        internal StreamThatImplementsWriteSpan(byte[] internalBuffer)
        {
            _internalBuffer = internalBuffer;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            buffer.CopyTo(_internalBuffer.AsSpan((int)Position));
            Position += buffer.Length;
        }

        public override void Flush() { }
    }
}