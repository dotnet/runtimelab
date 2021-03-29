using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal sealed class Http1TestStream : HttpTestStream
    {
        private readonly int _streamIdx;
        private readonly Http1TestConnection _connection;
        internal readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(0);
        internal readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(0);
        internal bool _nextReaderReleased, _nextWriterReleased;

        public Http1TestStream(Http1TestConnection connection, int streamIdx)
        {
            _connection = connection;
            _streamIdx = streamIdx;
        }

        public override ValueTask DisposeAsync() =>
            default;

        public override string ToString() =>
            _streamIdx.ToString();

        public override async Task<HttpTestRequest> ReceiveRequestAsync()
        {
            await _readSemaphore.WaitAsync().ConfigureAwait(false);
            return await _connection.ReceiveRequestAsync(this).ConfigureAwait(false);
        }

        public override Stream ReceiveContentStream() =>
            _connection.ReceiveContentStream(this);

        public override Task<TestHeadersSink> ReceiveTrailingHeadersAsync() =>
            _connection.ReceiveTrailingHeadersAsync(this);

        public override async Task SendResponseAsync(int statusCode = 200, TestHeadersSink? headers = null, string? content = null, TestHeadersSink? trailingHeaders = null)
        {
            await _writeSemaphore.WaitAsync();
            await _connection.SendResponseAsync(this, statusCode, headers, content, chunkedContent: null, trailingHeaders).ConfigureAwait(false);
        }

        public override async Task SendChunkedResponseAsync(int statusCode = 200, TestHeadersSink? headers = null, IList<string>? content = null, TestHeadersSink? trailingHeaders = null)
        {
            await _writeSemaphore.WaitAsync();
            await _connection.SendResponseAsync(this, statusCode, headers, content: null, chunkedContent: content, trailingHeaders).ConfigureAwait(false);
        }

        public async Task SendRawResponseAsync(string response)
        {
            await _writeSemaphore.WaitAsync();
            await _connection.SendRawResponseAsync(this, response).ConfigureAwait(false);
        }

        internal void ReleaseNextReader()
        {
            if (!_nextReaderReleased)
            {
                _nextReaderReleased = true;
                _connection.ReleaseNextReader();
            }
        }

        internal void ReleaseNextWriter()
        {
            if (!_nextWriterReleased)
            {
                _nextWriterReleased = true;
                _connection.ReleaseNextWriter();
            }
        }
    }
}
