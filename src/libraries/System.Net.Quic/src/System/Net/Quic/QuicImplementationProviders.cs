// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Quic.Implementations.Managed.Internal.Tls;

namespace System.Net.Quic
{
    public static class QuicImplementationProviders
    {
        public static Implementations.QuicImplementationProvider Mock { get; } = new Implementations.Mock.MockImplementationProvider();
        public static Implementations.QuicImplementationProvider MsQuic { get; } = new Implementations.MsQuic.MsQuicImplementationProvider();
        public static Implementations.QuicImplementationProvider Managed { get; } = new Implementations.Managed.ManagedQuicImplementationProvider(QuicTlsProvider.Openssl);
        public static Implementations.QuicImplementationProvider ManagedMockTls { get; } = new Implementations.Managed.ManagedQuicImplementationProvider(QuicTlsProvider.Mock);
        public static Implementations.QuicImplementationProvider Default => GetDefaultProvider();

        private static Implementations.QuicImplementationProvider GetDefaultProvider()
        {
            string? providerStr = Environment.GetEnvironmentVariable("DOTNETQUIC_PROVIDER")?.ToLower();

            switch (providerStr)
            {
                case "msquic":
                    return MsQuic;
                case "managedmocktls":
                    return ManagedMockTls;
                case "managed":
                    if (!Managed.IsSupported)
                    {
                        throw new NotSupportedException(
                            "Managed QUIC via OpenSSL is not available. Make sure the appropriate OpenSSL version is in PATH");
                    }
                    return Managed;
            }

            return Managed;
        }
    }
}
