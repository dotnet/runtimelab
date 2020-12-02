// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Quic.Implementations.Managed.Internal.Tls.OpenSsl
{
    internal sealed class  MockQuicTlsProvider : QuicTlsProvider
    {
        internal static readonly QuicTlsProvider Instance = new MockQuicTlsProvider();

        private MockQuicTlsProvider() {}

        internal override bool IsSupported => true;

        internal override ITls CreateClient(ManagedQuicConnection connection, QuicClientConnectionOptions options,
            TransportParameters localTransportParams) => new MockTls(connection, options, localTransportParams);


        internal override ITls CreateServer(ManagedQuicConnection connection, QuicListenerOptions options,
            TransportParameters localTransportParams) => new MockTls(connection, options, localTransportParams);
    }
}
