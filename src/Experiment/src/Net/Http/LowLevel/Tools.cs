// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel
{
    internal static class Tools
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int UnsafeByteOffset(ReadOnlySpan<byte> start, ReadOnlySpan<byte> end) =>
            (int)(void*)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(start), ref MemoryMarshal.GetReference(end));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int UnsafeByteOffset(ReadOnlySpan<byte> start, ref byte end) =>
            (int)(void*)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(start), ref end);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int UnsafeByteOffset(ref byte start, ReadOnlySpan<byte> end) =>
            (int)(void*)Unsafe.ByteOffset(ref start, ref MemoryMarshal.GetReference(end));

        public static bool TimeoutExpired(long curTicks, long fromTicks, TimeSpan timeoutLimit)
        {
            return timeoutLimit != Timeout.InfiniteTimeSpan && new TimeSpan((curTicks - fromTicks) * TimeSpan.TicksPerMillisecond) > timeoutLimit;
        }

        public static string EscapeIdnHost(string hostName) =>
            new UriBuilder() { Scheme = Uri.UriSchemeHttp, Host = hostName, Port = 80 }.Uri.IdnHost;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BlockForResult(in ValueTask task)
        {
            if (task.IsCompleted)
            {
                task.GetAwaiter().GetResult();
            }
            else
            {
                task.AsTask().GetAwaiter().GetResult();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T BlockForResult<T>(in ValueTask<T> task)
        {
            return task.IsCompleted ? task.GetAwaiter().GetResult() : task.AsTask().GetAwaiter().GetResult();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T BlockForResult<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }
        
        internal static void BlockForResult(Task task)
        {
            throw new NotImplementedException();
        }
    }
}
