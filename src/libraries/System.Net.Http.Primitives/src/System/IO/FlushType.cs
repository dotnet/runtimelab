// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO
{
    /// <summary>
    /// Flush methods for use with <see cref="IEnhancedStream.FlushAsync(FlushType, Threading.CancellationToken)"/>.
    /// </summary>
    public enum FlushType
    {
        /// <summary>
        /// Do not flush.
        /// </summary>
        None,

        /// <summary>
        /// Flush write buffers.
        /// </summary>
        FlushWrites,

        /// <summary>
        /// Flush write buffers and shutdown writes.
        /// </summary>
        FlushAndShutdownWrites
    }
}
