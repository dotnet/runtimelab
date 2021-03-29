// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel.Headers
{
    /// <summary>
    /// The :method pseudo-header.
    /// </summary>
    internal sealed class MethodHeader : PreparedHeaderName
    {
        public MethodHeader()
            : base(":method", http2StaticIndex: 2)
        {
            Get = new PreparedHeader(this, "GET", http2StaticIndex: 2);
            Post = new PreparedHeader(this, "POST", http2StaticIndex: 3);
        }

        public PreparedHeader Get { get; }
        public PreparedHeader Post { get; }
    }
}
