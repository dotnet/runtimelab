using System.Collections.Generic;
using Constants = System.IO.StreamSourceGeneration.StreamBoilerplateConstants;

namespace System.IO.StreamSourceGeneration;

internal readonly struct BoilerplateCandidateInfo
{
    internal BoilerplateCandidateInfo(string name, StreamOperation operation, string boilerplate, string boilerplateForUnsupported)
    {
        Name = name;
        Operation = operation;
        Boilerplate = boilerplate;
        BoilerplateForUnsupported = boilerplateForUnsupported;
    }

    internal readonly string Name;
    internal readonly StreamOperation Operation;
    internal readonly string Boilerplate;
    internal readonly string BoilerplateForUnsupported;

    internal static readonly List<BoilerplateCandidateInfo> s_boilerplateGenerationCandidates = new List<BoilerplateCandidateInfo>
    {
            //new("System.IO.Stream.Length", StreamOperation.Seek, string.Empty),
            //new("System.IO.Stream.Position", StreamOperation.Seek, string.Empty),
            new("System.IO.Stream.CanRead", StreamOperation.None, Constants.CanRead, Constants.CanReadUnsupported),
            new("System.IO.Stream.CanSeek", StreamOperation.None, Constants.CanSeek, Constants.CanSeekUnsupported),
            new("System.IO.Stream.CanWrite", StreamOperation.None, Constants.CanWrite, Constants.CanWriteUnsupported),
            new("System.IO.Stream.BeginRead(byte[], int, int, System.AsyncCallback?, object?)", 
                StreamOperation.ReadAsync, 
                Constants.BeginRead, 
                Constants.BeginReadUnsupported),
            new("System.IO.Stream.BeginWrite(byte[], int, int, System.AsyncCallback?, object?)", 
                StreamOperation.WriteAsync, 
                Constants.BeginWrite, 
                Constants.BeginWriteUnsupported),
            new("System.IO.Stream.EndRead(System.IAsyncResult)", 
                StreamOperation.None, 
                Constants.EndRead, 
                Constants.EndReadUnsupported),
            new("System.IO.Stream.EndWrite(System.IAsyncResult)", 
                StreamOperation.None, 
                Constants.EndWrite, 
                Constants.EndWriteUnsupported),
            new("System.IO.Stream.Read(byte[], int, int)", 
                StreamOperation.Read, 
                Constants.ReadByteArray, 
                Constants.ReadByteArrayUnsupported),
            new("System.IO.Stream.Read(System.Span<byte>)", 
                StreamOperation.Read, 
                Constants.ReadSpan, 
                Constants.ReadSpanUnsupported),
            new("System.IO.Stream.ReadAsync(byte[], int, int, System.Threading.CancellationToken)", 
                StreamOperation.ReadAsync, 
                Constants.ReadAsyncByteArray, 
                Constants.ReadAsyncByteArrayUnsupported),
            new("System.IO.Stream.ReadAsync(System.Memory<byte>, System.Threading.CancellationToken)", 
                StreamOperation.ReadAsync, 
                Constants.ReadAsyncMemory, 
                Constants.ReadAsyncMemoryUnsupported),
            new("System.IO.Stream.Seek(long, System.IO.SeekOrigin)", 
                StreamOperation.Seek, 
                Constants.Seek, 
                Constants.SeekUnsupported),
            new("System.IO.Stream.SetLength(long)", 
                StreamOperation.SetLength, 
                Constants.SetLength, 
                Constants.SetLengthUnsupported),
            new("System.IO.Stream.Write(byte[], int, int)", 
                StreamOperation.Write, 
                Constants.WriteByteArray,
                Constants.WriteByteArrayUnsupported),
            new("System.IO.Stream.Write(System.ReadOnlySpan<byte>)", 
                StreamOperation.Write, 
                Constants.WriteSpan, 
                Constants.WriteSpanUnsupported),
            new("System.IO.Stream.WriteAsync(byte[], int, int, System.Threading.CancellationToken)", 
                StreamOperation.WriteAsync, 
                Constants.WriteAsyncByteArray, 
                Constants.WriteAsyncByteArrayUnsupported),
            new("System.IO.Stream.WriteAsync(System.ReadOnlyMemory<byte>, System.Threading.CancellationToken)", 
                StreamOperation.WriteAsync, 
                Constants.WriteAsyncMemory, 
                Constants.WriteAsyncMemoryUnsupported)
    };
}

[Flags]
internal enum StreamOperation
{
    None = 0,
    Read = 1,
    Write = 2,
    Seek = 4,
    ReadAsync = 8,
    WriteAsync = 16,
    SetLength = 32,
}