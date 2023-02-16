namespace System.IO.StreamSourceGeneration
{
    internal static partial class StreamBoilerplateConstants
    {
        // Templates are used with string.Format so we need to escape curly braces by doubling them.
        internal const string WriteByteTemplate = @"
        public override void Write(byte[] buffer, int offset, int count)
        {{
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();
            {0}
        }}
";
        internal const string WriteByteCallsToWriteSpan = @"
            Write(buffer.AsSpan(offset, count));";
        internal const string WriteByteCallsToWriteAsyncByte = @"
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();";
        internal const string WriteByteCallsToWriteAsyncMemory = @"
            WriteAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();";

        internal const string WriteSpanTemplate = @"
        public override void Write(ReadOnlySpan<byte> buffer)
        {{
            EnsureCanWrite();
            {0}
        }}
";
        internal const string WriteSpanCallsToWriteByte = @"
            base.Write(buffer);";
        internal const string WriteSpanCallsToWriteAsyncByte = @"
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
            base.WriteAsync(buffer.ToArray()).GetAwaiter().GetResult();";

        internal const string WriteAsyncByteTemplate = @"
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {{
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();
            return {0}
        }}
";
        // TODO: find out the best implementations for each case.
        internal const string WriteAsyncByteCallsToWriteByte = @"
            Task.Run(() => Write(buffer, offset, count), cancellationToken);";
        internal const string WriteAsyncByteCallsToWriteSpan = @"
            Task.Run(() => Write(buffer.AsSpan(offset, count)), cancellationToken);";
        internal const string WriteAsyncByteCallsToWriteAsyncMemory = @"
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();";

        internal const string WriteAsyncMemoryTemplate = @"
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {{
            EnsureCanWrite();
            {0}
        }}
";
        // TODO: find out the best implementations for each case.
        internal const string WriteAsyncMemoryCallsToWriteByte = @"
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

        internal const string WriteAsyncMemoryCallsToWriteSpan = @"
            return new ValueTask(Task.Run(() => Write(buffer.Span), cancellationToken));";
        internal const string WriteAsyncMemoryCallsToWriteAsyncByte = @"
            return base.WriteAsync(buffer, cancellationToken);";
    }
}