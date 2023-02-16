namespace System.IO.StreamSourceGeneration
{
    [Flags]
    internal enum StreamOperationKind
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadAsync = 4,
        WriteAsync = 8,
        Seek = 16,
    }
}