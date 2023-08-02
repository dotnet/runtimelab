// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
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
    public const int BitBufferSize = 16;
    public const int MaxCodeBits = 7; //Bit length codes must not exceed this
    public const int MinMatch = 3;
    public const int MaxMatch = 258;
    public const int EndOfBlock = 256; //End of block literal code
    // Repeat previous bit length 3-6 times(2 bits of repeat count)
    public const int Rep3To6 = 16;
    // Repeat a zero length 3-10 times  (3 bits of repeat count)
    public const int Rep3To10 = 17;
    // Repeat a zero length 11-138 times  (7 bits of repeat count)
    public const int Rep11To138 = 18;


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

    public ulong _optLength;          // bit length of current block with optimal trees
    public ulong _staticLength;       // bit length of current block with static trees
    public uint _matchesInBlock;      // number of string matches in current block
    public uint _LeftToInsert;  // bytes at end of window left to insert

    public ushort _bitBuffer;      // Output buffer. bits are inserted starting
                                   // at the bottom (least significant bits).
    public int _bitsValid;    // Number of valid bits in bitBuffer
                              // All bits above the last valid bit are always zero.

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

    // Output a byte on the stream.
    public void PutByte(OutputWindow output, byte c)
    {
        output._pendingBuffer.Span[(int)output._pedingBufferBytes++] = c;
    }

    // Output a short LSB first on the stream.
    public void PutShort(OutputWindow output, ushort w)
    {
        PutByte(output, (byte)((w) & 0xff)); //LSB
        PutByte(output, (byte)((ushort)(w) >> 8)); //MSB
    }

    public void SendBits(OutputWindow output, int value, int length)
    {
        int len = length;
        if (_bitsValid > (int)BitBufferSize - len)
        {
            int val = (int)value;
            _bitBuffer |= (ushort)(val << _bitsValid);
            PutShort(output, _bitBuffer);
            _bitBuffer = (ushort)(val >> (BitBufferSize - _bitsValid));
            _bitsValid += len - BitBufferSize;
          }
        else
        {
            _bitBuffer |= (ushort)(value << _bitsValid);
            _bitsValid += len;
        }
    }

    public void SendCode(OutputWindow output, int codeIndex, CtData[] tree) 
        => SendBits(output, tree[codeIndex].Freq, tree[codeIndex].Len); //Freq is Code (In the C Zlib version
                                                                        // a union struct is used for binding the 2
                                                                        // For this first iteration, I wanted to make it as simple as possible
                                                                        // Might change later to c# union implementation

    // Flush the bits in the bit buffer to pending output (leaves at most 7 bits)
    // Flush the bit buffer, keeping at most 7 bits in it.
    public void FlushBits(OutputWindow output)
    {
        if (_bitsValid == 16)
        {
            PutShort(output, _bitBuffer);
            _bitBuffer = 0;
            _bitsValid = 0;
        }
        else if (_bitsValid >= 8)
        {
            PutByte(output, (Byte)_bitBuffer);
            _bitBuffer >>= 8;
            _bitsValid -= 8;
        }
    }
    // Flush the bit buffer and align the output on a byte boundary
    public void BitWindUp(OutputWindow output)
    {
        if (_bitsValid > 8)
        {
            PutShort(output, _bitBuffer);
        }
        else if (_bitsValid > 0)
        {
            PutByte(output, (Byte)_bitBuffer);
        }
        _bitBuffer = 0;
        _bitsValid = 0;
    }
    static int GetBitLength<T>()
    {
        return Marshal.SizeOf<T>() * 8;
    }
    // Reverse the first len bits of a code, using straightforward code
    // (a faster method would use a table)
    // IN assertion: 1 <= len <= 15
    // Code: the value to invert
    // Len: its bit length 
    public static uint BitReverse(uint code, int Len)
    {
        //int Len = Marshal.SizeOf(code) * 8;
        Debug.Assert((1 <= Len) && (Len <= 15));
        uint res = 0;
        do
        {
            res |= code & 1;
            code >>= 1;
            res <<= 1;
        } while (--Len > 0);
        return res >> 1;
    }
    public void InitBlock()
    {
        int n; /* iterates over tree elements */

        /* Initialize the trees. */
        for (n = 0; n < LitLenCodes; n++) _dynLitLenTree[n].Freq = 0;
        for (n = 0; n < DistanceCodes; n++) _dynDistanceTree[n].Freq = 0;
        for (n = 0; n < BitLengthsCodes; n++) _codesTree[n].Freq = 0;

        _dynLitLenTree[EndOfBlock].Freq = 1;
        _optLength = _staticLength = 0L;
        _symIndex = _matchesInBlock = 0;
    }
    public void TreeInit(OutputWindow output) {
        _literlDesc!.dynamicTree = _dynLitLenTree; // Dynamic part
        _literlDesc!.StaticTreeDesc = StaticLengthDesc; // Length Static table in StaticTreeTables.cs

        _distanceDesc!.dynamicTree = _dynDistanceTree;
        _distanceDesc!.StaticTreeDesc = StaticDistanceDesc; // Distance static table

        _codeDesc!.dynamicTree = _codesTree;
        _codeDesc!.StaticTreeDesc = StaticCodeDesc; // Codes' static table

        _bitBuffer = 0;
        _bitsValid = 0;
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
    public void FlushBlock() 
    {

    }

  
    //Generate the codes for a given tree and bit counts (which need not be
    //optimal).
    //tree - the tree to decorate 
    //maxCode - largest code with non zero frequency
    //codeCount - number of codes at each bit length 
    public void GenCodes(CtData [] tree, int maxCode, ushort[] codeCount)
    {
        ushort[] nextCode = new ushort[MaxBits + 1]; /* next code value for each bit length */
        uint code = 0;         /* running code value */
        int bits;                  /* bit index */
        int n;                     /* code index */

        // The distribution counts are first used to generate
        // the code values without bit reversal.
        for (bits = 1; bits <= MaxBits; bits++) {
            code = (code + codeCount[bits - 1]) << 1;
            nextCode[bits] = (ushort) code;
        }

        // Check that the bit counts in code's count (codeCount) are consistent.
        // The last code must be all ones.
        Debug.Assert(code + codeCount[MaxBits] - 1 == (1 << MaxBits) - 1,
                "inconsistent bit counts");

        for (n = 0;  n <= maxCode; n++) {
            int len = tree[n].Len;
            if (len == 0) continue;
            /* Now reverse the bits */
            tree[n].Freq = (ushort) BitReverse(nextCode[len]++, len);
        }
    }

}
