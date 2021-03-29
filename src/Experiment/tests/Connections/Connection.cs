using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A Stream-oriented connection.
    /// </summary>
    public abstract class Connection : IAsyncDisposable, IConnectionProperties
    {
        private Stream? _stream;
        private int _disposed;

        /// <summary>
        /// The connection's local endpoint, if any.
        /// </summary>
        public abstract EndPoint? LocalEndPoint { get; }

        /// <summary>
        /// The connection's remote endpoint, if any.
        /// </summary>
        public abstract EndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// The connection's stream.
        /// </summary>
        public Stream Stream => _disposed == 2 ? throw new ObjectDisposedException(GetType().Name) : _stream!;

        /// <summary>
        /// Constructs a new <see cref="Connection"/> with a stream.
        /// </summary>
        /// <param name="stream">The connection's stream.</param>
        protected Connection(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <inheritdoc/>
        public virtual async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            await DisposeAsyncCore().ConfigureAwait(false);
            Volatile.Write(ref _disposed, 2);

            Stream? stream = _stream;
            Debug.Assert(stream != null);

            _stream = null;

            await stream.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes of the connection.
        /// The connection's <see cref="Stream"/> will be disposed immediately after <see cref="DisposeAsyncCore()"/>.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected abstract ValueTask DisposeAsyncCore();

        /// <inheritdoc/>
        public virtual bool TryGetProperty(Type type, out object? value)
        {
            value = null;
            return false;
        }
    }
}
