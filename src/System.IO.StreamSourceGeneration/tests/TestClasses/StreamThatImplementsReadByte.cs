// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration.Tests.TestClasses
{
    [GenerateStreamBoilerplate]
    internal partial class StreamThatImplementsReadByte : Stream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            Random.Shared.NextBytes(buffer.AsSpan(offset, count));
            return count;
        }
    }
}