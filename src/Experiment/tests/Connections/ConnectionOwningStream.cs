using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    internal sealed class ConnectionOwningStream : Stream, IEnhancedStream
    {
        private Connection _connection;
        private Stream _stream;

        public ConnectionOwningStream(Connection connection)
        {
            _connection = connection;
            _stream = connection.Stream;
        }

        public bool CanShutdownWrites => _stream is IEnhancedStream { CanShutdownWrites: true };
        public bool CanScatterGather => _stream is IEnhancedStream { CanScatterGather: true };

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => _stream.CanSeek;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override long Position { get => _stream.Position; set => _stream.Position = value; }

        public override void Flush() => _stream.Flush();

        public void Flush(FlushType flushType)
        {
            if (_stream is IEnhancedStream enhancedStream)
            {
                enhancedStream.Flush(flushType);
            }
            else if (flushType == FlushType.FlushWrites)
            {
                _stream.Flush();
            }
            else if (flushType == FlushType.FlushAndShutdownWrites)
            {
                throw new Exception("Base stream does not support shutdowns.");
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _stream.FlushAsync();

        public ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken = default) =>
            _stream is IEnhancedStream enhancedStream ? enhancedStream.FlushAsync(flushType, cancellationToken) :
            flushType == FlushType.FlushWrites ? new ValueTask(_stream.FlushAsync(cancellationToken)) :
            cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) :
            flushType == FlushType.FlushAndShutdownWrites ? ValueTask.FromException(new Exception("Base stream does not support shutdowns.")) :
            default;

        public override int Read(Span<byte> buffer) =>
            _stream.Read(buffer);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.ReadAsync(buffer, cancellationToken);

        public override int ReadByte() =>
            _stream.ReadByte();

        public override int Read(byte[] buffer, int offset, int count) =>
            _stream.Read(buffer, offset, count);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            _stream.BeginRead(buffer, offset, count, callback, state);

        public override int EndRead(IAsyncResult asyncResult) =>
            _stream.EndRead(asyncResult);

        public int Read(IReadOnlyList<Memory<byte>> buffers) =>
            _stream is IEnhancedStream enhancedStream ? enhancedStream.Read(buffers) :
            buffers is null ? throw new ArgumentNullException(nameof(buffers)) :
            _stream.Read(buffers.Count != 0 ? buffers[0].Span : Array.Empty<byte>());

        public ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default) =>
            _stream is IEnhancedStream enhancedStream ? enhancedStream.ReadAsync(buffers, cancellationToken) :
            buffers is null ? throw new ArgumentNullException(nameof(buffers)) :
            _stream.ReadAsync(buffers.Count != 0 ? buffers[0] : Array.Empty<byte>(), cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            _stream.Seek(offset, origin);

        public override void SetLength(long value) =>
            _stream.SetLength(value);

        public override void Write(ReadOnlySpan<byte> buffer) =>
            _stream.Write(buffer);

        public override void Write(byte[] buffer, int offset, int count) =>
            _stream.Write(buffer, offset, count);

        public void Write(ReadOnlySpan<byte> buffer, FlushType flushType)
        {
            if (_stream is IEnhancedStream enhancedStream)
            {
                enhancedStream.Write(buffer, flushType);
            }
            else
            {
                _stream.Write(buffer);

                if (flushType != FlushType.None)
                {
                    if (flushType == FlushType.FlushAndShutdownWrites) throw new NotSupportedException("Underlying stream does not support shutdowns.");
                    _stream.Flush();
                }
            }
        }

        public void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None)
        {
            if (_stream is IEnhancedStream enhancedStream)
            {
                enhancedStream.Write(buffers, flushType);
            }
            else if (buffers is null)
            {
                throw new ArgumentNullException(nameof(buffers));
            }
            else
            {
                for (int i = 0, count = buffers.Count; i != count; ++i)
                {
                    _stream.Write(buffers[i].Span);
                }

                if (flushType != FlushType.None)
                {
                    if (flushType == FlushType.FlushAndShutdownWrites) throw new NotSupportedException("Underlying stream does not support shutdowns.");
                    _stream.Flush();
                }
            }
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType, CancellationToken cancellationToken = default)
        {
            if (_stream is IEnhancedStream enhancedStream)
            {
                await enhancedStream.WriteAsync(buffer, flushType, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (flushType != FlushType.None)
                {
                    if (flushType == FlushType.FlushAndShutdownWrites) throw new NotSupportedException("Underlying stream does not support shutdowns.");
                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default)
        {
            if (_stream is IEnhancedStream enhancedStream)
            {
                await enhancedStream.WriteAsync(buffers, flushType, cancellationToken).ConfigureAwait(false);
            }
            else if (buffers is null)
            {
                throw new ArgumentNullException(nameof(buffers));
            }
            else
            {
                for (int i = 0, count = buffers.Count; i != count; ++i)
                {
                    await _stream.WriteAsync(buffers[i], cancellationToken).ConfigureAwait(false);
                }

                if (flushType != FlushType.None)
                {
                    if (flushType == FlushType.FlushAndShutdownWrites) throw new NotSupportedException("Underlying stream does not support shutdowns.");
                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.WriteAsync(buffer, cancellationToken);

        public override void WriteByte(byte value) =>
            _stream.WriteByte(value);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            _stream.BeginWrite(buffer, offset, count, callback, state);

        public override void EndWrite(IAsyncResult asyncResult) =>
            _stream.EndWrite(asyncResult);

        public override bool CanTimeout => _stream.CanTimeout;

        public override void CopyTo(Stream destination, int bufferSize) =>
            _stream.CopyTo(destination, bufferSize);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            _stream.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if(disposing) _stream.Dispose();
        }

        public override ValueTask DisposeAsync() =>
            _stream.DisposeAsync();

        public override int ReadTimeout { get => _stream.ReadTimeout; set => _stream.ReadTimeout = value; }

        public override int WriteTimeout { get => _stream.WriteTimeout; set => _stream.WriteTimeout = value; }
    }
}
