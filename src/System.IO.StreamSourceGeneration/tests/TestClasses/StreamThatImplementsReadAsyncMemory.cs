// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.IO.StreamSourceGeneration.Tests.TestClasses;

[GenerateStreamBoilerplate]
internal partial class StreamThatImplementsReadAsyncMemory : Stream
{
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Random.Shared.NextBytes(buffer.Span);
        return ValueTask.FromResult(buffer.Length);
    }

    public override void Flush() { }
}
