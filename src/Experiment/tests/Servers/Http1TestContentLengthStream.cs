using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal class Http1TestContentLengthStream : TestStreamBase
    {
        private readonly Http1TestConnection _con;
        private readonly Http1TestStream? _stream;
        private long _lengthRemaining;

        public override bool CanRead => true;

        public Http1TestContentLengthStream(Http1TestConnection con, Http1TestStream? stream, long length)
        {
            _con = con;
            _stream = stream;
            _lengthRemaining = length;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_lengthRemaining == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return 0;
            }

            int recvLen = (int)Math.Min(buffer.Length, _lengthRemaining);

            if (_con._readBuffer.ActiveLength != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                recvLen = Math.Min(recvLen, _con._readBuffer.ActiveLength);
                _con._readBuffer.ActiveSpan[..recvLen].CopyTo(buffer.Span);
                _con._readBuffer.Discard(recvLen);
                _lengthRemaining -= recvLen;

                if (_lengthRemaining == 0)
                {
                    _stream?.ReleaseNextReader();
                }

                return recvLen;
            }
            else
            {
                recvLen = await _con._stream.ReadAsync(buffer[..recvLen], cancellationToken).ConfigureAwait(false);
                if (recvLen == 0) throw new Exception($"Unexpected end of stream with {_lengthRemaining} bytes remaining.");

                _lengthRemaining -= recvLen;

                if (_lengthRemaining == 0)
                {
                    _stream?.ReleaseNextReader();
                }

                return recvLen;
            }
        }
    }
}
