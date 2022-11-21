// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http
{
    internal interface IHttpStreamHeadersHandler
    {
        void OnStaticIndexedHeader(int index);
        void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value);
        void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value);
        void OnHeadersComplete(bool endStream);
<<<<<<< HEAD:src/libraries/Common/src/System/Net/Http/aspnetcore/IHttpHeadersHandler.cs

        // DIM to avoid breaking change on public interface (for Kestrel).
        public void OnDynamicIndexedHeader(int? index, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            OnHeader(name, value);
        }
=======
        void OnDynamicIndexedHeader(int? index, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value);
>>>>>>> 562aea2cb7d449d6e2e697df8cac56b599ec564d:src/libraries/Common/src/System/Net/Http/aspnetcore/IHttpStreamHeadersHandler.cs
    }
}
