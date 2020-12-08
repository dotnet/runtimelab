// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Quic.Implementations.Managed.Internal
{
    /// <summary>
    ///     QUIC connection state.
    /// </summary>
    internal enum QuicConnectionState
    {
        /// <summary>
        ///     The connection object has been created and not connected yet.
        /// </summary>
        None,

        /// <summary>
        ///     The connection handshake succeeded.
        /// </summary>
        Connected,

        /// <summary>
        ///     Closing sequence started, waiting for CONNECTION_CLOSE frame from the peer or for timeout
        /// </summary>
        Draining,

        /// <summary>
        ///     Closing sequence started, waiting for closing timeout.
        /// </summary>
        Closing,

        /// <summary>
        ///     A Non-RFC intermediate state before closing the connection. Used to signal that the connection resources
        ///     Should be reclaimed.
        /// </summary>
        BeforeClosed,

        /// <summary>
        ///     The connection has been closed and resources reclaimed.
        /// </summary>
        Closed,
    }
}
