// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel.Headers
{
    /// <summary>
    /// The :status pseudo-header.
    /// </summary>
    internal sealed class StatusHeader : PreparedHeaderName
    {
        public StatusHeader()
            : base(":status", http2StaticIndex: 8)
        {
            OK = new PreparedHeader(this, "200", http2StaticIndex: 8);
            NoContent = new PreparedHeader(this, "204", http2StaticIndex: 9);
            PartialContent = new PreparedHeader(this, "206", http2StaticIndex: 10);
            NotModified = new PreparedHeader(this, "304", http2StaticIndex: 11);
            BadRequest = new PreparedHeader(this, "400", http2StaticIndex: 12);
            NotFound = new PreparedHeader(this, "404", http2StaticIndex: 13);
            InternalServerError = new PreparedHeader(this, "500", http2StaticIndex: 14);
        }

        public PreparedHeader OK { get; }
        public PreparedHeader NoContent { get; }
        public PreparedHeader PartialContent { get; }
        public PreparedHeader NotModified { get; }
        public PreparedHeader BadRequest { get; }
        public PreparedHeader NotFound { get; }
        public PreparedHeader InternalServerError { get; }
    }
}
