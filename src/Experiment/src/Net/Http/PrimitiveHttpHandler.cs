// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.LowLevel;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    [UnsupportedOSPlatform("browser")]
    public sealed class PrimitiveHttpHandler : HttpMessageHandler
    {
        private static readonly HttpHeadersSink _headerSink = new();

        private readonly HttpConnection _connection;

        public PrimitiveHttpHandler(HttpConnection connection)
        {
            _connection = connection;
        }

        /// <summary>
        /// Gets a value that indicates whether the handler is supported on the current platform.
        /// </summary>
        public static bool IsSupported => true;

        protected override void Dispose(bool disposing)
        {
            Tools.BlockForResult(_connection.DisposeAsync());
            base.Dispose(disposing);
        }

        private string? GetHeaderValue(IEnumerable<string> values)
        {
            StringBuilder b = new();
            bool isFirst = true;
            foreach (var value in values)
            {
                if (!isFirst)
                    // The Header Description is internal in System.Net.Http. We use ',' as a separator for all headers.
                    b.Append(", ");
                b.Append(value);
                isFirst = false;
            }

            return b.ToString();
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Tools.BlockForResult(SendAsync(request, cancellationToken));
        }

        private void WriteHeaders(ref ValueHttpRequest httpRequest, HttpRequestMessage request)
        {
            foreach (var (key, values) in request.Headers)
            {
                if (key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    continue;
                var value = GetHeaderValue(values);
                if (value != null)
                {
                    httpRequest.WriteHeader(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(value));
                }
            }

            if (request.Content == null) return;

            foreach (var (key, values) in request.Content.Headers)
            {
                var value = GetHeaderValue(values);
                if (value != null)
                {
                    httpRequest.WriteHeader(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(value));
                }
            }
        }

        private async Task<ValueHttpRequest> PrepareRequest(ValueHttpRequest httpRequest, HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                throw new HttpRequestException("SR.SOME_MESSAGE_URI_IS_NULL");
            }

            bool hasContentLength = request.Headers.TransferEncodingChunked == false;
            
            if (request.Version is {Major:1, Minor:0} && hasContentLength)
            {
                throw new HttpRequestException("SR.SOME_MESSAGE_URI_IS_NULL");
            }

            httpRequest.ConfigureRequest(hasContentLength, false);
            httpRequest.WriteRequestStart(Encoding.ASCII.GetBytes(request.Method.Method),
                Encoding.ASCII.GetBytes(request.RequestUri.Host),
                Encoding.ASCII.GetBytes(request.RequestUri.PathAndQuery));

            WriteHeaders(ref httpRequest, request);

            if (request.Content is not null)
            {
                await using HttpContentStream copyStream = new(httpRequest, false);
                await request.Content.CopyToAsync(copyStream, cancellationToken).ConfigureAwait(false);
            }

            return httpRequest;
        }

        private async Task<HttpResponseMessage> ReadResponse(ValueHttpRequest httpRequest, HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage responseMessage = new(httpRequest.StatusCode)
            {
                RequestMessage = request,
                Version = httpRequest.Version ?? throw new HttpRequestException("SR.SOME_MESSAGE_VERSION_IS_MISSING")
            };


            if (request.Method == HttpMethod.Head
                || (httpRequest.StatusCode is >= HttpStatusCode.Continue and < HttpStatusCode.OK 
                    or HttpStatusCode.NoContent 
                    or HttpStatusCode.NotModified)
            ) // https://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.3
            {
                await ReadHeaders(httpRequest, responseMessage, cancellationToken).ConfigureAwait(false);
                // Drain the request and return it to connection.
                await httpRequest.DisposeAsync();
            }
            else
            {   
                PrimitiveHttpContentStream httpStream = new(httpRequest, responseMessage, _headerSink, true);
                responseMessage.Content = new HttpConnectionResponseContent(httpStream);
                await ReadHeaders(httpRequest, responseMessage, cancellationToken).ConfigureAwait(false);
            }

            


            return responseMessage;

            static async ValueTask ReadHeaders(ValueHttpRequest request, HttpResponseMessage responseMessage, CancellationToken cancellationToken)
            {
                if (await request.ReadToHeadersAsync(cancellationToken).ConfigureAwait(false))
                {
                    await request.ReadHeadersAsync(_headerSink, (responseMessage, false), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken).Result;
            }
            
            HttpPrimitiveVersion version = request.Version switch
            {
                {Major: 1, Minor: 0} => HttpPrimitiveVersion.Version10,
                {Major: 1, Minor: 1} => HttpPrimitiveVersion.Version11,
                _ => throw new HttpRequestException("SR.SOME_MESSAGE_UNSUPPORTED_VERSION")
            };
            
            ValueHttpRequest httpRequest = await _connection.CreateNewRequestAsync(version, request.VersionPolicy, cancellationToken)
                                           ?? throw new HttpRequestException("SR.SOME_MESSAGE_CANT_CREATE_REQUEST");

            httpRequest = await PrepareRequest(httpRequest, request, cancellationToken).ConfigureAwait(false);

            await httpRequest.CompleteRequestAsync(cancellationToken).ConfigureAwait(false);
            await httpRequest.ReadToFinalResponseAsync(cancellationToken).ConfigureAwait(false);

            HttpResponseMessage responseMessage =
                await ReadResponse(httpRequest, request, cancellationToken).ConfigureAwait(false);

            return responseMessage;
        }


        private class HttpHeadersSink : IHttpHeadersSink
        {
            public void OnHeader(object? state, ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue)
            {
                if (state is not (HttpResponseMessage responseMessage, bool isTrailing))
                {
                    Debug.Fail($"Expected {nameof(HttpResponseHeaders)}");
                    return;
                }

                string name = Encoding.ASCII.GetString(headerName);
                string value = Encoding.ASCII.GetString(headerValue);

                if (isTrailing)
                {
                    if (!responseMessage.TrailingHeaders.TryAddWithoutValidation(name, value))
                        throw new HttpRequestException("Invalid Response Trailing Header Name " + name);
                }
                else
                {
                    // This one is weird and can be improved once we have access to internals of System.Net.Http   
                    if (responseMessage.Headers.TryAddWithoutValidation(name, value)) return;
                    if (!responseMessage.Content.Headers.TryAddWithoutValidation(name, value))
                        throw new HttpRequestException("Invalid Response Header Name " + name);
                }
            }
        }
    }
}