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

            scatteredReadBuffers.Clear();

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
                    // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
                    _scatteredReadBuffers = scatteredReadBuffers;

                    // read buffer as a non-scattered read.
                    return Read(buffer.Span);
                }
            }

            // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
            _scatteredReadBuffers = scatteredReadBuffers;

            int recvLen = Socket.Receive(scatteredReadBuffers, SocketFlags.None, out SocketError errorCode);

            if (errorCode != SocketError.Success)
            {
                var ex = new SocketException((int)errorCode);
                throw new IOException(ex.Message, ex);
            }

            return recvLen;
        }

        /// <inheritdoc/>
        public virtual ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default)
        {
            if (buffers is null) return ValueTask.FromException<int>(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(buffers))));
            if (s_receiveAsyncWithCancellation is null) return ValueTask.FromException<int>(ExceptionDispatchInfo.SetCurrentStackTrace(new PlatformNotSupportedException($"The scatter overload of {nameof(ReadAsync)} is not supported on this platform.")));

            int bufferCount = buffers.Count;
            if (bufferCount <= 1) return ReadAsync(bufferCount == 1 ? buffers[0] : Array.Empty<byte>(), cancellationToken);

            List<ArraySegment<byte>> scatteredReadBuffers = _scatteredReadBuffers ?? new List<ArraySegment<byte>>(buffers.Count);

            scatteredReadBuffers.Clear();

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
                    // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
                    _scatteredReadBuffers = scatteredReadBuffers;

                    // read buffer as a non-scattered read.
                    return ReadAsync(buffer, cancellationToken);
                }
            }

            // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
            _scatteredReadBuffers = scatteredReadBuffers;

            return (_scatteredReadEventArgs ??= new ScatteredReadEventArgs()).ReadAsync(Socket, scatteredReadBuffers, cancellationToken);
        }

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
            if (s_sendAsyncWithCancellation is null) throw new PlatformNotSupportedException($"The gather overload of {nameof(WriteAsync)} is not supported on this platform.");

            if (buffers.Count <= 1)
            {
                Write(buffers.Count == 1 ? buffers[0].Span : Array.Empty<byte>().AsSpan(), flushType);
                return;
            }

            List<ArraySegment<byte>> gatheredWriteBuffers = _gatheredWriteBuffers ?? new List<ArraySegment<byte>>(buffers.Count);

            gatheredWriteBuffers.Clear();

            foreach (ReadOnlyMemory<byte> buffer in buffers)
            {
                if (!MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                {
                    EnhancedStream.EmulateWrite(this, buffers, flushType);
                    return;
                }

                gatheredWriteBuffers.Add(segment);
            }

            // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
            _gatheredWriteBuffers = gatheredWriteBuffers;

            Socket.Send(gatheredWriteBuffers, SocketFlags.None);

            if (flushType != FlushType.None)
            {
                Flush(flushType);
            }
        }

        /// <inheritdoc/>
        public virtual ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default)
        {
            ValueTask writeTask = WriteAsync(buffer, cancellationToken);

            if (flushType != FlushType.None && (flushType == FlushType.FlushAndShutdownWrites || GetType() != typeof(EnhancedNetworkStream)))
            {
                return FinishWriteThenFlushAsync(writeTask, flushType, cancellationToken);
            }

            return writeTask;
        }

        /// <inheritdoc/>
        public virtual ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default)
        {
            if (buffers == null) return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(buffers))));
            if (s_sendAsyncWithCancellation is null) return ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new PlatformNotSupportedException($"The gather overload of {nameof(WriteAsync)} is not supported on this platform.")));

            int bufferCount = buffers.Count;
            if (bufferCount <= 1) return WriteAsync(bufferCount == 1 ? buffers[0] : Array.Empty<byte>(), flushType, cancellationToken);

            List<ArraySegment<byte>> gatheredWriteBuffers = _gatheredWriteBuffers ?? new List<ArraySegment<byte>>(buffers.Count);

            gatheredWriteBuffers.Clear();

            for(int i = 0; i < bufferCount; ++i)
            {
                if (!MemoryMarshal.TryGetArray(buffers[i], out ArraySegment<byte> segment))
                {
                    return EnhancedStream.EmulateWriteAsync(this, buffers, flushType, cancellationToken);
                }

                gatheredWriteBuffers.Add(segment);
            }

            // volatile write (REL) to ensure above read (ACQ) has access to updated collection contents.
            _gatheredWriteBuffers = gatheredWriteBuffers;

            GatheredWriteEventArgs eventArgs = _gatheredWriteEventArgs ??= new GatheredWriteEventArgs();

            if (flushType != FlushType.None)
            {
                if (GetType() == typeof(EnhancedNetworkStream))
                {
                    return eventArgs.WriteAsync(Socket, gatheredWriteBuffers, shutdown: flushType == FlushType.FlushAndShutdownWrites, cancellationToken);
                }
                else
                {
                    ValueTask writeTask = eventArgs.WriteAsync(Socket, gatheredWriteBuffers, shutdown: false, cancellationToken);
                    return FinishWriteThenFlushAsync(writeTask, flushType, cancellationToken);
                }
            }

            return eventArgs.WriteAsync(Socket, gatheredWriteBuffers, shutdown: false, cancellationToken);
        }

        private async ValueTask FinishWriteThenFlushAsync(ValueTask valueTask, FlushType flushType, CancellationToken cancellationToken)
        {
            // TODO: optimize this to avoid a state machine.
            await valueTask.ConfigureAwait(false);
            await FlushAsync(flushType, cancellationToken).ConfigureAwait(false);
        }

        private sealed class ScatteredReadEventArgs : SocketAsyncEventArgs, IValueTaskSource<int>
        {
            private ManualResetValueTaskSourceCore<int> _valueTaskSource;

            public ScatteredReadEventArgs()
                : base(unsafeSuppressExecutionContextFlow: true)
            {
            }

            public ValueTask<int> ReadAsync(Socket socket, IList<ArraySegment<byte>> buffers, CancellationToken cancellationToken)
            {
                Debug.Assert(s_receiveAsyncWithCancellation is not null);

                _valueTaskSource.Reset();

                BufferList = buffers;

                try
                {
                    if (!s_receiveAsyncWithCancellation(socket, this, cancellationToken))
                    {
                        if (SocketError == SocketError.Success)
                        {
                            return new ValueTask<int>(BytesTransferred);
                        }

                        Exception ex = new SocketException((int)SocketError);
                        _valueTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                    }
                }
                catch (SocketException ex)
                {
                    _valueTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                }

                return new ValueTask<int>(this, _valueTaskSource.Version);
            }

            protected override void OnCompleted(SocketAsyncEventArgs e)
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
            private ManualResetValueTaskSourceCore<int> _valueTaskSource;
            private Socket? _shutdownSocket;

            public GatheredWriteEventArgs()
                : base(unsafeSuppressExecutionContextFlow: true)
            {
            }

            public ValueTask WriteAsync(Socket socket, IList<ArraySegment<byte>> buffers, bool shutdown, CancellationToken cancellationToken)
            {
                Debug.Assert(s_sendAsyncWithCancellation != null);

                _shutdownSocket = shutdown ? socket : null;
                _valueTaskSource.Reset();

                BufferList = buffers;
                try
                {
                    if (!s_sendAsyncWithCancellation(socket, this, cancellationToken))
                    {
                        if (SocketError == SocketError.Success)
                        {
                            if (shutdown)
                            {
                                socket.Shutdown(SocketShutdown.Send);
                            }

                            return default;
                        }

                        // Use the value task source for an exception case, to avoid allocating a Task.
                        var ex = new SocketException((int)SocketError);
                        _valueTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                    }
                }
                catch (SocketException ex)
                {
                    _valueTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                }

                return new ValueTask(this, _valueTaskSource.Version);
            }

            protected override void OnCompleted(SocketAsyncEventArgs e)
            {
                if (SocketError == SocketError.Success)
                {
                    if (_shutdownSocket is not null)
                    {
                        try
                        {
                            _shutdownSocket.Shutdown(SocketShutdown.Send);
                        }
                        catch (Exception ex)
                        {
                            _valueTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new IOException(ex.Message, ex)));
                            return;
                        }
                    }

                    _valueTaskSource.SetResult(BytesTransferred);
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
