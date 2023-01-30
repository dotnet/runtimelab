using System.IO.StreamSourceGeneration;

namespace ClassLibrary1;

[GenerateStreamBoilerplate(/*CanRead = false, CanWrite = false, CanSeek = false*/)]
public partial class MyStream : Stream
{
    public override long Length => throw new NotImplementedException();

    public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    private partial int ReadCore(Span<byte> buffer)
    {
        // Do Read 
        throw new NotImplementedException();
    }

    private partial ValueTask<int> ReadCoreAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        // Do ReadAsync
        throw new NotImplementedException();
    }

    private partial long SeekCore(long offset, SeekOrigin origin)
    {
        // Do Seek
        throw new NotImplementedException();
    }

    private partial void SetLengthCore(long value)
    {
        // Do SetLength
        throw new NotImplementedException();
    }

    private partial void WriteCore(ReadOnlySpan<byte> buffer)
    {
        // Do Write
        throw new NotImplementedException();
    }

    private partial ValueTask WriteCoreAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        // Do WriteAsync
        throw new NotImplementedException();
    }
}