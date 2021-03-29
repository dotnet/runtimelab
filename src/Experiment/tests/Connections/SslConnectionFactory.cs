using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A connection factory using SSL.
    /// </summary>
    public sealed class SslConnectionFactory : ConnectionFactory
    {
        private readonly ConnectionFactory _baseFactory;
        
        /// <summary>
        /// A connection property used to pass a <see cref="SslClientAuthenticationOptions"/> to <see cref="ConnectAsync(EndPoint, IConnectionProperties?, CancellationToken)"/>.
        /// </summary>
        public static ConnectionPropertyKey<SslClientAuthenticationOptions> SslClientAuthenticationOptionsPropertyKey => new();

        /// <summary>
        /// A connection property used to pass a <see cref="SslServerAuthenticationOptions"/> to <see cref="ConnectionListener.AcceptConnectionAsync(IConnectionProperties?, CancellationToken)"/>.
        /// </summary>
        public static ConnectionPropertyKey<SslServerAuthenticationOptions> SslServerAuthenticationOptionsPropertyKey => new();
        
        /// <summary>
        /// A connection property that returns the underlying <see cref="SslStream"/> of an established <see cref="Connection"/>.
        /// </summary>
        public static ConnectionPropertyKey<SslStream> SslStreamPropertyKey => new();

        /// <summary>
        /// Instantiates a new <see cref="SslConnectionFactory"/>.
        /// </summary>
        /// <param name="baseFactory">The base factory for the <see cref="SslConnectionFactory"/>.</param>
        public SslConnectionFactory(ConnectionFactory baseFactory)
        {
            _baseFactory = baseFactory;
        }

        /// <inheritdoc/>
        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            SslClientAuthenticationOptions sslOptions = options.GetProperty(SslClientAuthenticationOptionsPropertyKey);

            Connection baseConnection = await _baseFactory.ConnectAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            SslStream? stream = null;
            
            try
            {
                stream = new SslStream(baseConnection.Stream, leaveInnerStreamOpen: false);

                await stream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                return new SslConnection(baseConnection, stream);
            }
            catch
            {
                if (stream != null) await stream.DisposeAsync().ConfigureAwait(false);
                await baseConnection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <inheritdoc/>
        public override async ValueTask<ConnectionListener> ListenAsync(EndPoint? endPoint = null, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            SslServerAuthenticationOptions sslOptions = options.GetProperty(SslServerAuthenticationOptionsPropertyKey);

            ConnectionListener baseListener = await _baseFactory.ListenAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            return new SslListener(baseListener, sslOptions);
        }

        /// <inheritdoc/>
        protected override ValueTask DisposeAsyncCore() =>
            _baseFactory.DisposeAsync();

        private sealed class SslListener : FilteringConnectionListener
        {
            private readonly SslServerAuthenticationOptions _sslOptions;

            public SslListener(ConnectionListener baseListener, SslServerAuthenticationOptions sslOptions) : base(baseListener)
            {
                _sslOptions = sslOptions;
            }

            public override async ValueTask<Connection?> AcceptConnectionAsync(IConnectionProperties? options = null, CancellationToken cancellationToken = default)
            {
                Connection? baseConnection = await BaseListener.AcceptConnectionAsync(options, cancellationToken).ConfigureAwait(false);

                if (baseConnection == null)
                {
                    return null;
                }

                SslStream? stream = null;

                try
                {
                    stream = new SslStream(baseConnection.Stream, leaveInnerStreamOpen: false);
                    await stream.AuthenticateAsServerAsync(_sslOptions, cancellationToken).ConfigureAwait(false);
                    return new SslConnection(baseConnection, stream);
                }
                catch
                {
                    if(stream != null) await stream.DisposeAsync().ConfigureAwait(false);
                    await baseConnection.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }

        private sealed class SslConnection : FilteringConnection
        {
            public SslConnection(Connection baseConnection, SslStream stream) : base(baseConnection, stream)
            {
            }

            public override bool TryGetProperty(Type type, out object? value)
            {
                if (type == typeof(SslStream))
                {
                    value = (SslStream)Stream;
                    return true;
                }

                return BaseConnection.TryGetProperty(type, out value);
            }
        }
    }
}
