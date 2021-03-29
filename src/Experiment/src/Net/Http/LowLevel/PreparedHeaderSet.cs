// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A set of prepared headers, used to more efficiently write frequently reused headers.
    /// </summary>
    public sealed class PreparedHeaderSet : IEnumerable<PreparedHeader>
    {
        private readonly List<PreparedHeader> _headers = new List<PreparedHeader>();
        private byte[]? _http1Value, _http2Value;

        internal byte[] Http1Value => _http1Value ?? GetHttp1ValueSlow();
        internal byte[] Http2Value => _http2Value ?? GetHttp2ValueSlow();

        /// <summary>
        /// Adds a header to the <see cref="PreparedHeaderSet"/>
        /// </summary>
        /// <param name="header">The header to add.</param>
        public void Add(PreparedHeader header)
        {
            lock (_headers)
            {
                if (_http1Value is null)
                {
                    _headers.Add(header);
                    return;
                }
            }

            throw new Exception($"Unable to add to {nameof(PreparedHeaderSet)} after it has been used.");
        }

        /// <summary>
        /// Adds a header to the <see cref="PreparedHeaderSet"/>
        /// </summary>
        /// <param name="name">The name of the header to add.</param>
        /// <param name="value">The value of the header to add. The value will be ASCII-encoded.</param>
        public void Add(PreparedHeaderName name, string value) =>
            Add(new PreparedHeader(name, value));

        /// <summary>
        /// Adds a header to the <see cref="PreparedHeaderSet"/>
        /// </summary>
        /// <param name="name">The name of the header to add.</param>
        /// <param name="value">The value of the header to add.</param>
        public void Add(PreparedHeaderName name, ReadOnlySpan<byte> value) =>
            Add(new PreparedHeader(name, value));

        /// <summary>
        /// Adds a header to the <see cref="PreparedHeaderSet"/>
        /// </summary>
        /// <param name="name">The name of the header to add.</param>
        /// <param name="value">The value of the header to add. The value will be ASCII-encoded.</param>
        public void Add(string name, string value) =>
            Add(new PreparedHeader(name, value));

        /// <summary>
        /// Adds a header to the <see cref="PreparedHeaderSet"/>
        /// </summary>
        /// <param name="name">The name of the header to add.</param>
        /// <param name="value">The value of the header to add.</param>
        public void Add(string name, ReadOnlySpan<byte> value) =>
            Add(new PreparedHeader(name, value));

        private byte[] GetHttp1ValueSlow()
        {
            lock (_headers)
            {
                if (_http1Value is null)
                {
                    GetValuesSlow();
                }

                return _http1Value!;
            }
        }

        private byte[] GetHttp2ValueSlow()
        {
            lock (_headers)
            {
                if (_http2Value is null)
                {
                    GetValuesSlow();
                }

                return _http2Value!;
            }
        }

        private void GetValuesSlow()
        {
            Debug.Assert(Monitor.IsEntered(_headers));

            int totalHttp1Len = 0;
            int totalHttp2Len = 0;

            foreach (PreparedHeader header in _headers)
            {
                checked
                {
                    totalHttp1Len += header._http1Encoded.Length;
                    totalHttp2Len += header._http2Encoded.Length;
                }
            }

            byte[] http1Value = new byte[totalHttp1Len];
            byte[] http2Value = new byte[totalHttp2Len];

            Span<byte> write1Pos = http1Value;
            Span<byte> write2Pos = http2Value;

            foreach (PreparedHeader header in _headers)
            {
                header._http1Encoded.AsSpan().CopyTo(write1Pos);
                write1Pos = write1Pos[header._http1Encoded.Length..];

                header._http2Encoded.AsSpan().CopyTo(write2Pos);
                write2Pos = write2Pos[header._http2Encoded.Length..];
            }

            Volatile.Write(ref _http1Value, http1Value);
            Volatile.Write(ref _http2Value, http2Value);
        }

        /// <inheritdoc/>
        public override string ToString() => Encoding.ASCII.GetString(Http1Value);

        /// <inheritdoc/>
        public IEnumerator<PreparedHeader> GetEnumerator()
        {
            if (_http1Value is null)
            {
                // ensure _headers is frozen.
                GetValuesSlow();
            }

            return _headers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
