namespace System.IO.StreamSourceGeneration;

internal class StreamMembersConstants
{

    internal const string ReadByte = "System.IO.Stream.Read(byte[],int,int)";
    internal const string ReadSpan = "System.IO.Stream.Read(System.Span<byte>)";
    internal const string ReadAsyncByte = "System.IO.Stream.ReadAsync(byte[],int,int,System.Threading.CancellationToken)";
    internal const string ReadAsyncMemory = "System.IO.Stream.ReadAsync(System.Memory<byte>,System.Threading.CancellationToken)";

    internal const string WriteByte = "System.IO.Stream.Write(byte[],int,int)";
    internal const string WriteSpan = "System.IO.Stream.Write(System.ReadOnlySpan<byte>)";
    internal const string WriteAsyncByte = "System.IO.Stream.WriteAsync(byte[],int,int,System.Threading.CancellationToken)";
    internal const string WriteAsyncMemory = "System.IO.Stream.WriteAsync(System.ReadOnlyMemory<byte>,System.Threading.CancellationToken)";

    internal const string CanRead = "System.IO.Stream.CanRead";
    internal const string CanSeek = "System.IO.Stream.CanSeek";
    internal const string CanWrite = "System.IO.Stream.CanWrite";
    internal const string BeginRead = "System.IO.Stream.BeginRead(byte[], int, int, System.AsyncCallback?, object?)";
    internal const string BeginWrite = "System.IO.Stream.BeginWrite(byte[], int, int, System.AsyncCallback?, object?)";
    internal const string EndRead = "System.IO.Stream.EndRead(System.IAsyncResult)";
    internal const string EndWrite = "System.IO.Stream.EndWrite(System.IAsyncResult)";
    internal const string Seek = "System.IO.Stream.Seek(long,System.IO.SeekOrigin)";
    internal const string SetLength = "System.IO.Stream.SetLength(long)";
    internal const string Length = "System.IO.Stream.Length";
    internal const string Position = "System.IO.Stream.Position";
}
