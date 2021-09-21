// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A HTTP request.
    /// </summary>
    public abstract class HttpRequest
    {
        /// <summary>
        /// The GET method.
        /// </summary>
        public static ReadOnlySpan<byte> GetMethod => new byte[] { (byte)'G', (byte)'E', (byte)'T' };

        /// <summary>
        /// The POST method.
        /// </summary>
        public static ReadOnlySpan<byte> PostMethod => new byte[] { (byte)'P', (byte)'O', (byte)'S', (byte)'T' };

        /// <summary>
        /// The PUT method.
        /// </summary>
        public static ReadOnlySpan<byte> PutMethod => new byte[] { (byte)'P', (byte)'U', (byte)'T' };

        /// <summary>
        /// The HEAD method.
        /// </summary>
        public static ReadOnlySpan<byte> HeadMethod => new byte[] { (byte)'H', (byte)'E', (byte)'A', (byte)'D' };

        /// <summary>
        /// The CONNECT method.
        /// </summary>
        public static ReadOnlySpan<byte> ConnectMethod => new byte[] { (byte)'C', (byte)'O', (byte)'N', (byte)'N', (byte)'E', (byte)'C', (byte)'T' };

        /// <summary>
        /// <see cref="HttpRequest"/> instances will be reused across requests, which is potentially dangerous
        /// when a <see cref="ValueHttpRequest"/> is misused. This will be used to perform a sanity check similar to how <see cref="ValueTask"/> works.
        /// </summary>
        private int _version;

        /// <summary>
        /// The current <see cref="HttpReadType"/> for the request.
        /// </summary>
        protected internal HttpReadType ReadType { get; protected set; }

        /// <summary>
        /// The version of the HTTP response.
        /// Only valid when <see cref="ReadType"/> has been <see cref="HttpReadType.FinalResponse"/> or <see cref="HttpReadType.InformationalResponse"/>.
        /// </summary>
        protected internal Version? Version { get; protected set; }

        /// <summary>
        /// The status code of the HTTP response.
        /// Only valid when <see cref="ReadType"/> has been <see cref="HttpReadType.FinalResponse"/> or <see cref="HttpReadType.InformationalResponse"/>.
        /// </summary>
        protected internal HttpStatusCode StatusCode { get; protected set; }

        /// <summary>
        /// The ALT-SVC value from the HTTP response.
        /// Only valid when <see cref="ReadType"/> has been <see cref="HttpReadType.AltSvc"/>.
        /// </summary>
        protected internal virtual ReadOnlyMemory<byte> AltSvc => ReadOnlyMemory<byte>.Empty; // only used for HTTP/2 and should only be used uncommonly; virtual to not allocate field.

        /// <summary>
        /// Resets the request version.
        /// This must be called after the request has been processed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void Reset() =>
            Interlocked.Increment(ref _version);

        /// <summary>
        /// Gets a <see cref="ValueHttpRequest"/> for the current request.
        /// </summary>
        /// <returns>
        /// A <see cref="ValueHttpRequest"/> for the current request.
        /// The <see cref="ValueHttpRequest"/> will be invalidated once <see cref="Reset"/> is called.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal ValueHttpRequest GetValueRequest() =>
            new ValueHttpRequest(this, _version);

        /// <summary>
        /// Disposes of the request.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected internal abstract ValueTask DisposeAsync(int version);

        /// <summary>
        /// Configures the request.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="hasContentLength">If true, the request will contain the Content-Length header.</param>
        /// <param name="hasTrailingHeaders">If true, the request will send trailing headers.</param>
        protected internal abstract void ConfigureRequest(int version, bool hasContentLength, bool hasTrailingHeaders);

        /// <summary>
        /// Writes a CONNECT request.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="authority">The authority to CONNECT to.</param>
        protected internal abstract void WriteConnectRequest(int version, ReadOnlySpan<byte> authority);

        /// <summary>
        /// Writes a request.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="method">The request method to use.</param>
        /// <param name="authority">The authority that should process the request. Ends up in the "Host" or ":authority" header, depending on protocol version.</param>
        /// <param name="pathAndQuery">The path and query of the request.</param>
        protected internal abstract void WriteRequestStart(int version, ReadOnlySpan<byte> method, ReadOnlySpan<byte> authority, ReadOnlySpan<byte> pathAndQuery);

        /// <summary>
        /// Writes a header.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="name">The name of the header to write.</param>
        /// <param name="value">The value of the header to write.</param>
        protected internal abstract void WriteHeader(int version, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value);

        /// <summary>
        /// Writes a header.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="name">The name of the header to write.</param>
        /// <param name="value">The value of the header to write.</param>
        protected internal abstract void WriteHeader(int version, PreparedHeaderName name, ReadOnlySpan<byte> value);

        /// <summary>
        /// Writes a set of headers.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="headers">A set of headers to write to the request.</param>
        protected internal abstract void WriteHeader(int version, PreparedHeaderSet headers);

        /// <summary>
        /// Writes a trailing header.
        /// To use, trailing headers must be enabled via <see cref="ConfigureRequest(int, bool, bool)"/>.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="name">The name of the header to write.</param>
        /// <param name="value">The value of the header to write.</param>
        protected internal abstract void WriteTrailingHeader(int version, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value);

        /// <summary>
        /// Flushes the request and headers to network, if any.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected internal abstract ValueTask FlushHeadersAsync(int version, CancellationToken cancellationToken);

        /// <summary>
        /// Writes request content.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="buffer">The request content to write.</param>
        /// <param name="flush">If true, the underlying stream will be flushed immediately.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected internal abstract ValueTask WriteContentAsync(int version, ReadOnlyMemory<byte> buffer, bool flush, CancellationToken cancellationToken);

        /// <summary>
        /// Writes request content.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="buffers">The request content to write.</param>
        /// <param name="flush">If true, the underlying stream will be flushed immediately.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected internal abstract ValueTask WriteContentAsync(int version, IReadOnlyList<ReadOnlyMemory<byte>> buffers, bool flush, CancellationToken cancellationToken);

        /// <summary>
        /// Flushes the request, headers, and request content to network, if any.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected internal abstract ValueTask FlushContentAsync(int version, CancellationToken cancellationToken);

        /// <summary>
        /// Completes the request, flushing the request, headers, request content, and trailing headers to network, if any.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected internal abstract ValueTask CompleteRequestAsync(int version, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the next element from the HTTP response stream.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="HttpReadType"/> indicating the type of element read from the stream.</returns>
        protected internal abstract ValueTask<HttpReadType> ReadAsync(int version, CancellationToken cancellationToken);

        /// <summary>
        /// Reads headers, if any. Should be called when <see cref="ReadType"/> is <see cref="HttpReadType.Headers"/> or <see cref="HttpReadType.TrailingHeaders"/>.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="headersSink">A sink to retrieve headers.</param>
        /// <param name="state">User state to pass to <see cref="IHttpHeadersSink.OnHeader(object?, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="HttpReadType"/> indicating the type of element read from the stream.</returns>
        protected internal abstract ValueTask ReadHeadersAsync(int version, IHttpHeadersSink headersSink, object? state, CancellationToken cancellationToken);

        /// <summary>
        /// Reads response content, if any. Should be called when <see cref="ReadType"/> is <see cref="HttpReadType.Content"/>.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>The number of bytes read into <paramref name="buffer"/>.</returns>
        protected internal abstract ValueTask<int> ReadContentAsync(int version, Memory<byte> buffer, CancellationToken cancellationToken);

        /// <summary>
        /// Reads response content, if any. Should be called when <see cref="ReadType"/> is <see cref="HttpReadType.Content"/>.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="buffers">The buffers to read into.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>The number of bytes read into <paramref name="buffers"/>.</returns>
        protected internal abstract ValueTask<int> ReadContentAsync(int version, IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken);

        /// <summary>
        /// Throws if the request is disposed or if <paramref name="version"/> does not match the current request version.
        /// </summary>
        /// <param name="version">The version of the request to operate on.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ThrowIfDisposed(int version)
        {
            if (IsDisposed(version)) ThrowObjectDisposedException();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowObjectDisposedException()
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Tests if the request is disposed or if <paramref name="version"/> does not match the current request version.
        /// </summary>
        /// <param name="version">The version of the request to operate on.</param>
        /// <returns>
        /// If the request is not disposed and <paramref name="version"/> matches the current request version, true.
        /// Otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsDisposed(int version) => version != _version;

        /// <summary>
        /// Tests if the request is disposed or if <paramref name="version"/> does not match the current request version.
        /// </summary>
        /// <param name="version">The version of the request to operate on.</param>
        /// <param name="valueTask">If the test fails, receives a <see cref="ValueTask"/> with an <see cref="ObjectDisposedException"/>.</param>
        /// <returns>
        /// If the request is not disposed and <paramref name="version"/> matches the current request version, true.
        /// Otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsDisposed(int version, out ValueTask valueTask)
        {
            if (IsDisposed(version))
            {
                CreateObjectDisposedTask(out valueTask);
                return true;
            }
            else
            {
                valueTask = default;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CreateObjectDisposedTask(out ValueTask valueTask)
        {
            valueTask = ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(GetType().Name)));
        }

        /// <summary>
        /// Tests if the request is disposed or if <paramref name="version"/> does not match the current request version.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="ValueTask{TResult}"/> to return.</typeparam>
        /// <param name="version">The version of the request to operate on.</param>
        /// <param name="valueTask">If the test fails, receives a <see cref="ValueTask"/> with an <see cref="ObjectDisposedException"/>.</param>
        /// <returns>
        /// If the request is not disposed and <paramref name="version"/> matches the current request version, true.
        /// Otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsDisposed<T>(int version, out ValueTask<T> valueTask)
        {
            if (IsDisposed(version))
            {
                DoSetResult(out valueTask);
                return true;
            }
            else
            {
                valueTask = default;
                return false;
            }

            static void DoSetResult(out ValueTask<T> valueTask) =>
                valueTask = ValueTask.FromException<T>(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(nameof(HttpRequest))));
        }

        /// <summary>
        /// Writes a request.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="method">The request method to use.</param>
        /// <param name="uri">The URI to make the request for.</param>
        protected internal virtual void WriteRequestStart(int version, HttpMethod method, Uri uri)
        {
            string host = uri.IdnHost;
            int port = uri.Port;
            string authority =
                uri.HostNameType == UriHostNameType.IPv6
                ? $"[{host}]:{port}"
                : $"{host}:{port}";

            byte[] authorityBytes = Encoding.ASCII.GetBytes(authority);
            byte[] methodBytes = Encoding.ASCII.GetBytes(method.Method);
            byte[] pathAndQueryBytes = Encoding.ASCII.GetBytes(uri.PathAndQuery);

            WriteRequestStart(version, methodBytes, authorityBytes, pathAndQueryBytes);
        }

        /// <summary>
        /// Writes a header.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="name">The name of the header to write.</param>
        /// <param name="value">The value of the header to write.</param>
        protected internal virtual void WriteHeader(int version, string name, string value)
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            byte[] valueBytes = Encoding.ASCII.GetBytes(value);

            WriteHeader(version, nameBytes, valueBytes);
        }

        /// <summary>
        /// Writes a header.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="name">The name of the header to write.</param>
        /// <param name="values">The value of the header to write.</param>
        /// <param name="separator">A separator used when concatenating <paramref name="values"/>.</param>
        protected internal virtual void WriteHeader(int version, string name, IEnumerable<string> values, string separator)
        {
            WriteHeader(version, name, string.Join(separator, values));
        }

        /// <summary>
        /// Writes a trailing header.
        /// To use, trailing headers must be enabled during <see cref="ConfigureRequest(int, bool, bool)"/>.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="name">The name of the header to write.</param>
        /// <param name="value">The value of the header to write.</param>
        protected internal virtual void WriteTrailingHeader(int version, string name, string value)
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            byte[] valueBytes = Encoding.ASCII.GetBytes(value);

            WriteTrailingHeader(version, nameBytes, valueBytes);
        }

        /// <summary>
        /// Writes a trailing header.
        /// To use, trailing headers must be enabled during <see cref="ConfigureRequest(int, bool, bool)"/>.
        /// </summary>
        /// <param name="version">The version of the request to operate on. This must be validated by implementations.</param>
        /// <param name="name">The name of the header to write.</param>
        /// <param name="values">The value of the header to write.</param>
        /// <param name="separator">A separator used when concatenating <paramref name="values"/>.</param>
        protected internal virtual void WriteTrailingHeader(int version, string name, IEnumerable<string> values, string separator)
        {
            WriteTrailingHeader(version, name, string.Join(separator, values));
        }
    }
}
