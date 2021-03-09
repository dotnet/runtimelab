// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.Net.Sockets
{
    /// <summary>
    /// A <see cref="NetworkStream"/> that supports <see cref="IEnhancedStream"/>.
    /// </summary>
    public class EnhancedNetworkStream : NetworkStream, IEnhancedStream
    {
        private static readonly Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>? s_receiveAsyncWithCancellation = GetScatteredReceiveAsync();
        private static readonly Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>? s_sendAsyncWithCancellation = GetGatheredSendAsync();

        private volatile ScatteredReadEventArgs? _scatteredReadEventArgs;
        private volatile GatheredWriteEventArgs? _gatheredWriteEventArgs;

        private volatile List<ArraySegment<byte>>? _scatteredReadBuffers;
        private volatile List<ArraySegment<byte>>? _gatheredWriteBuffers;

        /// <inheritdoc/>
        public virtual bool CanScatterGather => s_sendAsyncWithCancellation is not null && s_receiveAsyncWithCancellation is not null;

        /// <inheritdoc/>
        public virtual bool CanShutdownWrites => true;

        private static Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>? GetScatteredReceiveAsync()
        {
            MethodInfo? method = typeof(Socket).GetMethod("ReceiveAsync", BindingFlags.NonPublic | BindingFlags.Instance, binder: null, new[] { typeof(SocketAsyncEventArgs), typeof(CancellationToken) }, modifiers: null);

            return method is not null && method.ReturnType == typeof(bool)
                ? (Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>)Delegate.CreateDelegate(typeof(Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>), firstArgument: null, method)
                : null;
        }

        private static Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>? GetGatheredSendAsync()
        {
            MethodInfo? method = typeof(Socket).GetMethod("SendAsync", BindingFlags.NonPublic | BindingFlags.Instance, binder: null, new[] { typeof(SocketAsyncEventArgs), typeof(CancellationToken) }, modifiers: null);

            return method is not null && method.ReturnType == typeof(bool)
                ? (Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>)Delegate.CreateDelegate(typeof(Func<Socket, SocketAsyncEventArgs, CancellationToken, bool>), firstArgument: null, method)
                : null;
        }

        /// <summary>
        /// Instantiates a new <see cref="EnhancedNetworkStream"/> over a <see cref="Socket"/>.
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> this stream will operate over.</param>
        /// <param name="ownsSocket">If true, the <paramref name="socket"/> will be disposed of along with this stream.</param>
        public EnhancedNetworkStream(Socket socket, bool ownsSocket) : base(socket, ownsSocket)
        {
        }

        /// <summary>
        /// Instantiates a new <see cref="EnhancedNetworkStream"/> over a <see cref="Socket"/>.
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> this stream will operate over.</param>
        /// <param name="access">The access permissions given to this stream.</param>
        /// <param name="ownsSocket">If true, the <paramref name="socket"/> will be disposed of along with this stream.</param>
        public EnhancedNetworkStream(Socket socket, FileAccess access, bool ownsSocket) : base(socket, access, ownsSocket)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // dispose the NetworkStream first, in case the event args are currently in use.
            base.Dispose(disposing);

            if (disposing)
            {
                _gatheredWriteEventArgs?.Dispose();
            }
        }

        /// <inheritdoc/>
        public sealed override void Flush() =>
            Flush(FlushType.FlushWrites);

        /// <inheritdoc/>
        public virtual void Flush(FlushType flushType)
        {
            if (flushType == FlushType.FlushAndShutdownWrites)
            {
                Socket.Shutdown(SocketShutdown.Send);
            }
        }

        public sealed override Task FlushAsync(CancellationToken cancellationToken) =>
            FlushAsync(FlushType.FlushWrites, cancellationToken).AsTask();

        /// <inheritdoc/>
        public virtual ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);

            if (flushType == FlushType.FlushAndShutdownWrites)
            {
                try
                {
                    Socket.Shutdown(SocketShutdown.Send);
                }
                catch (Exception ex)
                {
                    return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                }
            }

            return default;
        }

        /// <inheritdoc/>
        public virtual int Read(IReadOnlyList<Memory<byte>> buffers)
        {
            if (buffers == null) throw new ArgumentNullException(nameof(buffers));

            int bufferCount = buffers.Count;

            if (bufferCount <= 1)
            {
                return Read(bufferCount == 1 ? buffers[0].Span : Array.Empty<byte>());
            }

            List<ArraySegment<byte>> scatteredReadBuffers = _scatteredReadBuffers ?? new List<ArraySegment<byte>>(buffers.Count);

            try
            {
                for (int i = 0; i < bufferCount; ++i)
                {
                    Memory<byte> buffer = buffers[i];

                    if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                    {
                        // this will be the hot path.
                        scatteredReadBuffers.Add(segment);
                    }
                    else if (scatteredReadBuffers.Count > 0)
                    {
                        // read scatteredReadBuffers as scattered read.
                        break;
                    }
                    else
                    {
                        // read buffer as a non-scattered read.
                        return Read(buffer.Span);
                    }
                }

                int recvLen = Socket.Receive(scatteredReadBuffers, SocketFlags.None, out SocketError errorCode);

                if (errorCode != SocketError.Success)
                {
                    var ex = new SocketException((int)errorCode);
                    throw new IOException(ex.Message, ex);
                }

                return recvLen;
            }
            finally
            {
                scatteredReadBuffers.Clear();

                // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
                _scatteredReadBuffers = scatteredReadBuffers;
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default) =>
            buffers == null ? ValueTask.FromException<int>(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(buffers)))) :
            s_receiveAsyncWithCancellation is null ? ValueTask.FromException<int>(ExceptionDispatchInfo.SetCurrentStackTrace(new PlatformNotSupportedException($"The scatter overload of {nameof(ReadAsync)} is not supported on this platform."))) :
            buffers.Count > 1 ? (_scatteredReadEventArgs ??= new ScatteredReadEventArgs()).ReadAsync(Socket, buffers, cancellationToken) :
            ReadAsync(buffers.Count == 1 ? buffers[0] : Array.Empty<byte>(), cancellationToken);

        /// <inheritdoc/>
        public virtual void Write(ReadOnlySpan<byte> buffer, FlushType flushType)
        {
            Write(buffer);
            Flush(flushType);
        }

        /// <inheritdoc/>
        public virtual void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType)
        {
            if (buffers == null) throw new ArgumentNullException(nameof(buffers));

            int bufferCount = buffers.Count;

            if (bufferCount <= 1)
            {
                Write(bufferCount == 1 ? buffers[0].Span : Array.Empty<byte>(), flushType);
                return;
            }

            List<ArraySegment<byte>> gatheredWriteBuffers = _gatheredWriteBuffers ?? new List<ArraySegment<byte>>(buffers.Count);
            int totalGatheredWriteBytes = 0;

            try
            {
                for (int i = 0; i < bufferCount; ++i)
                {
                    ReadOnlyMemory<byte> buffer = buffers[i];

                    if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                    {
                        // this will be the hot path.
                        gatheredWriteBuffers.Add(segment);
                        totalGatheredWriteBytes += segment.Count;
                    }
                    else
                    {
                        if (gatheredWriteBuffers.Count > 0)
                        {
                            // write gatheredWriteBuffers as gathered write.
                            SendFullBuffer(gatheredWriteBuffers, totalGatheredWriteBytes);

                            gatheredWriteBuffers.Clear();
                            totalGatheredWriteBytes = 0;
                        }

                        // write buffer as a non-gathered write.
                        Write(buffer.Span);
                    }
                }

                if (gatheredWriteBuffers.Count > 0)
                {
                    int sent = Socket.Send(gatheredWriteBuffers, SocketFlags.None, out SocketError errorCode);
                    if (errorCode != SocketError.Success)
                    {
                        var ex = new SocketException((int)errorCode);
                        throw new IOException(ex.Message, ex);
                    }
                }
            }
            finally
            {
                gatheredWriteBuffers.Clear();

                // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
                _gatheredWriteBuffers = gatheredWriteBuffers;
            }
        }

        private void SendFullBuffer(List<ArraySegment<byte>> segments, int totalBytes)
        {
            while (true)
            {
                int sent = Socket.Send(segments, SocketFlags.None, out SocketError errorCode);

                if (errorCode != SocketError.Success)
                {
                    var ex = new SocketException((int)errorCode);
                    throw new IOException(ex.Message, ex);
                }

                totalBytes -= sent;

                if (totalBytes == 0)
                {
                    return;
                }

                while (true)
                {
                    ArraySegment<byte> segment = segments[0];

                    if (segment.Count >= sent)
                    {
                        sent -= segment.Count;
                        segments.RemoveAt(0);
                    }
                    else
                    {
                        segments[0] = segment.Slice(sent);
                        break;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default)
        {
            ValueTask writeTask = WriteAsync(buffer, cancellationToken);
            return flushType == FlushType.None ? writeTask : FinishWriteThenFlushAsync(writeTask, flushType, cancellationToken);
        }

        /// <inheritdoc/>
        public virtual ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default)
        {
            if (buffers == null) return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(buffers))));
            if (s_sendAsyncWithCancellation is null) return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new PlatformNotSupportedException($"The gather overload of {nameof(WriteAsync)} is not supported on this platform.")));
            if (buffers.Count <= 1) return WriteAsync(buffers.Count == 1 ? buffers[0] : Array.Empty<byte>().AsMemory(), flushType, cancellationToken);

            ValueTask writeTask = (_gatheredWriteEventArgs ??= new GatheredWriteEventArgs()).WriteAsync(Socket, buffers, cancellationToken);
            return flushType == FlushType.None ? writeTask : FinishWriteThenFlushAsync(writeTask, flushType, cancellationToken);
        }

        private async ValueTask FinishWriteThenFlushAsync(ValueTask valueTask, FlushType flushType, CancellationToken cancellationToken)
        {
            // TODO: optimize this to avoid a state machine.
            await valueTask.ConfigureAwait(false);
            await FlushAsync(flushType, cancellationToken).ConfigureAwait(false);
        }

        private sealed class ScatteredReadEventArgs : SocketAsyncEventArgs, IValueTaskSource<int>
        {
            private List<ArraySegment<byte>> _scatteredSegments = new List<ArraySegment<byte>>();
            private ManualResetValueTaskSourceCore<int> _valueTaskSource;

            public ScatteredReadEventArgs()
                : base(unsafeSuppressExecutionContextFlow: true)
            {
            }

            public ValueTask<int> ReadAsync(Socket socket, IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken)
            {
                _valueTaskSource.Reset();
                _scatteredSegments.Clear();

                for (int i = 0, count = buffers.Count; i != count; ++i)
                {
                    Memory<byte> buffer = buffers[i];

                    if (buffer.Length == 0)
                    {
                        continue;
                    }

                    if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                    {
                        _scatteredSegments.Add(segment);
                    }
                    else if (_scatteredSegments.Count > 0)
                    {
                        break; // read into current segments.
                    }
                    else
                    {
                        // read into current buffer as single buffer.

                        if (BufferList is not null)
                        {
                            BufferList = null;
                        }

                        SetBuffer(Buffer);
                        goto doReceive;
                    }
                }

                if (_scatteredSegments.Count == 0)
                {
                    _scatteredSegments.Add(Array.Empty<byte>());
                }

                if (BufferList is null)
                {
                    SetBuffer(default);
                }

                BufferList = _scatteredSegments;

            doReceive:
                if (!s_receiveAsyncWithCancellation!(socket, this, cancellationToken))
                {
                    OnCompleted();
                }

                return new ValueTask<int>(this, _valueTaskSource.Version);
            }

            protected override void OnCompleted(SocketAsyncEventArgs e) =>
                OnCompleted();

            public void OnCompleted()
            {
                if (SocketError == SocketError.Success)
                {
                    _valueTaskSource.SetResult(BytesTransferred);
                }
                else
                {
                    Exception ex = new SocketException((int)SocketError);
                    _valueTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                }
            }

            int IValueTaskSource<int>.GetResult(short token) =>
                _valueTaskSource.GetResult(token);

            ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token) =>
                _valueTaskSource.GetStatus(token);

            void IValueTaskSource<int>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
                _valueTaskSource.OnCompleted(continuation, state, token, flags);
        }

        private sealed class GatheredWriteEventArgs : SocketAsyncEventArgs, IValueTaskSource
        {
            private readonly List<ArraySegment<byte>> _gatheredSegments = new List<ArraySegment<byte>>();
            private Socket? _userSocket;
            private IReadOnlyList<ReadOnlyMemory<byte>>? _userBuffers;
            private CancellationToken _userCancellationToken;
            private ManualResetValueTaskSourceCore<int> _valueTaskSource;
            private int _userBuffersIndex, _userBuffersByteOffset, _userBuffersBytesRemaining;

            public GatheredWriteEventArgs()
                : base(unsafeSuppressExecutionContextFlow: true)
            {
            }

            public ValueTask WriteAsync(Socket socket, IReadOnlyList<ReadOnlyMemory<byte>> buffers, CancellationToken cancellationToken)
            {
                _userSocket = socket;
                _userBuffers = buffers;
                _userCancellationToken = cancellationToken;
                _userBuffersIndex = 0;
                _userBuffersByteOffset = 0;

                _valueTaskSource.Reset();
                Start();
                return new ValueTask(this, _valueTaskSource.Version);
            }

            private void Start()
            {
                Debug.Assert(_userBuffers != null);
                Debug.Assert(_userSocket != null);
                Debug.Assert(s_sendAsyncWithCancellation != null);

                while (true)
                {
                    _gatheredSegments.Clear();
                    _userBuffersBytesRemaining = 0;

                    ReadOnlyMemory<byte> buffer;
                    int byteOffset = _userBuffersByteOffset;

                    for (int i = _userBuffersIndex, count = _userBuffers.Count; i < count; ++i)
                    {
                        buffer = _userBuffers[i];

                        if (byteOffset != 0)
                        {
                            buffer = buffer.Slice(byteOffset);
                            byteOffset = 0;
                        }

                        _userBuffersBytesRemaining += buffer.Length;

                        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                        {
                            // this will be the hot path.
                            _gatheredSegments.Add(segment);
                        }
                        else if (_gatheredSegments.Count > 0)
                        {
                            // send _gatheredSegments as gathered write.
                            break;
                        }
                        else
                        {
                            // send out buffer as non-gathered write.

                            if (BufferList != null)
                            {
                                BufferList = null;
                            }

                            SetBuffer(MemoryMarshal.AsMemory(buffer));

                            goto doSend;
                        }
                    }

                    if (_gatheredSegments.Count == 0)
                    {
                        _gatheredSegments.Add(Array.Empty<byte>());
                    }

                    if (BufferList is null)
                    {
                        SetBuffer(default);
                    }

                    BufferList = _gatheredSegments;

                doSend:
                    if (s_sendAsyncWithCancellation!(_userSocket, this, _userCancellationToken))
                    {
                        return;
                    }

                    OnCompleted(completedSynchronously: true);
                }
            }

            protected override void OnCompleted(SocketAsyncEventArgs e) =>
                OnCompleted(completedSynchronously: false);

            public void OnCompleted(bool completedSynchronously)
            {
                Debug.Assert(_userBuffers != null);

                if (_gatheredSegments != null)
                {
                    _gatheredSegments.Clear();
                }

                if (SocketError == SocketError.Success)
                {
                    int bytesTransferred = BytesTransferred;
                    int left = _userBuffersBytesRemaining -= bytesTransferred;
                    if (left == 0)
                    {
                        _valueTaskSource.SetResult(left);
                    }
                    else
                    {
                        // advance buffer.

                        while (bytesTransferred != 0)
                        {
                            left = _userBuffers[_userBuffersIndex].Length - _userBuffersByteOffset;

                            if (bytesTransferred >= left)
                            {
                                _userBuffersByteOffset = 0;
                                ++_userBuffersIndex;
                                bytesTransferred -= left;
                                continue;
                            }
                            else
                            {
                                _userBuffersByteOffset += bytesTransferred;
                                break;
                            }
                        }

                        if (!completedSynchronously)
                        {
                            Start();
                        }
                    }
                }
                else
                {
                    var ex = new SocketException((int)SocketError);
                    _valueTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                }
            }

            void IValueTaskSource.GetResult(short token) =>
                _valueTaskSource.GetResult(token);

            ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) =>
                _valueTaskSource.GetStatus(token);

            void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
                _valueTaskSource.OnCompleted(continuation, state, token, flags);
        }
    }
}
