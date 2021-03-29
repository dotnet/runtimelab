// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// Defines flags indicating properties of a header.
    /// </summary>
    [Flags]
    public enum HttpHeaderFlags
    {
        /// <summary>
        /// No flags set.
        /// </summary>
        None = 0,

        /// <summary>
        /// The header's name is huffman coded.
        /// </summary>
        /// <remarks>
        /// TODO: make this HTTP/2 specific?
        /// </remarks>
        NameHuffmanCoded = 1,

        /// <summary>
        /// The header's value is huffman coded.
        /// </summary>
        /// <remarks>
        /// TODO: make this HTTP/2 specific?
        /// </remarks>
        ValueHuffmanCoded = 2,

        /// <summary>
        /// The header must never be compressed.
        /// </summary>
        NeverCompressed = 4
    }
}
