// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.ManagedZLib;

/// <summary>
/// This class maintains a window for decompressed output.
/// We need to keep this because the decompressed information can be
/// a literal or a length/distance pair. For length/distance pair,
/// we need to look back in the output window and copy bytes from there.
/// We use a byte array of WindowSize circularly.
/// </summary>
internal class OutputWindow
{
    public int _windowSize;
    private int _windowMask;
    private byte[] _window; // The window is 2^n bytes where n is number of bits
    private int _lastIndex; // Position to where we should write next byte
    private int _bytesUsed; // Number of bytes in the output window that haven't been consumed yet.
    public byte Window(uint bit) => _window[bit];
    
    public const int CodesMaxBit = 15; // All codes must not exceed MaxBit bits
    //Min&Max matching lengths.
    public const int MinMatch = 3;
    public const int MaxMatch = 258;
    public const int MinLookahead = MaxMatch + MinMatch + 1;
    public uint _insert; // ----------------------------------------Se usa para FillWindow

    // To speed up deflation, hash chains are never searched beyond this
    // length.  A higher limit improves compression ratio but degrades the speed.
    public uint MaxChainLength;
    // Attempt to find a better match only when the current match is strictly
    // smaller than this value. This mechanism is used only for compression levels >= 4.
    public uint MaxLazyMatch;
    // Insert new strings in the hash table only if the match length is not
    // greater than this length. This saves time but degrades compression.
    // max_insert_length is used only for compression levels <= 3.
    public uint MaxInsertLength() => MaxLazyMatch;

    //Window position at the beginning of the current output block. Gets
    //negative when the window is moved backwards.
    long blockStart; //This might be AvailOut
    public int _method; // It can just be deflate and it might not be used
                 // anywhere else but in init for error checking.
                 // Putting it just it case, might delete later.

    public int _strHashIndex;  // hash index of string to be inserted 
    public int _hashSize;      // number of elements in hash table 
    public int _hashBits;      // log2(hash_size)
    public int _hashMask;      // hash_size-1
    ushort[]? _hashHead; // The window is 2^n bytes where n is number of bits
    ushort[]? _prev; // The window is 2^n bytes where n is number of bits

    //Number of bits by which ins_h must be shifted at each input
    //step. It must be such that after MIN_MATCH steps, the oldest
    //byte no longer takes part in the hash key, that is:
    //hash_shift * MIN_MATCH >= hash_bits
    public int _hashShift;

    // Minimum amount of lookahead, except at the end of the
    // input file, then MIN_MATCH+1.
    public uint _matchLength;           // length of best match 
    public byte _prevMatch;             // previous match 
    public int _matchAvailable;         // set if previous match exists 
    public uint _strStart;              // start of string to insert 
    public uint _matchStart;            // start of matching string 
    public uint _lookahead;             // number of valid bytes ahead in window 
    public uint _prevLength; //Length of the best match at previous step. Matches not greater than this
                             //are discarded.This is used in the lazy match evaluation.
 
    public int _niceMatch;  // Stop searching when current match exceeds this 
    public int _goodMatch;
    public ulong _highWater; 

    
    public Memory<byte> _pendingBuffer; //Output still pending
    public Memory<byte> _pendingOut;    //Next pending byte to output to the stream
    public uint _litBufferSize; 
    public ulong _penBufferSize;       //Size of pending buffer
    public ulong _pedingBufferBytes;   //number of bytes in pending buffer

