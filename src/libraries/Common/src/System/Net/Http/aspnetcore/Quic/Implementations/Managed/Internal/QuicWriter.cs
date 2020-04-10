using System.Buffers.Binary;

namespace System.Net.Quic.Implementations.Managed.Internal
{
    /// <summary>
    ///     Helper class for writing QUIC primitives to a byte buffer.
    /// </summary>
    internal class QuicWriter
    {
        // underlying buffer to which data are being written.
        private ArraySegment<byte> _buffer;
        // number of bytes already written into the buffer.
        private int _written;

        public QuicWriter(ArraySegment<byte> buffer)
        {
            _buffer = buffer;
        }

        internal int BytesWritten => _written;

        internal int BytesAvailable => _buffer.Count - BytesWritten;

        internal ArraySegment<byte> Buffer => _buffer;

        internal void Reset(ArraySegment<byte> buffer, int offset = 0)
        {
            _buffer = buffer;
            _written = offset;
        }

        internal void Reset(byte[] buffer)
        {
            Reset(new ArraySegment<byte>(buffer, 0, buffer.Length));
        }

        internal void WriteUInt8(byte value)
        {
            CheckSizeAvailable(sizeof(byte));
            _buffer[_written] = value;
            Advance(sizeof(byte));
        }

        internal void WriteInt16(short value)
        {
            BinaryPrimitives.WriteInt16BigEndian(GetWritableSpan(sizeof(short)), value);
        }

        internal void WriteInt24(int value)
        {
            var destination = GetWritableSpan(3);
            destination[0] = (byte)(value >> 16);
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value);
        }

        internal void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32BigEndian(GetWritableSpan(sizeof(int)), value);
        }

        internal void WriteUInt64(long value)
        {
            BinaryPrimitives.WriteInt64BigEndian(GetWritableSpan(sizeof(long)), value);
        }

        internal void WriteVarInt(long value)
        {
            QuicPrimitives.WriteVarInt(GetWritableSpan(1 << QuicPrimitives.GetVarIntLengthLogarithm(value)), value);
        }

        internal void WriteSpan(ReadOnlySpan<byte> data)
        {
            data.CopyTo(GetWritableSpan(data.Length));
        }

        internal Span<byte> GetWritableSpan(int length)
        {
            var span = GetSpan(length);
            Advance(length);
            return span;
        }

        private void Advance(int bytes)
        {
            _written += bytes;
        }

        private Span<byte> GetSpan(int length)
        {
            CheckSizeAvailable(length);
            return _buffer.AsSpan(BytesWritten, length);
        }

        private void CheckSizeAvailable(int size)
        {
            if (BytesAvailable < size) throw new ArgumentException("Buffer too short");
        }
    }
}
