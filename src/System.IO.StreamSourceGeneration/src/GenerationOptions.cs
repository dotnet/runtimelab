using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace System.IO.StreamSourceGeneration;

internal readonly struct GenerationOptions
{
    public readonly INamedTypeSymbol ClassSymbol;
    public readonly bool CanRead;
    public readonly bool CanWrite;
    public readonly bool CanSeek;

    public GenerationOptions(bool canRead, bool canWrite, bool canSeek, INamedTypeSymbol classSymbol)
    {
        CanRead = canRead;
        CanWrite = canWrite;
        CanSeek = canSeek;
        ClassSymbol = classSymbol;
    }

    internal bool IsOptedIn(StreamOperation operation)
    {
        if ((operation & (StreamOperation.Read | StreamOperation.ReadAsync)) != 0)
        {
            return CanRead;
        }

        if ((operation & (StreamOperation.Write | StreamOperation.WriteAsync)) != 0)
        {
            return CanWrite;
        }

        if ((operation & StreamOperation.Seek) != 0)
        {
            return CanSeek;
        }

        if ((operation & StreamOperation.SetLength) != 0)
        {
            return CanSeek && CanWrite;
        }

        Debug.Assert(operation == StreamOperation.None);
        return true;
    }
}
