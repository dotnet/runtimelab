// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration
{
    internal static partial class StreamBoilerplateConstants
    {
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
            throw new NotSupportedException(""Stream does not support writing."");
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
            throw new NotSupportedException(""Stream does not support writing."");
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
            throw new NotSupportedException(""Stream does not support writing or seeking."");
        }
";

        internal const string WriteByteArrayUnsupported = @"
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(""Stream does not support writing."");
        }
";

        internal const string WriteSpanUnsupported = @"
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException(""Stream does not support writing."");
        }
";

        internal const string WriteAsyncByteArrayUnsupported = @"
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException(""Stream does not support writing."");
        }
";

        internal const string WriteAsyncMemoryUnsupported = @"
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(""Stream does not support writing."");
        }
";

        internal const string PositionUnsupported = @"
        public override long Position
        {
            get => throw new NotSupportedException(""Stream does not support seeking."");
            set => throw new NotSupportedException(""Stream does not support seeking."");
        }
";

        internal const string LengthUnsupported = @"
        public override long Length => throw new NotSupportedException(""Stream does not support seeking."");
";
    }
}