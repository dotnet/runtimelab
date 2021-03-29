// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// The current status of an <see cref="HttpConnection"/>.
    /// </summary>
    public enum HttpConnectionStatus
    {
        /// <summary>
        /// The <see cref="HttpConnection"/> is open and accepting requests.
        /// </summary>
        Open,
        /// <summary>
        /// The <see cref="HttpConnection"/> is open, but might reject requests.
        /// <see cref="HttpConnection.CreateNewRequestAsync(HttpPrimitiveVersion, System.Net.Http.HttpVersionPolicy, System.Threading.CancellationToken)"/> may return <c>null</c>.
        /// </summary>
        Closing,
        /// <summary>
        /// The <see cref="HttpConnection"/> has been closed and is no longer accepting requests.
        /// <see cref="HttpConnection.CreateNewRequestAsync(HttpPrimitiveVersion, System.Net.Http.HttpVersionPolicy, System.Threading.CancellationToken)"/> will return <c>null</c>.
        /// </summary>
        Closed
    }
}
