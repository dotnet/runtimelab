// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.StreamSourceGeneration.Tests.TestClasses;
using System.Threading;
using Xunit;

namespace System.IO.StreamSourceGeneration.Tests;

public class GenerateStreamBoilerplateGeneratedCodeTests
{
    [Fact]
    public void Stream_CantRead_CantWrite_CantSeek_MembersThrow()
    {
        var s = new StreamThatCantReadCantWriteCantSeek();

        Assert.Throws<NotSupportedException>(() => s.Read(Span<byte>.Empty));
        Assert.Throws<NotSupportedException>(() => s.Read(Array.Empty<byte>(), 0, 0));
        Assert.ThrowsAsync<NotSupportedException>(() => s.ReadAsync(Memory<byte>.Empty, CancellationToken.None).AsTask());
        Assert.ThrowsAsync<NotSupportedException>(() => s.ReadAsync(Array.Empty<byte>(), 0, 0, CancellationToken.None));
        Assert.Throws<NotSupportedException>(() => s.BeginRead(Array.Empty<byte>(), 0, 0, null, null));
        Assert.Throws<NotSupportedException>(() => s.EndRead(null));

        Assert.Throws<NotSupportedException>(() => s.Write(ReadOnlySpan<byte>.Empty));
        Assert.Throws<NotSupportedException>(() => s.Write(Array.Empty<byte>(), 0, 0));
        Assert.ThrowsAsync<NotSupportedException>(() => s.WriteAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None).AsTask());
        Assert.ThrowsAsync<NotSupportedException>(() => s.WriteAsync(Array.Empty<byte>(), 0, 0, CancellationToken.None));
        Assert.Throws<NotSupportedException>(() => s.BeginWrite(Array.Empty<byte>(), 0, 0, null, null));
        Assert.Throws<NotSupportedException>(() => s.EndWrite(null));

        Assert.False(s.CanRead);
        Assert.False(s.CanSeek);
        Assert.False(s.CanWrite);
        Assert.Throws<NotSupportedException>(() => s.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => s.SetLength(0));
        Assert.Throws<NotSupportedException>(() => s.Length);
        Assert.Throws<NotSupportedException>(() => s.Position);
        Assert.Throws<NotSupportedException>(() => s.Position = 0);
    }
}
