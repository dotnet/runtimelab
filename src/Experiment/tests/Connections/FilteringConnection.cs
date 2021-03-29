using System.IO;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A connection that filters another connection.
    /// </summary>
    public class FilteringConnection : Connection
    {
        /// <summary>
        /// The base connection.
        /// </summary>
        protected Connection BaseConnection { get; }

        /// <inheritdoc/>
        public override EndPoint? LocalEndPoint => BaseConnection.LocalEndPoint;

        /// <inheritdoc/>
        public override EndPoint? RemoteEndPoint => BaseConnection.RemoteEndPoint;

        /// <summary>
        /// Instantiates a new <see cref="FilteringConnection"/>
        /// </summary>
        /// <param name="baseConnection">The base connection for the <see cref="FilteringConnection"/>.</param>
        /// <param name="stream">The connection's stream.</param>
        public FilteringConnection(Connection baseConnection, Stream stream) : base(stream)
        {
            BaseConnection = baseConnection ?? throw new ArgumentNullException(nameof(baseConnection));
        }

        /// <inheritdoc/>
        protected override async ValueTask DisposeAsyncCore()
        {
            await Stream.DisposeAsync().ConfigureAwait(false);
            await BaseConnection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
