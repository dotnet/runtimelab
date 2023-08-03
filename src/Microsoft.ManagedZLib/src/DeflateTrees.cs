// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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
    public ulong _staticLen;       // bit length of current block with static trees
    public uint _matchesInBlock;      // number of string matches in current block
    public uint _LeftToInsert;  // bytes at end of window left to insert

    public ushort _bitBuffer;      // Output buffer. bits are inserted starting
                                   // at the bottom (least significant bits).
    public int _bitsValid;    // Number of valid bits in bitBuffer
                              // All bits above the last valid bit are always zero.

    static public ushort GetDistCode(ushort dist)
        => (dist < 256) ? StaticTreeTables.DistanceCode[dist] : StaticTreeTables.DistanceCode[256 + (dist>>7)];
    // Extra bits for each length code.
    private static ReadOnlySpan<int> ExtraLengthBits
        => new int[LengthCodes] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
    private static ReadOnlySpan<int> ExtraDistanceBits 
        => new int[DistanceCodes] { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };    
    private static ReadOnlySpan<int> ExtraCodeBits
        => new int[BitLengthsCodes] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 7 };
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
        _optLength = _staticLen = 0L;
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

    // Index within the heap array of least frequent node in the Huffman tree
    public const int Smallest = 1;

    // Remove the smallest element from the heap and recreate the heap with
    // one less element.Updates heap and heap_len.
    public void PriorityQueueRemove (CtData[] Tree, out int removedTop)
    {
        removedTop = Heap.Span[Smallest];
        Heap.Span[Smallest] = Heap.Span[_heapLen--];
        PriorityQueueDownHeap(Tree, Smallest);
    }

    // Compares to subtrees, using the tree depth as tie breaker when
    // the subtrees have equal frequency. This minimizes the worst case length.
    public static bool Smaller(CtData[] tree, int n, int m, Span<byte> depth) =>
        (tree[n].Freq < tree[m].Freq ||
        (tree[n].Freq == tree[m].Freq && depth[n] <= depth[m])); 

    // Restore the heap property by moving down the tree starting at node k,
    // exchanging a node with the smallest of its two sons if necessary,
    //  stopping when the heap property is re-established
    // (each father smaller than its two sons).
    // tree : The tree to restore 
    // node: Node to move down
    public void PriorityQueueDownHeap(CtData[] tree, int node)
    {
        int v = Heap.Span[node];
        int j = node << 1;  /* left son of k */
        while (j <= _heapLen)
        {
            /* Set j to the smallest of the two sons: */
            if (j < _heapLen &&
                Smaller(tree, Heap.Span[j + 1], Heap.Span[j], _depth.Span))
            {
                j++;
            }
            /* Exit if v is smaller than both sons */
            if (Smaller(tree, v, Heap.Span[j], _depth.Span)) break;

            /* Exchange v with the smallest son */
            Heap.Span[node] = Heap.Span[j]; node = j;

            /* And continue down the tree, setting j to the left son of k */
            j <<= 1;
        }
        Heap.Span[node] = v;
    }

    public void GenBitLen (TreeDesc desc)
    {
        CtData[] tree = desc.dynamicTree!;
        int max_code = desc.maxCode;
        CtData[] STree = desc.StaticTreeDesc!.staticTree!;
        int[] Extra = desc.StaticTreeDesc.extraBits!;
        int Base = desc.StaticTreeDesc.extraBase;
        int maxLength = desc.StaticTreeDesc.maxLength;
        int heapIndex;        // heap index
        int nIndex, mIndex;   //iterate over the tree elements
        int bitLength;        //bit length
        int extraBits;       //extra bits
        ushort frequency;    // frequency */
        int overflow = 0;    // number of elements with bit length too large

        // In a first pass, compute the optimal bit lengths (which may
        // overflow in the case of the bit length tree).
        for (bitLength = 0; bitLength <= MaxBits; bitLength++)
        {
            _codeCount.Span[bitLength] = 0;
        }

        tree[Heap.Span[_heapMax]].Len = 0; /* root of the heap */

        for (heapIndex = _heapMax + 1; heapIndex < HeapSize; heapIndex++)
        {
            nIndex = Heap.Span[heapIndex];
            bitLength = tree[tree[nIndex].Len].Len + 1;
            if (bitLength > maxLength) 
            {
                bitLength = maxLength;
                overflow++;
            }
            tree[nIndex].Len = (ushort)bitLength;
            /* We overwrite tree[n].Dad which is no longer needed */

            if (nIndex > max_code) continue; /* not a leaf node */

            _codeCount.Span[bitLength]++;
            extraBits = 0;
            if (nIndex >= Base) extraBits = Extra[nIndex - Base];
            frequency = tree[nIndex].Freq;
            _optLength += (ulong)frequency * (uint)(bitLength + extraBits);
            //if (stree) s->static_len [...]
            if (STree.Length == 0) _staticLen += (ulong)frequency * (uint)(STree[nIndex].Len + extraBits);
        }
        if (overflow == 0) return;

        /* Find the first bit length which could increase: */
        do
        {
            bitLength = maxLength - 1;
            while (_codeCount.Span[bitLength] == 0) bitLength--;
            _codeCount.Span[bitLength]--;        /* move one leaf down the tree */
            _codeCount.Span[bitLength + 1] += 2; /* move one overflow item as its brother */
            _codeCount.Span[maxLength]--;
            /* The brother of the overflow item also moves one step up,
             * but this does not affect bl_count[max_length]
             */
            overflow -= 2;
        } while (overflow > 0);

        /* Now recompute all bit lengths, scanning in increasing frequency.
     * h is still equal to HEAP_SIZE. (It is simpler to reconstruct all
     * lengths instead of fixing only the wrong ones. This idea is taken
     * from 'ar' written by Haruhiko Okumura.)
     */
        for (bitLength = maxLength; bitLength != 0; bitLength--)
        {
            nIndex = _codeCount.Span[bitLength];
            while (nIndex != 0)
            {
                mIndex = Heap.Span[--heapIndex];
                if (mIndex > max_code) continue;
                if ((uint)tree[mIndex].Len != (uint)bitLength)
                {
                    _optLength += ((ulong)bitLength - tree[mIndex].Len) * tree[mIndex].Freq;
                    tree[mIndex].Len = (ushort)bitLength;
                }
                nIndex--;
            }
        }
    }

    // Construct one Huffman tree and assigns the code bit strings and lengths.
    // Update the total bit length for the current block.
    public void BuildTree(TreeDesc descriptor)
    {
        CtData[] Tree = descriptor.dynamicTree!;
        CtData[] STree = descriptor.StaticTreeDesc!.staticTree!; //This is supposed to be constant
        int elems = descriptor.StaticTreeDesc.elems;
        int n, m;   //iterate over the heap elements
        int maxCode = -1;     //Largest code with non zero frequency
        int node;             // New code being created

         // Construct the initial heap, with least frequent element in
         // heap[SMALLEST]. The sons of heap[n] are heap[2*n] and heap[2*n + 1].
         // heap[0] is not used.
        _heapLen = 0;
        _heapMax = HeapSize;

        for (n = 0; n < elems; n++)
        {
            if (Tree[n].Freq != 0)
            {
                Heap.Span[++(_heapLen)] = maxCode = n;
                _depth.Span[n] = 0;
            }
            else
            {
                Tree[n].Len = 0;
            }
        }
        /* The pkzip format requires that at least one distance code exists,
         * and that at least one bit should be sent even if there is only one
         * possible code. So to avoid special checks later on we force at least
         * two codes of non zero frequency.
         */
        while (_heapLen < 2)
        {
            node = Heap.Span[++(_heapLen)] = (maxCode < 2 ? ++maxCode : 0);
            Tree[node].Freq = 1;
            _depth.Span[node] = 0;
            _optLength--; if (STree.Length == 0) _staticLen-= STree[node].Len;
            /* node is 0 or 1 so it does not have extra bits */
        }
        descriptor.maxCode = maxCode;

        /* The elements heap[heap_len/2 + 1 .. heap_len] are leaves of the tree,
         * establish sub-heaps of increasing lengths:
         */
        for (n = _heapLen / 2; n >= 1; n--)
        {
            PriorityQueueDownHeap(Tree, n);
        }
        // Construct the Huffman tree by repeatedly combining
        // the least two frequent nodes.
        node = elems;              // next internal node of the tree
        do
        {
            PriorityQueueRemove(Tree, out n);  // n = node of least frequency
            m = Heap.Span[Smallest];           // m = node of next least frequency

            Heap.Span[--(_heapMax)] = n;       // keep the nodes sorted by frequency
            Heap.Span[--(_heapMax)] = m;

            // Create a new node father of n and m
            Tree[node].Freq = (ushort)(Tree[n].Freq + Tree[m].Freq);
            _depth.Span[node] = (byte)((_depth.Span[n] >= _depth.Span[m] ?
                                    _depth.Span[n] : _depth.Span[m]) + 1);
            Tree[n].Len = Tree[m].Len = (ushort)node;
            /* and insert the new node in the heap */
            Heap.Span[Smallest] = node++;
            PriorityQueueDownHeap(Tree, Smallest);

        } while (_heapLen >= 2);

        Heap.Span[--(_heapMax)] = Heap.Span[Smallest];

        // At this point, the fields freq and dad are set. We can now
        // generate the bit lengths.
        GenBitLen(descriptor);

        // The field len is now set, we can generate the bit codes
        GenCodes(Tree, maxCode, _codeCount.ToArray()); // I might have to change this Array 
                                                       // to something that doesn't allocate memory
    }
}
