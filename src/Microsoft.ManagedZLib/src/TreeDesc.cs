// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.ManagedZLib;

public class TreeDesc
{
    public CtData[] _dynamicTree;
    public int _maxCode;
    public StaticTreesDesc _StaticTreeDesc;

    public TreeDesc(CtData[] dynamicTree, StaticTreesDesc StaticTreeDesc)
    {
        _dynamicTree = dynamicTree;
        _StaticTreeDesc = StaticTreeDesc;
        _maxCode = 0;
    }

    // Reverse the first len bits of a code, using straightforward code
    // (a faster method would use a table)
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

    //Generate the codes for a given tree and bit counts (which need not be
    //optimal).
    //tree - the tree to decorate 
    //_maxCode - largest code with non zero frequency
    //codeCount - number of codes at each bit length 
    public void GenCodes(Span<ushort> codeCount)
    {
        ushort[] nextCode = new ushort[DeflateTrees.MaxBits + 1]; /* next code value for each bit length */
        uint code = 0;         /* running code value */
        int bits;                  /* bit index */
        int n;                     /* code index */

        // The distribution counts are first used to generate
        // the code values without bit reversal.
        for (bits = 1; bits <= DeflateTrees.MaxBits; bits++)
        {
            code = (code + codeCount[bits - 1]) << 1;
            nextCode[bits] = (ushort)code;
        }

        // Check that the bit counts in code's count (codeCount) are consistent.
        // The last code must be all ones.
        Debug.Assert(code + codeCount[DeflateTrees.MaxBits] - 1 == (1 << DeflateTrees.MaxBits) - 1,
                "inconsistent bit counts");

        for (n = 0; n <= _maxCode; n++)
        {
            int len = _dynamicTree[n].Len;
            if (len == 0) continue;
            /* Now reverse the bits */
            _dynamicTree[n].Freq = (ushort)BitReverse(nextCode[len]++, len);
        }
    }

    // Index within the heap array of least frequent node in the Huffman tree
    public const int Smallest = 1;

    // Remove the smallest element from the heap and recreate the heap with
    // one less element.Updates heap and heap_len.
    public void PriorityQueueRemove(out int removedTop, int[] Heap, ref int heapLen, byte[] depth)
    {
        removedTop = Heap[Smallest];
        Heap[Smallest] = Heap[heapLen--];
        PriorityQueueDownHeap(Smallest, Heap, ref heapLen, depth);
    }

    // Compares to subtrees, using the tree depth as tie breaker when
    // the subtrees have equal frequency. This minimizes the worst case length.
    public static bool Smaller(CtData[] tree, int n, int m, byte[] depth) =>
        (tree[n].Freq < tree[m].Freq ||
        (tree[n].Freq == tree[m].Freq && depth[n] <= depth[m]));

    // Restore the heap property by moving down the tree starting at node (parameter)"node",
    // exchanging a node with the smallest of its two sons if necessary,
    //  stopping when the heap property is re-established
    // (each father smaller than its two sons).
    // tree : The tree to restore 
    // node: Node to move down
    public void PriorityQueueDownHeap(int node, int[] Heap, ref int heapLen, byte[] depth)
    {
        int v = Heap[node];
        int j = node << 1;  /* left son of node */
        while (j <= heapLen)
        {
            /* Set j to the smallest of the two sons: */
            if (j < heapLen &&
                Smaller(_dynamicTree, Heap[j + 1], Heap[j], depth))
            {
                j++;
            }
            /* Exit if v is smaller than both sons */
            if (Smaller(_dynamicTree, v, Heap[j], depth)) break;

            /* Exchange v with the smallest son */
            Heap[node] = Heap[j]; node = j;

            /* And continue down the tree, setting j to the left son of k */
            j <<= 1;
        }
        Heap[node] = v;
    }



