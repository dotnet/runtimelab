// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{

    /// <summary>
    /// An enhanced version of <see cref="Stream"/>.
    /// </summary>
    public interface IEnhancedStream
    {
        /// <summary>
        /// If true, the <see cref="FlushAsync(FlushType, CancellationToken)"/> method supports being called with <see cref="FlushType.FlushAndShutdownWrites"/>.
        /// </summary>
        bool CanShutdownWrites { get; }

        /// <summary>
        /// If true, the <see cref="ReadAsync(IReadOnlyList{Memory{byte}}, CancellationToken)"/> and <see cref="WriteAsync(IReadOnlyList{ReadOnlyMemory{byte}}, FlushType, CancellationToken)"/> methods will perform optimal scattered reads and gathered writes.
        /// </summary>
        bool CanScatterGather { get; }

        /// <summary>
        /// Reads a list of buffers as a single I/O.
        /// </summary>
        /// <param name="buffers">The buffers to read into.</param>
        /// <returns>The number of bytes read.</returns>
        int Read(IReadOnlyList<Memory<byte>> buffers);

        /// <summary>
        /// Reads a list of buffers as a single I/O.
        /// </summary>
        /// <param name="buffers">The buffers to read into.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>The number of bytes read.</returns>
        ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a list of buffers as a single I/O.
        /// </summary>
        /// <param name="buffer">The buffer to write.</param>
        /// <param name="flushType">The type of flush to perform.</param>
        void Write(ReadOnlySpan<byte> buffer, FlushType flushType);

        /// <summary>
        /// Writes a list of buffers as a single I/O.
        /// </summary>
        /// <param name="buffers">The buffers to write.</param>
        /// <param name="flushType">The type of flush to perform.</param>
        void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None);

        /// <summary>
        /// Writes a list of buffers as a single I/O.
        /// </summary>
        /// <param name="buffer">The buffer to write.</param>
        /// <param name="flushType">The type of flush to perform.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a list of buffers as a single I/O.
        /// </summary>
        /// <param name="buffers">The buffers to write.</param>
        /// <param name="flushType">The type of flush to perform.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default);

        /// <summary>
        /// Flushes a stream's write buffers, optionally shutting down writes.
        /// </summary>
        /// <param name="flushType">The type of flush to perform.</param>
        void Flush(FlushType flushType);

        /// <summary>
        /// Flushes a stream's write buffers, optionally shutting down writes.
        /// </summary>
        /// <param name="flushType">The type of flush to perform.</param>
        /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken = default);
    }
}
