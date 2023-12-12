// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration
{
    internal static partial class StreamBoilerplateConstants
    {
        // Templates are used with string.Format so we need to escape curly braces by doubling them.
        internal const string WriteBytesTemplate = @"
        public override void Write(byte[] buffer, int offset, int count)
        {{
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();
            {0}
        }}
";
        internal const string WriteBytesCallsToWriteSpan = @"
            Write(buffer.AsSpan(offset, count));";
        internal const string WriteBytesCallsToWriteAsyncBytes = @"
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();";
        internal const string WriteBytesCallsToWriteAsyncMemory = @"
            WriteAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();";

        internal const string WriteSpanTemplate = @"
        public override void Write(ReadOnlySpan<byte> buffer)
        {{
            EnsureCanWrite();
            {0}
        }}
";
        internal const string WriteSpanCallsToWriteAsyncBytes = @"
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(sharedBuffer);
                WriteAsync(sharedBuffer, 0, buffer.Length).GetAwaiter().GetResult();
            }
            finally 
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }";
        internal const string WriteSpanCallsToWriteAsyncMemory = @"
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(sharedBuffer);
                WriteAsync(sharedBuffer.AsMemory(0, buffer.Length)).GetAwaiter().GetResult();
            }
            finally 
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }";

        internal const string WriteAsyncBytesTemplate = @"
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {{
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();
            {0}
        }}
";
        internal const string WriteAsyncBytesCallsToWriteBytes = @"
            return Task.Run(() => Write(buffer, offset, count), cancellationToken);";
        internal const string WriteAsyncBytesCallsToWriteSpan = @"
            return Task.Run(() => Write(buffer.AsSpan(offset, count)), cancellationToken);";
        internal const string WriteAsyncBytesCallsToWriteAsyncMemory = @"
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();";

        internal const string WriteAsyncMemoryTemplate = @"
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {{
            EnsureCanWrite();
            {0}
        }}
";
        internal const string WriteAsyncMemoryCallsToWriteBytes = @"
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
            {
                return new ValueTask(Task.Run(() => Write(array.Array!, array.Offset, array.Count), cancellationToken));
            }

            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            buffer.Span.CopyTo(sharedBuffer);
            var vt = new ValueTask(Task.Run(() =>
            {
                Write(sharedBuffer, 0, buffer.Length);
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }, cancellationToken));

            return vt;";

        internal const string WriteByteCallsToWriteSpan = @"
        public override void WriteByte(byte value)
        {
            Span<byte> oneByteArray = stackalloc byte[1];
            oneByteArray[0] = value;
            Write(oneByteArray);
        }
";
    }
}