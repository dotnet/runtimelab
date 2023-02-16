// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.StreamSourceGeneration.Tests.TestClasses;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamSourceGeneration.Tests
{
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
        public async Task StreamThatImplementsReadSuccessfullyReads() // Add the other read streams 
        {
            foreach (Stream s in new Stream[] {
            new StreamThatImplementsReadSpan(),
            new StreamThatImplementsReadByte(),
            new StreamThatImplementsReadAsyncMemory(),
            new StreamThatImplementsReadAsyncByte() })
            {
                byte[] dummyBuffer = new byte[10];
                int expected = dummyBuffer.Length;

                Assert.Equal(expected, s.Read(dummyBuffer.AsSpan()));
                Assert.Equal(expected, s.Read(dummyBuffer, 0, dummyBuffer.Length));

                Assert.Equal(expected, await s.ReadAsync(dummyBuffer.AsMemory(), CancellationToken.None).AsTask());
                Assert.Equal(expected, await s.ReadAsync(dummyBuffer, 0, dummyBuffer.Length, CancellationToken.None));
                Assert.Equal(expected, await Task.Factory.FromAsync(s.BeginRead, s.EndRead, dummyBuffer, 0, dummyBuffer.Length, null)); // todo find out how to consume IAsyncResult

                Assert.True(s.ReadByte() > -1);
                Assert.True(s.CanRead);

                s.Dispose();
            }
        }

        [Fact]
        public async Task StreamThatImplementsWriteSuccessfullyWrites() // Add the other write streams
        {
            byte[] dummyBuffer;
            byte[] streamBuffer = new byte[1024];

            foreach (Stream s in new Stream[] {
            new StreamThatImplementsWriteSpan(streamBuffer),
            new StreamThatImplementsWriteByte(streamBuffer),
            new StreamThatImplementsWriteAsyncMemory(streamBuffer),
            new StreamThatImplementsWriteAsyncByte(streamBuffer), })
            {
                dummyBuffer = new byte[10];

                Random.Shared.NextBytes(dummyBuffer);
                s.Write(dummyBuffer.AsSpan());
                AssertStreamBufferSequenceEqual();

                Random.Shared.NextBytes(dummyBuffer);
                s.Write(dummyBuffer, 0, dummyBuffer.Length);
                AssertStreamBufferSequenceEqual();

                Random.Shared.NextBytes(dummyBuffer);
                await s.WriteAsync(dummyBuffer, 0, dummyBuffer.Length);
                AssertStreamBufferSequenceEqual();

                Random.Shared.NextBytes(dummyBuffer);
                await s.WriteAsync(dummyBuffer);
                AssertStreamBufferSequenceEqual();


                Random.Shared.NextBytes(dummyBuffer);
                await Task.Factory.FromAsync(s.BeginWrite, s.EndWrite, dummyBuffer, 0, dummyBuffer.Length, null);
                AssertStreamBufferSequenceEqual();

                byte randomByte = (byte)Random.Shared.Next();
                s.WriteByte(randomByte);
                dummyBuffer = new byte[] { randomByte };

                Assert.True(s.CanWrite);

                s.Dispose();

                void AssertStreamBufferSequenceEqual()
                {
                    int start = (int)s.Position - dummyBuffer.Length;
                    ReadOnlySpan<byte> dummyBufferSpan = dummyBuffer;
                    ReadOnlySpan<byte> streamBufferSpan = streamBuffer.AsSpan(start, dummyBuffer.Length);

                    Assert.True(dummyBufferSpan.SequenceEqual(streamBufferSpan));
                }
            }
        }

        [Fact]
        public void StreamThatImplementsSeekSuccessfullySeeks()
        {
            long position = 0;
            long length = 42;
            Stream s = new StreamThatImplementsSeek(position: position, lenght: length);

            Assert.True(s.CanSeek);
            Assert.Equal(position, s.Position);
            Assert.Equal(length, s.Length);

            position = s.Seek(10, SeekOrigin.Begin);
            Assert.Equal(10, position);
            Assert.Equal(10, s.Position);
            Assert.Equal(42, s.Length);

            position = s.Seek(5, SeekOrigin.Current);
            Assert.Equal(15, position);
            Assert.Equal(15, s.Position);
            Assert.Equal(42, s.Length);

            position = s.Seek(10, SeekOrigin.End);
            Assert.Equal(52, position);
            Assert.Equal(52, s.Position);
            Assert.Equal(52, s.Length);
        }
    }
}