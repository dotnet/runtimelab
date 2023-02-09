// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.StreamSourceGeneration.Tests.TestClasses;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamSourceGeneration.Tests;

public class GenerateStreamBoilerplateGeneratedCodeTests
{
    [Fact]
    public void StreamThatImplementsNothingThrowsOnAllGeneratedMethods() // Except Flush.
    {
        using Stream s = new StreamThatImplementsNothing();
        byte[] dummyBuffer = Array.Empty<byte>();

        Assert.Throws<NotSupportedException>(() => s.Read(dummyBuffer.AsSpan()));
        Assert.Throws<NotSupportedException>(() => s.Read(dummyBuffer, 0, 0));
        Assert.ThrowsAsync<NotSupportedException>(() => s.ReadAsync(dummyBuffer.AsMemory(), CancellationToken.None).AsTask());
        Assert.ThrowsAsync<NotSupportedException>(() => s.ReadAsync(dummyBuffer, 0, 0, CancellationToken.None));
        Assert.Throws<NotSupportedException>(() => s.BeginRead(dummyBuffer, 0, 0, null, null));
        Assert.Throws<NotSupportedException>(() => s.EndRead(null));

        Assert.Throws<NotSupportedException>(() => s.Write(dummyBuffer.AsSpan()));
        Assert.Throws<NotSupportedException>(() => s.Write(dummyBuffer, 0, 0));
        Assert.ThrowsAsync<NotSupportedException>(() => s.WriteAsync(dummyBuffer.AsMemory(), CancellationToken.None).AsTask());
        Assert.ThrowsAsync<NotSupportedException>(() => s.WriteAsync(dummyBuffer, 0, 0, CancellationToken.None));
        Assert.Throws<NotSupportedException>(() => s.BeginWrite(dummyBuffer, 0, 0, null, null));
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

    [Fact]
    public async Task StreamThatImplementsReadSuccessfullyReads()
    {
        using Stream s = new StreamThatImplementsRead();
        byte[] dummyBuffer = new byte[9];
        int expected = dummyBuffer.Length;

        Assert.Equal(expected, s.Read(dummyBuffer.AsSpan()));
        Assert.Equal(expected, s.Read(dummyBuffer, 0, dummyBuffer.Length));

        Assert.Equal(expected, await s.ReadAsync(dummyBuffer.AsMemory(), CancellationToken.None).AsTask());
        Assert.Equal(expected, await s.ReadAsync(dummyBuffer, 0, dummyBuffer.Length, CancellationToken.None));
        Assert.Equal(expected, await Task.Factory.FromAsync(s.BeginRead, s.EndRead, dummyBuffer, 0, dummyBuffer.Length, null)); // todo find out how to consume IAsyncResult

        Assert.True(s.ReadByte() > -1);
        Assert.True(s.CanRead);
    }
}
