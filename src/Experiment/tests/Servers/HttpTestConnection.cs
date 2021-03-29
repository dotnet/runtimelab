using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal abstract class HttpTestConnection : IAsyncDisposable
    {
        public abstract ValueTask DisposeAsync();
        public abstract Task<HttpTestStream> AcceptStreamAsync();

        public async Task<HttpTestFullRequest> ReceiveAndSendSingleRequestAsync(int statusCode = 200, TestHeadersSink? headers = null, string? content = null, TestHeadersSink? trailingHeaders = null)
        {
            HttpTestStream stream = await AcceptStreamAsync().ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                return await stream.ReceiveAndSendAsync(statusCode, headers, content, trailingHeaders).ConfigureAwait(false);
            }
        }
    }
}
