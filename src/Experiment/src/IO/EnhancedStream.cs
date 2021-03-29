using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    internal static class EnhancedStream
    {
        private const int EmulatedBufferLength = 8192;

        public static void EmulateWrite(Stream stream, IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType)
        {
            ValueTask t = EmulateWriteImpl(stream, buffers, flushType, isAsync: false, cancellationToken: default);
            Debug.Assert(t.IsCompleted);
            t.GetAwaiter().GetResult();
        }

        public static ValueTask EmulateWriteAsync(Stream stream, IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType, CancellationToken cancellationToken) =>
            EmulateWriteImpl(stream, buffers, flushType, isAsync: true, cancellationToken);

        static async ValueTask EmulateWriteImpl(Stream stream, IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType, bool isAsync, CancellationToken cancellationToken)
        {
            Debug.Assert(isAsync || !cancellationToken.CanBeCanceled, "Cancellation is only supported with async.");

            var enhancedStream = stream as IEnhancedStream;

            byte[] sendBuffer = ArrayPool<byte>.Shared.Rent(minimumLength: EmulatedBufferLength);
            int fillLength = 0;
            bool written = false;

            try
            {
                for (int i = 0, count = buffers.Count; i != count; ++i)
                {
                    ReadOnlyMemory<byte> buffer = buffers[i];

                    int remaining = sendBuffer.Length - fillLength;
                    if (remaining > buffer.Length)
                    {
                        // entire buffer can fit into the send buffer.

                        buffer.Span.CopyTo(sendBuffer.AsSpan(fillLength));
                        fillLength += buffer.Length;
                        continue;
                    }

                    if (fillLength == 0)
                    {
                        // the buffer is too large to buffer, and nothing is buffered.
                        // just send it directly.

                        written = true;

                        if (isAsync)
                        {
                            ValueTask writeTask =
                                enhancedStream is not null
                                ? enhancedStream.WriteAsync(buffer, i == count - 1 ? flushType : FlushType.None, cancellationToken)
                                : stream.WriteAsync(buffer, cancellationToken);

                            await writeTask.ConfigureAwait(false);
                        }
                        else
                        {
                            if (enhancedStream is not null) enhancedStream.Write(buffer.Span, i == count - 1 ? flushType : FlushType.None);
                            else stream.Write(buffer.Span);
                        }
                        continue;
                    }

                    int remainingIfBuffered = buffer.Length - remaining;
                    if (remainingIfBuffered >= sendBuffer.Length)
                    {
                        // some of the buffer *could* fit into current buffer,
                        // but doing so wouldn't reduce I/O count.
                        // just send both without buffering.

                        if (isAsync)
                        {
                            await stream.WriteAsync(sendBuffer.AsMemory(0, fillLength), cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            stream.Write(sendBuffer.AsSpan(0, fillLength));
                        }

                        fillLength = 0;
                        written = true;

                        if (isAsync)
                        {
                            ValueTask writeTask =
                                enhancedStream is not null
                                ? enhancedStream.WriteAsync(buffer, i == count - 1 ? flushType : FlushType.None, cancellationToken)
                                : stream.WriteAsync(buffer, cancellationToken);

                            await writeTask.ConfigureAwait(false);
                        }
                        else
                        {
                            if (enhancedStream is not null) enhancedStream.Write(buffer.Span, i == count - 1 ? flushType : FlushType.None);
                            else stream.Write(buffer.Span);
                        }
                        continue;
                    }

                    // if the send buffer is filled, what is remaining of the buffer will be small enough to avoid an I/O.

                    buffer.Slice(0, remaining).Span.CopyTo(sendBuffer.AsSpan(fillLength));

                    if (isAsync)
                    {
                        await stream.WriteAsync(sendBuffer, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        stream.Write(sendBuffer);
                    }

                    written = true;

                    buffer.Slice(remaining).CopyTo(sendBuffer);
                    fillLength = remainingIfBuffered;
                }

                if (fillLength != 0 || !written)
                {
                    // flush out buffer.
                    // in the case that no call to Write has occurred yet, make sure to call it at least
                    // once in case a 0-byte write is being used for some kind of backpressure detection.

                    if (isAsync)
                    {
                        ValueTask writeTask =
                            enhancedStream is not null
                            ? enhancedStream.WriteAsync(sendBuffer.AsMemory(0, fillLength), flushType, cancellationToken)
                            : stream.WriteAsync(sendBuffer.AsMemory(0, fillLength), cancellationToken);

                        await writeTask.ConfigureAwait(false);
                    }
                    else
                    {
                        if (enhancedStream is not null) enhancedStream.Write(sendBuffer.AsSpan(0, fillLength), flushType);
                        else stream.Write(sendBuffer.AsSpan(0, fillLength));
                    }
                }
            }
            finally
            {
                // it is safe to return the array here even in an
                // exception as the stream is not writing to the
                // buffer, so runaway streams won't cause weirdness.
                ArrayPool<byte>.Shared.Return(sendBuffer);
            }

            if (flushType != FlushType.None && enhancedStream is null)
            {
                if (flushType != FlushType.FlushWrites)
                {
                    throw new Exception($"{nameof(FlushType)} of '{flushType}' is not supported on this stream.");
                }

                if (isAsync)
                {
                    // wrap in a ValueTask to avoid extra state machine for Task awaitable.
                    await new ValueTask(stream.FlushAsync(cancellationToken)).ConfigureAwait(false);
                }
                else
                {
                    stream.Flush();
                }
            }
        }
    }
}
