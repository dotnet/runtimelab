// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO.StreamSourceGeneration
{
    [Flags]
    internal enum StreamMember
    {
        ReadBytes,
        ReadSpan,
        ReadAsyncBytes,
        ReadAsyncMemory,
        WriteBytes,
        WriteSpan,
        WriteAsyncBytes,
        WriteAsyncMemory,
        BeginRead,
        BeginWrite,
        ReadByte,
        WriteByte,
        CanRead,
        CanSeek,
        CanWrite,
        EndRead,
        EndWrite,
        Length,
        Position,
        Seek,
        SetLength,
    }

    internal static class StreamMemberExtensions
    {
        internal static bool IsRead(this StreamMember member)
        {
            return member >= StreamMember.ReadBytes && member <= StreamMember.ReadAsyncMemory;
        }

        internal static bool IsWrite(this StreamMember member)
        {
            return member >= StreamMember.WriteBytes && member <= StreamMember.WriteAsyncMemory;
        }

        internal static bool IsAsync(this StreamMember member)
        {
            return member
                is StreamMember.ReadAsyncBytes
                or StreamMember.ReadAsyncMemory
                or StreamMember.WriteAsyncBytes
                or StreamMember.WriteAsyncMemory;
        }
    }
}
