using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A connection factory using sockets.
    /// </summary>
    public class SocketConnectionFactory : ConnectionFactory
    {
        /// <summary>
        /// A property key to retrieve the underlying <see cref="Socket"/> of the connection.
        /// </summary>
        public static ConnectionPropertyKey<Socket> SocketPropertyKey => new ();

        private readonly AddressFamily _addressFamily;
        private readonly SocketType _socketType;
        private readonly ProtocolType _protocolType;

        /// <summary>
        /// Instantiates a new <see cref="SocketConnectionFactory"/>.
        /// </summary>
        /// <param name="socketType">The <see cref="SocketType"/> to use when creating sockets.</param>
        /// <param name="protocolType">The <see cref="ProtocolType"/> to use when creating sockets.</param>
        /// <param name="addressFamily">
        /// The <see cref="AddressFamily"/> to use when creating sockets.
        /// If <see cref="AddressFamily.Unspecified"/> (the default), IPv6 or IPv4 will be used based on platform support.
        /// </param>
        public SocketConnectionFactory(SocketType socketType = SocketType.Stream, ProtocolType protocolType = ProtocolType.Tcp, AddressFamily addressFamily = AddressFamily.Unspecified)
        {
            _addressFamily =
                _addressFamily != AddressFamily.Unspecified ? addressFamily :
                Socket.OSSupportsIPv6 ? AddressFamily.InterNetworkV6 :
                AddressFamily.InterNetwork;
            _socketType = socketType;
            _protocolType = protocolType;
        }

        /// <inheritdoc/>
        protected override ValueTask DisposeAsyncCore() =>
            default;

        /// <summary>
        /// Creates a <see cref="Socket"/> used by the connection factory.
        /// </summary>
        /// <param name="addressFamily">The <see cref="AddressFamily"/> of the <see cref="Socket"/> to create.</param>
        /// <param name="socketType">The <see cref="SocketType"/> of the <see cref="Socket"/> to create.</param>
        /// <param name="protocolType">The <see cref="ProtocolType"/> of the <see cref="Socket"/> to create.</param>
        /// <param name="options">Options given to <see cref="ConnectAsync(EndPoint, IConnectionProperties?, CancellationToken)"/>, <see cref="ListenAsync(EndPoint?, IConnectionProperties?, CancellationToken)"/>, or <see cref="ConnectionListener.AcceptConnectionAsync(IConnectionProperties?, CancellationToken)"/>.</param>
        /// <returns>A new <see cref="Socket"/>.</returns>
        protected virtual Socket CreateSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, IConnectionProperties? options)
        {
            var sock = new Socket(addressFamily, socketType, protocolType);

            try
            {
                if (addressFamily == AddressFamily.InterNetworkV6)
                {
                    sock.DualMode = true;
                }

                if (protocolType == ProtocolType.Tcp)
                {
                    sock.NoDelay = true;
                }

                return sock;
            }
            catch
            {
                sock.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a <see cref="NetworkStream"/> over a <see cref="Socket"/>.
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> to create a <see cref="NetworkStream"/> over.</param>
        /// <param name="options">Options given to <see cref="ConnectAsync(EndPoint, IConnectionProperties?, CancellationToken)"/>, <see cref="ListenAsync(EndPoint?, IConnectionProperties?, CancellationToken)"/>, or <see cref="ConnectionListener.AcceptConnectionAsync(IConnectionProperties?, CancellationToken)"/>.</param>
        /// <returns>A new <see cref="NetworkStream"/>. This stream must take ownership over <paramref name="socket"/>.</returns>
        /// <remarks>The default implementation returns a <see cref="EnhancedNetworkStream"/>, offering better performance for supporting usage.</remarks>
        protected virtual NetworkStream CreateNetworkStream(Socket socket, IConnectionProperties? options) =>
            new EnhancedNetworkStream(socket ?? throw new ArgumentNullException(nameof(socket)), ownsSocket: true);

        /// <inheritdoc/>
        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            switch (endPoint)
            {
                case null: throw new ArgumentNullException(nameof(endPoint));
                case IPEndPoint ip6 when ip6.Address.Equals(IPAddress.IPv6Any):
                    // For compatibility with a listener's EndPoint being Any, connect to Loopback instead.
                    endPoint = new IPEndPoint(IPAddress.IPv6Loopback, ip6.Port);
                    break;
                case IPEndPoint ip4 when ip4.Address.Equals(IPAddress.Any):
                    endPoint = new IPEndPoint(IPAddress.Loopback, ip4.Port);
                    break;
            }

            Socket sock = CreateSocket(_addressFamily, _socketType, _protocolType, options);

            try
            {
                await sock.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
                return new SocketConnection(sock, CreateNetworkStream(sock, options));
            }
            catch
            {
                sock.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        public override ValueTask<ConnectionListener> ListenAsync(EndPoint? endPoint = null, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled<ConnectionListener>(cancellationToken);

            if (endPoint == null)
            {
                IPAddress address;

                if (_addressFamily == AddressFamily.InterNetworkV6)
                {
                    address = IPAddress.IPv6Any;
                }
                else if (_addressFamily == AddressFamily.InterNetwork)
                {
                    address = IPAddress.Any;
                }
                else
                {
                    return ValueTask.FromException<ConnectionListener>(ExceptionDispatchInfo.SetCurrentStackTrace(new ArgumentNullException(nameof(endPoint))));
                }

                endPoint = new IPEndPoint(address, 0);
            }

            Socket sock = CreateSocket(_addressFamily, _socketType, _protocolType, options);

            try
            {
                sock.Bind(endPoint!); // relying on Bind() checking args.
                sock.Listen();
                return new ValueTask<ConnectionListener>(new SocketListener(this, sock));
            }
            catch(Exception ex)
            {
                sock.Dispose();
                return ValueTask.FromException<ConnectionListener>(ex);
            }
        }

        private sealed class SocketListener : ConnectionListener
        {
            private readonly SocketConnectionFactory _connectionFactory;
            private readonly Socket _listener;
            private readonly EventArgs _args = new EventArgs();

            public override EndPoint? EndPoint => _listener.LocalEndPoint;

            public SocketListener(SocketConnectionFactory connectionFactory, Socket listener)
            {
                _connectionFactory = connectionFactory;
                _listener = listener;
            }

            protected override ValueTask DisposeAsyncCore()
            {
                try
                {
                    _listener.Dispose();
                    _args.Dispose();
                    return default;
                }
                catch (Exception ex)
                {
                    return ValueTask.FromException(ex);
                }
            }

            public override async ValueTask<Connection?> AcceptConnectionAsync(IConnectionProperties? options = null, CancellationToken cancellationToken = default)
            {
                CancellationTokenRegistration tokenRegistration = default;

                try
                {
                    Debug.Assert(_args.AcceptSocket == null);
                    _args.AcceptSocket = null;
                    _args.Reset();

                    if (_listener.AcceptAsync(_args))
                    {
                        tokenRegistration = cancellationToken.UnsafeRegister(static o => ((Socket)o!).Dispose(), _listener);
                    }
                    else
                    {
                        _args.SetResult();
                    }

                    await _args.Task.ConfigureAwait(false);

                    if (_args.SocketError == SocketError.Success)
                    {
                        Socket socket = _args.AcceptSocket!;
                        _args.AcceptSocket = null;

                        try
                        {
                            return new SocketConnection(socket, _connectionFactory.CreateNetworkStream(socket, options));
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }

                    if (_args.SocketError == SocketError.OperationAborted)
                    {
                        return null;
                    }

                    var sockEx = new SocketException((int)_args.SocketError);

                    throw _args.SocketError == SocketError.OperationAborted && cancellationToken.IsCancellationRequested
                        ? (Exception)new OperationCanceledException("The connect has been canceled.", sockEx, cancellationToken)
                        : sockEx;
                }
                finally
                {
                    await tokenRegistration.DisposeAsync().ConfigureAwait(false);

                    if (_args.AcceptSocket is Socket acceptSocket)
                    {
                        acceptSocket.Dispose();
                        _args.AcceptSocket = null;
                    }
                }
            }
        }

        private sealed class SocketConnection : Connection
        {
            public override EndPoint? LocalEndPoint => Socket.LocalEndPoint;
            public override EndPoint? RemoteEndPoint => Socket.RemoteEndPoint;

            private Socket Socket { get; }

            public SocketConnection(Socket socket, Stream stream) : base(stream)
            {
                Socket = socket;
            }

            protected override ValueTask DisposeAsyncCore()
                => default;

            public override bool TryGetProperty(Type type, out object? value)
            {
                if (type == typeof(Socket))
                {
                    value = Socket;
                    return true;
                }

                if (type == null)
                {
                    throw new ArgumentNullException(nameof(type));
                }

                value = null;
                return false;
            }
        }

        private sealed class EventArgs : SocketTaskEventArgs<int>
        {
            public void SetResult() => base.SetResult(0);
            protected override void OnCompleted(SocketAsyncEventArgs e) =>
                SetResult(0);
        }
    }
}
