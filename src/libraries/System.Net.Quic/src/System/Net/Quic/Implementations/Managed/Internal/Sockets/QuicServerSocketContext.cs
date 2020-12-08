// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Quic.Implementations.Managed.Internal.Headers;
using System.Net.Quic.Implementations.Managed.Internal.Tls;
using System.Threading.Channels;

namespace System.Net.Quic.Implementations.Managed.Internal.Sockets
{
    internal sealed class QuicServerSocketContext : QuicSocketContext
    {
        private readonly QuicTlsProvider _tlsProvider;

        private readonly ChannelWriter<QuicConnectionProvider> _newConnections;
        internal QuicListenerOptions ListenerOptions { get; }

        private ImmutableDictionary<EndPoint, QuicConnectionContext> _connectionsByEndpoint;

        private bool _acceptNewConnections;

        internal QuicServerSocketContext(QuicTlsProvider tlsProvider, IPEndPoint localEndPoint,
            QuicListenerOptions listenerOptions,
            ChannelWriter<QuicConnectionProvider> newConnectionsWriter)
            : base(localEndPoint, null, true)
        {
            _tlsProvider = tlsProvider;
            _newConnections = newConnectionsWriter;
            ListenerOptions = listenerOptions;

            _connectionsByEndpoint = ImmutableDictionary<EndPoint, QuicConnectionContext>.Empty;

            _acceptNewConnections = true;
        }

        protected override void OnDatagramReceived(in DatagramInfo datagram)
        {
            bool isNewConnection = false;
            if (!_connectionsByEndpoint.TryGetValue(datagram.RemoteEndpoint, out QuicConnectionContext? connectionCtx))
            {
                if (!_acceptNewConnections || HeaderHelpers.GetPacketType(datagram.Buffer[0]) != PacketType.Initial)
                {
                    // TODO-RZ: send CONNECTION_REFUSED for valid initial packets
                    return;
                }

                // new connection attempt
                if (!HeaderHelpers.TryFindDestinationConnectionId(datagram.Buffer.AsSpan(), out var dcid))
                {
                    // drop packet
                    return;
                }

                // TODO-RZ: handle connection failures when the initial packet is discarded (e.g. because connection id is
                // too long). This likely will need moving header parsing from Connection to socket context.
                connectionCtx = new QuicConnectionContext(_tlsProvider, this, datagram.RemoteEndpoint, dcid);
                ImmutableInterlocked.TryAdd(ref _connectionsByEndpoint, datagram.RemoteEndpoint, connectionCtx);

                isNewConnection = true;
            }

            connectionCtx.IncomingDatagramWriter.TryWrite(datagram);

            // the start is deferred until we write the first datagram into the queue to prevent the thread going to
            // sleep immediately after start
            if (isNewConnection)
                connectionCtx.Start();
        }

        /// <summary>
        ///     Signals that the context should no longer accept new connections.
        /// </summary>
        internal void StopOrOrphan()
        {
            _acceptNewConnections = false;
            _newConnections.TryComplete();
            if (_connectionsByEndpoint.IsEmpty)
            {
                SignalStop();
            }
        }

        private void OnConnectionHandshakeCompleted(ManagedQuicConnection connection)
        {
            // Connection established -> pass it to the listener
            _newConnections.TryWrite(connection);
        }

        protected internal override bool OnConnectionStateChanged(ManagedQuicConnection connection, QuicConnectionState newState)
        {
            switch (newState)
            {
                case QuicConnectionState.None:
                    return false;
                case QuicConnectionState.Connected:
                    OnConnectionHandshakeCompleted(connection);
                    return true;
                case QuicConnectionState.Closing:
                    return false;
                case QuicConnectionState.Draining:
                    // RFC: Servers that retain an open socket for accepting new connections SHOULD NOT exit the closing
                    // or draining period early.

                    // this means that we need to keep the connection in the map until the timer runs out, closing event
                    // will be already signaled to user.
                    if (_acceptNewConnections)
                    {
                        return true;
                    }

                    // if not accepting new connections, we can close straight away
                    connection.LocalClose();
                    goto case QuicConnectionState.BeforeClosed;
                case QuicConnectionState.BeforeClosed:
                    // draining timer elapsed, remove the connection
                    DetachConnection(connection);
                    return true;

                case QuicConnectionState.Closed: // We should not get there in a closed state
                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }
        }

        protected override void OnException(Exception e)
        {
            foreach (var ctx in _connectionsByEndpoint.Values)
            {
                ctx.Connection.OnSocketContextException(e);
            }
        }

        protected internal override void DetachConnection(ManagedQuicConnection connection)
        {
            bool removed = ImmutableInterlocked.TryRemove(ref _connectionsByEndpoint, connection.RemoteEndPoint, out _);
            if (_connectionsByEndpoint.IsEmpty && !_acceptNewConnections)
            {
                SignalStop();
            }

            Debug.Assert(removed);
        }
    }
}
