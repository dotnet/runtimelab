using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal abstract class HttpTestStream : IAsyncDisposable
    {
        public abstract ValueTask DisposeAsync();
        public abstract Task<HttpTestRequest> ReceiveRequestAsync();
        public abstract Stream ReceiveContentStream();
        public abstract Task<TestHeadersSink> ReceiveTrailingHeadersAsync();
        public abstract Task SendResponseAsync(int statusCode = 200, TestHeadersSink? headers = null, string? content = null, TestHeadersSink? trailingHeaders = null);
        public abstract Task SendChunkedResponseAsync(int statusCode = 200, TestHeadersSink? headers = null, IList<string>? content = null, TestHeadersSink? trailingHeaders = null);

        public async Task<string> ReceiveContentStringAsync()
        {
            Stream stream = ReceiveContentStream();
            await using (stream.ConfigureAwait(false))
            {
                using var sr = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                return await sr.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        public async Task<HttpTestFullRequest> ReceiveFullRequestAsync()
        {
            HttpTestRequest request = await ReceiveRequestAsync().ConfigureAwait(false);
            string content = await ReceiveContentStringAsync().ConfigureAwait(false);
            TestHeadersSink trailingHeaders = await ReceiveTrailingHeadersAsync().ConfigureAwait(false);
            return new HttpTestFullRequest(request.Method, request.PathAndQuery, request.Version, request.Headers, content, trailingHeaders);
        }

        public async Task<HttpTestFullRequest> ReceiveAndSendAsync(int statusCode = 200, TestHeadersSink? headers = null, string? content = null, TestHeadersSink? trailingHeaders = null)
        {
            HttpTestFullRequest request = await ReceiveFullRequestAsync().ConfigureAwait(false);
            await SendResponseAsync(statusCode, headers, content, trailingHeaders).ConfigureAwait(false);
            return request;
        }

        public async Task<HttpTestFullRequest> ReceiveAndSendChunkedAsync(int statusCode = 200, TestHeadersSink? headers = null, IList<string>? content = null, TestHeadersSink? trailingHeaders = null)
        {
            HttpTestFullRequest request = await ReceiveFullRequestAsync().ConfigureAwait(false);
            await SendChunkedResponseAsync(statusCode, headers, content, trailingHeaders).ConfigureAwait(false);
            return request;
        }
    }
}
