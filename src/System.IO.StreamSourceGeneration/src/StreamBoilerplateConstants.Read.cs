// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration
{
    internal static partial class StreamBoilerplateConstants
    {
        internal const string ReadBytesTemplate = @"
        public override int Read(byte[] buffer, int offset, int count)
        {{
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();
            {0}
        }}
";
        internal const string ReadByteCallsToReadSpan = @"
            return Read(buffer.AsSpan(offset, count));";
        internal const string ReadByteCallsToReadAsyncByte = @"
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();";
        internal const string ReadByteCallsToReadAsyncMemory = @"
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).GetAwaiter().GetResult();";

        internal const string ReadSpanTemplate = @"
        public override int Read(Span<byte> buffer)
        {{
            EnsureCanRead();
            {0}
        }}
";
        internal const string ReadSpanCallsToReadByte = @"
            return base.Read(buffer);";
        internal const string ReadSpanCallsToReadAsyncByte = @"
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int numRead = ReadAsync(sharedBuffer, 0, buffer.Length).GetAwaiter().GetResult();
                sharedBuffer.AsSpan(0, numRead).CopyTo(buffer);
                return numRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }";

        internal const string ReadSpanCallsToReadAsyncMemory = @"
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int numRead = ReadAsync(sharedBuffer.AsMemory(0, buffer.Length), CancellationToken.None).GetAwaiter().GetResult();
                sharedBuffer.AsSpan(0, numRead).CopyTo(buffer);
                return numRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }";

        internal const string ReadAsyncBytesTemplate = @"
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {{
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();
            {0}
        }}
";
        // TODO: find out the best implementations for each case.
        internal const string ReadAsyncByteCallsToReadByte = @"
            return Task.Run(() => Read(buffer, offset, count), cancellationToken);";
        internal const string ReadAsyncByteCallsToReadSpan = @"
            return Task.Run(() => Read(buffer.AsSpan(offset, count)), cancellationToken);";
        internal const string ReadAsyncByteCallsToReadAsyncMemory = @"
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();";

        internal const string ReadAsyncMemoryTemplate = @"
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {{
            EnsureCanRead();
            {0}
        }}
";
        // TODO: find out the best implementations for each case.
        internal const string ReadAsyncMemoryCallsToReadByte = @"
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
            {
                return new ValueTask<int>(Task.Run(() => Read(array.Array!, array.Offset, array.Count), cancellationToken));
            }

            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            return new ValueTask<int>(Task.Run(() => 
            {
                int bytesRead = Read(sharedBuffer, 0, buffer.Length);
                ArrayPool<byte>.Shared.Return(sharedBuffer);
                return bytesRead;
            }, cancellationToken));";

        internal const string ReadAsyncMemoryCallsToReadSpan = @"
            return base.ReadAsync(buffer, cancellationToken);";
        internal const string ReadAsyncMemoryCallsToReadAsyncByte = @"
            return base.ReadAsync(buffer, cancellationToken);";
    }
}