// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A HTTP version.
    /// </summary>
    public sealed class HttpPrimitiveVersion
    {
        /// <summary>
        /// HTTP/1.0
        /// </summary>
        public static HttpPrimitiveVersion Version10 { get; } = new HttpPrimitiveVersion(1, 0, BitConverter.IsLittleEndian ? 0x302E312F50545448UL : 0x485454502F312E30UL);

        /// <summary>
        /// HTTP/1.1
        /// </summary>
        public static HttpPrimitiveVersion Version11 { get; } = new HttpPrimitiveVersion(1, 1, BitConverter.IsLittleEndian ? 0x312E312F50545448UL : 0x485454502F312E31UL);

        internal readonly ulong _encoded;

        /// <summary>
        /// The major version.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// The minor version.
        /// </summary>
        public int Minor { get; }

        private HttpPrimitiveVersion(int majorVersion, int minorVersion, ulong encoded)
        {
            Major = majorVersion;
            Minor = minorVersion;
            _encoded = encoded;
        }
    }
}
