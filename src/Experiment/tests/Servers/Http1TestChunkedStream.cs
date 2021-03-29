using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal sealed class Http1TestChunkedStream : TestStreamBase
    {
        private readonly Http1TestConnection _con;
        private long? _totalLengthRemaining;
        private long? _curChunkLengthRemaining;

        public override bool CanRead => true;

        public Http1TestChunkedStream(Http1TestConnection con, long? length)
        {
            _con = con;
            _totalLengthRemaining = length;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_curChunkLengthRemaining == 0)
            {
                string dataTrailer = await _con.ReadLineAsync().ConfigureAwait(false);
                Assert.Empty(dataTrailer);

                _curChunkLengthRemaining = null;
            }

            if (_curChunkLengthRemaining == null)
            {
                string lengthString = await _con.ReadLineAsync().ConfigureAwait(false);
                _curChunkLengthRemaining = long.Parse(lengthString, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

                if (_curChunkLengthRemaining == 0)
                {
                    // final chunk.
                    if (_totalLengthRemaining is not null and not 0)
                    {
                        throw new Exception("Chunked stream contains less data than Content-Length indicates.");
                    }
                    return 0;
                }
            }

            int recvLen = (int)Math.Min(buffer.Length, _curChunkLengthRemaining.Value);

            if (_con._readBuffer.ActiveLength != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                recvLen = Math.Min(recvLen, _con._readBuffer.ActiveLength);
                _con._readBuffer.ActiveSpan[..recvLen].CopyTo(buffer.Span);
                _con._readBuffer.Discard(recvLen);
            }
            else
            {
                recvLen = await _con._stream.ReadAsync(buffer[..recvLen], cancellationToken).ConfigureAwait(false);
                if (recvLen == 0) throw new Exception($"Unexpected end of stream with minimum {_totalLengthRemaining ?? _curChunkLengthRemaining} bytes remaining.");
            }

            _curChunkLengthRemaining -= recvLen;
            _totalLengthRemaining -= recvLen;

            if (_totalLengthRemaining < 0)
            {
                throw new Exception("Chunked stream contains more data than Content-Length indicates.");
            }

            return recvLen;
        }
    }
}
