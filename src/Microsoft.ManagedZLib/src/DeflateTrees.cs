// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using static Microsoft.ManagedZLib.ManagedZLib;

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

    // The three kinds of block type - This might need to go somewhere else
    public const int StoredBlock = 0;
    public const int StaticTrees = 1;
    public const int DynamicTrees = 2;

    // Classification of possible data types - Not really used in the algorithm's functionality
                                              // But can be used for improvements later
    public const int Binary = 0; // TO-DO enum
    public const int Text = 1;
    public const int Ascii = Text;   /* for compatibility with 1.2.2 and earlier */
    public const int Unknown = 2;

    //To build Huffman tree (dynamic trees)
    CtData[] _dynLitLenTree;   // literal and length tree
    CtData[] _dynDistanceTree; // distance tree
    CtData[] _codesTree;       // Huffman tree for bit lengths

    TreeDesc? _literalDesc;        // Description for literal tree\\
    TreeDesc? _distanceDesc;      // Description for distance tree \\
    TreeDesc? _codeDesc;         // Description for bit length tree \\

    // Number of codes at each bit length for an optimal tree
    public ushort[] _codeCount = new ushort[MaxBits + 1];
    // Depth of each subtree used as tie breaker for trees of equal frequency
    public byte[] _depth = new byte[HeapSize];
    // The sons of heap[n] are heap[2*n] and heap[2*n+1]. heap[0] is not used.
    // The same heap array is used to build all trees.
    public int[] Heap = new int[HeapSize]; // heap used to build the Huffman trees
    int _heapLen;               /* number of elements in the heap */
    int _heapMax;               /* element of largest frequency */


    public Memory<byte> _symBuffer;     // Buffer for distances and literal/lengths
    // For tallying - building a frequency table:
    // Count the ocurrences of a Length-dintance pair
    internal uint _symIndex;   // Index for symBuf - sym_next in C
    internal uint _symEnd;     // symbol table full when symIndex reaches this

    public ulong _optLength;          // bit length of current block with optimal trees
    public ulong _staticLen;       // bit length of current block with static trees
    public uint _matchesInBlock;      // number of string matches in current block
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

        _dynLitLenTree = new CtData[HeapSize];
        _dynDistanceTree = new CtData[2 * DistanceCodes + 1];
        _codesTree = new CtData[2 * BitLengthsCodes + 1];

        int n; /* iterates over tree elements */

        /* Initialize the trees. */
        for (n = 0; n < _dynLitLenTree.Length; n++) 
        { 
            _dynLitLenTree[n] = new CtData() { Freq = 0, Len = 0 }; 
        }
        for (n = 0; n < _dynDistanceTree.Length; n++)
        {
            _dynDistanceTree[n] = new CtData() { Freq = 0, Len = 0 };
        }
            
        for (n = 0; n < _codesTree.Length; n++)
        {
            _codesTree[n] = new CtData() { Freq = 0, Len = 0 };
        }

        _literalDesc = new TreeDesc(_dynLitLenTree, StaticLengthDesc);
        _distanceDesc = new TreeDesc(_dynDistanceTree, StaticDistanceDesc);
        _codeDesc = new TreeDesc(_codesTree, StaticCodeDesc);
        
        _bitBuffer = 0;
        _bitsValid = 0;

        _dynLitLenTree[EndOfBlock].Freq = 1;
        _optLength = _staticLen = 0L;
        _symIndex = _matchesInBlock = 0;
    }

    // Output a byte on the stream.
    public void PutByte(OutputWindow output, byte c)
    {
        output._pendingBuffer.Span[(int)output._pendingBufferBytes++] = c;
    }

    // Output a short LSB first on the stream.
    public void PutShort(OutputWindow output, ushort w)
    {
        PutByte(output, (byte)((w) & 0xff)); //LSB
        PutByte(output, (byte)((ushort)(w) >> 8)); //MSB
    }

    //Put a short in the pending buffer. The 16-bit value is put in MSB order.
    public void PutShortMSB(OutputWindow output, uint w)
    {
        PutByte(output, (byte)((ushort)(w) >> 8)); //MSB
        PutByte(output, (byte)((w) & 0xff)); //LSB
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

    public void SendCode(OutputWindow output,CtData tree) 
        => SendBits(output, tree.Freq, tree.Len); //Freq is Code (In the C Zlib version
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

    // Send one empty static block to give enough lookahead for inflate.
    // This takes 10 bits, of which 7 may remain in the bit buffer.
    public void TreeAlign(OutputWindow output)
    {
        SendBits(output, (int)BlockType.StaticTrees << 1 , 3);
        SendCode(output, StaticTreeTables.StaticLengthTree[EndOfBlock]);
        FlushBits(output);
    }

    // Determine the best encoding for the current block: dynamic trees, static
    // trees or store, and write out the encoded block.
    // buffer: Input block, or NULL if too old
    // storedLen: Length of input block
    // last: If this is the last block or no
    public void FlushBlock(OutputWindow output, Memory<byte> buffer, ulong storedLen, bool last) //1:true - 0:false
    {
        // _optLength: bit length of current block with optimal trees
        // optLenBytes: _optLength in bytes
        ulong optLenBytes;
        // _sdtaticLen: bit length of current block with static trees
        // staticLenBytes: _staticLen in Bytes
        ulong staticLenBytes;
        int maxBitLenIndex = 0;  // Index of last bit length code of non zero freq

        /* Build the Huffman trees unless a stored block is forced */
        if (output._level > 0)
        {

            /* Check if the file is binary or text */
            if (output._dataType == Unknown) //Data type is just used here - It's like a good to know now
                                             // But can be used for improvements per type.
            {
                output._dataType = DetectDataType();
            }

            /* Construct the literal and distance trees */
            _literalDesc!.BuildTree(HeapSize, _depth, ref _heapLen, ref _heapMax, _codeCount,
                Heap, ref _optLength, ref _staticLen);

            _distanceDesc!.BuildTree(HeapSize, _depth, ref _heapLen, ref _heapMax, _codeCount,
                Heap, ref _optLength, ref _staticLen);
            // At this point, opt_len and static_len are the total bit lengths of
            // the compressed block data, excluding the tree representations.

            // Build the bit length tree for the above two trees, and get the index
            // in bl_order of the last bit length code to send.
            maxBitLenIndex = BuildBitLenTree();

            /* Determine the best encoding. Compute the block lengths in bytes. */
            optLenBytes = (_optLength + 3 + 7) >> 3;
            staticLenBytes = (_staticLen + 3 + 7) >> 3;
        }
        else
        {
            Debug.Assert(buffer.Span != null, "lost buf");
            optLenBytes = staticLenBytes = storedLen + 5; /* force a stored block */
        }

        if (storedLen + 4 <= optLenBytes && buffer.Span != null)
        {
            /* 4: two words for the lengths */
             // The test buf != NULL is only necessary if LIT_BUFSIZE > WSIZE.
             // Otherwise we can't have processed more than WSIZE input bytes since
             // the last block flush, because compression would have been
             // successful. If LIT_BUFSIZE <= WSIZE, it is never too late to
             // transform a block into a stored block.
             
            TreeStoredBlock(output, buffer.Span, storedLen, last);

        }
        else if (staticLenBytes == optLenBytes)
        {
            SendBits(output, ((int)BlockType.StaticTrees << 1) + (last ? 1 : 0), 3);
            CompressBlock(output, StaticTreeTables.StaticLengthTree, StaticTreeTables.StaticDistanceTree);
        }
        else
        {
            SendBits(output, ((int)BlockType.DynamicTrees << 1) + (last ? 1 : 0), 3);
            SendAllTrees(output, _literalDesc!._maxCode + 1, _distanceDesc!._maxCode + 1,
                           maxBitLenIndex + 1);
            CompressBlock(output, _dynLitLenTree, _dynDistanceTree);
        }

        InitBlock();

        if (last)
        {
            BitWindUp(output);
        }
    }

    // Scan a literal or distance tree to determine the frequencies
    // of the codes in the bit length tree.
    // Input: Tree* to be scanned and its largest (max) code of non zero frequency
    public void ScanTree(CtData[] tree, int maxCode)
    {
        int n;                     // iterates over all tree elements 
        int prevlen = -1;          // last emitted length 
        int curlen;                // length of current code 
        int nextlen = tree[0].Len; // length of next code 
        int count = 0;             // repeat count of the current code 
        int maxCount = 7;          // max repeat count 
        int minCount = 4;          // min repeat count 

        if (nextlen == 0) 
        {
            maxCount = 138;
            minCount = 3;
        } 
        tree[maxCode + 1].Len = (ushort)0xffff; // guard 

        for (n = 0; n <= maxCode; n++)
        {
            curlen = nextlen;
            nextlen = tree[n + 1].Len;
            if (++count < maxCount && curlen == nextlen)
            {
                continue;
            }
            else if (count < minCount)
            {
                _codesTree[curlen].Freq += (ushort)count;
            }
            else if (curlen != 0)
            {
                if (curlen != prevlen) _codesTree[curlen].Freq++;
                _codesTree[Rep3To6].Freq++;
            }
            else if (count <= 10)
            {
                _codesTree[Rep3To10].Freq++;
            }
            else
            {
                _codesTree[Rep11To138].Freq++;
            }
            count = 0; prevlen = curlen;
            if (nextlen == 0)
            {
                maxCount = 138;
                minCount = 3;
            }
            else if (curlen == nextlen)
            {
                maxCount = 6;
                minCount = 3;
            }
            else
            {
                maxCount = 7;
                minCount = 4;
            }
        }
    }

    // Construct the Huffman tree for the bit lengths and return the index in
    // bl_order of the last bit length code to send.
    public int BuildBitLenTree()
    {
        int maxBLenIndex;  // index of last bit length code of non zero freq

        // Determine the bit length frequencies for literal and distance trees
        ScanTree(_dynLitLenTree, _literalDesc!._maxCode);
        ScanTree(_dynDistanceTree, _distanceDesc!._maxCode);

        // Build the bit length tree:
        _codeDesc!.BuildTree(HeapSize, _depth, ref _heapLen, ref _heapMax, _codeCount,
            Heap, ref _optLength, ref _staticLen);
        /* opt_len now includes the length of the tree representations, except the
         * lengths of the bit lengths codes and the 5 + 5 + 4 bits for the counts.
         */

         // Determine the number of bit length codes to send. The pkzip format
         // requires that at least 4 bit length codes be sent. (appnote.txt says
         // 3 but the actual value used is 4.)
         
        for (maxBLenIndex = BitLengthsCodes - 1; maxBLenIndex >= 3; maxBLenIndex--)
        {
            if (_codesTree[CodeOrder[maxBLenIndex]].Len != 0) break;
        }
        // Update opt_len to include the bit length tree and counts 
        _optLength += 3 * ((ulong)maxBLenIndex + 1) + 5 + 5 + 4;

        return maxBLenIndex;
    }

    // Send the header for a block using dynamic Huffman trees: the counts, the
    // lengths of the bit length codes, the literal tree and the distance tree.
    public void SendAllTrees(OutputWindow output, int LitCodes, int DistCodes, int BitLenCodes) // numb of codes for each tree
    {
        int rank;                    // index in bl_order or codesOrder 

        Debug.Assert(LitCodes >= 257 && DistCodes >= 1 && BitLenCodes >= 4, "not enough codes");
        Debug.Assert(LitCodes <= LitLenCodes && DistCodes <= DistanceCodes && BitLenCodes <= BitLengthsCodes,
                "too many codes");

        SendBits(output, LitCodes - 257, 5);  /* not +255 as stated in appnote.txt */
        SendBits(output, DistCodes - 1, 5);
        SendBits(output, BitLenCodes - 4, 4);  /* not -3 as stated in appnote.txt */
        for (rank = 0; rank < BitLenCodes; rank++)
        {
            SendBits(output, _codesTree[CodeOrder[rank]].Len, 3);
        }
        SendTree(output, _dynLitLenTree, LitCodes - 1);  /* literal tree */

        SendTree(output, _dynDistanceTree, DistCodes - 1);  /* distance tree */
    }

    public void SendTree(OutputWindow output, CtData[]Tree, int maxCode)
    {
        int n;                     // iterates over all tree elements 
        int prevlen = -1;          // last emitted length 
        int curlen;                // length of current code 
        int nextlen = Tree[0].Len; // length of next code 
        int count = 0;             // repeat count of the current code 
        int maxCount = 7;          // max repeat count 
        int minCount = 4;          // min repeat count 

        if (nextlen == 0)
        {
            maxCount = 138;
            minCount = 3;
        }
        for (n = 0; n <= maxCode; n++)
        {
            curlen = nextlen; nextlen = Tree[n + 1].Len;
            if (++count < maxCount && curlen == nextlen)
            {
                continue;
            }
            else if (count < minCount)
            {
                do { SendCode(output, _codesTree[curlen]); } while (--count != 0);

            }
            else if (curlen != 0)
            {
                if (curlen != prevlen)
                {
                    SendCode(output, _codesTree[curlen]); count--;
                }
                Debug.Assert(count >= 3 && count <= 6, " 3_6?");
                SendCode(output,_codesTree[Rep3To6]);
                SendBits(output, count - 3, 2);

            }
            else if (count <= 10)
            {
                SendCode(output, _codesTree[Rep3To10]);
                SendBits(output, count - 3, 3);

            }
            else
            {
                SendCode(output, _codesTree[Rep11To138]);
                SendBits(output, count - 11, 7);
            }
            count = 0; prevlen = curlen;
            if (nextlen == 0)
            {
                maxCount = 138;
                minCount = 3;
            }
            else if (curlen == nextlen)
            {
                maxCount = 6;
                minCount = 3;
            }
            else
            {
                maxCount = 7;
                minCount = 4;
            }
        }
    }

    // Send a stored block
    public void TreeStoredBlock(OutputWindow output, Span<byte> buffer, ulong storedLen, bool last)
    {
        SendBits(output, (StoredBlock << 1) + (last ? 1 : 0), 3);  // send block type 
        BitWindUp(output);        /* align on byte boundary */
        PutShort(output, (ushort)storedLen);
        PutShort(output, (ushort)~storedLen);
        if (storedLen != 0)
        {
            //Amount to copy to pendingBuff is storedLen
            Span<byte> destBuffer = output._pendingBuffer.Span.Slice((int)output._pendingBufferBytes, (int)storedLen);
            buffer.Slice(0, (int)storedLen).CopyTo(destBuffer);
            output._pendingBuffIndex += (uint)output._pendingBufferBytes;
        }
        output._pendingBufferBytes += storedLen;
    }

    // Send the block data compressed using the given Huffman trees
    public void CompressBlock(OutputWindow output, CtData[] LitTree, CtData[] DistTree)
    {
        int dist;          // distance of matched string
        int lc;             // match length or unmatched char (if dist == 0)
        uint symIndex = 0;  // running index in sym_buf
        uint code;          // the code to send
        int extra;          // number of extra bits to send
        if (_symIndex != 0)
        {
            do
            {
                dist = _symBuffer.Span[(int)symIndex++] & 0xff;
                dist += (_symBuffer.Span[(int)symIndex++] & 0xff) << 8;
                lc = _symBuffer.Span[(int)symIndex++];
                if (dist == 0)
                {
                    SendCode(output, LitTree[lc]); // send a literal byte
                }
                else
                {
                    /* Here, lc is the match length - MIN_MATCH */
                    code = StaticTreeTables.LengthCode[lc];
                    SendCode(output, LitTree[code + Literals + 1]);   // send length code
                    extra = ExtraLengthBits[(int)code];
                    if (extra != 0)
                    {
                        lc -= StaticTreeTables.baseLength[code];
                        SendBits(output, lc, extra);       // send the extra length bits
                    }
                    dist--; /* dist is now the match distance - 1 */
                    code = GetDistCode((ushort)dist);
                    Debug.Assert(code < DistanceCodes, "bad DistanceCode");

                    SendCode(output, DistTree[code]);       // send the distance code
                    extra = ExtraDistanceBits[(int)code];
                    if (extra != 0)
                    {
                        dist -= StaticTreeTables.baseLDistance[code];
                        SendBits(output, (int)dist, extra);   /* send the extra distance bits */
                    }
                } /* literal or match pair ? */

                /* Check that the overlay between pending_buf and sym_buf is ok: */
                Debug.Assert(output._pendingBufferBytes < output._litBufferSize + symIndex, "pendingBuf overflow");

            } while (symIndex < _symIndex);
        }

        SendCode(output, LitTree[EndOfBlock]);
    }
    /* ===========================================================================
     * Check if the data type is TEXT or BINARY, using the following algorithm:
     * - TEXT if the two conditions below are satisfied:
     *    a) There are no non-portable control characters belonging to the
     *       "block list" (0..6, 14..25, 28..31).
     *    b) There is at least one printable character belonging to the
     *       "allow list" (9 {TAB}, 10 {LF}, 13 {CR}, 32..255).
     * - BINARY otherwise.
     * - The following partially-portable control characters form a
     *   "gray list" that is ignored in this detection algorithm:
     *   (7 {BEL}, 8 {BS}, 11 {VT}, 12 {FF}, 26 {SUB}, 27 {ESC}).
     * IN assertion: the fields Freq of dyn_ltree are set.
     */
    public int DetectDataType()
    {
        /* block_mask is the bit mask of block-listed bytes
     * set bits 0..6, 14..25, and 28..31
     * 0xf3ffc07f = binary 11110011111111111100000001111111
     */
        ulong blockMask = 0xf3ffc07fUL;
        int n;

        /* Check for non-textual ("block-listed") bytes. */
        for (n = 0; n <= 31; n++, blockMask >>= 1)
            if ((blockMask & 1)!=0 && (_dynLitLenTree[n].Freq != 0))
                return Binary;

        /* Check for textual ("allow-listed") bytes. */
        if (_dynLitLenTree[9].Freq != 0 || _dynLitLenTree[10].Freq != 0
                || _dynLitLenTree[13].Freq != 0)
            return Text;
        for (n = 32; n < Literals; n++)
            if (_dynLitLenTree[n].Freq != 0)
                return Text;

        /* There are no "block-listed" or "allow-listed" bytes:
         * this stream either is empty or has tolerated ("gray-listed") bytes only.
         */
        return Binary;
    }
    
}
