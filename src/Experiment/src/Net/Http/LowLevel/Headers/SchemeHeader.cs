// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel.Headers
{
    /// <summary>
    /// The :scheme pseudo-header.
    /// </summary>
    internal sealed class SchemeHeader : PreparedHeaderName
    {
        public SchemeHeader()
            : base(":scheme", http2StaticIndex: 6)
        {
            Http = new PreparedHeader(this, "http", http2StaticIndex: 6);
            Https = new PreparedHeader(this, "https", http2StaticIndex: 7);
        }

        public PreparedHeader Http { get; }
        public PreparedHeader Https { get; }
    }
}
