// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel.Headers
{
    /// <summary>
    /// The :path pseudo-header.
    /// </summary>
    internal sealed class PathHeader : PreparedHeaderName
    {
        public PathHeader()
            : base(":path", http2StaticIndex: 4)
        {
            Root = new PreparedHeader(this, "/", http2StaticIndex: 4);
            IndexHtml = new PreparedHeader(this, "/index.html", http2StaticIndex: 5);
        }

        public PreparedHeader Root { get; }
        public PreparedHeader IndexHtml { get; }
    }
}
