using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests
{
    internal abstract class TestStreamBase : Stream, IEnhancedStream
    {
        public virtual bool CanScatterGather => true;
        public virtual bool CanShutdownWrites => false;

        public override bool CanRead => false;

        public override bool CanWrite => false;

        public override bool CanSeek => false;
        public override long Length => throw new InvalidOperationException();
        public override long Position { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }

        public sealed override void Flush() =>
            Flush(FlushType.FlushWrites);

        public virtual void Flush(FlushType flushType)
        {
        }

        public sealed override Task FlushAsync(CancellationToken cancellationToken) =>
            FlushAsync(FlushType.FlushWrites, cancellationToken).AsTask();

        public virtual ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);
            return default;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotImplementedException();

        public override void SetLength(long value) => throw new
            NotImplementedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(ExceptionDispatchInfo.SetCurrentStackTrace(new NotImplementedException()));

        public virtual ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default) =>
            ReadAsync(buffers.Count != 0 ? buffers[0] : default, cancellationToken);

        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            TaskToApm.Begin(ReadAsync(buffer, offset, count), callback, state);

        public sealed override int EndRead(IAsyncResult asyncResult) =>
            TaskToApm.End<int>(asyncResult);

        public override int Read(Span<byte> buffer) =>
            throw new NotImplementedException();

        public sealed override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public virtual int Read(IReadOnlyList<Memory<byte>> buffers) =>
            buffers.Count == 0 ? 0 : Read(buffers[0].Span);

        public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            WriteAsync(buffer, FlushType.None, cancellationToken);

        public virtual ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new NotImplementedException()));

        public virtual async ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None, CancellationToken cancellationToken = default)
        {
            int count = buffers.Count;

            if (count != 0)
            {
                for (int i = 0, last = count - 1; i != last; ++i)
                {
                    await WriteAsync(buffers[i], FlushType.None, cancellationToken).ConfigureAwait(false);
                }

                await WriteAsync(buffers[count - 1], flushType, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await FlushAsync(flushType, cancellationToken).ConfigureAwait(false);
            }
        }

        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), FlushType.None, cancellationToken).AsTask();

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            TaskToApm.Begin(WriteAsync(buffer, offset, count), callback, state);

        public sealed override void EndWrite(IAsyncResult asyncResult) =>
            TaskToApm.End(asyncResult);

        public sealed override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count), FlushType.None);

        public sealed override void Write(ReadOnlySpan<byte> buffer) =>
            Write(buffer, FlushType.None);

        public virtual void Write(ReadOnlySpan<byte> buffer, FlushType flushType) =>
            throw new NotImplementedException();

        public virtual void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None)
        {
            int count = buffers.Count;

            if (count != 0)
            {
                for (int i = 0, last = count - 1; i != last; ++i)
                {
                    Write(buffers[i].Span, FlushType.None);
                }

                Write(buffers[count - 1].Span, flushType);
            }
            else
            {
                Flush(flushType);
            }
        }

    }
}
