﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Net.Security;
using System.Threading.Tasks;
using System.Net.Quic.Implementations;

namespace System.Net.Quic.Tests
{
    public abstract class QuicTestBase<T>
        where T : IQuicImplProviderFactory, new()
    {
        private static readonly IQuicImplProviderFactory s_factory = new T();

        public static QuicImplementationProvider ImplementationProvider { get; } = s_factory.GetProvider();
        public static bool IsSupported => ImplementationProvider.IsSupported;

        public static SslApplicationProtocol ApplicationProtocol { get; } = new SslApplicationProtocol("quictest");

        public SslServerAuthenticationOptions GetSslServerAuthenticationOptions()
        {
            return new SslServerAuthenticationOptions()
            {
                ApplicationProtocols = new List<SslApplicationProtocol>() { ApplicationProtocol }
            };
        }

        public SslClientAuthenticationOptions GetSslClientAuthenticationOptions()
        {
            return new SslClientAuthenticationOptions()
            {
                ApplicationProtocols = new List<SslApplicationProtocol>() { ApplicationProtocol }
            };
        }

        internal QuicConnection CreateQuicConnection(IPEndPoint endpoint)
        {
            return new QuicConnection(ImplementationProvider, endpoint, GetSslClientAuthenticationOptions());
        }

        internal QuicListener CreateQuicListener()
        {
            return CreateQuicListener(new IPEndPoint(IPAddress.Loopback, 0));
        }

        internal QuicListener CreateQuicListener(IPEndPoint endpoint)
        {
            QuicListener listener = new QuicListener(ImplementationProvider, new QuicListenerOptions()
            {
                ListenEndPoint = endpoint,
                ServerAuthenticationOptions = GetSslServerAuthenticationOptions(),
                CertificateFilePath = "Certs/cert.crt",
                PrivateKeyFilePath = "Certs/cert.key"
            });
            listener.Start();
            return listener;
        }

        internal async Task RunClientServer(Func<QuicConnection, Task> clientFunction, Func<QuicConnection, Task> serverFunction, int millisecondsTimeout = 10_000)
        {
            using QuicListener listener = CreateQuicListener();

            await new[]
            {
                Task.Run(async () =>
                {
                    using QuicConnection serverConnection = await listener.AcceptConnectionAsync();
                    await serverFunction(serverConnection);
                }),
                Task.Run(async () =>
                {
                    using QuicConnection clientConnection = CreateQuicConnection(listener.ListenEndPoint);
                    await clientConnection.ConnectAsync();
                    await clientFunction(clientConnection);
                })
            }.WhenAllOrAnyFailed(millisecondsTimeout);
        }
    }

    public interface IQuicImplProviderFactory
    {
        QuicImplementationProvider GetProvider();
    }

    public sealed class MsQuicProviderFactory : IQuicImplProviderFactory
    {
        public QuicImplementationProvider GetProvider() => QuicImplementationProviders.MsQuic;
    }

    public sealed class MockProviderFactory : IQuicImplProviderFactory
    {
        public QuicImplementationProvider GetProvider() => QuicImplementationProviders.Mock;
    }

    public sealed class ManagedProviderFactory : IQuicImplProviderFactory
    {
        public QuicImplementationProvider GetProvider() => QuicImplementationProviders.Managed;
    }

    public sealed class ManagedMockTlsProviderFactory : IQuicImplProviderFactory
    {
        public QuicImplementationProvider GetProvider() => QuicImplementationProviders.ManagedMockTls;
    }
}
