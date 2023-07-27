// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.ManagedZLib;

//Static part of the Huffman tree
internal class StaticTreesDesc
{
    public CtData[]? staticTree { get; set; }   // static tree or NULL */
    public byte[]? extraBits { get; set; }     // extra bits for each code or NULL */
    public int extraBase { get; set; }         // base index for extra_bits */
    public int elems { get; set; }             // max number of elements in the tree */
    public int maxLength { get; set; }         // max bit length for the codes */

    public StaticTreesDesc(CtData[]? staticTree, ReadOnlySpan<byte> extraBits, int extraBase, int elems, int maxLength)
    {
        this.staticTree = staticTree;
        this.extraBits = extraBits.ToArray();
        this.extraBase = extraBase;
        this.elems = elems;
        this.maxLength = maxLength;
    }
}
