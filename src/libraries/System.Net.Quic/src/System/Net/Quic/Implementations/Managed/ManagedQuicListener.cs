// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Net.Quic.Implementations.Managed.Internal.Sockets;
using System.Net.Quic.Implementations.Managed.Internal.Tls;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace System.Net.Quic.Implementations.Managed
{
    internal class ManagedQuicListener : QuicListenerProvider
    {
        private bool _disposed;

        private readonly ChannelReader<QuicConnectionProvider> _acceptQueue;
        private readonly QuicServerSocketContext _socketContext;

        public ManagedQuicListener(QuicTlsProvider tlsProvider, QuicListenerOptions options)
        {
            if (options.ServerAuthenticationOptions?.ServerCertificate == null)
            {
                if (!File.Exists(options.CertificateFilePath))
                    throw new FileNotFoundException("Certificate file not found", options.CertificateFilePath);
                if (!File.Exists(options.PrivateKeyFilePath))
                    throw new FileNotFoundException("Private key file not found", options.PrivateKeyFilePath);
            }

            var listenEndPoint = options.ListenEndPoint ?? new IPEndPoint(IPAddress.Any, 0);

            var channel = Channel.CreateBounded<QuicConnectionProvider>(new BoundedChannelOptions(options.ListenBacklog)
            {
                SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropWrite
            });

            _acceptQueue = channel.Reader;
            _socketContext = new QuicServerSocketContext(tlsProvider, listenEndPoint, options, channel.Writer);
        }

        internal override IPEndPoint ListenEndPoint
        {
            get
            {
                ThrowIfDisposed();
                return _socketContext.LocalEndPoint;
            }
        }

        internal override ValueTask<QuicConnectionProvider> AcceptConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _acceptQueue.ReadAsync(cancellationToken);
        }

        internal override void Start()
        {
            ThrowIfDisposed();

            _socketContext.Start();
        }

        internal override void Close()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (_disposed) return;
            _socketContext.StopOrOrphan();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ManagedQuicListener));
            }
        }
    }
}
