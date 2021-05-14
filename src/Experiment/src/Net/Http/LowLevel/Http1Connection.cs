// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A HTTP/1 connection.
    /// </summary>
    public partial class Http1Connection : HttpBaseConnection
    {
        private static ushort s_encodedCrLf => BitConverter.IsLittleEndian ? (ushort)0x0A0D : (ushort)0x0D0Au; // "\r\n"
        private static ushort s_encodedColonSpace => BitConverter.IsLittleEndian ? (ushort)0x203A : (ushort)0x3A20; // ": "
        private static ulong s_encodedNewLineHostPrefix => BitConverter.IsLittleEndian ? 0x203A74736F480A0DUL : 0x0D0A486F73743A20UL; // "\r\nHost: "
        private static ulong s_encodedNewLineTransferEncodingChunked_0 => BitConverter.IsLittleEndian ? 0x66736E6172540A0DUL : 0x0D0A5472616E7366UL; // "\r\nTransf"
        private static ulong s_encodedNewLineTransferEncodingChunked_1 => BitConverter.IsLittleEndian ? 0x646F636E452D7265UL : 0x65722D456E636F64UL; // "er-Encod"
        private static ulong s_encodedNewLineTransferEncodingChunked_2 => BitConverter.IsLittleEndian ? 0x756863203A676E69UL : 0x696E673A20636875UL; // "ing: chu"
        private static uint s_encodedNewLineTransferEncodingChunked_3 => BitConverter.IsLittleEndian ? 0x64656B6EU : 0x6E6B6564U; // "nked"
        private static ulong s_encodedConnectPrefix => BitConverter.IsLittleEndian ? 0x205443454E4E4F43UL : 0x434F4E4E45435420UL; // "CONNECT "
        private static readonly ReadOnlyMemory<byte> s_encodedCRLFMemory = new byte[] { (byte)'\r', (byte)'\n' };
        private static ReadOnlySpan<byte> s_EncodedTransferEncodingName => new byte[] { (byte)'t', (byte)'r', (byte)'a', (byte)'n', (byte)'s', (byte)'f', (byte)'e', (byte)'r', (byte)'-', (byte)'e', (byte)'n', (byte)'c', (byte)'o', (byte)'d', (byte)'i', (byte)'n', (byte)'g' };
        private static ReadOnlySpan<byte> s_EncodedTransferEncodingChunkedValue => new byte[] { (byte)'c', (byte)'h', (byte)'u', (byte)'n', (byte)'k', (byte)'e', (byte)'d' };
        private static ReadOnlySpan<byte> s_EncodedContentLengthName => new byte[] { (byte)'c', (byte)'o', (byte)'n', (byte)'t', (byte)'e', (byte)'n', (byte)'t', (byte)'-', (byte)'l', (byte)'e', (byte)'n', (byte)'g', (byte)'t', (byte)'h' };
        private static ReadOnlySpan<byte> s_EncodedConnectionName => new byte[] { (byte)'c', (byte)'o', (byte)'n', (byte)'n', (byte)'e', (byte)'c', (byte)'t', (byte)'i', (byte)'o', (byte)'n' };
        private static ReadOnlySpan<byte> s_EncodedConnectionCloseValue => new byte[] { (byte)'c', (byte)'l', (byte)'o', (byte)'s', (byte)'e' };
        private static ReadOnlySpan<byte> s_EncodedConnectionKeepAliveValue => new byte[] { (byte)'k', (byte)'e', (byte)'e', (byte)'p', (byte)'-', (byte)'a', (byte)'l', (byte)'i', (byte)'v', (byte)'e' };

        internal readonly HttpPrimitiveVersion _version;
        internal readonly Stream _stream;
        internal readonly IEnhancedStream _enhancedStream;
        internal VectorArrayBuffer _readBuffer;
        internal ArrayBuffer _writeBuffer;
        private readonly List<ReadOnlyMemory<byte>> _gatheredWriteBuffer = new(3);
        private bool _requestIsChunked, _responseHasContentLength, _responseIsChunked, _readingFirstResponseChunk;
        private volatile bool _closeConnection;
        private WriteState _writeState;
        private ReadState _readState;
        private ReadHeadersState _readHeadersState;
        private ReadContentState _readContentState;
        private ulong _responseContentBytesRemaining, _responseChunkBytesRemaining;
        private HttpConnectionStatus _connectionState;
        private Exception? _connectionException;

        private readonly ResettableValueTaskSource<HttpReadType> _readTaskSource = new();
        private readonly ResettableValueTaskSource<int> _readHeadersAndContentTaskSource = new();
        private ConfiguredValueTaskAwaitable<int>.ConfiguredValueTaskAwaiter _readBytesAwaitable;
        private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter _readAwaitable;
        private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter _readHeadersAwaitable;
        private byte[]? _drainContentBuffer;
        private Http1Request? _readRequest;
        private CancellationToken _readToken;
        private IHttpHeadersSink? _headersSink;
        private object? _headersSinkState;
        private Memory<byte> _readUserBuffer;
        private readonly Action _doReadFunc;
        private readonly Action _doReadHeadersFunc;
        private readonly Action _doReadUnenvelopedContentFunc;
        private readonly Action _doReadChunkedContentFunc;

        private object _sync => _gatheredWriteBuffer; // this guards the following three fields.
        private IntrusiveLinkedList<Http1Request> _requestCache; // a cache of requests.
        private IntrusiveLinkedList<Http1Request> _activeRequests; // currently active requests. current reader is _activeRequests.Front.
        private Http1Request? _currentWriter; // the current writer.

        private enum WriteState : byte
        {
            Unstarted,
            RequestWritten,
            HeadersWritten,
            HeadersFlushed,
            ContentWritten,
            TrailingHeadersWritten,
            Finished
        }

        private enum ReadState : byte
        {
            ReadToResponse,
            OnResponseBytesReceived,
            ReadToHeaders,
            SkipHeaders,
            OnHeadersRead,
            ReadToContent,
            SkipContent,
            OnContentReceived,
            ReadToTrailingHeaders,
            SkipTrailingHeaders,
            OnTrailingHeadersRead,
            EndOfStream,
            OnException,
            RethrowException
        }

        private enum ReadHeadersState : byte
        {
            ReadHeaders,
            OnHeaderBytesReceived,
            OnException,
            RethrowException
        }

        private enum ReadContentState : byte
        {
            ReadContent,
            OnContentBytesReceived,
            OnException,
            RethrowException,
            ReadChunkHeader,
            OnChunkHeaderReceived
        }

        /// <inheritdoc/>
        public override HttpConnectionStatus Status => _connectionState;

        /// <summary>
        /// Instantiates a new <see cref="Http1Connection"/> over a given <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read and write to.</param>
        /// <param name="version">
        /// The HTTP version to make requests as.
        /// This must be one of <see cref="HttpPrimitiveVersion.Version10"/> or <see cref="HttpPrimitiveVersion.Version11"/>.
        /// </param>
        public Http1Connection(Stream stream, HttpPrimitiveVersion version)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (version == null) throw new ArgumentNullException(nameof(version));

            if (version != HttpPrimitiveVersion.Version10 && version != HttpPrimitiveVersion.Version11)
            {
                throw new ArgumentException($"{nameof(Http1Connection)} may only be used for HTTP/1.0 and HTTP/1.1.", nameof(version));
            }

            if (stream is not IEnhancedStream { CanScatterGather: true } enhancedStream)
            {
                throw new ArgumentException($"{nameof(Http1Connection)} must be an {nameof(IEnhancedStream)} that supports scatter/gather. Consider wrapping the stream with a {nameof(WriteBufferingStream)}.");
            }

            _doReadFunc = DoRead;
            _doReadHeadersFunc = DoReadHeaders;
            _doReadUnenvelopedContentFunc = DoReadUnenvelopedContent;
            _doReadChunkedContentFunc = DoReadChunkedContent;
            _version = version;
            _stream = stream;
            _enhancedStream = enhancedStream;
            _readBuffer = new VectorArrayBuffer(initialSize: 4096);
            _writeBuffer = new ArrayBuffer(initialSize: 64);

            PrepareForNextReader();
        }

        /// <inheritdoc/>
        protected override async ValueTask DisposeAsyncCore()
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _readBuffer.Dispose();
            _writeBuffer.Dispose();
            _connectionState = HttpConnectionStatus.Closed;
        }

        /// <inheritdoc/>
        public override ValueTask<ValueHttpRequest?> CreateNewRequestAsync(HttpPrimitiveVersion version, HttpVersionPolicy versionPolicy, CancellationToken cancellationToken = default)
        {
            if (version.Major != 1)
            {
                if (versionPolicy != HttpVersionPolicy.RequestVersionOrLower)
                {
                    return ValueTask.FromException<ValueHttpRequest?>(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception($"Unable to create request for HTTP/{version.Major}.{version.Minor} with a {nameof(Http1Connection)}.")));
                }

                version = _version;
            }

            if (version != _version)
            {
                return ValueTask.FromException<ValueHttpRequest?>(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception($"Unable to create request for HTTP/{version.Major}.{version.Minor} with a {nameof(Http1Connection)} configured for HTTP/{_version.Major}.{_version.Minor}.")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<ValueHttpRequest?>(cancellationToken);
            }

            if (_closeConnection)
            {
                return ValueTask.FromResult<ValueHttpRequest?>(null);
            }

            Http1Request request;
            bool waitForWrite = true;
            bool waitForRead;

            lock (_sync)
            {
                waitForRead = _activeRequests.Front is not null;

                if (_requestCache.PopBack() is Http1Request cachedRequest)
                {
                    cachedRequest.Init(this, waitForRead);
                    request = cachedRequest;
                }
                else
                {
                    request = new Http1Request(this, waitForRead);
                }

                if (_currentWriter is null)
                {
                    waitForWrite = false;
                    _currentWriter = request;
                }

                _activeRequests.PushBack(request);
            }

            if (waitForWrite)
            {
                Debug.Assert(waitForRead == true);

                // waiting for another request to complete.
                return request.GetWriteWaitTask(cancellationToken);
            }

            // no active request: start immediately.
            PrepareForNextWriter();

            if (!waitForRead)
            {
                PrepareForNextReader();
            }

            return new ValueTask<ValueHttpRequest?>(request.GetValueRequest());
        }

        private async ValueTask SetConnectionExceptionAsync(Exception ex)
        {
            if (Interlocked.CompareExchange(ref _connectionException, ex, null) == null)
            {
                _connectionState = HttpConnectionStatus.Closed;
                try
                {
                    await _stream.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // do nothing. dispose will be retried when user disposes connection.
                }
            }
        }

        private Exception CreateRethrowException() =>
            new Exception($"{nameof(Http1Connection)} request failed. See InnerException for details.", _connectionException);

        private void ReleaseNextWriter()
        {
            Http1Request? nextWriter;

            lock (_sync)
            {
                Debug.Assert(_currentWriter != null);

                nextWriter = _currentWriter.ListHeader.Next;
                _currentWriter = nextWriter;
            }

            if (nextWriter != null)
            {
                PrepareForNextWriter();
                nextWriter.ReleaseWriteWait();
            }
        }

        private void PrepareForNextWriter()
        {
            _writeState = WriteState.Unstarted;
            _requestIsChunked = true;
        }

        internal void ReleaseNextReader(Http1Request request)
        {
            Http1Request? nextReader;

            lock (_sync)
            {
                nextReader = request.ListHeader.Next;

                Debug.Assert(request == _activeRequests.Front);
                _activeRequests.PopFront();

                _requestCache.PushBack(request);
            }

            if (nextReader != null)
            {
                if (!_closeConnection)
                {
                    PrepareForNextReader();
                    nextReader.ReleaseReadWait();
                }
                else
                {
                    nextReader.FailReadWait(CreateRethrowException());
                }
            }
        }

        private void PrepareForNextReader()
        {
            _responseHasContentLength = false;
            _responseIsChunked = false;
            _readingFirstResponseChunk = false;
            _readState = ReadState.ReadToResponse;
        }

        internal void DrainFailed()
        {
            _closeConnection = true;
            // TODO: loop through waiters and cancel tasks.
        }

        /// <inheritdoc/>
        public override ValueTask PrunePoolsAsync(long curTicks, TimeSpan lifetimeLimit, TimeSpan idleLimit, CancellationToken cancellationToken = default) =>
            default;

        internal void ConfigureRequest(bool hasContentLength, bool hasTrailingHeaders)
        {
            if (_version == HttpPrimitiveVersion.Version11)
            {
                _requestIsChunked = !hasContentLength || hasTrailingHeaders;
            }
            else
            {
                Debug.Assert(_version == HttpPrimitiveVersion.Version10);

                if (hasTrailingHeaders)
                {
                    throw new Exception("HTTP/1.0 does not support chunked encoding necessary for trailing headers.");
                }

                _requestIsChunked = false;
            }
        }

        internal void WriteConnectRequest(ReadOnlySpan<byte> authority)
        {
            if (_writeState != WriteState.Unstarted)
            {
                throw new InvalidOperationException();
            }

            if (authority.Length == 0)
            {
                throw new ArgumentException();
            }

            _requestIsChunked = false;

            int len = GetEncodeConnectRequestLength(authority);
            _writeBuffer.EnsureAvailableSpace(len);
            EncodeConnectRequest(authority, _version, _writeBuffer.AvailableSpan);
            _writeBuffer.Commit(len);
            _writeState = WriteState.RequestWritten;
        }

        internal static int GetEncodeConnectRequestLength(ReadOnlySpan<byte> authority)
        {
            return authority.Length + 21; // CONNECT {authority} HTTP/x.x\r\n\r\n
        }

        internal static unsafe void EncodeConnectRequest(ReadOnlySpan<byte> authority, HttpPrimitiveVersion version, Span<byte> buffer)
        {
            Debug.Assert(authority.Length > 0);
            Debug.Assert(buffer.Length >= GetEncodeConnectRequestLength(authority));

            ref byte pBuf = ref MemoryMarshal.GetReference(buffer);

            Unsafe.WriteUnaligned(ref pBuf, s_encodedConnectPrefix); // "CONNECT "
            pBuf = ref Unsafe.Add(ref pBuf, 8);

            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(authority), (uint)authority.Length);
            pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)authority.Length);

            pBuf = (byte)' ';
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 1), version._encoded); // "HTTP/x.x"
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 9), s_encodedCrLf);
            pBuf = ref Unsafe.Add(ref pBuf, 11);

            int length = Tools.UnsafeByteOffset(buffer, ref pBuf);
            Debug.Assert(length == GetEncodeConnectRequestLength(authority));
        }

        internal void WriteRequestStart(ReadOnlySpan<byte> method, ReadOnlySpan<byte> authority, ReadOnlySpan<byte> pathAndQuery)
        {
            if (_writeState != WriteState.Unstarted)
            {
                throw new InvalidOperationException();
            }

            if (method.Length == 0 || authority.Length == 0 || pathAndQuery.Length == 0)
            {
                throw new ArgumentException("All parameters must be specified.");
            }

            int len = GetEncodeRequestLength(method, authority, pathAndQuery);
            _writeBuffer.EnsureAvailableSpace(len);
            EncodeRequest(method, authority, pathAndQuery, _version, _writeBuffer.AvailableSpan);
            _writeBuffer.Commit(len);
            _writeState = WriteState.RequestWritten;
        }

        internal int GetEncodeRequestLength(ReadOnlySpan<byte> method, ReadOnlySpan<byte> authority, ReadOnlySpan<byte> pathAndQuery) =>
            method.Length + pathAndQuery.Length + authority.Length + (!_requestIsChunked ? 20 : 48); // "{method} {uri} HTTP/x.x\r\nHost: {origin}\r\nTransfer-Encoding: chunked\r\n"

        internal unsafe void EncodeRequest(ReadOnlySpan<byte> method, ReadOnlySpan<byte> authority, ReadOnlySpan<byte> pathAndQuery, HttpPrimitiveVersion version, Span<byte> buffer)
        {
            Debug.Assert(authority.Length > 0);
            Debug.Assert(method.Length > 0);
            Debug.Assert(pathAndQuery.Length > 0);
            Debug.Assert(buffer.Length >= GetEncodeRequestLength(authority, method, pathAndQuery));

            ref byte pBuf = ref MemoryMarshal.GetReference(buffer);

            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(method), (uint)method.Length);
            pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)method.Length);

            pBuf = (byte)' ';
            pBuf = ref Unsafe.Add(ref pBuf, 1);

            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(pathAndQuery), (uint)pathAndQuery.Length);
            pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)pathAndQuery.Length);

            pBuf = (byte)' ';
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 1), version._encoded); // "HTTP/x.x"
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 9), s_encodedNewLineHostPrefix); // "\r\nHost: "
            pBuf = ref Unsafe.Add(ref pBuf, 17);

            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(authority), (uint)authority.Length);
            pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)authority.Length);

            if (!_requestIsChunked)
            {
                // \r\n (end of headers)
                Unsafe.WriteUnaligned(ref pBuf, s_encodedCrLf);
                pBuf = ref Unsafe.Add(ref pBuf, 2);
            }
            else
            {
                // \r\nTransfer-Encoding: chunked\r\n
                Unsafe.WriteUnaligned(ref pBuf, s_encodedNewLineTransferEncodingChunked_0);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 8), s_encodedNewLineTransferEncodingChunked_1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 16), s_encodedNewLineTransferEncodingChunked_2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 24), s_encodedNewLineTransferEncodingChunked_3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref pBuf, 28), s_encodedCrLf);
                pBuf = ref Unsafe.Add(ref pBuf, 30);
            }

            int length = Tools.UnsafeByteOffset(buffer, ref pBuf);
            Debug.Assert(length == GetEncodeRequestLength(authority, method, pathAndQuery));
        }

        internal void WriteHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            if (name.Length == 0) throw new ArgumentException($"{nameof(name)} must have a non-zero length.", nameof(name));

            MoveWriteHeaderStateForward();

            int len = GetEncodeHeaderLength(name, value);
            _writeBuffer.EnsureAvailableSpace(len);
            EncodeHeader(name, value, _writeBuffer.AvailableSpan);
            _writeBuffer.Commit(len);
        }

        internal void WriteHeader(PreparedHeaderName name, ReadOnlySpan<byte> value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            MoveWriteHeaderStateForward();

            int len = GetEncodeHeaderLength(name, value);
            _writeBuffer.EnsureAvailableSpace(len);
            EncodeHeader(name, value, _writeBuffer.AvailableSpan);
            _writeBuffer.Commit(len);
        }

        internal void WriteHeader(PreparedHeaderSet headers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            MoveWriteHeaderStateForward();

            byte[] headersBuffer = headers.Http1Value;

            _writeBuffer.EnsureAvailableSpace(headersBuffer.Length);
            headersBuffer.AsSpan().CopyTo(_writeBuffer.AvailableSpan);
            _writeBuffer.Commit(headersBuffer.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveWriteHeaderStateForward()
        {
            switch (_writeState)
            {
                case WriteState.Unstarted:
                    throw new InvalidOperationException($"Must call {nameof(ValueHttpRequest)}.{nameof(HttpRequest.WriteRequestStart)} prior to writing headers.");
                case WriteState.RequestWritten:
                    _writeState = WriteState.HeadersWritten;
                    return;
                case WriteState.HeadersWritten:
                    return;
                default:
                    throw new InvalidOperationException("Can not write headers after headers have been flushed.");
            }
        }

        internal void WriteTrailingHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            if (!_requestIsChunked)
            {
                throw new InvalidOperationException($"Can not write trailing headers without first enabling them during {nameof(ValueHttpRequest)}.{nameof(ValueHttpRequest.ConfigureRequest)}.");
            }

            if (name.Length == 0)
            {
                throw new ArgumentException();
            }

            switch (_writeState)
            {
                case WriteState.Unstarted:
                    throw new InvalidOperationException($"Must call {nameof(ValueHttpRequest)}.{nameof(HttpRequest.WriteRequestStart)} prior to writing headers.");
                case WriteState.RequestWritten:
                case WriteState.HeadersWritten:
                    WriteCRLF(); // end of headers.
                    goto case WriteState.ContentWritten;
                case WriteState.HeadersFlushed:
                case WriteState.ContentWritten:
                    // The first trailing header is being written. End chunked content with a 0-length chunk.
                    WriteChunkEnvelopeStart(0);
                    _writeState = WriteState.TrailingHeadersWritten;
                    break;
                case WriteState.TrailingHeadersWritten:
                    break;
                default: // case WriteState.Finished:
                    Debug.Assert(_writeState == WriteState.Finished);
                    throw new InvalidOperationException("Can not write headers after the request has been completed.");
            }

            int len = GetEncodeHeaderLength(name, value);
            _writeBuffer.EnsureAvailableSpace(len);
            EncodeHeader(name, value, _writeBuffer.AvailableSpan);
            _writeBuffer.Commit(len);
        }

        internal static int GetEncodeHeaderLength(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            Debug.Assert(name.Length > 0);
            return name.Length + value.Length + 4; // {name}: {value}\r\n
        }

        internal static unsafe int EncodeHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, Span<byte> buffer)
        {
            Debug.Assert(name.Length > 0);
            Debug.Assert(buffer.Length >= GetEncodeHeaderLength(name, value));

            ref byte pBuf = ref MemoryMarshal.GetReference(buffer);

            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(name), (uint)name.Length);
            pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)name.Length);

            Unsafe.WriteUnaligned(ref pBuf, s_encodedColonSpace);
            pBuf = ref Unsafe.Add(ref pBuf, 2);

            if (value.Length != 0)
            {
                Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(value), (uint)value.Length);
                pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)value.Length);
            }

            Unsafe.WriteUnaligned(ref pBuf, s_encodedCrLf);
            pBuf = ref Unsafe.Add(ref pBuf, 2);

            int length = (int)(void*)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(buffer), ref pBuf);
            Debug.Assert(length == GetEncodeHeaderLength(name, value));

            return length;
        }

        internal static int GetEncodeHeaderLength(PreparedHeaderName name, ReadOnlySpan<byte> value)
        {
            return name._http1Encoded.Length + value.Length + 2; // {name}{value}\r\n
        }

        internal static unsafe int EncodeHeader(PreparedHeaderName name, ReadOnlySpan<byte> value, Span<byte> buffer)
        {
            Debug.Assert(buffer.Length >= GetEncodeHeaderLength(name, value));

            ref byte pBuf = ref MemoryMarshal.GetReference(buffer);

            byte[] encodedName = name._http1Encoded;
            Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetArrayDataReference(encodedName), (uint)encodedName.Length);
            pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)encodedName.Length);

            if (value.Length != 0)
            {
                Unsafe.CopyBlockUnaligned(ref pBuf, ref MemoryMarshal.GetReference(value), (uint)value.Length);
                pBuf = ref Unsafe.Add(ref pBuf, (IntPtr)(uint)value.Length);
            }

            Unsafe.WriteUnaligned(ref pBuf, s_encodedCrLf);
            pBuf = ref Unsafe.Add(ref pBuf, 2);

            int length = (int)(void*)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(buffer), ref pBuf);
            Debug.Assert(length == GetEncodeHeaderLength(name, value));

            return length;
        }

        private void WriteCRLF()
        {
            _writeBuffer.EnsureAvailableSpace(2);

            ref byte pBuf = ref MemoryMarshal.GetReference(_writeBuffer.AvailableSpan);
            Unsafe.WriteUnaligned(ref pBuf, s_encodedCrLf);

            _writeBuffer.Commit(2);
        }

        /// <param name="completingRequest">If true, the entire request side is completed. If false, only flushing headers.</param>
        private void FlushHeadersHelper(bool completingRequest)
        {
            switch (_writeState)
            {
                case WriteState.Unstarted:
                    throw new InvalidOperationException($"{nameof(ValueHttpRequest)}.{nameof(HttpRequest.WriteRequestStart)} must be called prior to calling this.");
                case WriteState.RequestWritten:
                case WriteState.HeadersWritten:
                    if (!completingRequest)
                    {
                        _writeState = WriteState.HeadersFlushed;
                        // end of headers to be written below.
                    }
                    else
                    {
                        _writeState = WriteState.Finished;
                        if (_requestIsChunked)
                        {
                            // end headers.
                            WriteCRLF();

                            // end of content.
                            WriteChunkEnvelopeStart(0);

                            // end of trailing headers to be written below.
                        }
                    }
                    break;
                case WriteState.HeadersFlushed:
                case WriteState.ContentWritten:
                    if (!_requestIsChunked)
                    {
                        if (completingRequest)
                        {
                            Debug.Assert(_writeBuffer.ActiveLength == 0);
                            _writeState = WriteState.Finished;
                            return;
                        }

                        throw new InvalidOperationException("Can not write headers after content without trailing headers being enabled.");
                    }

                    // 0-length chunk to end content is normally written with the first trailing
                    // header written, otherwise it needs to be written here.
                    WriteChunkEnvelopeStart(0);
                    _writeState = WriteState.Finished;
                    break;
                case WriteState.TrailingHeadersWritten:
                    _writeState = WriteState.Finished;
                    // end of trailing headers to be written below.
                    break;
                default: //case WriteState.Finished:
                    Debug.Assert(_writeState == WriteState.Finished);

                    if (completingRequest)
                    {
                        Debug.Assert(_writeBuffer.ActiveLength == 0);
                        return;
                    }

                    throw new InvalidOperationException("Cannot flush headers on an already completed request.");
            }

            WriteCRLF(); // CRLF to end headers.
        }

        internal async ValueTask FlushHeadersAsync(CancellationToken cancellationToken)
        {
            try
            {
                FlushHeadersHelper(completingRequest: false);
                await _stream.WriteAsync(_writeBuffer.ActiveMemory, cancellationToken).ConfigureAwait(false);
                _writeBuffer.Discard(_writeBuffer.ActiveLength);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await SetConnectionExceptionAsync(ex).ConfigureAwait(false);
                throw CreateRethrowException();
            }
        }

        internal async ValueTask CompleteRequestAsync(CancellationToken cancellationToken)
        {
            try
            {
                FlushHeadersHelper(completingRequest: true);

                FlushType flushType = _closeConnection && _enhancedStream.CanShutdownWrites
                    ? FlushType.FlushAndShutdownWrites
                    : FlushType.FlushWrites;

                ValueTask flushTask;

                if (_writeBuffer.ActiveLength != 0)
                {
                    flushTask = _enhancedStream.WriteAsync(_writeBuffer.ActiveMemory, flushType, cancellationToken);
                    _writeBuffer.Discard(_writeBuffer.ActiveLength);
                }
                else
                {
                    flushTask = _enhancedStream.FlushAsync(flushType, cancellationToken);
                }

                await flushTask.ConfigureAwait(false);

                if (_closeConnection)
                {
                    _connectionState = HttpConnectionStatus.Closing;
                }

                ReleaseNextWriter();
            }
            catch (Exception ex)
            {
                await SetConnectionExceptionAsync(ex).ConfigureAwait(false);
                throw CreateRethrowException();
            }
        }

        internal async ValueTask WriteContentAsync(ReadOnlyMemory<byte> buffer, IReadOnlyList<ReadOnlyMemory<byte>>? buffers, bool flush, CancellationToken cancellationToken)
        {
            FlushType flushType = flush ? FlushType.FlushWrites : FlushType.None;

            try
            {
                switch (_writeState)
                {
                    case WriteState.RequestWritten:
                    case WriteState.HeadersWritten:
                        FlushHeadersHelper(completingRequest: false);
                        _writeState = WriteState.ContentWritten;
                        break;
                    case WriteState.HeadersFlushed:
                        _writeState = WriteState.ContentWritten;
                        break;
                    case WriteState.ContentWritten:
                        break;
                    default:
                        Debug.Assert(_writeState == WriteState.TrailingHeadersWritten || _writeState == WriteState.Finished);
                        throw new InvalidOperationException("Content can not be written after the request has been completed or trailing headers have been written.");
                }

                ValueTask task;

                if (_requestIsChunked)
                {
                    // chunked, write bytes as "{length in hex}\r\n" + buffer + "\r\n"

                    ulong bufferLength = 0;

                    if (buffers != null)
                    {
                        for (int i = 0, count = buffers.Count; i < count; ++i)
                        {
                            bufferLength += (uint)buffers[i].Length;
                        }
                    }
                    else
                    {
                        bufferLength = (uint)buffer.Length;
                    }

                    WriteChunkEnvelopeStart(bufferLength);

                    _gatheredWriteBuffer.Clear();
                    _gatheredWriteBuffer.Add(_writeBuffer.ActiveMemory);
                    _writeBuffer.Discard(_writeBuffer.ActiveLength);

                    if (buffers != null)
                    {
                        _gatheredWriteBuffer.AddRange(buffers);
                    }
                    else
                    {
                        _gatheredWriteBuffer.Add(buffer);
                    }

                    _gatheredWriteBuffer.Add(s_encodedCRLFMemory);

                    task = _enhancedStream.WriteAsync(_gatheredWriteBuffer, flushType, cancellationToken);
                }
                else if (_writeBuffer.ActiveLength == 0)
                {
                    // non-chunked, headers are already flushed, write bytes directly.
                    if (buffers != null) task = _enhancedStream.WriteAsync(buffers, flushType, cancellationToken);
                    else task = _enhancedStream.WriteAsync(buffer, flushType, cancellationToken);
                }
                else
                {
                    // non-chunked, headers need flushing.

                    _gatheredWriteBuffer.Clear();
                    _gatheredWriteBuffer.Add(_writeBuffer.ActiveMemory);
                    _writeBuffer.Discard(_writeBuffer.ActiveLength);

                    if (buffers != null)
                    {
                        _gatheredWriteBuffer.AddRange(buffers);
                    }
                    else
                    {
                        _gatheredWriteBuffer.Add(buffer);
                    }

                    task = _enhancedStream.WriteAsync(_gatheredWriteBuffer, flushType, cancellationToken);
                }

                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await SetConnectionExceptionAsync(ex).ConfigureAwait(false);
                throw CreateRethrowException();
            }
        }

        private void WriteChunkEnvelopeStart(ulong chunkLength)
        {
            _writeBuffer.EnsureAvailableSpace(18);

            Span<byte> buffer = _writeBuffer.AvailableSpan;

            bool success = Utf8Formatter.TryFormat(chunkLength, buffer, out int envelopeLength, format: 'X');
            Debug.Assert(success == true);

            Debug.Assert(envelopeLength <= 16);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), envelopeLength), s_encodedCrLf);

            _writeBuffer.Commit(envelopeLength + 2);
        }

        internal async ValueTask FlushContentAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _enhancedStream.FlushAsync(FlushType.FlushWrites, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await SetConnectionExceptionAsync(ex).ConfigureAwait(false);
                throw CreateRethrowException();
            }
        }

        internal ValueTask<HttpReadType> ReadAsync(Http1Request requestStream, CancellationToken cancellationToken)
        {
            _readTaskSource.Reset();
            _readRequest = requestStream;
            _readToken = cancellationToken;

            DoRead();
            return _readTaskSource.Task;
        }

        private void DoRead()
        {
            try
            {
                int readBytes;

                switch (_readState)
                {
                    case ReadState.ReadToResponse:
                        if (!TryReadResponse())
                        {
                            _readBuffer.EnsureAvailableSpace(1);

#pragma warning disable CA2012 // Use ValueTasks correctly
                            _readBytesAwaitable = _stream.ReadAsync(_readBuffer.AvailableMemory, _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                            if (_readBytesAwaitable.IsCompleted)
                            {
                                goto case ReadState.OnResponseBytesReceived;
                            }

                            _readState = ReadState.OnResponseBytesReceived;
                            _readBytesAwaitable.UnsafeOnCompleted(_doReadFunc);
                            return;
                        }

                        HttpReadType result;

                        Debug.Assert(_readRequest != null);
                        if ((int)_readRequest.StatusCode >= 200)
                        {
                            // reset values from previous requests.
                            _responseContentBytesRemaining = 0;
                            _responseChunkBytesRemaining = 0;
                            _responseHasContentLength = false;
                            _responseIsChunked = false;
                            _readingFirstResponseChunk = true;

                            result = HttpReadType.FinalResponse;
                        }
                        else
                        {
                            result = HttpReadType.InformationalResponse;
                        }
                        _readState = ReadState.ReadToHeaders;
                        CompleteRead(result);
                        return;
                    case ReadState.OnResponseBytesReceived:
                        readBytes = _readBytesAwaitable.GetResult();
                        if (readBytes == 0) throw new Exception("Unexpected EOF");
                        _readBuffer.Commit(readBytes);

                        goto case ReadState.ReadToResponse;
                    case ReadState.ReadToHeaders:
                        _readState = ReadState.SkipHeaders;
                        CompleteRead(HttpReadType.Headers);
                        return;
                    case ReadState.SkipHeaders:
                        // only hit when caller didn't read headers (just called ReadAsync() again)
                        _readState = ReadState.OnHeadersRead;

#pragma warning disable CA2012 // Use ValueTasks correctly
                        _readAwaitable = ReadHeadersAsync(NullHttpHeaderSink.Instance, state: null, _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                        if (!_readAwaitable.IsCompleted)
                        {
                            _readAwaitable.UnsafeOnCompleted(_doReadFunc);
                            return;
                        }
                        goto case ReadState.OnHeadersRead;
                    case ReadState.OnHeadersRead:
                        // only hit if SkipHeaders happens.
                        _readAwaitable.GetResult();
                        goto case ReadState.ReadToContent;
                    case ReadState.ReadToContent:
                        if (_readingFirstResponseChunk == false)
                        {
                            // last header of chunked encoding trailers.
                            goto case ReadState.EndOfStream;
                        }

                        Debug.Assert(_readRequest != null);
                        if ((int)_readRequest.StatusCode < 200)
                        {
                            // informational status code. more responses coming.
                            goto case ReadState.ReadToResponse;
                        }

                        // move to content.
                        _readState = ReadState.SkipContent;
                        CompleteRead(HttpReadType.Content);
                        return;
                    case ReadState.SkipContent:
                        // only hit when caller didn't read content (just called ReadAsync() again)
                        _drainContentBuffer ??= ArrayPool<byte>.Shared.Rent(8192);

#pragma warning disable CA2012 // Use ValueTasks correctly
                        _readBytesAwaitable = ReadContentAsync(_drainContentBuffer, _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                        if (!_readBytesAwaitable.IsCompleted)
                        {
                            _readBytesAwaitable.UnsafeOnCompleted(_doReadFunc);
                            return;
                        }
                        goto case ReadState.OnContentReceived;
                    case ReadState.OnContentReceived:
                        // only hit if SkipContent happens.
                        Debug.Assert(_drainContentBuffer != null);

                        try
                        {
                            readBytes = _readBytesAwaitable.GetResult();

                            if(readBytes == 0)
                                _readState = ReadState.OnContentReceived;
                        }
                        catch
                        {
                            ArrayPool<byte>.Shared.Return(_drainContentBuffer);
                            _drainContentBuffer = null;
                            throw;
                        }

                        if (readBytes != 0)
                        {
                            goto case ReadState.SkipContent;
                        }

                        ArrayPool<byte>.Shared.Return(_drainContentBuffer);
                        _drainContentBuffer = null;

                        goto case ReadState.ReadToTrailingHeaders;
                    case ReadState.ReadToTrailingHeaders:
                        if (!_responseIsChunked)
                        {
                            goto case ReadState.EndOfStream;
                        }
                        else
                        {
                            _readState = ReadState.SkipTrailingHeaders;
                            CompleteRead(HttpReadType.TrailingHeaders);
                            return;
                        }
                    case ReadState.SkipTrailingHeaders:
                        _readState = ReadState.OnTrailingHeadersRead;

#pragma warning disable CA2012 // Use ValueTasks correctly
                        _readAwaitable = ReadHeadersAsync(NullHttpHeaderSink.Instance, state: null, _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                        if (!_readAwaitable.IsCompleted)
                        {
                            _readAwaitable.UnsafeOnCompleted(_doReadFunc);
                            return;
                        }
                        goto case ReadState.OnTrailingHeadersRead;
                    case ReadState.OnTrailingHeadersRead:
                        _readAwaitable.GetResult();
                        goto case ReadState.EndOfStream;
                    case ReadState.EndOfStream:
                        _readState = ReadState.EndOfStream;
                        CompleteRead(HttpReadType.EndOfStream);
                        return;
                    case ReadState.OnException:
                        _readState = ReadState.RethrowException;
                        _readAwaitable.GetResult();
                        goto case ReadState.RethrowException;
                    case ReadState.RethrowException:
                        _readTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(CreateRethrowException()));
                        return;
                    default:
                        Debug.Fail($"Unknown {nameof(_readState)} '{_readState}'.");
                        return;
                }
            }
            catch (Exception ex)
            {
                _readState = ReadState.OnException;

#pragma warning disable CA2012 // Use ValueTasks correctly
                _readAwaitable = SetConnectionExceptionAsync(ex).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                _readAwaitable.UnsafeOnCompleted(_doReadFunc);
            }
        }

        private void CompleteRead(HttpReadType readType)
        {
            Debug.Assert(_readRequest != null);
            _readRequest.SetCurrentReadType(readType);
            _readRequest = null;
            _readTaskSource.SetResult(readType);
        }

        private bool TryReadResponse()
        {
            ReadOnlySpan<byte> buffer = _readBuffer.ActiveSpan;

            if (buffer.Length == 0)
            {
                return false;
            }

            Version version;
            HttpStatusCode statusCode;

            if (buffer.Length >= 9 && buffer[8] == ' ')
            {
                ulong x = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(buffer));

                if (x == HttpPrimitiveVersion.Version11._encoded)
                {
                    version = HttpVersion.Version11;
                }
                else if (x == HttpPrimitiveVersion.Version10._encoded)
                {
                    version = HttpVersion.Version10;
                }
                else
                {
                    goto unknownVersion;
                }

                buffer = buffer.Slice(9);
            }
            else
            {
                goto unknownVersion;
            }

        parseStatusCode:
            if (buffer.Length < 4)
            {
                return false;
            }

            uint s100, s10, s1;
            if ((s100 = buffer[0] - (uint)'0') > 9 || (s10 = buffer[1] - (uint)'0') > 9 || (s1 = buffer[2] - (uint)'0') > 9 || buffer[3] != (byte)' ')
            {
                throw new Exception("invalid status code.");
            }

            statusCode = (HttpStatusCode)(s100 * 100u + s10 * 10u + s1);
            buffer = buffer.Slice(4);

            int idx = buffer.IndexOf((byte)'\n');
            if (idx == -1)
            {
                return false;
            }

            buffer = buffer.Slice(idx + 1);
            _readBuffer.Discard(_readBuffer.ActiveLength - buffer.Length);

            Debug.Assert(_readRequest != null);
            _readRequest.SetCurrentResponseLine(version, statusCode);

            return true;

        unknownVersion:
            idx = buffer.IndexOf((byte)' ');
            if (idx == -1) return false;
            version = HttpVersion.Unknown;
            buffer = buffer.Slice(idx + 1);
            goto parseStatusCode;
        }

        internal ValueTask ReadHeadersAsync(IHttpHeadersSink headersSink, object? state, CancellationToken cancellationToken = default)
        {
            switch (_readState)
            {
                case ReadState.SkipHeaders:
                case ReadState.OnHeadersRead:
                case ReadState.SkipTrailingHeaders:
                case ReadState.OnTrailingHeadersRead:
                    break;
                default:
                    return default;
            }

            _readHeadersAndContentTaskSource.Reset();
            _readHeadersState = ReadHeadersState.ReadHeaders;
            _headersSink = headersSink;
            _headersSinkState = state;
            _readToken = cancellationToken;
            DoReadHeaders();
            return _readHeadersAndContentTaskSource.UntypedTask;
        }

        private void DoReadHeaders()
        {
            try
            {
                switch (_readHeadersState)
                {
                    case ReadHeadersState.ReadHeaders:
                        Debug.Assert(_headersSink != null);

                        bool headersFinished = ReadHeadersImpl(_readBuffer.ActiveSpan, _headersSink, _headersSinkState, out int bytesConsumed);
                        _readBuffer.Discard(bytesConsumed);

                        if (!headersFinished)
                        {
                            _readBuffer.EnsureAvailableSpace(1);

#pragma warning disable CA2012 // Use ValueTasks correctly
                            _readBytesAwaitable = _stream.ReadAsync(_readBuffer.AvailableMemory, _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                            if (!_readBytesAwaitable.IsCompleted)
                            {
                                _readHeadersState = ReadHeadersState.OnHeaderBytesReceived;
                                _readBytesAwaitable.UnsafeOnCompleted(_doReadHeadersFunc);
                                return;
                            }

                            goto case ReadHeadersState.OnHeaderBytesReceived;
                        }

                        if (_readState == ReadState.SkipHeaders)
                        {
                            _readState = ReadState.ReadToContent;
                        }
                        else if (_readState == ReadState.SkipTrailingHeaders)
                        {
                            _readState = ReadState.EndOfStream;
                        }
                        _readHeadersAndContentTaskSource.SetResult(0);
                        return;
                    case ReadHeadersState.OnHeaderBytesReceived:
                        int bytesRead = _readBytesAwaitable.GetResult();
                        if (bytesRead == 0) throw new Exception("Unexpected EOF.");

                        _readBuffer.Commit(bytesRead);
                        goto case ReadHeadersState.ReadHeaders;
                    case ReadHeadersState.OnException:
                        _readState = ReadState.RethrowException;
                        _readHeadersAwaitable.GetResult();
                        goto case ReadHeadersState.RethrowException;
                    case ReadHeadersState.RethrowException:
                        _readHeadersAndContentTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(CreateRethrowException()));
                        return;
                    default:
                        Debug.Fail($"Unknown {nameof(_readHeadersState)} '{_readHeadersState}'.");
                        return;
                }
            }
            catch (Exception ex)
            {
                _readHeadersState = ReadHeadersState.OnException;

#pragma warning disable CA2012 // Use ValueTasks correctly
                _readHeadersAwaitable = SetConnectionExceptionAsync(ex).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                _readHeadersAwaitable.UnsafeOnCompleted(_doReadHeadersFunc);
            }
        }

        partial void ProcessKnownHeaders(ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue)
        {
            switch (headerName.Length)
            {
                case 10 when EqualsIgnoreCase(headerName, s_EncodedConnectionName) && EqualsIgnoreCase(headerValue, s_EncodedConnectionCloseValue):
                    _closeConnection = true;
                    break;
                case 14 when EqualsIgnoreCase(headerName, s_EncodedContentLengthName):
                    if (!TryParseDecimalInteger(headerValue, out _responseContentBytesRemaining))
                    {
                        throw new Exception("Response Content-Length header contains a malformed or too-large value.");
                    }
                    _responseHasContentLength = true;
                    break;
                case 17 when EqualsIgnoreCase(headerName, s_EncodedTransferEncodingName) && EqualsIgnoreCase(headerValue, s_EncodedTransferEncodingChunkedValue):
                    _responseIsChunked = true;
                    break;
            }
        }

        // TODO: in .NET 6, migrate to https://github.com/dotnet/runtime/issues/28230
        private static bool EqualsIgnoreCase(ReadOnlySpan<byte> wireValue, ReadOnlySpan<byte> expectedValueLowerCase)
        {
            if (wireValue.Length != expectedValueLowerCase.Length) return false;

            ref byte xRef = ref MemoryMarshal.GetReference(wireValue);
            ref byte yRef = ref MemoryMarshal.GetReference(expectedValueLowerCase);

            for (uint i = 0; i < (uint)wireValue.Length; ++i)
            {
                byte xv = Unsafe.Add(ref xRef, (IntPtr)i);

                if ((xv - (uint)'A') <= ('Z' - 'A'))
                {
                    xv |= 0x20;
                }

                if (xv != Unsafe.Add(ref yRef, (IntPtr)i)) return false;
            }

            return true;
        }

        internal ValueTask<int> ReadContentAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_readState != ReadState.SkipContent && _readState != ReadState.OnResponseBytesReceived)
            {
                return new ValueTask<int>(0);
            }

            _readHeadersAndContentTaskSource.Reset();
            _readUserBuffer = buffer;
            _readToken = cancellationToken;
            _readContentState = ReadContentState.ReadContent;

            if (!_responseIsChunked)
            {
                DoReadUnenvelopedContent();
            }
            else
            {
                DoReadChunkedContent();
            }

            return _readHeadersAndContentTaskSource.Task;
        }

        private void DoReadUnenvelopedContent()
        {
            try
            {
                int takeLength;

                switch (_readContentState)
                {
                    case ReadContentState.ReadContent:
                        int maxReadLength = _readUserBuffer.Length;

                        if (_responseHasContentLength && _responseContentBytesRemaining <= (uint)maxReadLength)
                        {
                            if (_responseContentBytesRemaining == 0)
                            {
                                // End of content reached, or empty buffer.

                                if (_readState != ReadState.OnContentReceived)
                                {
                                    // Move to end of stream if user called.
                                    _readState = ReadState.EndOfStream;
                                }

                                _readHeadersAndContentTaskSource.SetResult(0);
                                return;
                            }

                            maxReadLength = (int)_responseContentBytesRemaining;
                        }

                        Debug.Assert(maxReadLength > 0 || _readUserBuffer.Length == 0);

                        int bufferedLength = _readBuffer.ActiveLength;
                        if (bufferedLength != 0)
                        {
                            takeLength = Math.Min(bufferedLength, maxReadLength);
                            if (takeLength != 0)
                            {
                                _readBuffer.ActiveSpan.Slice(0, takeLength).CopyTo(_readUserBuffer.Span);
                                _readBuffer.Discard(takeLength);
                            }

                            if (_responseHasContentLength)
                            {
                                _responseContentBytesRemaining -= (uint)takeLength;
                            }

                            _readHeadersAndContentTaskSource.SetResult(takeLength);
                            return;
                        }

#pragma warning disable CA2012 // Use ValueTasks correctly
                        _readBytesAwaitable = _stream.ReadAsync(_readUserBuffer.Slice(0, maxReadLength), _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                        if (!_readBytesAwaitable.IsCompleted)
                        {
                            _readContentState = ReadContentState.OnContentBytesReceived;
                            _readBytesAwaitable.UnsafeOnCompleted(_doReadUnenvelopedContentFunc);
                            return;
                        }

                        goto case ReadContentState.OnContentBytesReceived;
                    case ReadContentState.OnContentBytesReceived:
                        takeLength = _readBytesAwaitable.GetResult();

                        if (takeLength == 0 && _readUserBuffer.Length != 0)
                        {
                            if (_responseHasContentLength) throw new Exception("Unexpected EOF");

                            if (_readState != ReadState.OnContentReceived)
                            {
                                // Move to end of stream if user called.
                                _readState = ReadState.EndOfStream;
                            }
                        }

                        if (_responseHasContentLength)
                        {
                            _responseContentBytesRemaining -= (uint)takeLength;
                        }

                        _readHeadersAndContentTaskSource.SetResult(takeLength);
                        return;
                    case ReadContentState.OnException:
                        _readContentState = ReadContentState.RethrowException;
                        _readAwaitable.GetResult();
                        goto case ReadContentState.RethrowException;
                    case ReadContentState.RethrowException:
                        _readHeadersAndContentTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(CreateRethrowException()));
                        return;
                    default:
                        Debug.Fail($"Unknown (unenveloped) {nameof(_readContentState)} '{_readContentState}'.");
                        return;
                }
            }
            catch (Exception ex)
            {
                _readContentState = ReadContentState.OnException;

#pragma warning disable CA2012 // Use ValueTasks correctly
                _readAwaitable = SetConnectionExceptionAsync(ex).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                _readAwaitable.UnsafeOnCompleted(_doReadUnenvelopedContentFunc);
            }
        }

        private void DoReadChunkedContent()
        {
            try
            {
                int takeLength;
                ulong takeLengthExtended;

                switch (_readContentState)
                {
                    case ReadContentState.ReadContent:
                        if (_responseChunkBytesRemaining == 0)
                        {
                            goto case ReadContentState.ReadChunkHeader;
                        }

                        int maxReadLength = (int)Math.Min((uint)_readUserBuffer.Length, _responseChunkBytesRemaining);

                        int bufferedLength = _readBuffer.ActiveLength;
                        if (bufferedLength != 0)
                        {
                            takeLength = Math.Min(bufferedLength, maxReadLength);
                            if (takeLength != 0)
                            {
                                _readBuffer.ActiveSpan.Slice(0, takeLength).CopyTo(_readUserBuffer.Span);
                                _readBuffer.Discard(takeLength);
                            }

                            takeLengthExtended = (uint)takeLength;
                            _responseChunkBytesRemaining -= takeLengthExtended;

                            if (_responseHasContentLength)
                            {
                                _responseContentBytesRemaining -= takeLengthExtended;
                            }

                            _readHeadersAndContentTaskSource.SetResult(takeLength);
                            return;
                        }

#pragma warning disable CA2012 // Use ValueTasks correctly
                        _readBytesAwaitable = _stream.ReadAsync(_readUserBuffer.Slice(0, maxReadLength), _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                        if (!_readBytesAwaitable.IsCompleted)
                        {
                            _readContentState = ReadContentState.OnContentBytesReceived;
                            _readBytesAwaitable.UnsafeOnCompleted(_doReadChunkedContentFunc);
                            return;
                        }

                        goto case ReadContentState.OnContentBytesReceived;
                    case ReadContentState.OnContentBytesReceived:
                        takeLength = _readBytesAwaitable.GetResult();

                        if (takeLength == 0 && _readUserBuffer.Length != 0)
                        {
                            if (_responseHasContentLength) throw new Exception("Unexpected EOF");

                            if (_readState != ReadState.OnContentReceived)
                            {
                                // Move to end of stream if user called.
                                _readState = ReadState.EndOfStream;
                            }
                        }

                        takeLengthExtended = (uint)takeLength;
                        _responseChunkBytesRemaining -= takeLengthExtended;

                        if (_responseHasContentLength)
                        {
                            _responseContentBytesRemaining -= takeLengthExtended;
                        }

                        _readHeadersAndContentTaskSource.SetResult(takeLength);
                        return;
                    case ReadContentState.OnException:
                        _readContentState = ReadContentState.RethrowException;
                        _readAwaitable.GetResult();
                        goto case ReadContentState.RethrowException;
                    case ReadContentState.RethrowException:
                        _readHeadersAndContentTaskSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(CreateRethrowException()));
                        return;
                    case ReadContentState.ReadChunkHeader:
                        if (TryReadNextChunkSize(out _responseChunkBytesRemaining))
                        {
                            if (_responseChunkBytesRemaining == 0)
                            {
                                // Final chunk. Move to trailing headers, if the user called this.
                                if (_readState != ReadState.OnContentReceived)
                                {
                                    _readState = ReadState.ReadToTrailingHeaders;
                                }

                                _readHeadersAndContentTaskSource.SetResult(0);
                                return;
                            }

                            goto case ReadContentState.ReadContent;
                        }

                        _readBuffer.EnsureAvailableSpace(1);

#pragma warning disable CA2012 // Use ValueTasks correctly
                        _readBytesAwaitable = _stream.ReadAsync(_readBuffer.AvailableMemory, _readToken).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                        if (!_readBytesAwaitable.IsCompleted)
                        {
                            _readContentState = ReadContentState.OnChunkHeaderReceived;
                            _readBytesAwaitable.UnsafeOnCompleted(_doReadChunkedContentFunc);
                            return;
                        }

                        goto case ReadContentState.OnChunkHeaderReceived;
                    case ReadContentState.OnChunkHeaderReceived:
                        takeLength = _readBytesAwaitable.GetResult();
                        if (takeLength == 0) throw new Exception("Unexpected EOF, expected chunk envelope.");

                        _readBuffer.Commit(takeLength);
                        goto case ReadContentState.ReadChunkHeader;
                    default:
                        Debug.Fail($"Unknown (chunked) {nameof(_readContentState)} '{_readContentState}'.");
                        return;
                }
            }
            catch (Exception ex)
            {
                _readContentState = ReadContentState.OnException;

#pragma warning disable CA2012 // Use ValueTasks correctly
                _readAwaitable = SetConnectionExceptionAsync(ex).ConfigureAwait(false).GetAwaiter();
#pragma warning restore CA2012 // Use ValueTasks correctly

                _readAwaitable.UnsafeOnCompleted(_doReadUnenvelopedContentFunc);
            }
        }

        private bool TryReadNextChunkSize(out ulong chunkSize)
        {
            Span<byte> buffer = _readBuffer.ActiveSpan;
            int previousChunkNewlineLength = 0;

            if (!_readingFirstResponseChunk)
            {
                if (buffer.Length == 0)
                {
                    goto needMore;
                }

                byte b = buffer[0];
                if (b == '\r')
                {
                    if (buffer.Length == 1)
                    {
                        goto needMore;
                    }

                    if (buffer[1] == '\n')
                    {
                        buffer = buffer[2..];
                        previousChunkNewlineLength = 2;
                    }
                    else
                    {
                        throw new Exception("Invalid chunked envelope; expected newline to end chunk.");
                    }
                }
                else if (b == '\n')
                {
                    buffer = buffer[1..];
                    previousChunkNewlineLength = 1;
                }
                else
                {
                    throw new Exception("Invalid chunked envelope; expected newline to end chunk.");
                }
            }

            int lfIdx = buffer.IndexOf((byte)'\n');
            int valueEndIdx = lfIdx;

            if (lfIdx == -1)
            {
                goto needMore;
            }

            if (lfIdx != 0 && buffer[lfIdx - 1] == '\r')
            {
                valueEndIdx = lfIdx - 1;
            }

            if (valueEndIdx != 0)
            {
                int chunkExtBeginIdx = buffer.Slice(0, valueEndIdx).IndexOf((byte)';');
                if (chunkExtBeginIdx != -1) valueEndIdx = chunkExtBeginIdx;
            }

            if (valueEndIdx == 0 || !TryParseHexInteger(buffer.Slice(0, valueEndIdx), out chunkSize))
            {
                throw new Exception($"Invalid chunked envelope; expected chunk size. idx: {valueEndIdx}.");
            }

            _readBuffer.Discard(previousChunkNewlineLength + lfIdx + 1);
            _readingFirstResponseChunk = false;

            return true;

        needMore:
            chunkSize = 0;
            return false;
        }

        private static bool TryParseHexInteger(ReadOnlySpan<byte> buffer, out ulong value)
        {
            if (buffer.Length == 0)
            {
                value = default;
                return false;
            }

            ulong contentLength = 0;

            try
            {
                foreach (byte b in buffer)
                {
                    uint digit;

                    if ((digit = b - (uint)'0') <= 9)
                    {
                    }
                    else if ((digit = b - (uint)'a') <= 'f' - 'a')
                    {
                        digit += 10;
                    }
                    else if ((digit = b - (uint)'A') <= 'F' - 'A')
                    {
                        digit += 10;
                    }
                    else
                    {
                        value = default;
                        return false;
                    }

                    contentLength = checked(contentLength * 16u + digit);
                }
            }
            catch (OverflowException)
            {
                value = default;
                return false;
            }

            value = contentLength;
            return true;
        }

        private static bool TryParseDecimalInteger(ReadOnlySpan<byte> buffer, out ulong value)
        {
            if (buffer.Length == 0)
            {
                value = default;
                return false;
            }

            ulong contentLength = 0;

            try
            {
                foreach (byte b in buffer)
                {
                    uint digit = b - (uint)'0';
                    if (digit > 9)
                    {
                        value = default;
                        return false;
                    }

                    contentLength = checked(contentLength * 10u + digit);
                }
            }
            catch (OverflowException)
            {
                value = default;
                return false;
            }

            value = contentLength;
            return true;
        }
    }
}
