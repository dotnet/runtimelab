// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A prepared header name.
    /// </summary>
    public class PreparedHeaderName
    {
        internal readonly byte[] _http1Encoded, _http2Encoded;
        internal readonly uint _http2StaticIndex;

        /// <summary>
        /// The name of the header.
        /// </summary>
        public string Name { get; }

        internal PreparedHeaderName(string name, uint http2StaticIndex = 0)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException($"{nameof(name)} must not be null or empty.", nameof(name));

            Name = name;
            _http1Encoded = Encoding.ASCII.GetBytes(name + ": ");
            _http2Encoded = Encoding.ASCII.GetBytes(name.ToLowerInvariant());
            _http2StaticIndex = http2StaticIndex;
        }

        /// <summary>
        /// Instantiates a new <see cref="PreparedHeaderName"/>.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        public PreparedHeaderName(string name)
            : this(name, 0)
        {
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
