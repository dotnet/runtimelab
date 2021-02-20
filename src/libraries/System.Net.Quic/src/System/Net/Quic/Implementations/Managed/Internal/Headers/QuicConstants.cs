// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;

namespace System.Net.Quic.Implementations.Managed.Internal.Headers
{
    internal static class QuicConstants
    {
        /// <summary>
        ///     Minimum size of the Initial packets sent by the client.
        /// </summary>
        internal const int MinimumClientInitialDatagramSize = 1200;

        /// <summary>
        ///     Cipher suite used for the Initial encryption level.
        /// </summary>
        internal const TlsCipherSuite InitialCipherSuite = TlsCipherSuite.TLS_AES_128_GCM_SHA256;

        /// <summary>
        ///     Size of the smallest valid QUIC packet. 1B header, 20B payload + integrity tag combined
        /// </summary>
        internal const int MinimumPacketSize = 21;

        internal static class Internal
        {
            /// <summary>
            ///     Size of the buffers used to store parts of the stream
            /// </summary>
            internal const int StreamChunkSize = 16 * 1024;

            /// <summary>
            ///     Size of the buffers used to store parts of the stream
            /// </summary>
            internal const int StreamChunkPoolSize = 16;

            /// <summary>
            ///     Maximum UDP datagram size this implementation is willing to receive
            /// </summary>
            internal const int MaximumAllowedDatagramSize = 1452 /*1252*/;
        }
    }
}