    /// <summary>
    /// The constructor will recieve the window bits and with that construct the 
    /// output window. In theory, the Output window is divided in 2 parts, 
    /// depending in the type of deflate, each one of 32K and 64K.
    ///     +---------+---------+
    ///     | 32K/64K | 32K/64K | Giving a total of 64K/128K sliding window
    ///     +---------+---------+
    /// The decompressed input would go to the second part. 
    /// This class represents the first part: 
    /// A search buffer for the length/distance pair to look back.
    /// </summary>
    internal OutputWindow(int windowBits)
    {
        _windowSize = 1 << windowBits; //logaritmic base 2
        _windowMask = _windowSize - 1;
        _window = new byte[_windowSize];
    }
    // With Deflate64 we can have up to a 65536 length as well as up to a 65538 distance. This means we need a Window that is at
    // least 131074 bytes long so we have space to retrieve up to a full 64kb in lookback and place it in our buffer without
    // overwriting existing data. OutputBuffer requires that the WindowSize be an exponent of 2, so we round up to 2^18.
    internal OutputWindow() //deflate64
    {
        _windowSize = 262144;
        _windowMask = 262143;
        _window = new byte[_windowSize];
    }
    internal OutputWindow(int windowBits, int memLevel)
    {
        _windowSize = 1 << windowBits; //logaritmic base 2
        _windowMask = _windowSize - 1;
        _window = new byte[_windowSize];
        _prev = new ushort[_windowSize];

        _hashBits = memLevel + 7;
        _hashSize = 1 << _hashBits;
        _hashMask = _hashSize - 1;
        _strHashIndex = 0;
        // MinMatch = 3 , MaxMatch = 258 for Lengths
        _hashShift = ((_hashBits + MinMatch - 1) / MinMatch);
        _hashHead = new ushort[_hashSize];

        _highWater = 0; //Nothing written to the _window yet
        _litBufferSize = 1U << (memLevel+6); //16K by default
        _penBufferSize = (ulong)_litBufferSize * 4;
        _pendingBuffer = new byte[_penBufferSize];
    }
    //Update a hash value with the given input byte
    public void UpdateHash(byte inputByte)
    {
        _strHashIndex = ((_strHashIndex << _hashShift) ^ inputByte) & _hashMask;
    }

    //Insert string str in the dictionary and set match_head to the previous head
    //of the hash chain (the most recent string with same hash key). Return
    //the previous length of the hash chain.
    //If this file is compiled with -DFASTEST, the compression level is forced
    //to 1, and no hash chains are maintained.
    //IN  assertion: all calls to INSERT_STRING are made with consecutive input
    //characters and the first MIN_MATCH bytes of str are valid (except for
    //the last MIN_MATCH-1 bytes of the input file).
    public ushort InsertString(uint strStart)
    {
        ushort match_head;
        Debug.Assert(_hashHead != null);
        Debug.Assert(_prev != null);
        UpdateHash(_window[strStart + (MinMatch - 1)]);
        match_head = _prev[strStart & _windowMask] = _hashHead[_strHashIndex];
        _hashHead[_strHashIndex] = (ushort)strStart;
        return match_head;
    }

    public uint LongestMatch(uint currHashHead) 
    {
        Debug.Assert(_window != null);
        uint chainLength = MaxChainLength;
        Span<byte> scan = _window.AsSpan((int)_strStart); //Initial scan point
        Span<byte> match;
        int len;
        int bestLength = (int)_prevLength;
        int niceMatch = _niceMatch;
        /* Stop when cur_match becomes <= limit. To simplify the code,
        * we prevent matches with the string of window index 0.
        */
        uint limit = (_strStart > (uint)MaxDistance())? _strStart - (uint)MaxDistance(): 0;
        ushort[]? prev = _prev;
        int wMask = _windowMask;
        // The code is optimized for HASH_BITS >= 8 and MAX_MATCH-2 multiple of 16.
        // It is easy to get rid of this optimization if necessary.
        Debug.Assert(_hashBits >= 8 && MaxMatch == 258, "Code too clever");
        /* Do not waste too much time if we already have a good match: */
        if (_prevLength >= _goodMatch)
        {
            chainLength >>= 2;
        }
        //Do not look for matches beyond the end of the input. This is necessary
        // to make deflate deterministic.
        if ((uint)niceMatch > _lookahead) 
        { 
            niceMatch = (int)_lookahead; 
        }

        Debug.Assert((ulong)_strStart <= (ulong)_windowSize - MinLookahead, "need lookahead");

        do
        {
            Debug.Assert(currHashHead < _strStart, "no future");
            match = _window.AsSpan((int)currHashHead);         
            len = scan.CommonPrefixLength(match);

            if (len > bestLength)
            {
                _matchStart = currHashHead;
                bestLength = len;
                if (len >= niceMatch)
                {
                    break;
                }
            }

        } while ((currHashHead = prev![currHashHead & wMask]) > limit && (--chainLength != 0));

        if ((uint)bestLength <= _lookahead) return (uint)bestLength;

        return _lookahead;
    }

    internal void ClearBytesUsed() => _bytesUsed = 0;
    public int MaxDistance() => _windowSize - MinLookahead;
    /// <summary>Add a byte to output window.</summary>
    public void Write(byte b)
    {
        Debug.Assert(_bytesUsed < _windowSize, "Can't add byte when window is full!");
        _window[_lastIndex++] = b;
        _lastIndex &= _windowMask;
        ++_bytesUsed;
    }

