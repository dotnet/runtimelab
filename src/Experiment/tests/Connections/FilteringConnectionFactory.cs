using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A connection factory that filters another connection factory.
    /// </summary>
    public abstract class FilteringConnectionFactory : ConnectionFactory
    {
        /// <summary>
        /// The base connection factory.
        /// </summary>
        protected ConnectionFactory BaseFactory { get; }

        /// <summary>
        /// Instantiates a new <see cref="FilteringConnectionFactory"/>.
        /// </summary>
        /// <param name="baseFactory">The base connection factory for the <see cref="FilteringConnectionFactory"/>.</param>
        public FilteringConnectionFactory(ConnectionFactory baseFactory)
        {
            BaseFactory = baseFactory ?? throw new ArgumentNullException(nameof(baseFactory));
        }

        /// <inheritdoc/>
        protected override ValueTask DisposeAsyncCore()
            => BaseFactory.DisposeAsync();
    }
}
