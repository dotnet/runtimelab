using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A connection factory that adds write buffering to underlying connections.
    /// </summary>
    public sealed class WriteBufferingConnectionFactory : FilteringConnectionFactory
    {
        private readonly int _writeBufferLength = 4096;

        /// <summary>
        /// Specifies the write buffer size to use.
        /// Defaults to 4096.
        /// </summary>
        public int BufferLength
        {
            get => _writeBufferLength;
            init =>
                _writeBufferLength = value > 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(BufferLength)} must be set to a positive value.");
        }

        /// <summary>
        /// Instantiates a new <see cref="WriteBufferingConnectionFactory"/>.
        /// </summary>
        /// <param name="baseFactory">The underlying factory that will have write buffering added to its connections.</param>
        public WriteBufferingConnectionFactory(ConnectionFactory baseFactory) : base(baseFactory)
        {
        }

        /// <inheritdoc/>
        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            Connection con = await BaseFactory.ConnectAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            return new FilteringConnection(con, new WriteBufferingStream(con.Stream, ownsStream: true, _writeBufferLength));
        }

        /// <inheritdoc/>
        public override async ValueTask<ConnectionListener> ListenAsync(EndPoint? endPoint = null, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            ConnectionListener listener = await BaseFactory.ListenAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            return new WriteBufferingConnectionListener(_writeBufferLength, listener);
        }

        private sealed class WriteBufferingConnectionListener : FilteringConnectionListener
        {
            private readonly int _writeBufferLength;

            public WriteBufferingConnectionListener(int writeBufferLength, ConnectionListener baseListener) : base(baseListener)
            {
                _writeBufferLength = writeBufferLength;
            }

            public override async ValueTask<Connection?> AcceptConnectionAsync(IConnectionProperties? options = null, CancellationToken cancellationToken = default)
            {
                Connection? con = await BaseListener.AcceptConnectionAsync(options, cancellationToken).ConfigureAwait(false);
                if (con == null) return con;

                return new FilteringConnection(con, new WriteBufferingStream(con.Stream, ownsStream: true, _writeBufferLength));
            }
        }
    }
}