    //Important part of LZ77 algorithm
    public void WriteLengthDistance(int length, int distance)
    {
        //Checking there's enough space for copying the output
        Debug.Assert((_bytesUsed + length) <= _windowSize, "No Enough space");

        // Move backwards distance bytes in the output stream,
        // and copy length bytes from this position to the output stream.
        _bytesUsed += length;
        int copyStart = (_lastIndex - distance) & _windowMask;

        // Total space that would be taken by copying the length bytes
        int border = _windowSize - length;
        if (copyStart <= border && _lastIndex < border)
        {
            if (length <= distance)
            {
                //Copying into the look-ahead buffer (where the decompressed input is stored)
                Array.Copy(_window, copyStart, _window, _lastIndex, length);
                _lastIndex += length;
            }
            else
            {
                // The referenced string may overlap the current
                // position; for example, if the last 2 bytes decoded have values
                // X and Y, a string reference with <length = 5, distance = 2>
                // adds X,Y,X,Y,X to the output stream.
                while (length-- > 0)
                {
                    _window[_lastIndex++] = _window[copyStart++];
                }
            }
        }
        else
        {
            // Copy byte by byte
            while (length-- > 0)
            {
                _window[_lastIndex++] = _window[copyStart++];
                _lastIndex &= _windowMask;
                copyStart &= _windowMask;
            }
        }
    }

    /// <summary>
    /// Copy up to length of bytes from input directly.
    /// This is used for uncompressed block, after passing through Decode().
    /// </summary>
    public int CopyFrom(InputBuffer input, int length)
    {
        /// <summary> 
        /// Either how much input is available or how much free space in the output buffer we have. 
        /// length = Amount of decompressed that we are trying to put in the output window.
        /// It will lead us to either copy LEN bytes or just the amount available in the output window
        // taking into account the byte boundaries.
        /// </summary>
        length = Math.Min(Math.Min(length, _windowSize - _bytesUsed), input.AvailableBytes);
        int copied;

        // We might need wrap around to copy all bytes.
        int spaceLeft = _windowSize - _lastIndex;
        if (length > spaceLeft) //Checking is within the boundaries
        {
            // Copy the first part
            copied = input.CopyTo(_window, _lastIndex, spaceLeft);
            if (copied == spaceLeft)
            {
                // Only try to copy the second part if we have enough bytes in input
                copied += input.CopyTo(_window, 0, length - spaceLeft);
            }
        }
        else
        {
            // Only one copy is needed if there is no wrap around.
            copied = input.CopyTo(_window, _lastIndex, length);
        }

        _lastIndex = (_lastIndex + copied) & _windowMask;
        _bytesUsed += copied;
        return copied; 
    }

    /// <summary>Free space in output window.</summary>
    public int FreeBytes => _windowSize - _bytesUsed;

    /// <summary>Bytes not consumed in output window.</summary>
    public int AvailableBytes => _bytesUsed;

    /// <summary>Copy the decompressed bytes to output buffer.</summary>
    public int CopyTo(Span<byte> usersOutput)
    {
        int copy_lastIndex;

        if (usersOutput.Length > _bytesUsed)
        {
            // We can copy all the decompressed bytes out
            copy_lastIndex = _lastIndex; //Last index auxiliar
            usersOutput = usersOutput.Slice(0,_bytesUsed);
        }
        else
        {
            // Copy length of bytes
            copy_lastIndex = (_lastIndex - _bytesUsed + usersOutput.Length) & _windowMask;
        }

        int copied = usersOutput.Length;

        int spaceLeft = usersOutput.Length - copy_lastIndex;
        if (spaceLeft > 0)
        {
            // this means we need to copy two parts separately
            // copy the spaceLeft-bytes from the end of the output window
            _window.AsSpan(_windowSize - spaceLeft, spaceLeft).CopyTo(usersOutput);
            usersOutput = usersOutput.Slice(spaceLeft, copy_lastIndex);
        }
        _window.AsSpan(copy_lastIndex - usersOutput.Length, usersOutput.Length).CopyTo(usersOutput);
        _bytesUsed -= copied;
        Debug.Assert(_bytesUsed >= 0, "check this function and find why we copied more bytes than we have");
        return copied;
    }
}

