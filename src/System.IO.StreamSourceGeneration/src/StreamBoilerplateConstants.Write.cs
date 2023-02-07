namespace System.IO.StreamSourceGeneration;

internal static partial class StreamBoilerplateConstants
{
    internal static string GetWriteMemberToCallForTemplate(string candidateName, string memberToCall)
    {
        return candidateName switch
        {
            StreamMembersConstants.WriteByte => memberToCall switch
            {
                StreamMembersConstants.WriteSpan => WriteByteCallsToWriteSpan,
                StreamMembersConstants.WriteAsyncByte => WriteByteCallsToWriteAsyncByte,
                StreamMembersConstants.WriteAsyncMemory => WriteByteCallsToWriteAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.WriteSpan => memberToCall switch
            {
                StreamMembersConstants.WriteByte => WriteSpanCallsToWriteByte,
                StreamMembersConstants.WriteAsyncByte => WriteSpanCallsToWriteAsyncByte,
                StreamMembersConstants.WriteAsyncMemory => WriteSpanCallsToWriteAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.WriteAsyncByte => memberToCall switch
            {
                StreamMembersConstants.WriteByte => WriteAsyncByteCallsToWriteByte,
                StreamMembersConstants.WriteSpan => WriteAsyncByteCallsToWriteSpan,
                StreamMembersConstants.WriteAsyncMemory => WriteAsyncByteCallsToWriteAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.WriteAsyncMemory => memberToCall switch
            {
                StreamMembersConstants.WriteByte => WriteAsyncMemoryCallsToWriteByte,
                StreamMembersConstants.WriteSpan => WriteAsyncMemoryCallsToWriteSpan,
                StreamMembersConstants.WriteAsyncMemory => WriteAsyncMemoryCallsToWriteAsyncByte,
                _ => throw new InvalidOperationException()
            },
            _ => throw new InvalidOperationException()
        };
    }

    internal const string WriteByteTemplate = @"
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();

            {0};
        }
";
    internal const string WriteByteCallsToWriteSpan = "Read(buffer.AsSpan(offset, count))";
    internal const string WriteByteCallsToWriteAsyncByte = "ReadAsync(buffer, offset, count).GetAwaiter().GetResult()";
    internal const string WriteByteCallsToWriteAsyncMemory = "ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult()";

    internal const string WriteSpanTemplate = @"
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCanWrite();

            {0};
        }
";
    internal const string WriteSpanCallsToWriteByte = "base.Read(buffer)";
    internal const string WriteSpanCallsToWriteAsyncByte = "ReadAsync(buffer, offset, count).GetAwaiter().GetResult()";
    internal const string WriteSpanCallsToWriteAsyncMemory = "ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult()";

    internal const string WriteAsyncByteTemplate = @"
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();

            return {0};
        }
";
    // TODO: find out the best implementations for each case.
    internal const string WriteAsyncByteCallsToWriteByte = "Task.Run(() => Read(buffer, offset, count), cancellationToken)";
    internal const string WriteAsyncByteCallsToWriteSpan = "Task.Run(() => Read(buffer.AsSpan(offset, count)), cancellationToken)";
    internal const string WriteAsyncByteCallsToWriteAsyncMemory = "ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask()";

    internal const string WriteAsyncMemoryTemplate = @"
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureCanWrite();

            return {0};
        }
";
    // TODO: find out the best implementations for each case.
    internal const string WriteAsyncMemoryCallsToWriteByte = "base.ReadAsync(buffer, cancellationToken)";
    internal const string WriteAsyncMemoryCallsToWriteSpan = "base.ReadAsync(buffer, cancellationToken)";
    internal const string WriteAsyncMemoryCallsToWriteAsyncByte = "base.ReadAsync(buffer, cancellationToken)";
}
