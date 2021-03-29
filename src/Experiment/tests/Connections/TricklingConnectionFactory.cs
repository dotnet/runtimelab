using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    internal sealed class TricklingConnectionFactory : FilteringConnectionFactory
    {
        private static readonly int[] s_defaultTrickleSequence = new[] { 1 };
        private readonly IEnumerable<int> _trickleSequence = s_defaultTrickleSequence;

        public IEnumerable<int> TrickleSequence
        {
            get => _trickleSequence;
            init
            {
                Debug.Assert(!value.Any(x => x <= 0));
                _trickleSequence = value.ToArray();
            }
        }

        public bool ForceAsync { get; init; }

        public TricklingConnectionFactory(ConnectionFactory baseFactory)
            : base(baseFactory)
        {
        }

        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            Connection c = await BaseFactory.ConnectAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            return new FilteringConnection(c, new TricklingStream(c.Stream, ForceAsync, _trickleSequence));
        }

        public override async ValueTask<ConnectionListener> ListenAsync(EndPoint? endPoint = null, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            ConnectionListener listener = await BaseFactory.ListenAsync(endPoint, options, cancellationToken).ConfigureAwait(false);
            return new TricklingListener(listener, _trickleSequence, ForceAsync);
        }

        private sealed class TricklingListener : FilteringConnectionListener
        {
            private readonly IEnumerable<int> _trickleSequence;
            private readonly bool _forceAsync;

            public TricklingListener(ConnectionListener baseListener, IEnumerable<int> trickleSequence, bool forceAsync) : base(baseListener)
            {
                _trickleSequence = trickleSequence;
                _forceAsync = forceAsync;
            }

            public override async ValueTask<Connection?> AcceptConnectionAsync(IConnectionProperties? options = null, CancellationToken cancellationToken = default)
            {
                Connection? c = await BaseListener.AcceptConnectionAsync(options, cancellationToken).ConfigureAwait(false);
                return c != null ? new FilteringConnection(c, new TricklingStream(c.Stream, _forceAsync, _trickleSequence)) : null;
            }
        }
    }
}
