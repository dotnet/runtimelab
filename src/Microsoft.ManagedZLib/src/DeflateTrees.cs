// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Microsoft.ManagedZLib;

internal class DeflateTrees
{
    public const int LengthCodes = 29; /* number of length codes, not counting the special END_BLOCK code */
    public const int Literals = 256; //Num of literal bytes from 0 to 255
    public const int LitLenCodes = Literals + 1 + LengthCodes; /* number of Literal or Length codes, including the END_BLOCK code */
    public const int DistanceCodes = 30;
    public const int BitLengthsCodes = 19; // Number of bits used to transfer the bit lengths
    public const int HeapSize = 2 * LitLenCodes + 1;
    public const int MaxBits = 15;
    public const int MaxCodeBits = 7;
    public const int MinMatch = 3;
    public const int MaxMatch = 258;

    //To build Huffman tree (dynamic trees)
    CtData[] _dynLitLenTree   = new CtData[HeapSize];                 // literal and length tree
    CtData[] _dynDistanceTree = new CtData[2 * DistanceCodes + 1];    // distance tree
    CtData[] _codesTree       = new CtData[2 * BitLengthsCodes + 1];  // Huffman tree for bit lengths

    TreeDesc? _literlDesc;              // Description for literal tree\\
    TreeDesc? _distanceDesc;           // Description for distance tree \\
    TreeDesc? _codeDesc;              // Description for bit length tree \\

    // Number of codes at each bit length for an optimal tree
    public Memory <ushort> _codeCount = new ushort[MaxBits + 1];
    // Depth of each subtree used as tie breaker for trees of equal frequency
    public Memory<byte> _depth = new byte[HeapSize];
    // The sons of heap[n] are heap[2*n] and heap[2*n+1]. heap[0] is not used.
    // The same heap array is used to build all trees.
    public Memory<int> Heap = new int[HeapSize]; // heap used to build the Huffman trees
    int _heapLen;   // number of elements in the heap
    int _heapMax;   // element of largest frequency
    

    public Memory<byte> _symBuffer;     // Buffer for distances and literal/lengths
    // For tallying - building a frequency table:
    // Count the ocurrences of a Length-dintance pair
    internal uint _symIndex;   // Index for symBuf - sym_next in C
    internal uint _symEnd;     // symbol table full when symIndex reaches this 
    ulong _optLength;          // bit length of current block with optimal trees
    ulong _staticLength;       // bit length of current block with static trees
    uint _matchesInBlock;      // number of string matches in current block
    uint _insertLeftToInsert;  // bytes at end of window left to insert
    static public ushort GetDistCode(ushort dist)
        => (dist < 256) ? StaticTreeTables.DistanceCode[dist] : StaticTreeTables.DistanceCode[256 + (dist>>7)];
    // Extra bits for each length code.
    private static ReadOnlySpan<byte> ExtraLengthBits
        => new byte[LengthCodes] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
    private static ReadOnlySpan<byte> ExtraDistanceBits 
        => new byte[DistanceCodes] { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };    
    private static ReadOnlySpan<byte> ExtraCodeBits
        => new byte[BitLengthsCodes] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 7 };
    private static ReadOnlySpan<byte> CodeOrder //blOrder
        => new byte[BitLengthsCodes] { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

    static StaticTreesDesc StaticLengthDesc = 
        new(StaticTreeTables.StaticLengthTree, ExtraLengthBits, Literals + 1, LitLenCodes, MaxBits);

    static StaticTreesDesc StaticDistanceDesc =
        new(StaticTreeTables.StaticDistanceTree, ExtraDistanceBits, 0, DistanceCodes, MaxBits);
    const CtData[]? nullDataPtr = null;
    static StaticTreesDesc StaticCodeDesc =
        new( nullDataPtr, ExtraCodeBits, 0, BitLengthsCodes, MaxCodeBits);
    internal DeflateTrees(Memory<byte> symBuffer, uint litBufferSize) 
    {
        //For tallying:
        _symBuffer = symBuffer;
        // We avoid equality with lit_bufsize*3 because of wraparound at 64K
        // on 16 bit machines and because stored blocks are restricted to 64K-1 bytes.
        _symEnd = (litBufferSize - 1) * 3;
    }
    public void TreeInit() {
        _literlDesc!.dynamicTree = _dynLitLenTree; // Dynamic part
        _literlDesc!.StaticTreeDesc = StaticLengthDesc; // Length Static table in StaticTreeTables.cs

        _distanceDesc!.dynamicTree = _dynDistanceTree;
        _distanceDesc!.StaticTreeDesc = StaticDistanceDesc; // Distance static table

        _codeDesc!.dynamicTree = _codesTree;
        _codeDesc!.StaticTreeDesc = StaticCodeDesc; // Codes' static table
    }
    // For building the frequency table (nummber of ocurrences)
    // per alphabet (Lit-Length, distance) for Huffman Tree construction
    public bool TreeTallyLit(byte WindowValue) //Whether or not to flush the block
    {
        byte DynLTreeIndex = WindowValue;
        _symBuffer.ToArray()[_symIndex++] = 0;
        _symBuffer.ToArray()[_symIndex++] = 0;
        _symBuffer.ToArray()[_symIndex++] = DynLTreeIndex;
        _dynLitLenTree[DynLTreeIndex].Freq++; //Literal-Length frenquency count 
                                              // For later building the code: Bit String
        return (_symIndex == _symEnd);
    }

    public bool TreeTallyDist(uint distance, uint length) 
    {
        byte len = (byte)length;
        ushort dist = (ushort)distance;
        _symBuffer.ToArray()[_symIndex++] = (byte)dist;
        _symBuffer.ToArray()[_symIndex++] = (byte)(dist>>8);
        _symBuffer.ToArray()[_symIndex++] = len;
        dist--;
        _dynLitLenTree[StaticTreeTables.LengthCode[len]+Literals+1].Freq++;
        _dynDistanceTree[GetDistCode(dist)].Freq++; //Distance frenquency count

        return (_symIndex == _symEnd);
    }
}
