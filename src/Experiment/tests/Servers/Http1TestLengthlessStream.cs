using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Servers
{
    internal sealed class Http1TestLengthlessStream : TestStreamBase
    {
        private readonly Http1TestConnection _con;

        public override bool CanRead => true;

        public Http1TestLengthlessStream(Http1TestConnection con)
        {
            _con = con;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_con._readBuffer.ActiveLength != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int recvLen = Math.Min(buffer.Length, _con._readBuffer.ActiveLength);
                _con._readBuffer.ActiveSpan[..recvLen].CopyTo(buffer.Span);
                _con._readBuffer.Discard(recvLen);
                return recvLen;
            }
            else
            {
                return await _con._stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
