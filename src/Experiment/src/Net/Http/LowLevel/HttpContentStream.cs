using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A <see cref="Stream"/> that reads/writes content over a <see cref="ValueHttpRequest"/>.
    /// </summary>
    public class HttpContentStream : Stream, IEnhancedStream
    {
        protected internal ValueHttpRequest _request;

        private readonly bool _ownsRequest;
        private UnsafeSpanWrappingMemoryOwner? _spanReadWrapper, _spanWriteWrapper;
        private StreamState _readState;
        private bool _completed;

        /// <summary>
        /// The <see cref="ValueHttpRequest"/> being operated on.
        /// </summary>
        public ValueHttpRequest Request => _request;

        /// <inheritdoc/>
        public virtual bool CanShutdownWrites => true;

        /// <inheritdoc/>
        public virtual bool CanScatterGather => true;

        /// <inheritdoc/>
        public override bool CanRead => _readState < StreamState.EndOfStream;

        /// <inheritdoc/>
        public override bool CanWrite => _readState != StreamState.Disposed;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override long Length => throw new InvalidOperationException();

        /// <inheritdoc/>
        public override long Position { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }

        /// <summary>
        /// Instantiates a new <see cref="HttpContentStream"/>.
        /// </summary>
        /// <param name="request">The <see cref="ValueHttpRequest"/> to operate on.</param>
        /// <param name="ownsRequest">If true, the <paramref name="request"/> will be disposed once the <see cref="HttpContentStream"/> is disposed.</param>
        public HttpContentStream(ValueHttpRequest request, bool ownsRequest)
        {
            _request = request;
            _ownsRequest = ownsRequest;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Tools.BlockForResult(DisposeAsync());
            }
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (_readState != StreamState.Disposed)
            {
                if (_ownsRequest)
                {
                    if (!_completed)
                    {
                        try
                        {
                            // Drain. As we don't have connection factory we darin instead of close connection.
                            // await _request.DrainAsync().ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // DisposeAsync should not throw.
                        }
                    }

                    await _request.DisposeAsync().ConfigureAwait(false);
                }

                _readState = StreamState.Disposed;
            }
        }

        /// <inheritdoc/>
        public sealed override void Flush() =>
            Flush(FlushType.FlushWrites);

        /// <inheritdoc/>
        public virtual void Flush(FlushType flushType) =>
            Tools.BlockForResult(FlushAsync(flushType));

        /// <inheritdoc/>
        public sealed override Task FlushAsync(CancellationToken cancellationToken) =>
            FlushAsync(FlushType.FlushWrites, cancellationToken).AsTask();

        /// <inheritdoc/>
        public virtual async ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken = default)
        {
            if (_readState == StreamState.Disposed)
            {
                throw new ObjectDisposedException(nameof(HttpContentStream));
            }

            if (flushType != FlushType.None)
            {
                try
                {
                    await _request.FlushContentAsync(cancellationToken).ConfigureAwait(false);
                    if (flushType == FlushType.FlushAndShutdownWrites)
                    {
                        await _request.CompleteRequestAsync(cancellationToken).ConfigureAwait(false);
                        _completed = true;
                    }
                }
                catch (Exception ex)
                {
                    throw new IOException(ex.Message, ex);
                }
            }
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new InvalidOperationException();

        /// <inheritdoc/>
        public override void SetLength(long value) =>
            throw new InvalidOperationException();

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            switch (_readState)
            {
                case StreamState.EndOfStream:
                    return 0;
                case StreamState.Disposed:
                    throw new ObjectDisposedException(GetType().Name);
            }

            try
            {
                int len;

                while((len = await _request.ReadContentAsync(buffer, cancellationToken).ConfigureAwait(false)) == 0)
                {
                    if (!await _request.ReadToNextContentAsync(cancellationToken).ConfigureAwait(false))
                    {
                        _readState = StreamState.EndOfStream;
                        return 0;
                    }
                }

                return len;
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default)
        {
            switch (_readState)
            {
                case StreamState.EndOfStream:
                    return 0;
                case StreamState.Disposed:
                    throw new ObjectDisposedException(GetType().Name);
            }

            try
            {
                int len;

                while ((len = await _request.ReadContentAsync(buffers, cancellationToken).ConfigureAwait(false)) == 0)
                {
                    if (!await _request.ReadToNextContentAsync(cancellationToken).ConfigureAwait(false))
                    {
                        _readState = StreamState.EndOfStream;
                        return 0;
                    }
                }

                return len;
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc/>
        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            TaskToApm.Begin(ReadAsync(buffer, offset, count), callback, state);

        /// <inheritdoc/>
        public sealed override int EndRead(IAsyncResult asyncResult) =>
            TaskToApm.End<int>(asyncResult);

        public override unsafe int Read(Span<byte> buffer)
        {
            _spanReadWrapper ??= new UnsafeSpanWrappingMemoryOwner();

            fixed (byte* ptr = buffer)
            {
                _spanReadWrapper.SetPointer(ptr, buffer.Length);
                return Tools.BlockForResult(ReadAsync(_spanReadWrapper.Memory));
            }
        }

        /// <inheritdoc/>
        public sealed override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        public virtual int Read(IReadOnlyList<Memory<byte>> buffers) =>
            Tools.BlockForResult(ReadAsync(buffers));

        /// <inheritdoc/>
        public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            WriteAsync(buffer, FlushType.None, cancellationToken);

        /// <inheritdoc/>
        public virtual async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType, CancellationToken cancellationToken = default)
        {
            if (_readState == StreamState.Disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            try
            {
                await _request.WriteContentAsync(buffer, flush: flushType == FlushType.FlushWrites, cancellationToken).ConfigureAwait(false);

                if (flushType == FlushType.FlushAndShutdownWrites)
                {
                    await _request.CompleteRequestAsync(cancellationToken).ConfigureAwait(false);
                    _completed = true;
                }
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default)
        {
            if (_readState == StreamState.Disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            try
            {
                await _request.WriteContentAsync(buffers, flush: flushType == FlushType.FlushWrites, cancellationToken).ConfigureAwait(false);

                if (flushType == FlushType.FlushAndShutdownWrites)
                {
                    await _request.CompleteRequestAsync(cancellationToken).ConfigureAwait(false);
                    _completed = true;
                }
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message, ex);
            }
        }

        /// <inheritdoc/>
        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), FlushType.None, cancellationToken).AsTask();

        /// <inheritdoc/>
        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            TaskToApm.Begin(WriteAsync(buffer, offset, count), callback, state);

        /// <inheritdoc/>
        public sealed override void EndWrite(IAsyncResult asyncResult) =>
            TaskToApm.End(asyncResult);

        /// <inheritdoc/>
        public virtual unsafe void Write(ReadOnlySpan<byte> buffer, FlushType flushType)
        {
            _spanWriteWrapper ??= new UnsafeSpanWrappingMemoryOwner();

            fixed (byte* ptr = buffer)
            {
                _spanWriteWrapper.SetPointer(ptr, buffer.Length);
                Tools.BlockForResult(WriteAsync(_spanWriteWrapper.Memory, flushType));
            }
        }

        /// <inheritdoc/>
        public sealed override void Write(ReadOnlySpan<byte> buffer) =>
            Write(buffer, FlushType.None);

        /// <inheritdoc/>
        public sealed override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count), FlushType.None);

        /// <inheritdoc/>
        public virtual void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None) =>
            Tools.BlockForResult(WriteAsync(buffers, flushType));

        private enum StreamState : byte
        {
            Reading,
            EndOfStream,
            Disposed
        }
    }
}
