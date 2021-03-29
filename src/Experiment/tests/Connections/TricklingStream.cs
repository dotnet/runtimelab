using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    internal sealed class TricklingStream : TestStreamBase
    {
        private readonly Stream _baseStream;
        private readonly IEnhancedStream _enhancedStream;
        private readonly int[] _trickleReadSequence, _trickleWriteSequence;
        private readonly bool _forceAsync;
        private int _readIdx, _writeIdx;

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanWrite => _baseStream.CanWrite;
        public override bool CanShutdownWrites => _baseStream is IEnhancedStream { CanShutdownWrites: true };
        public override bool CanScatterGather => _baseStream is IEnhancedStream { CanScatterGather: true };

        public TricklingStream(Stream baseStream, bool forceAsync, IEnumerable<int> trickleSequence)
            : this(baseStream, forceAsync, trickleSequence, trickleSequence)
        {
        }

        public TricklingStream(Stream baseStream, bool forceAsync, IEnumerable<int> trickleReadSequence, IEnumerable<int> trickleWriteSequence)
        {
            _baseStream = baseStream;
            _enhancedStream = baseStream as IEnhancedStream ?? throw new ArgumentException($"Stream must be an {nameof(IEnhancedStream)}.", nameof(baseStream));
            _trickleReadSequence = trickleReadSequence.ToArray();
            _trickleWriteSequence = trickleWriteSequence.ToArray();
            _forceAsync = forceAsync;

            Debug.Assert(_trickleReadSequence.Length > 0);
            Debug.Assert(_trickleWriteSequence.Length > 0);
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing) _baseStream.Dispose();
        }

        public override ValueTask DisposeAsync() =>
            _baseStream.DisposeAsync();

        public override void Flush(FlushType flushType) =>
            _enhancedStream.Flush(flushType);

        public override async ValueTask FlushAsync(FlushType flushType, CancellationToken cancellationToken)
        {
            ValueTask task = _enhancedStream.FlushAsync(flushType, cancellationToken);

            if (task.IsCompleted && _forceAsync)
            {
                await Task.Yield();
            }

            await task.ConfigureAwait(false);
        }

        private static int NextSize(int[] sequence, ref int idx)
        {
            int size = sequence[idx];
            idx = (idx + 1) % sequence.Length;
            return size;
        }

        public override int Read(Span<byte> buffer)
        {
            int readLength = Math.Min(buffer.Length, NextSize(_trickleReadSequence, ref _readIdx));
            return _baseStream.Read(buffer.Slice(0, readLength));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int readLength = Math.Min(buffer.Length, NextSize(_trickleReadSequence, ref _readIdx));

            ValueTask<int> readTask = _baseStream.ReadAsync(buffer.Slice(0, readLength), cancellationToken);

            if (readTask.IsCompleted && _forceAsync)
            {
                await Task.Yield();
            }

            return await readTask.ConfigureAwait(false);
        }

        public override int Read(IReadOnlyList<Memory<byte>> buffers) =>
            _enhancedStream.Read(GetNextReadBuffers(buffers));

        public override async ValueTask<int> ReadAsync(IReadOnlyList<Memory<byte>> buffers, CancellationToken cancellationToken = default)
        {
            ValueTask<int> readTask = _enhancedStream.ReadAsync(GetNextReadBuffers(buffers), cancellationToken);

            if (readTask.IsCompleted && _forceAsync)
            {
                await Task.Yield();
            }

            return await readTask.ConfigureAwait(false);
        }

        private List<Memory<byte>> GetNextReadBuffers(IReadOnlyList<Memory<byte>> buffers)
        {
            int maxReadLength = NextSize(_trickleReadSequence, ref _readIdx);

            var newBuffers = new List<Memory<byte>>();

            for (int i = 0; maxReadLength != 0 && i != buffers.Count; ++i)
            {
                Memory<byte> buffer = buffers[i];

                int take = Math.Min(maxReadLength, buffer.Length);
                newBuffers.Add(buffer.Slice(0, take));

                maxReadLength -= take;
            }

            return newBuffers;
        }

        public override void Write(ReadOnlySpan<byte> buffer, FlushType flushType)
        {
            while (buffer.Length != 0)
            {
                int writeLength = Math.Min(buffer.Length, NextSize(_trickleWriteSequence, ref _writeIdx));

                _enhancedStream.Write(buffer.Slice(0, writeLength), writeLength == buffer.Length ? flushType : FlushType.None);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, FlushType flushType, CancellationToken cancellationToken = default)
        {
            bool isSync = false;

            while (buffer.Length != 0)
            {
                int writeLength = Math.Min(buffer.Length, NextSize(_trickleWriteSequence, ref _writeIdx));

                ValueTask writeTask = _enhancedStream.WriteAsync(buffer.Slice(0, writeLength), writeLength == buffer.Length ? flushType : FlushType.None, cancellationToken);
                isSync = isSync || writeTask.IsCompleted;

                await writeTask.ConfigureAwait(false);
            }

            if (isSync && _forceAsync)
            {
                await Task.Yield();
            }
        }

        public override void Write(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType = FlushType.None)
        {
            if (buffers is null) throw new ArgumentNullException(nameof(buffers));

            if (buffers.Count == 0)
            {
                _enhancedStream.Write(buffers, flushType);
                return;
            }

            var sendBuffers = new List<ReadOnlyMemory<byte>>();
            int take = 0;

            for (int bufferIdx = 0, bufferCount = buffers.Count; bufferIdx != bufferCount; ++bufferIdx)
            {
                ReadOnlyMemory<byte> buffer = buffers[bufferIdx];

                while (buffer.Length != 0)
                {
                    if (take == 0)
                    {
                        if (sendBuffers.Count != 0)
                        {
                            _enhancedStream.Write(sendBuffers, FlushType.None);
                            sendBuffers.Clear();
                        }

                        take = NextSize(_trickleWriteSequence, ref _writeIdx);
                    }

                    int bufTake = Math.Min(buffer.Length, take);
                    ReadOnlyMemory<byte> sendChunk = buffer.Slice(0, bufTake);
                    buffer = buffer.Slice(bufTake);
                    take -= bufTake;

                    sendBuffers.Add(sendChunk);
                }
            }

            Debug.Assert(sendBuffers.Count != 0);
            _enhancedStream.Write(sendBuffers, flushType);
        }

        public override async ValueTask WriteAsync(IReadOnlyList<ReadOnlyMemory<byte>> buffers, FlushType flushType, CancellationToken cancellationToken = default)
        {
            if (buffers is null) throw new ArgumentNullException(nameof(buffers));

            ValueTask writeTask;

            if (buffers.Count == 0)
            {
                writeTask = _enhancedStream.WriteAsync(buffers, flushType, cancellationToken);

                if (writeTask.IsCompleted && _forceAsync)
                {
                    await Task.Yield();
                }

                await writeTask.ConfigureAwait(false);
                return;
            }

            bool isAsync = false;
            var sendBuffers = new List<ReadOnlyMemory<byte>>();
            int take = 0;

            for (int bufferIdx = 0, bufferCount = buffers.Count; bufferIdx != bufferCount; ++bufferIdx)
            {
                ReadOnlyMemory<byte> buffer = buffers[bufferIdx];

                while (buffer.Length != 0)
                {
                    if (take == 0)
                    {
                        if (sendBuffers.Count != 0)
                        {
                            writeTask = _enhancedStream.WriteAsync(sendBuffers, FlushType.None, cancellationToken);
                            isAsync |= !writeTask.IsCompleted;
                            await writeTask.ConfigureAwait(false);
                            sendBuffers.Clear();
                        }

                        take = NextSize(_trickleWriteSequence, ref _writeIdx);
                    }

                    int bufTake = Math.Min(buffer.Length, take);
                    ReadOnlyMemory<byte> sendChunk = buffer.Slice(0, bufTake);
                    buffer = buffer.Slice(bufTake);
                    take -= bufTake;
                    
                    sendBuffers.Add(sendChunk);
                }
            }

            Debug.Assert(sendBuffers.Count != 0);

            writeTask = _enhancedStream.WriteAsync(sendBuffers, flushType, cancellationToken);
            isAsync |= !writeTask.IsCompleted;
            await writeTask.ConfigureAwait(false);

            if (!isAsync && _forceAsync)
            {
                await Task.Yield();
            }
        }
    }
}
