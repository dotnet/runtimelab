// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ------------------------------------------------------------------------------
// Changes to this file must follow the https://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.IO
{
    public enum FlushType
    {
        None = 0,
        FlushWrites = 1,
        FlushAndShutdownWrites = 2,
    }
    public partial interface IEnhancedStream
    {
        bool CanScatterGather { get; }
        bool CanShutdownWrites { get; }
        void Flush(System.IO.FlushType flushType);
        System.Threading.Tasks.ValueTask FlushAsync(System.IO.FlushType flushType, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        int Read(System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers);
        System.Threading.Tasks.ValueTask<int> ReadAsync(System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        void Write(System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, System.IO.FlushType flushType = System.IO.FlushType.None);
        void Write(System.ReadOnlySpan<byte> buffer, System.IO.FlushType flushType);
        System.Threading.Tasks.ValueTask WriteAsync(System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, System.IO.FlushType flushType = System.IO.FlushType.None, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        System.Threading.Tasks.ValueTask WriteAsync(System.ReadOnlyMemory<byte> buffer, System.IO.FlushType flushType, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
    }
    public partial class WriteBufferingStream : System.IO.Stream, System.IO.IEnhancedStream
    {
        public WriteBufferingStream(System.IO.Stream baseStream, bool ownsStream, int bufferLength) { }
        public override bool CanRead { get { throw null; } }
        public bool CanScatterGather { get { throw null; } }
        public override bool CanSeek { get { throw null; } }
        public bool CanShutdownWrites { get { throw null; } }
        public override bool CanTimeout { get { throw null; } }
        public override bool CanWrite { get { throw null; } }
        public override long Length { get { throw null; } }
        public override long Position { get { throw null; } set { } }
        public override int ReadTimeout { get { throw null; } set { } }
        public override int WriteTimeout { get { throw null; } set { } }
        public override System.IAsyncResult BeginRead(byte[] buffer, int offset, int count, System.AsyncCallback? callback, object? state) { throw null; }
        public sealed override System.IAsyncResult BeginWrite(byte[] buffer, int offset, int count, System.AsyncCallback? callback, object? state) { throw null; }
        public override void CopyTo(System.IO.Stream destination, int bufferSize) { }
        public override System.Threading.Tasks.Task CopyToAsync(System.IO.Stream destination, int bufferSize, System.Threading.CancellationToken cancellationToken) { throw null; }
        protected override void Dispose(bool disposing) { }
        public override System.Threading.Tasks.ValueTask DisposeAsync() { throw null; }
        public override int EndRead(System.IAsyncResult asyncResult) { throw null; }
        public sealed override void EndWrite(System.IAsyncResult asyncResult) { }
        public sealed override void Flush() { }
        public virtual void Flush(System.IO.FlushType flushType) { }
        public virtual System.Threading.Tasks.ValueTask FlushAsync(System.IO.FlushType flushType, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public sealed override System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public override int Read(byte[] buffer, int offset, int count) { throw null; }
        public virtual int Read(System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers) { throw null; }
        public override int Read(System.Span<byte> buffer) { throw null; }
        public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken) { throw null; }
        public virtual System.Threading.Tasks.ValueTask<int> ReadAsync(System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public override System.Threading.Tasks.ValueTask<int> ReadAsync(System.Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public override int ReadByte() { throw null; }
        public sealed override long Seek(long offset, System.IO.SeekOrigin origin) { throw null; }
        public sealed override void SetLength(long value) { }
        public sealed override void Write(byte[] buffer, int offset, int count) { }
        public virtual void Write(System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, System.IO.FlushType flushType = System.IO.FlushType.None) { }
        public sealed override void Write(System.ReadOnlySpan<byte> buffer) { }
        public virtual void Write(System.ReadOnlySpan<byte> buffer, System.IO.FlushType flushType) { }
        public sealed override System.Threading.Tasks.Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken) { throw null; }
        public virtual System.Threading.Tasks.ValueTask WriteAsync(System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, System.IO.FlushType flushType, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public virtual System.Threading.Tasks.ValueTask WriteAsync(System.ReadOnlyMemory<byte> buffer, System.IO.FlushType flushType, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public sealed override System.Threading.Tasks.ValueTask WriteAsync(System.ReadOnlyMemory<byte> buffer, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public override void WriteByte(byte value) { }
    }
}
namespace System.Net.Http.Primitives
{
    public partial class Http1Connection : System.Net.Http.Primitives.HttpBaseConnection
    {
        public Http1Connection(System.IO.Stream stream, System.Net.Http.Primitives.HttpPrimitiveVersion version) { }
        public static int HeaderBufferPadding { get { throw null; } }
        public override System.Net.Http.Primitives.HttpConnectionStatus Status { get { throw null; } }
        public override System.Threading.Tasks.ValueTask<System.Net.Http.Primitives.ValueHttpRequest?> CreateNewRequestAsync(System.Net.Http.Primitives.HttpPrimitiveVersion version, System.Net.Http.HttpVersionPolicy versionPolicy, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        protected override System.Threading.Tasks.ValueTask DisposeAsyncCore() { throw null; }
        public override System.Threading.Tasks.ValueTask PrunePoolsAsync(long curTicks, System.TimeSpan lifetimeLimit, System.TimeSpan idleLimit, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
    }
    public abstract partial class HttpBaseConnection : System.Net.Http.Primitives.HttpConnection
    {
        internal HttpBaseConnection() { }
        protected void RefreshLastUsed(long curTicks) { }
    }
    public abstract partial class HttpConnection : System.IAsyncDisposable
    {
        protected HttpConnection() { }
        public abstract System.Net.Http.Primitives.HttpConnectionStatus Status { get; }
        public abstract System.Threading.Tasks.ValueTask<System.Net.Http.Primitives.ValueHttpRequest?> CreateNewRequestAsync(System.Net.Http.Primitives.HttpPrimitiveVersion version, System.Net.Http.HttpVersionPolicy versionPolicy, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        public System.Threading.Tasks.ValueTask DisposeAsync() { throw null; }
        protected abstract System.Threading.Tasks.ValueTask DisposeAsyncCore();
        public abstract System.Threading.Tasks.ValueTask PrunePoolsAsync(long curTicks, System.TimeSpan lifetimeLimit, System.TimeSpan idleLimit, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
    }
    public enum HttpConnectionStatus
    {
        Open = 0,
        Closing = 1,
        Closed = 2,
    }
    [System.FlagsAttribute]
    public enum HttpHeaderFlags
    {
        None = 0,
        NameHuffmanCoded = 1,
        ValueHuffmanCoded = 2,
        NeverCompressed = 4,
    }
    public sealed partial class HttpPrimitiveVersion
    {
        internal HttpPrimitiveVersion() { }
        public int Major { get { throw null; } }
        public int Minor { get { throw null; } }
        public static System.Net.Http.Primitives.HttpPrimitiveVersion Version10 { get { throw null; } }
        public static System.Net.Http.Primitives.HttpPrimitiveVersion Version11 { get { throw null; } }
    }
    public enum HttpReadType
    {
        None = 0,
        InformationalResponse = 1,
        FinalResponse = 2,
        Headers = 3,
        Content = 4,
        TrailingHeaders = 5,
        EndOfStream = 6,
        AltSvc = 7,
    }
    public abstract partial class HttpRequest
    {
        protected HttpRequest() { }
        protected internal virtual System.ReadOnlyMemory<byte> AltSvc { get { throw null; } }
        public static System.ReadOnlySpan<byte> ConnectMethod { get { throw null; } }
        public static System.ReadOnlySpan<byte> GetMethod { get { throw null; } }
        public static System.ReadOnlySpan<byte> PostMethod { get { throw null; } }
        public static System.ReadOnlySpan<byte> PutMethod { get { throw null; } }
        protected internal System.Net.Http.Primitives.HttpReadType ReadType { get { throw null; } protected set { } }
        protected internal System.Net.HttpStatusCode StatusCode { get { throw null; } protected set { } }
        protected internal System.Version? Version { get { throw null; } protected set { } }
        protected internal abstract System.Threading.Tasks.ValueTask CompleteRequestAsync(int version, System.Threading.CancellationToken cancellationToken);
        protected internal abstract void ConfigureRequest(int version, bool hasContentLength, bool hasTrailingHeaders);
        protected internal abstract System.Threading.Tasks.ValueTask DisposeAsync(int version);
        protected internal abstract System.Threading.Tasks.ValueTask FlushContentAsync(int version, System.Threading.CancellationToken cancellationToken);
        protected internal abstract System.Threading.Tasks.ValueTask FlushHeadersAsync(int version, System.Threading.CancellationToken cancellationToken);
        protected internal System.Net.Http.Primitives.ValueHttpRequest GetValueRequest() { throw null; }
        protected bool IsDisposed(int version) { throw null; }
        protected bool IsDisposed(int version, out System.Threading.Tasks.ValueTask valueTask) { throw null; }
        protected bool IsDisposed<T>(int version, out System.Threading.Tasks.ValueTask<T> valueTask) { throw null; }
        protected internal abstract System.Threading.Tasks.ValueTask<System.Net.Http.Primitives.HttpReadType> ReadAsync(int version, System.Threading.CancellationToken cancellationToken);
        protected internal abstract System.Threading.Tasks.ValueTask<int> ReadContentAsync(int version, System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers, System.Threading.CancellationToken cancellationToken);
        protected internal abstract System.Threading.Tasks.ValueTask<int> ReadContentAsync(int version, System.Memory<byte> buffer, System.Threading.CancellationToken cancellationToken);
        protected internal abstract System.Threading.Tasks.ValueTask ReadHeadersAsync(int version, System.Net.Http.Primitives.IHttpHeadersSink headersSink, object? state, System.Threading.CancellationToken cancellationToken);
        protected void Reset() { }
        protected void ThrowIfDisposed(int version) { }
        protected internal abstract void WriteConnectRequest(int version, System.ReadOnlySpan<byte> authority);
        protected internal abstract System.Threading.Tasks.ValueTask WriteContentAsync(int version, System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, bool flush, System.Threading.CancellationToken cancellationToken);
        protected internal abstract System.Threading.Tasks.ValueTask WriteContentAsync(int version, System.ReadOnlyMemory<byte> buffer, bool flush, System.Threading.CancellationToken cancellationToken);
        protected internal abstract void WriteHeader(int version, System.Net.Http.Primitives.PreparedHeaderName name, System.ReadOnlySpan<byte> value);
        protected internal abstract void WriteHeader(int version, System.Net.Http.Primitives.PreparedHeaderSet headers);
        protected internal abstract void WriteHeader(int version, System.ReadOnlySpan<byte> name, System.ReadOnlySpan<byte> value);
        protected internal virtual void WriteHeader(int version, string name, System.Collections.Generic.IEnumerable<string> values, string separator) { }
        protected internal virtual void WriteHeader(int version, string name, string value) { }
        protected internal virtual void WriteRequestStart(int version, System.Net.Http.HttpMethod method, System.Uri uri) { }
        protected internal abstract void WriteRequestStart(int version, System.ReadOnlySpan<byte> method, System.ReadOnlySpan<byte> authority, System.ReadOnlySpan<byte> pathAndQuery);
        protected internal abstract void WriteTrailingHeader(int version, System.ReadOnlySpan<byte> name, System.ReadOnlySpan<byte> value);
        protected internal virtual void WriteTrailingHeader(int version, string name, System.Collections.Generic.IEnumerable<string> values, string separator) { }
        protected internal virtual void WriteTrailingHeader(int version, string name, string value) { }
    }
    public partial interface IHttpHeadersSink
    {
        void OnHeader(object? state, System.ReadOnlySpan<byte> headerName, System.ReadOnlySpan<byte> headerValue);
        void OnHeader(object? state, System.ReadOnlySpan<byte> headerName, System.ReadOnlySpan<byte> headerValue, System.Net.Http.Primitives.HttpHeaderFlags flags) { }
    }
    public sealed partial class PreparedHeader
    {
        public PreparedHeader(System.Net.Http.Primitives.PreparedHeaderName name, System.ReadOnlySpan<byte> value) { }
        public PreparedHeader(System.Net.Http.Primitives.PreparedHeaderName name, string value) { }
        public PreparedHeader(string name, System.ReadOnlySpan<byte> value) { }
        public PreparedHeader(string name, string value) { }
        public string Name { get { throw null; } }
        public string Value { get { throw null; } }
        public override string ToString() { throw null; }
    }
    public partial class PreparedHeaderName
    {
        public PreparedHeaderName(string name) { }
        public static System.Net.Http.Primitives.PreparedHeaderName Accept { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName AcceptCharset { get { throw null; } }
        public static System.Net.Http.Primitives.Headers.AcceptEncodingHeader AcceptEncoding { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName AcceptLanguage { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName AcceptRanges { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName AccessControlAllowOrigin { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Age { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Allow { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Authorization { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName CacheControl { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ContentDisposition { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ContentEncoding { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ContentLanguage { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ContentLength { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ContentLocation { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ContentRange { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ContentType { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Cookie { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Date { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ETag { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Expect { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Expires { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName From { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Host { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName IfMatch { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName IfModifiedSince { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName IfNoneMatch { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName IfRange { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName IfUnmodifiedSince { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName LastModified { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Link { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Location { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName MaxForwards { get { throw null; } }
        public string Name { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ProxyAuthenticate { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName ProxyAuthorization { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Range { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Referer { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Refresh { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName RetryAfter { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Server { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName SetCookie { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName StrictTransportSecurity { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName TransferEncoding { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName UserAgent { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Vary { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName Via { get { throw null; } }
        public static System.Net.Http.Primitives.PreparedHeaderName WWWAuthenticate { get { throw null; } }
        public override string ToString() { throw null; }
    }
    public sealed partial class PreparedHeaderSet : System.Collections.Generic.IEnumerable<System.Net.Http.Primitives.PreparedHeader>, System.Collections.IEnumerable
    {
        public PreparedHeaderSet() { }
        public void Add(System.Net.Http.Primitives.PreparedHeader header) { }
        public void Add(System.Net.Http.Primitives.PreparedHeaderName name, System.ReadOnlySpan<byte> value) { }
        public void Add(System.Net.Http.Primitives.PreparedHeaderName name, string value) { }
        public void Add(string name, System.ReadOnlySpan<byte> value) { }
        public void Add(string name, string value) { }
        public System.Collections.Generic.IEnumerator<System.Net.Http.Primitives.PreparedHeader> GetEnumerator() { throw null; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw null; }
        public override string ToString() { throw null; }
    }
    public partial struct ValueHttpRequest : System.IAsyncDisposable
    {
        private object _dummy;
        private int _dummyPrimitive;
        public ValueHttpRequest(System.Net.Http.Primitives.HttpRequest request, int requestVersion) { throw null; }
        public System.ReadOnlyMemory<byte> AltSvc { get { throw null; } }
        public System.Net.Http.Primitives.HttpReadType ReadType { get { throw null; } }
        public System.Net.HttpStatusCode StatusCode { get { throw null; } }
        public System.Version? Version { get { throw null; } }
        public System.Threading.Tasks.ValueTask CompleteRequestAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public void ConfigureRequest(bool hasContentLength, bool hasTrailingHeaders) { }
        public System.Threading.Tasks.ValueTask DisposeAsync() { throw null; }
        public System.Threading.Tasks.ValueTask<bool> DrainAsync(int maximumContentSize, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask DrainAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask FlushContentAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask FlushHeadersAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<System.Net.Http.Primitives.HttpReadType> ReadAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<int> ReadContentAsync(System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<int> ReadContentAsync(System.Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask ReadHeadersAsync(System.Net.Http.Primitives.IHttpHeadersSink headersSink, object? state, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<bool> ReadToContentAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<bool> ReadToFinalResponseAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<bool> ReadToHeadersAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<bool> ReadToInformationalResponseAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<bool> ReadToNextContentAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<bool> ReadToNextInformationalResponseAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask<bool> ReadToTrailingHeadersAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public void WriteConnectRequest(System.ReadOnlySpan<byte> authority) { }
        public System.Threading.Tasks.ValueTask WriteContentAsync(System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, bool flush = false, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.ValueTask WriteContentAsync(System.ReadOnlyMemory<byte> buffer, bool flush = false, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public void WriteHeader(System.Net.Http.Primitives.PreparedHeaderName name, System.ReadOnlySpan<byte> value) { }
        public void WriteHeader(System.Net.Http.Primitives.PreparedHeaderSet headers) { }
        public void WriteHeader(System.ReadOnlySpan<byte> name, System.ReadOnlySpan<byte> value) { }
        public void WriteHeader(string name, System.Collections.Generic.IEnumerable<string> values, string separator) { }
        public void WriteHeader(string name, string value) { }
        public void WriteRequestStart(System.Net.Http.HttpMethod method, System.Uri uri) { }
        public void WriteRequestStart(System.ReadOnlySpan<byte> method, System.ReadOnlySpan<byte> authority, System.ReadOnlySpan<byte> pathAndQuery) { }
        public void WriteTrailingHeader(System.ReadOnlySpan<byte> name, System.ReadOnlySpan<byte> value) { }
        public void WriteTrailingHeader(string name, System.Collections.Generic.IEnumerable<string> values, string separator) { }
        public void WriteTrailingHeader(string name, string value) { }
    }
}
namespace System.Net.Http.Primitives.Headers
{
    public sealed partial class AcceptEncodingHeader : System.Net.Http.Primitives.PreparedHeaderName
    {
        internal AcceptEncodingHeader() : base (default(string)) { }
        public System.Net.Http.Primitives.PreparedHeader GzipDeflate { get { throw null; } }
    }
}
namespace System.Net.Sockets
{
    public partial class EnhancedNetworkStream : System.Net.Sockets.NetworkStream, System.IO.IEnhancedStream
    {
        public EnhancedNetworkStream(System.Net.Sockets.Socket socket, bool ownsSocket) : base (default(System.Net.Sockets.Socket)) { }
        public EnhancedNetworkStream(System.Net.Sockets.Socket socket, System.IO.FileAccess access, bool ownsSocket) : base (default(System.Net.Sockets.Socket)) { }
        public virtual bool CanScatterGather { get { throw null; } }
        public virtual bool CanShutdownWrites { get { throw null; } }
        protected override void Dispose(bool disposing) { }
        public sealed override void Flush() { }
        public virtual void Flush(System.IO.FlushType flushType) { }
        public virtual System.Threading.Tasks.ValueTask FlushAsync(System.IO.FlushType flushType, System.Threading.CancellationToken cancellationToken) { throw null; }
        public sealed override System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken cancellationToken) { throw null; }
        public virtual int Read(System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers) { throw null; }
        public virtual System.Threading.Tasks.ValueTask<int> ReadAsync(System.Collections.Generic.IReadOnlyList<System.Memory<byte>> buffers, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public virtual void Write(System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, System.IO.FlushType flushType) { }
        public virtual void Write(System.ReadOnlySpan<byte> buffer, System.IO.FlushType flushType) { }
        public virtual System.Threading.Tasks.ValueTask WriteAsync(System.Collections.Generic.IReadOnlyList<System.ReadOnlyMemory<byte>> buffers, System.IO.FlushType flushType = System.IO.FlushType.None, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public virtual System.Threading.Tasks.ValueTask WriteAsync(System.ReadOnlyMemory<byte> buffer, System.IO.FlushType flushType = System.IO.FlushType.None, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
    }
}
