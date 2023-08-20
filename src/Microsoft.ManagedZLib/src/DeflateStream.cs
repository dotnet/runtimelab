// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ManagedZLib;

public partial class DeflateStream : Stream
{
    private const int DefaultBufferSize = 8192; // Default block size(8KB)
    private Stream _stream;
    private Inflater? _inflater;
    private Deflater? _deflater;
    private byte[]? _buffer;
    private bool _activeAsyncOperation;
    private CompressionMode _mode;
    private bool _leaveOpen;
    private bool _wroteBytes;
    internal DeflateStream(Stream stream, CompressionMode mode, long uncompressedSize) : this(stream, mode, leaveOpen: false, ManagedZLib.Deflate_DefaultWindowBits, uncompressedSize)
    {
    }

    public DeflateStream(Stream stream, CompressionMode mode) : this(stream, mode, leaveOpen: false)
    {
    }

    public DeflateStream(Stream stream, CompressionMode mode, bool leaveOpen) : this(stream, mode, leaveOpen, ManagedZLib.Deflate_DefaultWindowBits)
    {
    }

    // Implies mode = Compress
    public DeflateStream(Stream stream, CompressionLevel compressionLevel) : this(stream, compressionLevel, leaveOpen: false)
    {
    }

    // Implies mode = Compress
    public DeflateStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen) : this(stream, compressionLevel, leaveOpen, ManagedZLib.Deflate_DefaultWindowBits)
    {
    }

    /// <summary>
    /// Internal constructor to check stream validity and call the correct initialization function depending on
    /// the value of the CompressionMode given.
    /// </summary>
    public DeflateStream(Stream stream, CompressionMode mode, bool leaveOpen, int windowBits, long uncompressedSize = -1)
    {
        ArgumentNullException.ThrowIfNull(stream);

        switch (mode)
        {
            case CompressionMode.Decompress:
                if (!stream.CanRead)
                    throw new ArgumentException("NotSupported_UnreadableStream - Stream does not support reading.", nameof(stream));

                _inflater = new Inflater(windowBits, uncompressedSize);
                _stream = stream;
                _mode = CompressionMode.Decompress;
                _leaveOpen = leaveOpen;
                break;

            case CompressionMode.Compress:
                InitializeDeflater(stream, leaveOpen, windowBits, CompressionLevel.Optimal);
                break;

            default:
                throw new ArgumentException("ArgumentOutOfRange_Enum - Enum value was out of legal range.", nameof(mode));
        }
        //For iflater having a buffer with the default size is enough for reading the input stream (compressed data)
        // For compressing this will vary depending on the Level of compression needed.
        // Reading more data at a time is more efficient
        _buffer = new byte[DefaultBufferSize];
    }

    /// <summary>
    /// Internal constructor to specify the compressionlevel as well as the windowbits
    /// </summary>
    internal DeflateStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen, int windowBits)
    {
        _buffer = new byte[DefaultBufferSize]; //Instead of using array pool in Read** When tests working check if it's possible a change back
        ArgumentNullException.ThrowIfNull(stream);

        InitializeDeflater(stream, leaveOpen, windowBits, compressionLevel);
    }

    /// <summary>
    /// Sets up this DeflateStream to be used for Zlib Deflation/Compression
    /// </summary>
    [MemberNotNull(nameof(_stream))]
    internal void InitializeDeflater(Stream stream, bool leaveOpen, int windowBits, CompressionLevel compressionLevel)
    {
        Debug.Assert(stream != null);
        if (!stream.CanWrite)
            throw new ArgumentException("NotSupported_UnwritableStream - Stream does not support writing.", nameof(stream));

        _deflater = new Deflater(compressionLevel, windowBits);
        _stream = stream;
        _mode = CompressionMode.Compress;
        _leaveOpen = leaveOpen;
        InitializeBuffer();
    }

    [MemberNotNull(nameof(_buffer))]
    private void InitializeBuffer()
    {
        Debug.Assert(_buffer == null);
        _buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
    }

    public Stream BaseStream => _stream;

    public override bool CanRead
    {
        get
        {
            if (_stream == null)
            {
                return false;
            }

            return (_mode == CompressionMode.Decompress && _stream.CanRead);
        }
    }

    public override bool CanWrite
    {
        get
        {
            if (_stream == null)
            {
                return false;
            }

            return (_mode == CompressionMode.Compress && _stream.CanWrite);
        }
    }

    public override bool CanSeek => false;

    public override long Length { get => throw new NotSupportedException(); }

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush()
    {
        EnsureNotDisposed();
        if (_mode == CompressionMode.Compress)
            FlushBuffers();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int ReadByte()
    {
        EnsureDecompressionMode();
        EnsureNotDisposed();
        // Sanity check
        // Try to read a single byte from zlib without allocating an array, pinning an array, etc.
        // If zlib doesn't have any data, fall back to the base stream implementation, which will do that.
        Debug.Assert(_inflater != null);
        byte b = default;
        return Read(new Span<byte>(ref b)) == 1 ? b : -1;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Debug.Assert(_inflater != null);

        //Input class referring to the stream passed through the constructor
        return Read(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (GetType() != typeof(DeflateStream))
        {
            // DeflateStream is not sealed, and a derived type may have overridden Read(byte[], int, int) prior
            // to this Read(Span<byte>) overload being introduced.  In that case, this Read(Span<byte>) overload
            // should use the behavior of Read(byte[],int,int) overload.
            return base.Read(buffer);
        }
        else
        {
            //Read Core
            EnsureDecompressionMode();
            EnsureNotDisposed();
            Debug.Assert(_inflater != null);
            Debug.Assert(_buffer != null);
            int bytesRead;

            while (true)
            {
                // Try to decompress any data from the inflater's underlying input stream
                // into the caller's buffer.
                bytesRead = _inflater.Inflate(buffer);

                // If BytesRead (input available) < buffer.Length,
                // the slice will be smaller thant the original.
                buffer = buffer.Slice(bytesRead); 

                if (bytesRead != 0 && InflatorIsFinished)
                {
                    break;
                }

                // We were unable to decompress any data.
                // If the inflater needs additional input
                // data to proceed, read some to populate it.
                if (_inflater.NeedsInput())
                {
                    int n = _stream.Read(_buffer, 0, _buffer.Length);
                    if (n <= 0)
                    {
                        // - Inflater didn't return any data although a non-empty output buffer was passed by the caller.
                        // - More input is needed but there is no more input available.
                        // - Inflation is not finished yet.
                        // - Provided input wasn't completely empty
                        // In such case, we are dealing with a truncated input stream.
                        if (s_useStrictValidation && !buffer.IsEmpty && !_inflater.Finished() && _inflater.NonEmptyInput())
                        {
                            ThrowTruncatedInvalidData();
                        }
                        break;
                    }
                    else if (n > _buffer.Length)
                    {
                        ThrowGenericInvalidData();
                    }
                    else
                    {
                        //Filling the input buffer
                        _inflater.SetInput(_buffer, 0, n);
                    }
                }

                if (buffer.IsEmpty)
                {
                    // The caller provided a zero-byte buffer.  This is typically done in order to avoid allocating/renting
                    // a buffer until data is known to be available.  We don't have perfect knowledge here, as _inflater.Inflate
                    // will return 0 whether or not more data is required, and having input data doesn't necessarily mean it'll
                    // decompress into at least one byte of output, but it's a reasonable approximation for the 99% case.  If it's
                    // wrong, it just means that a caller using zero-byte reads as a way to delay getting a buffer to use for a
                    // subsequent call may end up getting one earlier than otherwise preferred.
                    Debug.Assert(bytesRead == 0);
                    break;
                }

            }

            return bytesRead; 
        }
    }

    private bool InflatorIsFinished =>
        // If the stream is finished then we have a few potential cases here:
        // 1. DeflateStream => return
        // 2. GZipStream that is finished but may have an additional GZipStream appended => feed more input
        // 3. GZipStream that is finished and appended with garbage => return
        _inflater!.Finished() &&
        (!_inflater.IsGzipStream() || !_inflater.NeedsInput());

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_stream is null, this);
    }

    private void EnsureDecompressionMode()
    {
        if (_mode != CompressionMode.Decompress)
            ThrowCannotReadFromDeflateStreamException();

        static void ThrowCannotReadFromDeflateStreamException() =>
            throw new InvalidOperationException("CannotReadFromDeflateStream - Reading from the compression stream is not supported.");
    }

    private void EnsureCompressionMode()
    {
        if (_mode != CompressionMode.Compress)
            ThrowCannotWriteToDeflateStreamException();

        static void ThrowCannotWriteToDeflateStreamException() =>
            throw new InvalidOperationException("CannotWriteToDeflateStream - Writing to the compression stream is not supported.");
    }

    private static void ThrowGenericInvalidData() =>
        // The stream is either malicious or poorly implemented and returned a number of
        // bytes < 0 || > than the buffer supplied to it.
        throw new InvalidDataException("GenericInvalidData - Found invalid data while decoding.");

    private static void ThrowTruncatedInvalidData() =>
        throw new InvalidDataException("TruncatedData - Found truncated data while decoding.");

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            throw new NotImplementedException();

    public override int EndRead(IAsyncResult asyncResult)
    {
        throw new NotImplementedException();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        WriteCore(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override void WriteByte(byte value)
    {
        if (GetType() != typeof(DeflateStream))
        {
            // DeflateStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
            // to this WriteByte override being introduced.  In that case, this WriteByte override
            // should use the behavior of the Write(byte[],int,int) overload.
            base.WriteByte(value);
        }
        else
        {
            WriteCore(new ReadOnlySpan<byte>(in value));
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (GetType() != typeof(DeflateStream))
        {
            // DeflateStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
            // to this Write(ReadOnlySpan<byte>) overload being introduced.  In that case, this Write(ReadOnlySpan<byte>) overload
            // should use the behavior of Write(byte[],int,int) overload.
            base.Write(buffer);
        }
        else
        {
            WriteCore(buffer);
        }
    }
    // This is also used by GZipStream and ZLibStream
    internal void WriteCore(ReadOnlySpan<byte> buffer)
    {
        EnsureCompressionMode();
        EnsureNotDisposed();

        Debug.Assert(_deflater != null);
        // Write compressed the bytes we already passed to the deflater:
        WriteDeflaterOutput();

        // Pass new bytes through deflater and write them too:
        _deflater.SetInput(buffer.ToArray());
        WriteDeflaterOutput();
        _wroteBytes = true;
    }

    private void WriteDeflaterOutput()
    {
        Debug.Assert(_deflater != null && _buffer != null);
        while (!_deflater.NeedsInput())
        {
            int compressedBytes = _deflater.GetDeflateOutput(_buffer);
            if (compressedBytes > 0)
            {
                _stream.Write(_buffer, 0, compressedBytes);
            }
        }
    }

    // This is called by Flush:
    private void FlushBuffers()
    {
        if (_wroteBytes)
        {
            // Compress any bytes left:
            WriteDeflaterOutput();

            Debug.Assert(_deflater != null && _buffer != null);
            // Pull out any bytes left inside deflater:
            bool flushSuccessful;
            do
            {
                int compressedBytes = _deflater.Flush(_buffer, out flushSuccessful);
                if (flushSuccessful)
                {
                    _stream.Write(_buffer, 0, compressedBytes);
                }
                Debug.Assert(flushSuccessful == (compressedBytes > 0));
            } while (flushSuccessful);
        }

        // Always flush on the underlying stream
        _stream.Flush();
    }

    // This is called by Dispose:
    private void PurgeBuffers(bool disposing)
    {
        if (!disposing)
            return;

        if (_stream == null)
            return;

        if (_mode != CompressionMode.Compress)
            return;

        Debug.Assert(_deflater != null && _buffer != null);
        // Some deflaters (e.g. ZLib) write more than zero bytes for zero byte inputs.
        // This round-trips and we should be ok with this, but our legacy managed deflater
        // always wrote zero output for zero input and upstack code (e.g. ZipArchiveEntry)
        // took dependencies on it. Thus, make sure to only "flush" when we actually had
        // some input:
        if (_wroteBytes)
        {
            // Compress any bytes left
            WriteDeflaterOutput();

            // Pull out any bytes left inside deflater:
            bool finished;
            do
            {
                int compressedBytes = _deflater.Finish(_buffer, out finished);
                if (compressedBytes > 0)
                    _stream.Write(_buffer, 0, compressedBytes);
            } while (!finished);
        }
        else
        {
            // In case of zero length buffer, we still need to clean up the native created stream before
            // the object get disposed because eventually ManagedZLib.ReleaseHandle will get called during
            // the dispose operation and although it frees the stream but it return error code because the
            // stream state was still marked as in use. The symptoms of this problem will not be seen except
            // if running any diagnostic tools which check for disposing safe handle objects
            bool finished;
            do
            {
                _deflater.Finish(_buffer, out finished);
            } while (!finished);
        }
    }

    protected override void Dispose(bool disposing) //Vivi> This maybe be the only dispose we need, after handling streams
                                                    //Not one for de/inflater
    {
        try
        {
            PurgeBuffers(disposing);
        }
        finally
        {
            // Close the underlying stream even if PurgeBuffers threw.
            // Stream.Close() may throw here (may or may not be due to the same error).
            // In this case, we still need to clean up internal resources, hence the inner finally blocks.
            try
            {
                if (disposing && !_leaveOpen)
                    _stream?.Dispose();
            }
            finally
            {
                _stream = null!;

                byte[]? buffer = _buffer;
                if (buffer != null)
                {
                    _buffer = null;
                    if (!AsyncOperationIsActive)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                base.Dispose(disposing);
            }
        }
    }

    public override ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            throw new NotImplementedException();

    public override void EndWrite(IAsyncResult asyncResult)
    {
        throw new NotImplementedException();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        EnsureNotDisposed();
        if (!CanRead) throw new NotSupportedException();

        new CopyToStream(this, destination, bufferSize).CopyFromSourceToDestination();
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private sealed class CopyToStream : Stream
    {
        private readonly DeflateStream _deflateStream;
        private readonly Stream _destination;
        private readonly CancellationToken _cancellationToken;
        private byte[] _arrayPoolBuffer;

        public CopyToStream(DeflateStream deflateStream, Stream destination, int bufferSize) :
            this(deflateStream, destination, bufferSize, CancellationToken.None)
        {
        }

        public CopyToStream(DeflateStream deflateStream, Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Debug.Assert(deflateStream != null);
            Debug.Assert(destination != null);
            Debug.Assert(bufferSize > 0);

            _deflateStream = deflateStream;
            _destination = destination;
            _cancellationToken = cancellationToken;
            _arrayPoolBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        }

        public void CopyFromSourceToDestination()
        {
            try
            {
                Debug.Assert(_deflateStream._inflater != null);
                // Flush any existing data in the inflater to the destination stream.
                while (!_deflateStream._inflater.Finished())
                {
                    int bytesRead = _deflateStream._inflater.Inflate(_arrayPoolBuffer);
                    if (bytesRead > 0)
                    {
                        _destination.Write(_arrayPoolBuffer, 0, bytesRead);
                    }
                    else if (_deflateStream._inflater.NeedsInput())
                    {
                        // only break if we read 0 and ran out of input,
                        // if input is still available it may be another GZip payload
                        break;
                    }
                }

                // Now, use the source stream's CopyTo to push directly to our inflater via this helper stream
                _deflateStream._stream.CopyTo(this, _arrayPoolBuffer.Length);
                if (s_useStrictValidation && !_deflateStream._inflater.Finished())
                {
                    ThrowTruncatedInvalidData();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(_arrayPoolBuffer);
                _arrayPoolBuffer = null!;
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Debug.Assert(buffer != _arrayPoolBuffer);
            _deflateStream.EnsureNotDisposed();

            if (count <= 0)
            {
                return;
            }
            else if (count > buffer.Length - offset)
            {
                // The buffer stream is either malicious or poorly implemented and returned a number of
                // bytes larger than the buffer supplied to it.
                throw new InvalidDataException("GenericInvalidData - Found invalid data while decoding.");
            }

            Debug.Assert(_deflateStream._inflater != null);
            // Feed the data from base stream into the decompression engine.
            _deflateStream._inflater.SetInput(buffer, offset, count);

            // While there's more decompressed data available, forward it to the buffer stream.
            while (!_deflateStream._inflater.Finished())
            {
                int bytesRead = _deflateStream._inflater.Inflate(new Span<byte>(_arrayPoolBuffer));
                if (bytesRead > 0)
                {
                    _destination.Write(_arrayPoolBuffer, 0, bytesRead);
                }
                else if (_deflateStream._inflater.NeedsInput())
                {
                    // only break if we read 0 and ran out of input, if input is still available it may be another GZip payload
                    break;
                }
            }
        }

        public override bool CanWrite => true;
        public override void Flush() { }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }

    private bool AsyncOperationIsActive => _activeAsyncOperation != false;

    private void EnsureNoActiveAsyncOperation()
    {
        if (AsyncOperationIsActive)
            ThrowInvalidBeginCall();
    }

    private void AsyncOperationStarting()
    {
        if (_activeAsyncOperation != false)
        {
            ThrowInvalidBeginCall();
        }
    }

    private void AsyncOperationCompleting() =>_activeAsyncOperation = false;

    private static void ThrowInvalidBeginCall() =>
        throw new InvalidOperationException("InvalidBeginCall - Only one asynchronous reader or writer is allowed time at one time.");

    private static readonly bool s_useStrictValidation =
        AppContext.TryGetSwitch("System.IO.Compression.UseStrictValidation", out bool strictValidation) ? strictValidation : false;
}
