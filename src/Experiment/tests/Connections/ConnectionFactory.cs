using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A connection factory.
    /// </summary>
    public abstract class ConnectionFactory : IAsyncDisposable
    {
        private int _disposed;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return Interlocked.Exchange(ref _disposed, 1) == 0
                ? DisposeAsyncCore()
                : default;
        }

        /// <summary>
        /// Disposes of the connection factory.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected abstract ValueTask DisposeAsyncCore();

        /// <summary>
        /// Establishes a new <see cref="Connection"/> to an <paramref name="endPoint"/>.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> to continue to.</param>
        /// <param name="options">Any options used to control the operation.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>An established <see cref="Connection"/>.</returns>
        public abstract ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts listening at an <paramref name="endPoint"/>.
        /// </summary>
        /// <param name="endPoint">The <see cref="EndPoint"/> to listen on, if any.</param>
        /// <param name="options">Any options used to control the operation.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>An active <see cref="ConnectionListener"/>.</returns>
        public abstract ValueTask<ConnectionListener> ListenAsync(EndPoint? endPoint = null, IConnectionProperties? options = null, CancellationToken cancellationToken = default);
    }
}
