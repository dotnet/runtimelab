// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    /// <summary>
    /// A write buffering <see cref="Stream"/>.
    /// </summary>
    public class WriteBufferingStream : Stream, IEnhancedStream
    {
        private readonly Stream _baseStream;
        private IEnhancedStream? _enhancedBase;
        private List<ReadOnlyMemory<byte>>? _gatherBuffers;
        private readonly UnsafeSpanWrappingMemoryOwner _spanWrapper = new UnsafeSpanWrappingMemoryOwner();
        private readonly byte[] _buffer;
        private volatile int _bufferFillLength;
        private readonly bool _ownsStream;

        /// <inheritdoc/>
        public override bool CanRead => _baseStream.CanRead;

        /// <inheritdoc/>
        public override bool CanWrite => _baseStream.CanWrite;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanTimeout => _baseStream.CanTimeout;

        /// <inheritdoc/>
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override int ReadTimeout { get => _baseStream.ReadTimeout; set => _baseStream.WriteTimeout = value; }

        /// <inheritdoc/>
        public override int WriteTimeout { get => _baseStream.WriteTimeout; set => _baseStream.WriteTimeout = value; }

        /// <inheritdoc/>
        public bool CanScatterGather => true;

        /// <inheritdoc/>
        public bool CanShutdownWrites => _enhancedBase?.CanShutdownWrites == true;

        /// <summary>
        /// Instantiates a new <see cref="WriteBufferingStream"/>.
        /// </summary>
        /// <param name="baseStream">The base <see cref="Stream"/> to buffer writes over.</param>
        /// <param name="ownsStream">If true, <paramref name="baseStream"/> will be disposed when the <see cref="WriteBufferingStream"/> is disposed.</param>
        /// <param name="bufferLength">The number of bytes to buffer.</param>
        public WriteBufferingStream(Stream baseStream, bool ownsStream, int bufferLength)
        {
            if (baseStream == null) throw new ArgumentNullException(nameof(baseStream));
            if (bufferLength <= 0) throw new ArgumentOutOfRangeException(nameof(bufferLength), $"{nameof(bufferLength)} must be positive.");

            _baseStream = baseStream;
            _ownsStream = ownsStream;
            _enhancedBase = baseStream as IEnhancedStream;

            if (_enhancedBase?.CanScatterGather == true)
            {
                _gatherBuffers = new List<ReadOnlyMemory<byte>>(2);
            }

            _buffer = new byte[bufferLength];
            _bufferFillLength = 0;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flush(CanShutdownWrites ? FlushType.FlushAndShutdownWrites : FlushType.FlushWrites);
            }

            if (disposing)
            {
                _baseStream.Dispose();
            }
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            await FlushAsync(CanShutdownWrites ? FlushType.FlushAndShutdownWrites : FlushType.FlushWrites).ConfigureAwait(false);
            await _baseStream.DisposeAsync().ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override int ReadByte() =>
            _baseStream.ReadByte();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) =>
            _baseStream.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer) =>
            _baseStream.Read(buffer);

        /// <inheritdoc/>
        public virtual int Read(IReadOnlyList<Memory<byte>> buffers) =>
            _gatherBuffers is not null ? _enhancedBase!.Read(buffers) :
            buffers is null ? throw new ArgumentNullException(nameof(buffers)) :
            buffers.Count == 1 ? _baseStream.Read(buffers[0].Span) :
            _baseStream.Read(Array.Empty<byte>(), 0, 0);

        /// <inheritdoc/>
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            _baseStream.BeginRead(buffer, offset, count, callback, state);

        /// <inheritdoc/>
        public override int EndRead(IAsyncResult asyncResult) =>
            _baseStream.EndRead(asyncResult);

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _baseStream.ReadAsync(buffer, offset, count, cancellationToken);

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _baseStream.ReadAsync(buffer, cancellationToken);

        /// <inheritdoc/>
        public virtual ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default) =>
            _gatherBuffers is not null ? _enhancedBase!.ReadAsync(buffers, cancellationToken) :
            buffers is null ? ValueTask.FromException<int>(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(buffers)))) :
            _baseStream.ReadAsync(buffers.Count == 1 ? buffers[0] : Array.Empty<byte>(), cancellationToken);

        /// <inheritdoc/>
        public override void CopyTo(Stream destination, int bufferSize) =>
            _baseStream.CopyTo(destination, bufferSize);

        /// <inheritdoc/>
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            _baseStream.CopyToAsync(destination, bufferSize, cancellationToken);

        /// <inheritdoc/>
        public unsafe override void WriteByte(byte value)
        {
            Write(MemoryMarshal.CreateSpan(ref value, 1));
        }

        /// <inheritdoc/>
        public sealed override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            Write(buffer.AsSpan(offset, count), FlushType.None);
        }

        /// <inheritdoc/>
        public sealed override void Write(ReadOnlySpan<byte> buffer) =>
            Write(buffer, FlushType.None);

        /// <inheritdoc/>
        public virtual unsafe void Write(ReadOnlySpan<byte> buffer, FlushType flushType)
        {
            int bufferFillLength = _bufferFillLength;
            int available = _buffer.Length - bufferFillLength;

            if (available >= buffer.Length && flushType == FlushType.None)
            {
                // Append the caller's entire buffer to send buffer.

                buffer.CopyTo(_buffer.AsSpan(bufferFillLength));
                _bufferFillLength = bufferFillLength + buffer.Length;
                return;
            }

            if (bufferFillLength == 0)
            {
                // The caller's buffer is larger than the send buffer can hold, and there is no send buffer.
                // Send out the caller's buffer directly.

                if (_enhancedBase is not null)
                {
                    _enhancedBase.Write(buffer, flushType);
                    return;
                }

                _baseStream.Write(buffer);
            }
            else if (_gatherBuffers is not null)
            {
                // The caller's buffer is larger than the send buffer can hold, and there is some send buffer committed.
                // Write the send buffer and the caller's buffer together.

                Debug.Assert(_enhancedBase is not null);

                _gatherBuffers.Clear();
                _gatherBuffers.Add(_buffer.AsMemory(0, bufferFillLength));

                fixed (byte* ptr = buffer)
                {
                    _spanWrapper.SetPointer(ptr, buffer.Length);
                    _gatherBuffers.Add(_spanWrapper.Memory);
                    _enhancedBase.Write(_gatherBuffers, flushType);
                }

                _bufferFillLength = 0;
                return;
            }
            else
            {
                // The caller's buffer is larger than the send buffer can hold, and there is some send buffer committed.

                int remainingBufferLength = _buffer.Length - bufferFillLength;

                if (buffer.Length - remainingBufferLength < _buffer.Length && flushType == FlushType.None)
                {
                    // we will need 2 I/Os, but can save one of them for later.
                    // fill our buffer, then buffer the remaining bytes of callerBuffer

                    buffer.Slice(0, remainingBufferLength).CopyTo(_buffer.AsSpan(bufferFillLength));

                    _baseStream.Write(_buffer);

                    buffer = buffer.Slice(remainingBufferLength);
                    buffer.CopyTo(_buffer);
                    _bufferFillLength = buffer.Length;
                    return;
                }

                // we will need 2 I/Os regardless.
                // just write both buffers.

                _baseStream.Write(_buffer.AsSpan(0, bufferFillLength));

                if (_enhancedBase is not null)
                {
                    _enhancedBase.Write(buffer, flushType);
                    _bufferFillLength = 0;
                    return;
                }

                _baseStream.Write(buffer);
            }

            if (flushType != FlushType.None)
            {
                if (flushType == FlushType.FlushAndShutdownWrites) throw new NotSupportedException("Base stream does not support shutdown.");
                _baseStream.Flush();
            }

            _bufferFillLength = 0;
        }

        /// <inheritdoc/>
        public virtual void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None)
        {
            if (buffers == null) throw new ArgumentNullException(nameof(buffers));

            int totalBufferLength = 0;
            for (int i = 0, count = buffers.Count; i != count; ++i)
            {
                totalBufferLength += buffers[i].Length;
            }

            int bufferFillLength = _bufferFillLength;
            int available = _buffer.Length - bufferFillLength;

            if (available >= totalBufferLength)
            {
                for (int i = 0, count = buffers.Count; i != count; ++i)
                {
                    ReadOnlyMemory<byte> buffer = buffers[i];

                    buffer.Span.CopyTo(_buffer.AsSpan(bufferFillLength));
                    bufferFillLength += buffer.Length;
                }

                _bufferFillLength = bufferFillLength;
            }
            else if (_enhancedBase is not null)
            {
                Debug.Assert(_gatherBuffers is not null);

                if (bufferFillLength == 0)
                {
                    _enhancedBase.Write(buffers, flushType);
                }
                else
                {
                    _gatherBuffers.Clear();
                    _gatherBuffers.Add(_buffer.AsMemory(0, bufferFillLength));
                    _gatherBuffers.AddRange(buffers);

                    _enhancedBase.Write(_gatherBuffers, flushType);
                    _bufferFillLength = 0;
                }
            }
            else
            {
                for (int i = 0, count = buffers.Count; i != count; ++i)
                {
                    ReadOnlyMemory<byte> callerBuffer = buffers[i];
                    int remainingBufferLength = _buffer.Length - bufferFillLength;

                    if (callerBuffer.Length - remainingBufferLength < _buffer.Length)
                    {
                        // we will need 2 I/Os, but can save one of them for later.
                        // fill our buffer, then buffer the remaining bytes of callerBuffer

                        callerBuffer.Slice(0, remainingBufferLength).Span.CopyTo(_buffer.AsSpan(bufferFillLength));

                        _baseStream.Write(_buffer);

                        ReadOnlyMemory<byte> remainingBuffer = callerBuffer.Slice(remainingBufferLength);

                        if (remainingBuffer.Length != 0)
                        {
                            remainingBuffer.Span.CopyTo(_buffer);
                        }

                        bufferFillLength = remainingBuffer.Length;
                    }
                    else
                    {
                        // we will need 2 I/Os regardless.
                        // just write both buffers.

                        if (bufferFillLength != 0)
                        {
                            _baseStream.Write(_buffer, 0, bufferFillLength);
                        }

                        _baseStream.Write(callerBuffer.Span);

                        bufferFillLength = 0;
                    }
                }

                if (flushType != FlushType.None)
                {
                    if (flushType == FlushType.FlushAndShutdownWrites)
                    {
                        throw new NotSupportedException("Base stream does not support shutdowns.");
                    }

                    _baseStream.Flush();
                }

                _bufferFillLength = bufferFillLength;
            }
        }

        /// <inheritdoc/>
        public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            WriteAsync(buffer, FlushType.None, cancellationToken);

        /// <inheritdoc/>
        public virtual ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType, CancellationToken cancellationToken = default)
        {
            int bufferFillLength = _bufferFillLength;
            int available = _buffer.Length - bufferFillLength;

            if (available >= buffer.Length && flushType == FlushType.None)
            {
                // Append the caller's entire buffer to send buffer.

                if (cancellationToken.IsCancellationRequested)
                {
                    return ValueTask.FromCanceled(cancellationToken);
                }

                buffer.Span.CopyTo(_buffer.AsSpan(bufferFillLength));
                _bufferFillLength = bufferFillLength + buffer.Length;

                return default;
            }

            if (bufferFillLength == 0)
            {
                // The caller's buffer is larger than the send buffer can hold, and there is no send buffer.
                // Send out the caller's buffer directly.

                if (_enhancedBase is not null)
                {
                    return _enhancedBase.WriteAsync(buffer, flushType, cancellationToken);
                }

                if (flushType == FlushType.None)
                {
                    return _baseStream.WriteAsync(buffer, cancellationToken);
                }

                if (flushType == FlushType.FlushAndShutdownWrites)
                {
                    return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new NotSupportedException("Base stream does not support shutdown.")));
                }

                return FinishWriteAndFlushAsync(_baseStream.WriteAsync(buffer, cancellationToken), _baseStream, cancellationToken);

                static async ValueTask FinishWriteAndFlushAsync(ValueTask writeTask, Stream baseStream, CancellationToken cancellationToken)
                {
                    await writeTask.ConfigureAwait(false);
                    await baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            if (_gatherBuffers is not null)
            {
                // The caller's buffer is larger than the send buffer can hold, and there is some send buffer committed.
                // Write the send buffer and the caller's buffer together.

                Debug.Assert(_enhancedBase is not null);

                _gatherBuffers.Clear();
                _gatherBuffers.Add(_buffer.AsMemory(0, bufferFillLength));
                _gatherBuffers.Add(buffer);
                _bufferFillLength = 0;

                return _enhancedBase.WriteAsync(_gatherBuffers, flushType, cancellationToken);
            }

            // The caller's buffer is larger than the send buffer can hold, and there is some send buffer committed.

            int remainingBufferLength = _buffer.Length - bufferFillLength;

            if (buffer.Length - remainingBufferLength < _buffer.Length && flushType == FlushType.None)
            {
                // we will need 2 I/Os, but can save one of them for later.
                // fill our buffer, then buffer the remaining bytes of callerBuffer

                buffer.Slice(0, remainingBufferLength).Span.CopyTo(_buffer.AsSpan(bufferFillLength));
                ReadOnlyMemory<byte> remainingBuffer = buffer.Slice(remainingBufferLength);

                return FlushThenFillBufferAsync(remainingBuffer, cancellationToken);
            }

            // we will need 2 I/Os regardless.
            // just write both buffers.

            return FlushThenSend(buffer, flushType, cancellationToken);
        }

        private async ValueTask FlushThenSend(ReadOnlyMemory<byte> callerBuffer, FlushType flushType, CancellationToken cancellationToken)
        {
            Debug.Assert(_bufferFillLength != 0);

            await _baseStream.WriteAsync(_buffer.AsMemory(0, _bufferFillLength), cancellationToken).ConfigureAwait(false);
            _bufferFillLength = 0;

            if (_enhancedBase is not null)
            {
                await _enhancedBase.WriteAsync(callerBuffer, flushType, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _baseStream.WriteAsync(callerBuffer, cancellationToken).ConfigureAwait(false);

                if (flushType != FlushType.None)
                {
                    if (flushType == FlushType.FlushAndShutdownWrites) throw new NotSupportedException("Base stream does not support shutdown.");
                    await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async ValueTask FlushThenFillBufferAsync(ReadOnlyMemory<byte> callerBuffer, CancellationToken cancellationToken)
        {
            await _baseStream.WriteAsync(_buffer, cancellationToken).ConfigureAwait(false);
            callerBuffer.Span.CopyTo(_buffer);
            _bufferFillLength = callerBuffer.Length;
        }

        /// <inheritdoc/>
        public virtual ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType, CancellationToken cancellationToken = default)
        {
            if (buffers == null) return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(buffers))));

            int callerBufferCount = buffers.Count;

            if (callerBufferCount == 0)
            {
                return
                    flushType != FlushType.None ? FlushAsync(flushType, cancellationToken) :
                    cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) :
                    default;
            }

            if (callerBufferCount == 1)
            {
                return WriteAsync(buffers[0], flushType, cancellationToken);
            }

            int totalBufferLength = 0;
            for (int i = 0; i != callerBufferCount; ++i)
            {
                totalBufferLength += buffers[i].Length;
            }

            int bufferFillLength = _bufferFillLength;
            int available = _buffer.Length - bufferFillLength;

            if (available > totalBufferLength && flushType == FlushType.None)
            {
                // The caller's buffers can fit into the send buffer.

                if (cancellationToken.IsCancellationRequested)
                {
                    return ValueTask.FromCanceled(cancellationToken);
                }

                for (int i = 0, count = buffers.Count; i != count; ++i)
                {
                    ReadOnlyMemory<byte> buffer = buffers[i];

                    buffer.Span.CopyTo(_buffer.AsSpan(bufferFillLength));
                    bufferFillLength += buffer.Length;
                }

                _bufferFillLength = bufferFillLength;
                return default;
            }

            if (_gatherBuffers is null)
            {
                // The caller's buffer is larger than the send buffer can hold.
                // Emulate the gathered write.
                return EmulatedGatherWriteAsync(buffers, flushType, cancellationToken);
            }

            Debug.Assert(_enhancedBase is not null);

            if (bufferFillLength == 0)
            {
                // The caller's buffer is larger than the send buffer can hold, and there is no send buffer.
                // Send out the caller's buffer directly.

                return _enhancedBase.WriteAsync(buffers, flushType, cancellationToken);
            }

            // The caller's buffer is larger than the send buffer can hold.
            // Send out the send buffer along with the caller's buffers.

            _gatherBuffers.Clear();
            _gatherBuffers.Add(_buffer.AsMemory(0, bufferFillLength));
            _gatherBuffers.AddRange(buffers);

            _bufferFillLength = 0;
            return _enhancedBase.WriteAsync(_gatherBuffers, flushType, cancellationToken);
        }

        private async ValueTask EmulatedGatherWriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> callerBuffers, FlushType flushType, CancellationToken cancellationToken)
        {
            Debug.Assert(callerBuffers.Count >= 2);

            int lastBufferIdx = callerBuffers.Count - 1;

            for (int i = 0; i <= lastBufferIdx; ++i)
            {
                await WriteAsync(callerBuffers[i], FlushType.None, cancellationToken).ConfigureAwait(false);
            }

            await WriteAsync(callerBuffers[lastBufferIdx], flushType, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            TaskToApm.Begin(WriteAsync(buffer, offset, count), callback, state);

        /// <inheritdoc/>
        public sealed override void EndWrite(IAsyncResult asyncResult) =>
            TaskToApm.End(asyncResult);

        /// <inheritdoc/>
        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) return Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(buffer))));
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <inheritdoc/>
        public sealed override void Flush() =>
            Flush(FlushType.FlushWrites);

        /// <inheritdoc/>
        public virtual void Flush(FlushType flushType)
        {
            if (flushType == FlushType.None)
            {
                return;
            }

            int bufferFillLength = _bufferFillLength;

            if (_enhancedBase is not null)
            {
                if (bufferFillLength != 0)
                {
                    _enhancedBase.Write(_buffer.AsSpan(0, bufferFillLength), flushType);
                }
                else
                {
                    _enhancedBase.Flush(flushType);
                }
            }
            else
            {
                if (flushType == FlushType.FlushAndShutdownWrites)
                {
                    throw new NotSupportedException("Base stream does not support shutdowns.");
                }

                if (bufferFillLength != 0)
                {
                    _baseStream.Write(_buffer, 0, bufferFillLength);
                }

                _baseStream.Flush();
            }
        }

        /// <inheritdoc/>
        public sealed override Task FlushAsync(CancellationToken cancellationToken) =>
            FlushAsync(FlushType.FlushWrites, cancellationToken).AsTask();

        /// <inheritdoc/>
        public virtual ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken = default)
        {
            if (flushType == FlushType.None)
            {
                return cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) : default;
            }

            int bufferFillLength = _bufferFillLength;

            if (_enhancedBase is not null)
            {
                if (bufferFillLength != 0)
                {
                    _bufferFillLength = 0;
                    return _enhancedBase.WriteAsync(_buffer.AsMemory(0, bufferFillLength), flushType, cancellationToken);
                }
                else
                {
                    return _enhancedBase.FlushAsync(flushType, cancellationToken);
                }
            }

            if (flushType == FlushType.FlushAndShutdownWrites)
            {
                return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new NotSupportedException("Base stream does not support shutdowns.")));
            }

            if (bufferFillLength != 0)
            {
                return FinishWriteThenFlushAsync(_baseStream.WriteAsync(_buffer.AsMemory(0, bufferFillLength), cancellationToken), _baseStream, cancellationToken);
            }

            return new ValueTask(_baseStream.FlushAsync(cancellationToken));

            static async ValueTask FinishWriteThenFlushAsync(ValueTask writeTask, Stream baseStream, CancellationToken cancellationToken)
            {
                await writeTask.ConfigureAwait(false);
                await baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public sealed override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public sealed override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
