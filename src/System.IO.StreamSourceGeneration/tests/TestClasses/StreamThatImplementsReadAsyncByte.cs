// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.IO.StreamSourceGeneration.Tests.TestClasses
{
    [GenerateStreamBoilerplate]
    internal partial class StreamThatImplementsReadAsyncByte : Stream
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Random.Shared.NextBytes(buffer.AsSpan(offset, count));
            return Task.FromResult(count);
        }

        public override void Flush() { }
    }
}