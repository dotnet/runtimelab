// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    internal sealed class HttpConnectionResponseContent : HttpContent
    {
        private Stream? _stream;
        private bool _consumedStream; // separate from _stream so that Dispose can drain _stream

        public void SetStream(Stream stream)
        {
            Debug.Assert(stream.CanRead);
            Debug.Assert(!_consumedStream);

            _stream = stream;
        }

        private Stream ConsumeStream()
        {
            if (_consumedStream || _stream == null)
            {
                throw new InvalidOperationException("SR.net_http_content_stream_already_read");
            }
            _consumedStream = true;

            return _stream;
        }

        protected override void SerializeToStream(Stream stream, TransportContext? context,
            CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using Stream contentStream = ConsumeStream();
            const int bufferSize = 8192;
            contentStream.CopyTo(stream, bufferSize);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            Stream contentStream = ConsumeStream();
            const int bufferSize = 8192;
            await contentStream.CopyToAsync(stream, bufferSize, cancellationToken).ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken) =>
            ConsumeStream();

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult(ConsumeStream());
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {  
                _stream?.Dispose();
                _stream = null;
            }
            base.Dispose(disposing);
        }
    }
}
