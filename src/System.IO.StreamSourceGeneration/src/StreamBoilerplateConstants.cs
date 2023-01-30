namespace System.IO.StreamSourceGeneration;

internal static class StreamBoilerplateConstants
{
    internal const string UsingDirectives = @"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
";

    internal const string CanRead = @"
        public override bool CanRead => true;
";

    internal const string CanSeek = @"
        public override bool CanSeek => true;
";

    internal const string CanWrite = @"
        public override bool CanWrite => true;
";

    internal const string BeginRead = @"
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();
        
            return TaskToApm.Begin(ReadCoreAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask(), callback, state);
        }
";

    internal const string BeginWrite = @"
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();
        
            return TaskToApm.Begin(WriteCoreAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask(), callback, state);
        }
";

    internal const string EndRead = @"
        public override int EndRead(IAsyncResult asyncResult)
        {
            return TaskToApm.End<int>(asyncResult);
        }
";

    internal const string EndWrite = @"
        public override void EndWrite(IAsyncResult asyncResult)
        {
            TaskToApm.End(asyncResult);
        }
";

    internal const string ReadByteArray = @"
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();

            return ReadCore(buffer.AsSpan(offset, count));
        }
";

    internal const string ReadSpan = @"
        public override int Read(Span<byte> buffer)
        {
            EnsureCanRead();

            return ReadCore(buffer);
        }
";

    internal const string ReadAsyncByteArray = @"
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();

            return ReadCoreAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }
";

    internal const string ReadAsyncMemory = @"
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureCanRead();

            return ReadCoreAsync(buffer, cancellationToken);
        }
";

    internal const string Seek = @"
        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureCanSeek();

            long pos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentException(""Invalid seek origin"", nameof(origin))
            };

            if (pos < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
        
            return SeekCore(offset, origin);
        }
";

    internal const string SetLength = @"
        public override void SetLength(long value)
        {
            EnsureCanSeek();
            EnsureCanWrite();

            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            SetLengthCore(value);
        }
";

    internal const string WriteByteArray = @"
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();

            WriteCore(buffer.AsSpan(offset, count));
        }
";

    internal const string WriteSpan = @"
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCanWrite();

            WriteCore(buffer);
        }
";

    internal const string WriteAsyncByteArray = @"
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();

            return WriteCoreAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }
";

    internal const string WriteAsyncMemory = @"
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureCanWrite();

            return WriteCoreAsync(buffer, cancellationToken);
        }
";

    // Unsupported
    internal const string CanReadUnsupported = @"
        public override bool CanRead => false;
";

    internal const string CanSeekUnsupported = @"
        public override bool CanSeek => false;
";

    internal const string CanWriteUnsupported = @"
        public override bool CanWrite => false;
";

    internal const string BeginReadUnsupported = @"
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        throw new NotSupportedException(""Stream does not support reading."");
    }
";

    internal const string BeginWriteUnsupported = @"
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            throw new NotSupportedException(""Stream does not support writting."");
        }
";

    internal const string EndReadUnsupported = @"
        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotSupportedException(""Stream does not support reading."");
        }
";

    internal const string EndWriteUnsupported = @"
        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException(""Stream does not support writting."");
        }
";

    internal const string ReadByteArrayUnsupported = @"
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(""Stream does not support reading."");
        }
";

    internal const string ReadSpanUnsupported = @"
        public override int Read(Span<byte> buffer)
        {
            throw new NotSupportedException(""Stream does not support reading."");
        }
";

    internal const string ReadAsyncByteArrayUnsupported = @"
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException(""Stream does not support reading."");
        }
";

    internal const string ReadAsyncMemoryUnsupported = @"
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(""Stream does not support reading."");
        }
";

    internal const string SeekUnsupported = @"
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException(""Stream does not support seeking."");
        }
";

    internal const string SetLengthUnsupported = @"
        public override void SetLength(long value)
        {
            throw new NotSupportedException(""Stream does not support writting or seeking."");
        }
";

    internal const string WriteByteArrayUnsupported = @"
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(""Stream does not support writting."");
        }
";

    internal const string WriteSpanUnsupported = @"
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException(""Stream does not support writting."");
        }
";

    internal const string WriteAsyncByteArrayUnsupported = @"
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException(""Stream does not support writting."");
        }
";

    internal const string WriteAsyncMemoryUnsupported = @"
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(""Stream does not support writting."");
        }
";

    // Partials
    internal const string ReadCore = @"
        private partial int ReadCore(Span<byte> buffer);
";

    internal const string WriteCore = @"
        private partial void WriteCore(ReadOnlySpan<byte> buffer);
";

    internal const string SeekCore = @"
        private partial long SeekCore(long offset, SeekOrigin origin);
";

    internal const string ReadCoreAsync = @"
        private partial ValueTask<int> ReadCoreAsync(Memory<byte> buffer, CancellationToken cancellationToken);
";

    internal const string WriteCoreAsync = @"
        private partial ValueTask WriteCoreAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
";

    internal const string SetLengthCore = @"
        private partial void SetLengthCore(long value);
";

    // Helpers
    internal const string Helpers = @"
        private void EnsureCanRead()
        {
            if (!CanRead)
            {
                throw new NotSupportedException(""Stream does not support reading."");
            }
        }

        private void EnsureCanWrite()
        {
            if (!CanWrite)
            {
                throw new NotSupportedException(""Stream does not support writting."");
            }
        }

        private void EnsureCanSeek()
        {
            if (!CanSeek)
            {
                throw new NotSupportedException(""Stream does not support seeking."");
            }
        }
";
}
