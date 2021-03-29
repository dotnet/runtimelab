using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// An in-memory connection.
    /// </summary>
    public sealed class MemoryConnection : Connection
    {
        /// <inheritdoc/>
        public override EndPoint? LocalEndPoint { get; }

        /// <inheritdoc/>
        public override EndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// Opens a new in-memory connection.
        /// </summary>
        /// <param name="clientEndPoint">The <see cref="EndPoint"/> to use for the client connection, if any.</param>
        /// <param name="clientPipeOptions">Pipe options used when configuring client-side buffers. If null, <see cref="PipeOptions.Default"/> will be used.</param>
        /// <param name="serverEndPoint">The <see cref="EndPoint"/> to use for the server connection, if any.</param>
        /// <param name="serverPipeOptions">Pipe options used when configuring server-side buffers. If null, <see cref="PipeOptions.Default"/> will be used.</param>
        /// <returns>A tuple of the client and server connections.</returns>
        public static (Connection clientConnection, Connection serverConnection) Create(EndPoint? clientEndPoint = null, PipeOptions? clientPipeOptions = null, EndPoint? serverEndPoint = null, PipeOptions? serverPipeOptions = null)
        {
            var bufferA = new Pipe(clientPipeOptions ?? PipeOptions.Default);
            var bufferB = new Pipe(clientPipeOptions ?? PipeOptions.Default);

            Connection clientConnection = new MemoryConnection(bufferA.Reader, bufferB.Writer, clientEndPoint, serverEndPoint);
            Connection serverConnection = new MemoryConnection(bufferB.Reader, bufferA.Writer, serverEndPoint, clientEndPoint);

            return (clientConnection, serverConnection);
        }

        private MemoryConnection(PipeReader reader, PipeWriter writer, EndPoint? localEndPoint, EndPoint? remoteEndPoint) : base(new MemoryConnectionStream(reader, writer))
        {
            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <inheritdoc/>
        protected override ValueTask DisposeAsyncCore()
            => default;

        private sealed class MemoryConnectionStream : TestStreamBase
        {
            PipeReader? _reader;
            PipeWriter? _writer;

            public override bool CanScatterGather => true;
            public override bool CanShutdownWrites => true;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotImplementedException();

            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public MemoryConnectionStream(PipeReader reader, PipeWriter writer)
            {
                _reader = reader;
                _writer = writer;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _reader is PipeReader reader)
                {
                    _writer?.Complete();
                    reader.Complete();
                    _writer = null;
                    _reader = null;
                }
            }

            public override void Flush(FlushType flushType)
            {
                if (flushType == FlushType.FlushAndShutdownWrites && _writer is not null)
                {
                    try
                    {
                        _writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        throw new IOException(ex.Message, ex);
                    }
                }
            }

            public override ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ValueTask.FromCanceled(cancellationToken);
                }

                if (flushType == FlushType.FlushAndShutdownWrites && _writer is not null)
                {
                    try
                    {
                        _writer.Complete();
                    }
                    catch (Exception ex)
                    {
                        return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                    }
                }

                return default;
            }

            public override int Read(Span<byte> buffer)
            {
                if (_reader is not PipeReader reader) throw new ObjectDisposedException(nameof(MemoryConnectionStream));

                try
                {
                    return FinishRead(reader, buffer, Tools.BlockForResult(_reader.ReadAsync()), CancellationToken.None);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }

            public override int Read(IReadOnlyList<Memory<byte>> buffers)
            {
                if (_reader is not PipeReader reader) throw new ObjectDisposedException(nameof(MemoryConnectionStream));

                try
                {
                    return FinishRead(reader, buffers, Tools.BlockForResult(_reader.ReadAsync()), CancellationToken.None);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_reader is not PipeReader reader) throw new ObjectDisposedException(nameof(MemoryConnectionStream));

                try
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    return FinishRead(reader, buffer.Span, result, cancellationToken);
                }
                catch (Exception ex) when(ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }

            public override async ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default)
            {
                if (_reader is not PipeReader reader) throw new ObjectDisposedException(nameof(MemoryConnectionStream));

                try
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    return FinishRead(reader, buffers, result, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }

            private static int FinishRead(PipeReader reader, Span<byte> buffer, in ReadResult result, CancellationToken cancellationToken)
            {
                if (result.IsCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException();
                }

                ReadOnlySequence<byte> sequence = result.Buffer;
                long sequenceLength = sequence.Length;
                SequencePosition consumed = sequence.Start;

                try
                {
                    if (sequenceLength != 0)
                    {
                        int actual = (int)Math.Min(sequenceLength, buffer.Length);

                        if (actual != sequenceLength)
                        {
                            sequence = sequence.Slice(0, actual);
                        }

                        consumed = sequence.End;
                        sequence.CopyTo(buffer);

                        return actual;
                    }

                    Debug.Assert(result.IsCompleted, "An uncompleted Pipe should never return a 0-length buffer.");
                    return 0;
                }
                finally
                {
                    reader.AdvanceTo(consumed);
                }
            }

            private static int FinishRead(PipeReader reader, IReadOnlyList<Memory<byte>> buffers, in ReadResult result, CancellationToken cancellationToken)
            {
                if (result.IsCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException();
                }

                ReadOnlySequence<byte> sequence = result.Buffer;
                SequencePosition consumed = sequence.End;
                int totalTaken = 0;

                try
                {
                    if (sequence.Length == 0)
                    {
                        Debug.Assert(result.IsCompleted, "An uncompleted Pipe should never return a 0-length buffer.");
                        return 0;
                    }

                    for (int i = 0, count = buffers.Count; sequence.Length != 0 && i != count; ++i)
                    {
                        Span<byte> buffer = buffers[i].Span;
                        int take = (int)Math.Min(buffer.Length, sequence.Length);

                        SequencePosition takeEnd = sequence.GetPosition(take);

                        sequence.Slice(sequence.Start, takeEnd).CopyTo(buffer);
                        sequence = sequence.Slice(takeEnd);

                        totalTaken += take;
                    }
                    
                    consumed = sequence.End;
                    return totalTaken;
                }
                finally
                {
                    reader.AdvanceTo(consumed);
                }
            }

            public override void Write(ReadOnlySpan<byte> buffer, FlushType flushType)
            {
                if (_reader == null) throw new ObjectDisposedException(nameof(MemoryConnectionStream));
                if (_writer is not PipeWriter writer) throw new InvalidOperationException($"{nameof(MemoryConnectionStream)} cannot be written to after writes have been completed.");

                try
                {
                    Span<byte> originalWriterBuffer = default, remainingWriterBuffer = default;

                    while (buffer.Length != 0)
                    {
                        if (remainingWriterBuffer.Length == 0)
                        {
                            remainingWriterBuffer = originalWriterBuffer = writer!.GetSpan();
                        }

                        int take = Math.Min(buffer.Length, remainingWriterBuffer.Length);

                        buffer.Slice(0, take).CopyTo(remainingWriterBuffer);
                        buffer = buffer.Slice(take);
                        remainingWriterBuffer = remainingWriterBuffer.Slice(take);

                        if (remainingWriterBuffer.Length == 0)
                        {
                            writer!.Advance(originalWriterBuffer.Length);
                            FlushResult res = Tools.BlockForResult(writer.FlushAsync());

                            if (res.IsCanceled)
                            {
                                throw new OperationCanceledException();
                            }
                        }
                    }

                    if (remainingWriterBuffer.Length != 0)
                    {
                        writer!.Advance(originalWriterBuffer.Length - remainingWriterBuffer.Length);
                        FlushResult res = Tools.BlockForResult(writer.FlushAsync());

                        if (res.IsCanceled)
                        {
                            throw new OperationCanceledException();
                        }
                    }

                    if (flushType == FlushType.FlushAndShutdownWrites)
                    {
                        _writer.Complete();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }

            public override void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType) =>
                Tools.BlockForResult(WriteAsync(buffers, flushType));

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType, CancellationToken cancellationToken = default)
            {
                if (_reader is null) throw new ObjectDisposedException(nameof(MemoryConnectionStream));
                if (_writer is not PipeWriter writer) throw new InvalidOperationException($"{nameof(MemoryConnectionStream)} cannot be written to after writes have been completed.");

                try
                {
                    Memory<byte> originalWriterBuffer = default, remainingWriterBuffer = default;

                    while (buffer.Length != 0)
                    {
                        if (remainingWriterBuffer.Length == 0)
                        {
                            remainingWriterBuffer = originalWriterBuffer = writer.GetMemory();
                        }

                        int take = Math.Min(buffer.Length, remainingWriterBuffer.Length);

                        buffer.Slice(0, take).CopyTo(remainingWriterBuffer);
                        buffer = buffer.Slice(take);
                        remainingWriterBuffer = remainingWriterBuffer.Slice(take);

                        if (remainingWriterBuffer.Length == 0)
                        {
                            writer.Advance(originalWriterBuffer.Length);
                            FlushResult res = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                            if (res.IsCanceled)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                throw new OperationCanceledException();
                            }
                        }
                    }

                    if (remainingWriterBuffer.Length != 0)
                    {
                        writer.Advance(originalWriterBuffer.Length - remainingWriterBuffer.Length);
                        FlushResult res = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                        if (res.IsCanceled)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new OperationCanceledException();
                        }
                    }

                    if (flushType == FlushType.FlushAndShutdownWrites)
                    {
                        _writer.Complete();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }

            public override async ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType, CancellationToken cancellationToken = default)
            {
                if (_reader == null) throw new ObjectDisposedException(nameof(MemoryConnectionStream));
                if (_writer is not PipeWriter writer) throw new InvalidOperationException($"{nameof(MemoryConnectionStream)} cannot be written to after writes have been completed.");

                try
                {
                    Memory<byte> originalWriterBuffer = default, remainingWriterBuffer = default;

                    for(int i = 0, count = buffers.Count; i != count; ++i)
                    {
                        ReadOnlyMemory<byte> buffer = buffers[i];

                        while (buffer.Length != 0)
                        {
                            if (remainingWriterBuffer.Length == 0)
                            {
                                remainingWriterBuffer = originalWriterBuffer = writer.GetMemory();
                            }

                            int take = Math.Min(buffer.Length, remainingWriterBuffer.Length);

                            buffer.Slice(0, take).CopyTo(remainingWriterBuffer);
                            buffer = buffer.Slice(take);
                            remainingWriterBuffer = remainingWriterBuffer.Slice(take);

                            if (remainingWriterBuffer.Length == 0)
                            {
                                writer.Advance(originalWriterBuffer.Length);

                                FlushResult res = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                                if (res.IsCanceled)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    throw new OperationCanceledException();
                                }
                            }
                        }
                    }

                    if (remainingWriterBuffer.Length != 0)
                    {
                        writer.Advance(originalWriterBuffer.Length - remainingWriterBuffer.Length);

                        FlushResult res = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                        if (res.IsCanceled)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new OperationCanceledException();
                        }
                    }

                    if (flushType == FlushType.FlushAndShutdownWrites)
                    {
                        _writer.Complete();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }

            public override void CopyTo(Stream destination, int bufferSize) =>
                CopyToAsync(destination, bufferSize, CancellationToken.None).GetAwaiter().GetResult();

            public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                if (_reader is not PipeReader reader) throw new ObjectDisposedException(nameof(MemoryConnectionStream));
                try
                {
                    await reader.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new IOException(ex.Message, ex);
                }
            }
        }
    }
}
