// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration.Tests.TestClasses;

[GenerateStreamBoilerplate]
internal partial class StreamThatImplementsRead : Stream
{
    public override void Flush() { }

    public override int Read(Span<byte> buffer)
    {
        Random.Shared.NextBytes(buffer);
        return buffer.Length;
    }
}
