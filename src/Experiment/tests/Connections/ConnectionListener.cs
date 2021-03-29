using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A connection listener.
    /// </summary>
    public abstract class ConnectionListener : IAsyncDisposable
    {
        private int _disposed;

        /// <summary>
        /// The listener's local <see cref="EndPoint"/>, if any.
        /// </summary>
        public abstract EndPoint? EndPoint { get; }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return Interlocked.Exchange(ref _disposed, 1) == 0
                ? DisposeAsyncCore()
                : default;
        }

        /// <summary>
        /// Disposes of the connection.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected abstract ValueTask DisposeAsyncCore();

        /// <summary>
        /// Accepts a new <see cref="Connection"/>, if available.
        /// </summary>
        /// <param name="options">Any options used to control the operation.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>
        /// If the listener is active, an established <see cref="Connection"/>.
        /// If the operation was cancelled, null.
        /// </returns>
        public abstract ValueTask<Connection?> AcceptConnectionAsync(IConnectionProperties? options = null, CancellationToken cancellationToken = default);
    }
}