    public void GenBitLen(Span<int> Heap,
                          Span<ushort> _codeCount,
                          ref ulong _optLength,
                          ref ulong _staticLen,
                          ref int _heapMax)
    {
        CtData[] tree = _dynamicTree!;
        int max_code = _maxCode;
        CtData[] STree = _StaticTreeDesc.staticTree!;
        int[] Extra = _StaticTreeDesc.extraBits!;
        int Base = _StaticTreeDesc.extraBase;
        int maxLength = _StaticTreeDesc.maxLength;
        int heapIndex;        // heap index
        int nIndex, mIndex;   //iterate over the tree elements
        int bitLength;        //bit length
        int extraBits;       //extra bits
        ushort frequency;    // frequency */
        int overflow = 0;    // number of elements with bit length too large

        // In a first pass, compute the optimal bit lengths (which may
        // overflow in the case of the bit length tree).
        for (bitLength = 0; bitLength <= DeflateTrees.MaxBits; bitLength++)
        {
            _codeCount[bitLength] = 0;
        }

        tree[Heap[_heapMax]].Len = 0; /* root of the heap */

        for (heapIndex = _heapMax + 1; heapIndex < DeflateTrees.HeapSize; heapIndex++)
        {
            nIndex = Heap[heapIndex];
            bitLength = tree[tree[nIndex].Len].Len + 1;
            if (bitLength > maxLength)
            {
                bitLength = maxLength;
                overflow++;
            }
            tree[nIndex].Len = (ushort)bitLength;
            /* We overwrite tree[n].Dad which is no longer needed */

            if (nIndex > max_code) continue; /* not a leaf node */

            _codeCount[bitLength]++;
            extraBits = 0;
            if (nIndex >= Base) extraBits = Extra[nIndex - Base];
            frequency = tree[nIndex].Freq;
            _optLength += (ulong)frequency * (uint)(bitLength + extraBits);
            if (STree != null && STree.Length != 0)
            {
                _staticLen += (ulong)frequency * (uint)(STree[nIndex].Len + extraBits);
            }
        }
        if (overflow == 0) return;

        /* Find the first bit length which could increase: */
        do
        {
            bitLength = maxLength - 1;
            while (_codeCount[bitLength] == 0) bitLength--;
            _codeCount[bitLength]--;        /* move one leaf down the tree */
            _codeCount[bitLength + 1] += 2; /* move one overflow item as its brother */
            _codeCount[maxLength]--;
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
            nIndex = _codeCount[bitLength];
            while (nIndex != 0)
            {
                mIndex = Heap[--heapIndex];
                if (mIndex > max_code) continue;
                if (tree[mIndex].Len != (uint)bitLength)
                {
                    unchecked { _optLength += ((ulong)bitLength - tree[mIndex].Len) * tree[mIndex].Freq; }
                    tree[mIndex].Len = (ushort)bitLength;
                }
                nIndex--;
            }
        }
    }

    // Construct one Huffman tree and assigns the code bit strings and lengths.
    // Update the total bit length for the current block.
    public void BuildTree(int HeapSize,
                         byte[] _depth,
                         ref int _heapLen,
                         ref int _heapMax,
                         ushort[] _codeCount,
                         int[] Heap,
                         ref ulong _optLength,
                         ref ulong _staticLen)
    {
        int n, m;   //iterate over the heap elements
        int maxCode = -1;     //Largest code with non zero frequency
        int node;             // New code being created
        int elems = _StaticTreeDesc.elems;
        // Construct the initial heap, with least frequent element in
        // heap[SMALLEST]. The sons of heap[n] are heap[2*n] and heap[2*n + 1].
        // heap[0] is not used.
        _heapLen = 0;   // number of elements in the heap
        _heapMax = HeapSize;   // element of largest frequency

        for (n = 0; n < elems; n++)
        {
            if (_dynamicTree[n].Freq != 0)
            {
                Heap[++_heapLen] = maxCode = n;
                _depth[n] = 0;
            }
            else
            {
                _dynamicTree[n].Len = 0; // I already set this to 0 in the ctor
                                         // Check if it's really necessary. (Like for reseting the value or something)
            }
        }
        /* The pkzip format requires that at least one distance code exists,
         * and that at least one bit should be sent even if there is only one
         * possible code. So to avoid special checks later on we force at least
         * two codes of non zero frequency.
         */
        while (_heapLen < 2)
        {
            node = Heap[++_heapLen] = (maxCode < 2) ? ++maxCode : 0;
            _dynamicTree[node].Freq = 1;
            _depth[node] = 0;
            _optLength--;
            if (_StaticTreeDesc.staticTree!.Length != 0)
            {
                _staticLen -= _StaticTreeDesc.staticTree![node].Len;
            }
            /* node is 0 or 1 so it does not have extra bits */
        }
        _maxCode = maxCode;

        /* The elements heap[heap_len/2 + 1 .. heap_len] are leaves of the tree,
         * establish sub-heaps of increasing lengths:
         */
        for (n = _heapLen / 2; n >= 1; n--)
        {
            PriorityQueueDownHeap(n, Heap, ref _heapLen, _depth);
        }
        // Construct the Huffman tree by repeatedly combining
        // the least two frequent nodes.
        node = elems;              // next internal node of the tree
        do
        {
            PriorityQueueRemove(out n, Heap, ref _heapLen, _depth);  // n = node of least frequency
            m = Heap[Smallest];           // m = node of next least frequency

            Heap[--_heapMax] = n;       // keep the nodes sorted by frequency
            Heap[--_heapMax] = m;

            // Create a new node father of n and m
            _dynamicTree[node].Freq = (ushort)(_dynamicTree[n].Freq + _dynamicTree[m].Freq);
            _depth[node] = (byte)((_depth[n] >= _depth[m] ?
                                    _depth[n] : _depth[m]) + 1);
            _dynamicTree[n].Len = _dynamicTree[m].Len = (ushort)node;
            /* and insert the new node in the heap */
            Heap[Smallest] = node++;
            PriorityQueueDownHeap(Smallest, Heap, ref _heapLen, _depth);

        } while (_heapLen >= 2);

        Heap[--(_heapMax)] = Heap[Smallest];

        // At this point, the fields freq and dad are set. We can now
        // generate the bit lengths.
        GenBitLen(Heap, _codeCount, ref _optLength, ref _staticLen, ref _heapMax);

        // The field len is now set, we can generate the bit codes
        GenCodes(_codeCount);
    }
}

