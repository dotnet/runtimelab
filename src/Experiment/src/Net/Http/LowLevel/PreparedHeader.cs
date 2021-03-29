// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A prepared header, name and value.
    /// </summary>
    public sealed class PreparedHeader
    {
        internal readonly PreparedHeaderName _name;
        internal readonly byte[]
            _value,
            _http1Encoded,
            _http2Encoded;
        internal readonly uint _http2StaticIndex;

        /// <summary>
        /// The name of the header.
        /// </summary>
        public string Name => _name.Name;

        /// <summary>
        /// The value of the header.
        /// </summary>
        public string Value { get; }

        private PreparedHeader(PreparedHeaderName name, string value, byte[] valueEncoded, uint http2StaticIndex)
        {
            Value = value;
            _name = name;
            _value = valueEncoded;

            int http1EncodedLen = Http1Connection.GetEncodeHeaderLength(name, valueEncoded);
            _http1Encoded = new byte[http1EncodedLen];
            Http1Connection.EncodeHeader(name, valueEncoded, _http1Encoded);

            _http2Encoded =
                http2StaticIndex != 0 ? HPack.EncodeIndexedHeader(http2StaticIndex) :
                name._http2StaticIndex != 0 ? HPack.EncodeHeaderWithoutIndexing(name._http2StaticIndex, valueEncoded) :
                HPack.EncodeHeaderWithoutIndexing(name._http2Encoded, valueEncoded);

            _http2StaticIndex = http2StaticIndex;
        }

        internal PreparedHeader(PreparedHeaderName name, string value, uint http2StaticIndex = 0)
            : this(name, value, Encoding.ASCII.GetBytes(value), http2StaticIndex)
        {
        }

        /// <summary>
        /// Instantiates a new <see cref="PreparedHeader"/>.
        /// </summary>
        /// <param name="name">The <see cref="PreparedHeaderName"/> of the header.</param>
        /// <param name="value">The value of the header. This value will be ASCII-encoded.</param>
        public PreparedHeader(PreparedHeaderName name, string value)
            : this(name, value, 0)
        {
        }

        /// <summary>
        /// Instantiates a new <see cref="PreparedHeader"/>.
        /// </summary>
        /// <param name="name">The <see cref="PreparedHeaderName"/> of the header.</param>
        /// <param name="value">The value of the header.</param>
        public PreparedHeader(PreparedHeaderName name, ReadOnlySpan<byte> value)
            : this(name, Encoding.ASCII.GetString(value), value.ToArray(), 0)
        {
        }

        /// <summary>
        /// Instantiates a new <see cref="PreparedHeader"/>.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header. This value will be ASCII-encoded.</param>
        public PreparedHeader(string name, string value)
            : this(new PreparedHeaderName(name), value, 0)
        {
        }

        /// <summary>
        /// Instantiates a new <see cref="PreparedHeader"/>.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header.</param>
        public PreparedHeader(string name, ReadOnlySpan<byte> value)
            : this(new PreparedHeaderName(name), Encoding.ASCII.GetString(value), value.ToArray(), 0)
        {
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Name + ": " + Value;
    }
}
