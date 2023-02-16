namespace System.IO.StreamSourceGeneration.Tests.TestClasses
{
    [GenerateStreamBoilerplate]
    internal partial class StreamThatImplementsSeek : Stream
    {
        private long _position;
        private long _length;

        internal StreamThatImplementsSeek(long position, long lenght)
        {
            _position = position;
            _length = lenght;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newValue = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Invalid origin", nameof(origin))
            };

            if (newValue < 0)
            {
                throw new ArgumentException("Invalid offset", nameof(offset));
            }

            if (newValue > _length)
            {
                _length = newValue;
            }

            return _position = newValue;
        }

        public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

        public override long Length => _length;

        public override void Flush() { }
    }
}