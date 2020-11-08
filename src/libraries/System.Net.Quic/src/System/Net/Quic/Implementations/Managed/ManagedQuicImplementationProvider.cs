// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Quic.Implementations.Managed.Internal.Tls;
using System.Net.Quic.Implementations.Managed.Internal.Tls.OpenSsl;

namespace System.Net.Quic.Implementations.Managed
{
    internal class ManagedQuicImplementationProvider : QuicImplementationProvider
    {
        public override bool IsSupported => OpenSslQuicTlsProvider.IsSupported;

        internal override QuicListenerProvider CreateListener(QuicListenerOptions options) =>
            new ManagedQuicListener(OpenSslQuicTlsProvider.Instance, options);

        internal override QuicConnectionProvider CreateConnection(QuicClientConnectionOptions options) =>
            new ManagedQuicConnection(OpenSslQuicTlsProvider.Instance, options);
    }

    internal class ManagedMockTlsQuicImplementationProvider : QuicImplementationProvider
    {
        public override bool IsSupported => true; // mocked TLS is always supported

        internal override QuicListenerProvider CreateListener(QuicListenerOptions options) =>
            new ManagedQuicListener(MockQuicTlsProvider.Instance, options);

        internal override QuicConnectionProvider CreateConnection(QuicClientConnectionOptions options) =>
            new ManagedQuicConnection(MockQuicTlsProvider.Instance, options);
    }
}
