using System.Net.Http.LowLevel.Tests.Connections;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal sealed class Http1TestServer : HttpTestServer
    {
        private readonly ConnectionListener _listener;

        public override EndPoint? EndPoint => _listener.EndPoint;

        public Http1TestServer(ConnectionListener connectionListener)
        {
            _listener = connectionListener;
        }

        public override async Task<HttpTestConnection> AcceptAsync()
        {
            Connection? connection = await _listener.AcceptConnectionAsync().ConfigureAwait(false);
            Debug.Assert(connection != null);

            return new Http1TestConnection(connection);
        }

        public override ValueTask DisposeAsync() =>
            _listener.DisposeAsync();
    }
}
