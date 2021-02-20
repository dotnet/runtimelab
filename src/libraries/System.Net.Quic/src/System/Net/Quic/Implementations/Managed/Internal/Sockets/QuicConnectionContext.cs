// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Net.Quic.Implementations.Managed.Internal.Headers;
using System.Net.Quic.Implementations.Managed.Internal.Packets;
using System.Net.Quic.Implementations.Managed.Internal.Parsing;
using System.Net.Quic.Implementations.Managed.Internal.Tls;
using System.Net.Quic.Implementations.MsQuic.Internal;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.Net.Quic.Implementations.Managed.Internal.Sockets
{
    /// <summary>
    ///     Class hosting the background processing thread for a single instance of a QuicConnection.
    /// </summary>
    internal sealed class QuicConnectionContext
    {
        private readonly QuicSocketContext _parent;

        private readonly QuicSocketContext.RecvContext _recvContext;

        // TODO-RZ: maybe bounded channel with drop behavior would be better?
        private readonly Channel<DatagramInfo> _recvQueue = Channel.CreateUnbounded<DatagramInfo>(
            new UnboundedChannelOptions
            {
                SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false
            });

        private readonly QuicSocketContext.SendContext _sendContext;

        private Task _backgroundWorkerTask = Task.CompletedTask;
        private readonly QuicReader _reader = new QuicReader(Memory<byte>.Empty);

        private long _timer = long.MaxValue;
        private int _delay = int.MaxValue;
        private bool _waiting; // for debug only

        private ResettableValueTaskSource _waitCompletionSource = new ResettableValueTaskSource();
        // set together with signaling _waitCompletionSource to prevent data races when resetting the _waitCompletionSource
        private bool _stopWait;

        private readonly QuicWriter _writer = new QuicWriter(Memory<byte>.Empty);

        public QuicConnectionContext(QuicTlsProvider tlsProvider, QuicServerSocketContext parent, EndPoint remoteEndpoint, ReadOnlySpan<byte> odcid)
        {
            _parent = parent;
            Connection = new ManagedQuicConnection(tlsProvider, parent.ListenerOptions, this, remoteEndpoint, odcid);
            Connection.SetSocketContext(this);

            ObjectPool<SentPacket>? sentPacketPool = new ObjectPool<SentPacket>(256);
            _sendContext = new QuicSocketContext.SendContext(sentPacketPool);
            _recvContext = new QuicSocketContext.RecvContext(sentPacketPool);
        }

        public QuicConnectionContext(QuicClientSocketContext parent, ManagedQuicConnection connection)
        {
            _parent = parent;
            Connection = connection;

            ObjectPool<SentPacket>? sentPacketPool = new ObjectPool<SentPacket>(256);
            _sendContext = new QuicSocketContext.SendContext(sentPacketPool);
            _recvContext = new QuicSocketContext.RecvContext(sentPacketPool);
        }

        private ArrayPool<byte> ArrayPool => _parent.ArrayPool;
        internal ManagedQuicConnection Connection { get; }

        /// <summary>
        ///     Local endpoint of the socket backing the background processing.
        /// </summary>
        public IPEndPoint LocalEndPoint => _parent.LocalEndPoint;

        private void DoReceiveDatagram(DatagramInfo datagram)
        {
            _reader.Reset(datagram.Buffer.AsMemory(0, datagram.Length));

            _recvContext.Timestamp = Timestamp.Now;
            Connection.ReceiveData(_reader, datagram.RemoteEndpoint, _recvContext);
        }

        /// <summary>
        ///     Starts the background processing, if not yet started.
        /// </summary>
        public void Start()
        {
            _backgroundWorkerTask = Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            _parent.Start();
        }

        /// <summary>
        ///     Signals the thread that the pending wait or sleep should be interrupted because the connection has new
        ///     data from the application that need to be processed.
        /// </summary>
        public void WakeUp()
        {
            Volatile.Write(ref _stopWait, true);
            _waitCompletionSource.SignalWaiter();
        }

        public void EnqueueDatagram(DatagramInfo datagram)
        {
            _recvQueue.Writer.TryWrite(datagram);
            WakeUp();
        }

        private async Task Run()
        {
            void CheckForStateChange(ref QuicConnectionState oldState)
            {
                QuicConnectionState newState = Connection.ConnectionState;
                if (oldState != newState)
                {
                    _parent.OnConnectionStateChanged(Connection, newState);
                    oldState = newState;
                }
            }

            try
            {
                while (Connection.ConnectionState != QuicConnectionState.BeforeClosed)
                {
                    QuicConnectionState previousState = Connection.ConnectionState;

                    if (Timestamp.Now >= _timer)
                    {
                        Connection.OnTimeout(Timestamp.Now);
                        CheckForStateChange(ref previousState);
                    }

                    while (_recvQueue.Reader.TryRead(out DatagramInfo datagram))
                    {
                        DoReceiveDatagram(datagram);
                        ArrayPool.Return(datagram.Buffer);
                        CheckForStateChange(ref previousState);
                    }

                    if (Connection.GetWriteLevel(Timestamp.Now) != EncryptionLevel.None)
                    {
                        // TODO: discover path MTU
                        byte[]? buffer = ArrayPool.Rent(QuicConstants.Internal.MaximumAllowedDatagramSize);
                        _writer.Reset(buffer);
                        _sendContext.Timestamp = Timestamp.Now;
                        Connection.SendData(_writer, out var receiver, _sendContext);

                        if (_writer.BytesWritten > 0)
                        {
                            _parent.SendDatagram(new DatagramInfo(buffer, _writer.BytesWritten, receiver));
                        }

                        ArrayPool.Return(buffer);
                        CheckForStateChange(ref previousState);
                    }

                    // previous action may have caused the connection to close
                    if (Connection.ConnectionState == QuicConnectionState.BeforeClosed)
                        break;

                    long now = Timestamp.Now;
                    _timer = Connection.GetNextTimerTimestamp();
                    _delay = _timer < long.MaxValue ? (int) Timestamp.GetMilliseconds(_timer - now) : int.MaxValue;
                    if (_timer == long.MaxValue || _delay > 0)
                    {
                        Volatile.Write(ref _waiting, true);
                        // asynchronously wait until either the timer expires or we receive a new datagram
                        _waitCompletionSource.Reset();

                        // guard against race condition with WakeUp, if it was called just before Reset above
                        if (!Volatile.Read(ref _stopWait))
                        {
                            // cancelling this token will stop the (asynchronous) wait for next event
                            CancellationTokenSource cts = new CancellationTokenSource();
                            await using CancellationTokenRegistration registration = cts.Token.Register(static s =>
                            {
                                ((ResettableValueTaskSource?) s)?.SignalWaiter();
                            }, _waitCompletionSource);


                            if (_timer < long.MaxValue)
                            {
                                cts.CancelAfter(_delay);
                                Connection.Trace?.OnStartingWait(_delay);
                            }

                            await _waitCompletionSource.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                            // dispose of the registration now so that the wait source is not double completed unnecessarily
                            await registration.DisposeAsync().ConfigureAwait(false);

                            // make sure the token is canceled if the source was completed by the WakeUp method.
                            cts.Cancel();
                        }
                        else
                        {
                            // since we already resetted the wait source, we need to complete it
                            _waitCompletionSource.SignalWaiter();
                            await _waitCompletionSource.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                        }

                        Volatile.Write(ref _stopWait, false);
                        Volatile.Write(ref _waiting, false);
                    }
                }
            }
            catch (Exception e)
            {
                Connection.OnSocketContextException(e);
            }
            finally
            {
                Connection.DoCleanup();
            }
        }

        // TODO-RZ: this has been copied from StreamBuffer class, perhaps we should make it public and make it reuseable?
        private sealed class ResettableValueTaskSource : IValueTaskSource
        {
            // This object is used as the backing source for ValueTask.
            // There should only ever be one awaiter at a time; users of this object must ensure this themselves.
            // We use _hasWaiter to ensure mutual exclusion between successful completion and cancellation,
            // and dispose/clear the cancellation registration in GetResult to guarantee it will not affect subsequent waiters.
            // The rest of the logic is deferred to ManualResetValueTaskSourceCore.

            private ManualResetValueTaskSourceCore<bool> _waitSource; // mutable struct, do not make this readonly
            private CancellationTokenRegistration _waitSourceCancellation;
            private int _hasWaiter;

            ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _waitSource.GetStatus(token);

            void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) => _waitSource.OnCompleted(continuation, state, token, flags);

            void IValueTaskSource.GetResult(short token)
            {
                Debug.Assert(_hasWaiter == 0);

                // Clean up the registration.  This will wait for any in-flight cancellation to complete.
                _waitSourceCancellation.Dispose();
                _waitSourceCancellation = default;

                // Propagate any exceptions if there were any.
                _waitSource.GetResult(token);
            }

            public void SignalWaiter()
            {
                if (Interlocked.Exchange(ref _hasWaiter, 0) == 1)
                {
                    _waitSource.SetResult(true);
                }
            }

            private void CancelWaiter(CancellationToken cancellationToken)
            {
                if (Interlocked.Exchange(ref _hasWaiter, 0) == 1)
                {
                    _waitSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new OperationCanceledException(cancellationToken)));
                }
            }

            public void Reset()
            {
                if (_hasWaiter != 0)
                {
                    throw new InvalidOperationException("Concurrent use is not supported");
                }

                _waitSource.Reset();
                Volatile.Write(ref _hasWaiter, 1);
            }

            public void Wait()
            {
                _waitSource.RunContinuationsAsynchronously = false;
                new ValueTask(this, _waitSource.Version).AsTask().GetAwaiter().GetResult();
            }

            public ValueTask WaitAsync(CancellationToken cancellationToken)
            {
                _waitSource.RunContinuationsAsynchronously = true;

                _waitSourceCancellation = cancellationToken.UnsafeRegister(static (s, token) => ((ResettableValueTaskSource)s!).CancelWaiter(token), this);

                return new ValueTask(this, _waitSource.Version);
            }
        }
    }
}
