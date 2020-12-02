// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Quic.Implementations.Managed.Internal.Tls.OpenSsl;

namespace System.Net.Quic.Implementations.Managed.Internal.Tls
{
    internal abstract class QuicTlsProvider
    {
        internal static readonly QuicTlsProvider Openssl = OpenSslQuicTlsProvider.Instance;
        internal static readonly QuicTlsProvider Mock = MockQuicTlsProvider.Instance;

        internal static readonly QuicTlsProvider Default = Openssl.IsSupported ? Openssl : Mock;

        internal abstract bool IsSupported { get; }

        internal abstract ITls CreateClient(ManagedQuicConnection connection, QuicClientConnectionOptions options,
            TransportParameters localTransportParams);

        internal abstract ITls CreateServer(ManagedQuicConnection connection, QuicListenerOptions options,
            TransportParameters localTransportParams);
    }
}
