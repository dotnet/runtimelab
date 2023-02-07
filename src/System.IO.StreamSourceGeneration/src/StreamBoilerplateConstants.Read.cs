using System.Diagnostics;

namespace System.IO.StreamSourceGeneration;

internal static partial class StreamBoilerplateConstants
{
    internal static string GetReadMemberToCallForTemplate(string candidateName, string memberToCall)
    {
        return candidateName switch
        {
            StreamMembersConstants.ReadByte => memberToCall switch
            {
                StreamMembersConstants.ReadSpan => ReadByteCallsToReadSpan,
                StreamMembersConstants.ReadAsyncByte => ReadByteCallsToReadAsyncByte,
                StreamMembersConstants.ReadAsyncMemory => ReadByteCallsToReadAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.ReadSpan=> memberToCall switch
            {
                StreamMembersConstants.ReadByte => ReadSpanCallsToReadByte,
                StreamMembersConstants.ReadAsyncByte => ReadSpanCallsToReadAsyncByte,
                StreamMembersConstants.ReadAsyncMemory => ReadSpanCallsToReadAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.ReadAsyncByte => memberToCall switch
            {
                StreamMembersConstants.ReadByte => ReadAsyncByteCallsToReadByte,
                StreamMembersConstants.ReadSpan => ReadAsyncByteCallsToReadSpan,
                StreamMembersConstants.ReadAsyncMemory => ReadAsyncByteCallsToReadAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.ReadAsyncMemory => memberToCall switch
            {
                StreamMembersConstants.ReadByte => ReadAsyncMemoryCallsToReadByte,
                StreamMembersConstants.ReadSpan => ReadAsyncMemoryCallsToReadSpan,
                StreamMembersConstants.ReadAsyncMemory => ReadAsyncMemoryCallsToReadAsyncByte,
                _ => throw new InvalidOperationException()
            },
            _ => throw new InvalidOperationException()
        };
    }

    internal const string ReadByteTemplate = @"
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();

            return {0};
        }
";
    internal const string ReadByteCallsToReadSpan = "Read(buffer.AsSpan(offset, count))";
    internal const string ReadByteCallsToReadAsyncByte = "ReadAsync(buffer, offset, count).GetAwaiter().GetResult()";
    internal const string ReadByteCallsToReadAsyncMemory = "ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult()";

    internal const string ReadSpanTemplate = @"
        public override int Read(Span<byte> buffer)
        {
            EnsureCanRead();

            return {0};
        }
";
    internal const string ReadSpanCallsToReadByte = "base.Read(buffer)";
    internal const string ReadSpanCallsToReadAsyncByte = "ReadAsync(buffer, offset, count).GetAwaiter().GetResult()";
    internal const string ReadSpanCallsToReadAsyncMemory = "ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult()";


    internal const string ReadAsyncByteTemplate = @"
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();

            return {0};
        }
";
    // TODO: find out the best implementations for each case.
    internal const string ReadAsyncByteCallsToReadByte = "Task.Run(() => Read(buffer, offset, count), cancellationToken)";
    internal const string ReadAsyncByteCallsToReadSpan = "Task.Run(() => Read(buffer.AsSpan(offset, count)), cancellationToken)";
    internal const string ReadAsyncByteCallsToReadAsyncMemory = "ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask()";

    internal const string ReadAsyncMemoryTemplate = @"
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureCanRead();

            return {0};
        }
";
    // TODO: find out the best implementations for each case.
    internal const string ReadAsyncMemoryCallsToReadByte = "base.ReadAsync(buffer, cancellationToken)";
    internal const string ReadAsyncMemoryCallsToReadSpan = "base.ReadAsync(buffer, cancellationToken)";
    internal const string ReadAsyncMemoryCallsToReadAsyncByte = "base.ReadAsync(buffer, cancellationToken)";
}
