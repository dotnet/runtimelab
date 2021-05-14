using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.LowLevel;

namespace System.Net.Http
{
    /// <summary>
    /// A <see cref="Stream"/> that reads/writes content over a <see cref="ValueHttpRequest"/>.
    /// </summary>
    public class PrimitiveHttpContentStream : HttpContentStream, IEnhancedStream
    {
        private readonly HttpResponseMessage _responseMessage;
        private readonly IHttpHeadersSink _headerSink;
        private bool _trailingRead=false;
        /// <summary>
        /// Instantiates a new <see cref="HttpContentStream"/>.
        /// </summary>
        /// <param name="request">The <see cref="ValueHttpRequest"/> to operate on.</param>
        /// <param name="responseMessage">The <see cref="HttpResponseMessage"/> associated with this stream.</param>
        /// <param name="headerSink">The <see cref="IHttpHeadersSink"/> for extract http headers.</param>
        /// <param name="ownsRequest">If true, the <paramref name="request"/> will be disposed once the <see cref="HttpContentStream"/> is disposed.</param>
        public PrimitiveHttpContentStream(ValueHttpRequest request, HttpResponseMessage responseMessage, IHttpHeadersSink headerSink, bool ownsRequest):
            base(request, ownsRequest)
        {
            _responseMessage = responseMessage;
            _headerSink = headerSink;
        }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int bytesRead = await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0 && _trailingRead is false)
            {
                // End of the content, read trailing headers
                await ReadTrailingHeaders(cancellationToken).ConfigureAwait(false);
            }
            return bytesRead;
        }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default)
        {
            int bytesRead = await base.ReadAsync(buffers, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0 && _trailingRead is false)
            {
                // End of the content, read trailing headers
                await ReadTrailingHeaders(cancellationToken).ConfigureAwait(false);
            }
            return bytesRead;
        }

        private async ValueTask ReadTrailingHeaders(CancellationToken cancellationToken)
        {
            if (await _request.ReadToTrailingHeadersAsync(cancellationToken).ConfigureAwait(false))
            {
                await _request.ReadHeadersAsync(_headerSink, (_responseMessage, true), cancellationToken)
                    .ConfigureAwait(false);
            }
            _trailingRead = true;
        }
    }
}
