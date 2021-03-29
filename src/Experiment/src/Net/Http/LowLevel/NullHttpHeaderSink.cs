// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel
{
    internal sealed class NullHttpHeaderSink : IHttpHeadersSink
    {
        public static readonly NullHttpHeaderSink Instance = new NullHttpHeaderSink();

        public void OnHeader(object? state, ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue)
        {
        }

        public void OnHeader(object? state, ReadOnlySpan<byte> headerName, ReadOnlySpan<byte> headerValue, HttpHeaderFlags flags)
        {
        }
    }
}
