using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.LowLevel.Tests.Connections;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal sealed class Http1TestConnection : HttpTestConnection
    {
        private object Sync => _readQueue;
        private readonly Queue<Http1TestStream> _readQueue = new Queue<Http1TestStream>();
        private readonly Queue<Http1TestStream> _writeQueue = new Queue<Http1TestStream>();
        private readonly Connection _connection;
        internal readonly Stream _stream;
        internal ArrayBuffer _readBuffer;
        private int _activeReaders, _activeWriters, _streamIdx;

        internal long? _responseContentLength;
        internal bool _responseIsChunked;

        public Http1TestConnection(Connection connection)
        {
            _connection = connection;
            _stream = connection.Stream;
            _readBuffer = new ArrayBuffer(4096);
        }

        public override Task<HttpTestStream> AcceptStreamAsync()
        {
            Http1TestStream stream;
            bool startRead, startWrite;

            lock (Sync)
            {
                startRead = _activeReaders++ == 0;
                startWrite = _activeWriters++ == 0;

                stream = new Http1TestStream(this, ++_streamIdx);
                if(!startRead) _readQueue.Enqueue(stream);
                if(!startWrite) _writeQueue.Enqueue(stream);
            }

            if (startRead) stream._readSemaphore.Release();
            if (startWrite) stream._writeSemaphore.Release();

            return Task.FromResult<HttpTestStream>(stream);
        }

        public override async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _readBuffer.Dispose();
        }

        internal async Task<HttpTestRequest> ReceiveRequestAsync(Http1TestStream stream)
        {
            string request = await ReadLineAsync().ConfigureAwait(false);

            Match match = Regex.Match(request, @"^([^ ]+) ([^ ]+) HTTP/(\d).(\d)$");
            if (!match.Success) throw new Exception("Invalid request line.");

            string method = match.Groups[1].Value;
            string pathAndQuery = match.Groups[2].Value;
            int versionMajor = int.Parse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            int versionMinor = int.Parse(match.Groups[4].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            var version = new Version(versionMajor, versionMinor);

            TestHeadersSink headers = await ReadHeadersAsync().ConfigureAwait(false);

            _responseIsChunked = headers.TryGetSingleValue("transfer-encoding", out string? transferEncoding) && transferEncoding == "chunked";
            
            _responseContentLength = headers.TryGetSingleValue("content-length", out string? contentLength)
                ? int.Parse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture)
                : method switch
                {
                    "GET" when _responseIsChunked => null,
                    "GET" or "HEAD" or "DELETE" or "TRACE" => 0,
                    _ => null
                };

            if (!_responseIsChunked && _responseContentLength == 0)
            {
                stream.ReleaseNextReader();
            }

            return new HttpTestRequest(method, pathAndQuery, version, headers);
        }

        internal Stream ReceiveContentStream(Http1TestStream stream) =>
            _responseIsChunked ? new Http1TestChunkedStream(this, _responseContentLength) :
            _responseContentLength != null ? new Http1TestContentLengthStream(this, _responseContentLength != 0 ? stream : null, _responseContentLength.Value) :
            new Http1TestLengthlessStream(this);

        internal async Task<TestHeadersSink> ReceiveTrailingHeadersAsync(Http1TestStream stream)
        {
            if (_responseIsChunked)
            {
                TestHeadersSink headers = await ReadHeadersAsync().ConfigureAwait(false);
                stream.ReleaseNextReader();
                return headers;
            }
            else
            {

                return new TestHeadersSink();
            }
        }

        private async Task<TestHeadersSink> ReadHeadersAsync()
        {
            var headers = new TestHeadersSink();

            string header;
            while ((header = await ReadLineAsync().ConfigureAwait(false)).Length != 0)
            {
                (string headerName, string headerValue) = ParseHeader(header);
                headers.Add(headerName, headerValue);
            }

            return headers;
        }

        private static (string headerName, string headerValue) ParseHeader(string header)
        {
            int idx = header.IndexOf(':');
            if (idx == -1) throw new ArgumentException("The value is not a valid header.", nameof(header));

            string headerName = header[..idx];
            string headerValue = header[(idx + 1)..].Trim(' ');
            return (headerName, headerValue);
        }

        internal async Task<string> ReadLineAsync()
        {
            string? line;

            while (!TryReadLine(out line))
            {
                if (!await FillReadBufferAsync().ConfigureAwait(false))
                {
                    throw new Exception("Unexpected end of stream. Expected CRLF.");
                }
            }

            return line;
        }

        private bool TryReadLine([NotNullWhen(true)] out string? line)
        {
            ReadOnlySpan<byte> span = _readBuffer.ActiveSpan;

            int endIdx = span.IndexOf(new[] { (byte)'\r', (byte)'\n' });
            if (endIdx == -1)
            {
                line = null;
                return false;
            }

            line = Encoding.ASCII.GetString(span.Slice(0, endIdx));
            _readBuffer.Discard(endIdx + 2);
            return true;
        }

        private async Task<bool> FillReadBufferAsync()
        {
            _readBuffer.EnsureAvailableSpace(1);

            int readLen = await _stream.ReadAsync(_readBuffer.AvailableMemory).ConfigureAwait(false);
            if (readLen == 0) return false;

            _readBuffer.Commit(readLen);
            return true;
        }

        internal async Task SendResponseAsync(Http1TestStream stream, int statusCode, TestHeadersSink? headers, string? content, IList<string>? chunkedContent, TestHeadersSink? trailingHeaders)
        {
            Debug.Assert(content == null || chunkedContent == null, $"Only one of {nameof(content)} and {nameof(chunkedContent)} can be specified.");

            var newHeaders = new TestHeadersSink();

            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    foreach (var value in kvp.Value)
                    {
                        newHeaders.Add(kvp.Key, value);
                    }
                }
            }

            bool chunked = chunkedContent != null || trailingHeaders?.Count > 0;
            if (chunked && !newHeaders.ContainsKey("transfer-encoding"))
            {
                newHeaders.Add("transfer-encoding", "chunked");
            }

            if (!newHeaders.ContainsKey("content-length"))
            {
                int contentLength = content?.Length ?? chunkedContent?.Sum(x => (int?)x.Length) ?? 0;
                newHeaders.Add("content-length", contentLength.ToString(CultureInfo.InvariantCulture));
            }

            using var writer = new StreamWriter(_stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = false };

            await writer.WriteAsync("HTTP/1.1 ").ConfigureAwait(false);
            await writer.WriteAsync(statusCode.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await writer.WriteAsync(" ").ConfigureAwait(false);
            await writer.WriteAsync(GetReasonPhrase(statusCode)).ConfigureAwait(false);
            await writer.WriteAsync("\r\n").ConfigureAwait(false);
            await WriteHeadersAsync(writer, newHeaders).ConfigureAwait(false);

            if (chunked)
            {
                if (chunkedContent != null)
                {
                    foreach (string chunk in chunkedContent)
                    {
                        await WriteChunkAsync(chunk).ConfigureAwait(false);
                    }
                }
                else if (content?.Length > 0)
                {
                    await WriteChunkAsync(content).ConfigureAwait(false);
                }

                await writer.WriteAsync("0\r\n").ConfigureAwait(false);
                await WriteHeadersAsync(writer, trailingHeaders).ConfigureAwait(false);
            }
            else if (content?.Length > 0)
            {
                await writer.WriteAsync(content).ConfigureAwait(false);
            }

            await writer.FlushAsync().ConfigureAwait(false);
            stream.ReleaseNextWriter();

            async Task WriteChunkAsync(string chunk)
            {
                Debug.Assert(chunk.Length != 0, "Content chunks must be >0 in length.");
                await writer.WriteAsync(chunk.Length.ToString("X", CultureInfo.InvariantCulture)).ConfigureAwait(false);
                await writer.WriteAsync("\r\n").ConfigureAwait(false);
                await writer.WriteAsync(chunk).ConfigureAwait(false);
                await writer.WriteAsync("\r\n").ConfigureAwait(false);
            }
        }

        internal async Task SendRawResponseAsync(Http1TestStream stream, string response)
        {
            await _stream.WriteAsync(Encoding.UTF8.GetBytes(response)).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);
            stream.ReleaseNextWriter();
        }

        private static async Task WriteHeadersAsync(StreamWriter writer, TestHeadersSink? headers)
        {
            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    foreach (var value in kvp.Value)
                    {
                        await writer.WriteAsync(kvp.Key).ConfigureAwait(false);
                        await writer.WriteAsync(": ").ConfigureAwait(false);
                        await writer.WriteAsync(value).ConfigureAwait(false);
                        await writer.WriteAsync("\r\n").ConfigureAwait(false);
                    }
                }
            }
            await writer.WriteAsync("\r\n").ConfigureAwait(false);
        }

        internal void ReleaseNextReader()
        {
            Http1TestStream? stream;

            lock (Sync)
            {
                --_activeReaders;
                _readQueue.TryDequeue(out stream);
            }

            stream?._readSemaphore.Release();
        }

        internal void ReleaseNextWriter()
        {
            Http1TestStream? stream;

            lock (Sync)
            {
                --_activeWriters;
                _writeQueue.TryDequeue(out stream);
            }

            stream?._writeSemaphore.Release();
        }

        // Reason phrases according to RFC 2616 recommendation
        internal string GetReasonPhrase(int statusCode) => statusCode switch
        {
            100 =>"Continue",
            101 =>"Switching Protocols",
            200 =>"OK",
            201 =>"Created",
            202 =>"Accepted",
            203 =>"Non-Authoritative Information",
            204 =>"No Content",
            205 =>"Reset Content",
            206 =>"Partial Content",
            300 =>"Multiple Choices",
            301 =>"Moved Permanently",
            302 =>"Found",
            303 =>"See Other",
            304 =>"Not Modified",
            305 =>"Use Proxy",
            307 =>"Temporary Redirect",
            400 =>"Bad Request",
            401 =>"Unauthorized",
            402 =>"Payment Required",
            403 =>"Forbidden",
            404 =>"Not Found",
            405 =>"Method Not Allowed",
            406 =>"Not Acceptable",
            407 =>"Proxy Authentication Required",
            408 =>"Request Time-out",
            409 =>"Conflict",
            410 =>"Gone",
            411 =>"Length Required",
            412 =>"Precondition Failed",
            413 =>"Request Entity Too Large",
            414 =>"Request-URI Too Large",
            415 =>"Unsupported Media Type",
            416 =>"Requested range not satisfiable",
            417 =>"Expectation Failed",
            500 =>"Internal Server Error",
            501 =>"Not Implemented",
            502 =>"Bad Gateway",
            503 =>"Service Unavailable",
            504 =>"Gateway Time-out",
            505 =>"HTTP Version not supported",
            _ => $"Unknown Status Code ({statusCode})"
        };
            
           
    }
}
