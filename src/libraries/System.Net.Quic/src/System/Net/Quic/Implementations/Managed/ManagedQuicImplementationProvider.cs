// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Quic.Implementations.Managed.Internal.Tls;
using System.Net.Quic.Implementations.Managed.Internal.Tls.OpenSsl;

namespace System.Net.Quic.Implementations.Managed
{
    internal class ManagedQuicImplementationProvider : QuicImplementationProvider
    {
        private readonly QuicTlsProvider _tlsProvider;

        public ManagedQuicImplementationProvider(QuicTlsProvider tlsProvider) => _tlsProvider = tlsProvider;

        public override bool IsSupported => _tlsProvider.IsSupported;

        internal override QuicListenerProvider CreateListener(QuicListenerOptions options) =>
            new ManagedQuicListener(_tlsProvider, options);

        internal override QuicConnectionProvider CreateConnection(QuicClientConnectionOptions options) =>
            new ManagedQuicConnection(_tlsProvider, options);
    }
}
