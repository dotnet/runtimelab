// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using ZFlushCode = Microsoft.ManagedZLib.ManagedZLib.FlushCode;
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
    public int _availableOutput; //length of output buffer
    public Memory<byte> _output; // NextOut in madler/zlib
    public uint _nextOut;
    public ulong _adler; //Adler-32 or CRC-32 value of the uncompressed data
    public ulong _totalOutput;
    
    public ZFlushCode lastFlush; // value of flush param for previous deflate call

    public int _windowSize; // TO-DO: Change _windowSize from int to uint
    public ulong _actualWindowSize;
    private int _windowMask;
    //For security reasons, this might be better as private
    public Memory<byte> _window; // The window is 2^n bytes where n is number of bits
    private int _lastIndex; // Position to where we should write next byte
    private int _bytesUsed; // Number of bytes in the output window that haven't been consumed yet.
    public byte Window(int bit) => _window.Span[bit];
    public int WindowSize() => _windowSize;

    public const int CodesMaxBit = 15; // All codes must not exceed MaxBit bits
    //Min&Max matching lengths.
    public const int MinMatch = 3;
    public const int MaxMatch = 258;
    public const int MinLookahead = MaxMatch + MinMatch + 1;
    public uint _insert; // bytes at end of window left to insert
    public int _level;
    public ManagedZLib.CompressionStrategy _strategy;
    public int _dataType;  // best guess about the data type: binary or text

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
    public int WinInit() => MaxMatch;
    public Memory<byte> Buffer
    {
        get { return _output; }
        set { _output = value; }
    }
    //Window position at the beginning of the current output block. Gets
    //negative when the window is moved backwards.
    public long _blockStart; //This might be AvailOut
    public int _method; // It can just be deflate and it might not be used
                        // anywhere else but in init for error checking.
                        // Putting it just it case, might delete later.

    public int _strHashIndex;  // hash index of string to be inserted 
    public int _hashSize;      // number of elements in hash table 
    public int _hashBits;      // log2(hash_size)
    public int _hashMask;      // hash_size-1
    // Heads of the hash chains or NIL.
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
    public uint _prevMatch;             // previous match 
    public bool _matchAvailable;         // set if previous match exists 
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
    public ulong _pendingBufferBytes;   //number of bytes in pending buffer

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
    internal OutputWindow(int windowBits, int memLevel, ManagedZLib.CompressionLevel level)
    {
        _windowSize = 1 << windowBits; //logaritmic base 2
        _windowMask = _windowSize - 1;
        _window = new byte[_windowSize*2];
        _prev = new ushort[_windowSize];

        _hashBits = memLevel + 7;
        _hashSize = 1 << _hashBits;
        _hashMask = _hashSize - 1;
        _strHashIndex = 0;
        // MinMatch = 3 , MaxMatch = 258 for Lengths
        _hashShift = ((_hashBits + MinMatch - 1) / MinMatch);
        _hashHead = new ushort[_hashSize];

        _highWater = 0; //Nothing written to the _window yet
        _litBufferSize = 1U << (memLevel + 6); //16K by default
        _penBufferSize = (ulong)_litBufferSize * 4;
        _pendingBuffer = new byte[_penBufferSize];
        longestMatchInit(level);
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
    public ushort InsertString(int strStart)
    {
        ushort match_head;
        Debug.Assert(_hashHead != null);
        Debug.Assert(_prev != null);
        UpdateHash(_window.Span[strStart + (MinMatch - 1)]);
        match_head = _prev[strStart & _windowMask] = _hashHead[_strHashIndex];
        _hashHead[_strHashIndex] = (ushort)strStart;
        return match_head;
    }   

    // Fill the window when the lookahead becomes insufficient.
    // Updates strstart and lookahead.
    public void FillWindow(InputBuffer inputBuffer) //more: If space is needed, how much more
    {
        uint bytes; // n
        int availSpaceEnd; //Amount of free space at the end of the window
        int wsize = _windowSize;
        Debug.Assert(_lookahead < MinLookahead, "Already enough lookahead");

        do
        {
            availSpaceEnd = (int)(_actualWindowSize - _lookahead - _strStart); // It shouldn't be that large

            /* If the window is almost full and there is insufficient lookahead,
            * move the upper half to the lower one to make room in the upper half.
            */
            if (_strStart >= wsize + MaxDistance())
            {
                var upperHalf = _window.Slice(wsize); // Creo que en un init se duplica el tamanio de la ventana
                                                              // Checar inicializacion
                                                              // Creo que WindowSize debe tener el tamanio solo de una mitad 32K
                var from = _window.Slice(wsize, (int)wsize - availSpaceEnd);
                from.CopyTo(upperHalf);
                _matchStart -= (uint)wsize;
                _strStart -= (uint)wsize; /* we now have strstart >= MAX_DIST */
                _blockStart -= (long)wsize;
                if (_insert > _strStart)
                    _insert = _strStart;
                SlideHash();
                availSpaceEnd += wsize;
            }

            if (inputBuffer.AvailableBytes == 0) break;
            // This return number of bytes read
            bytes = ReadBuffer(inputBuffer, _window.Slice((int)_strStart,(int)_lookahead).Span, (uint)availSpaceEnd);
            _lookahead += (uint)bytes;

            /* Initialize the hash value now that we have some input: */
            if (_lookahead + _insert >= MinMatch)
            {
                uint str = _strStart - _insert;
                _strHashIndex = _window.Span[(int)str];
                UpdateHash(_window.Span[(int)str + 1]);
                while (_insert != 0)
                {
                    UpdateHash(_window.Span[(int)str + MinMatch - 1]);
                    _prev![str & _windowMask] = _hashHead![_strHashIndex];
                    _hashHead[_strHashIndex] = (ushort)str;
                    str++;
                    _insert--;
                    if (_lookahead + _insert < MinMatch)
                        break;
                }
            }
        } while (_lookahead < MinLookahead && inputBuffer.AvailableBytes != 0);

        if (_highWater < _actualWindowSize) //Migth change windowSize to ulong
        {
            ulong curr = _strStart + (ulong)(_lookahead);
            ulong init;

            if (_highWater < curr)
            {
                /* Previous high water mark below current data -- zero WIN_INIT
                 * bytes or up to end of window, whichever is less.
                 */
                init = _actualWindowSize - curr;
                if (init > (ulong)WinInit())
                    init = (ulong)WinInit();
                // Zeroing from curr
                _window.Span.Slice((int)curr).Clear();
                //Array.Clear(_window.ToArray(), 0, (int)init);// Clear without allocating an array - check howto

                _highWater = curr + init;
            }
            else if (_highWater < (ulong)curr + (ulong)WinInit())
            {
                /* High water mark at or above current data, but below current data
                 * plus WIN_INIT -- zero out to current data plus WIN_INIT, or up
                 * to end of window, whichever is less.
                 */
                init = (ulong)curr + (ulong)WinInit() - _highWater;
                if (init > _actualWindowSize - _highWater)
                    init = _actualWindowSize - _highWater;
                // Zeroing from _highWater
                _window.Span.Slice((int)_highWater, (int)init).Clear(); // Slice the window from _highWater
                _highWater += init;
            }
        }
        Debug.Assert(_strStart <= _actualWindowSize - MinLookahead,
           "not enough room for search");
    }

    // ---- ASK WHICH ONES ARE USED - It has to be at least 1 of deflate_fast, 1 of deflate slow and stored
    // Variables initialized depending on the level of compression
    //configuration_table = 
    ///*      good lazy nice chain */
    ///* 0 */ {0,    0,  0,    0, deflate_stored},  /* store only */
    ///* 1 */ {4,    4,  8,    4, deflate_fast}, /* max speed, no lazy matches */
    ///* 2 */ {4,    5, 16,    8, deflate_fast},
    ///* 3 */ {4,    6, 32,   32, deflate_fast},

    ///* 4 */ {4,    4, 16,   16, deflate_slow},  /* lazy matches */
    ///* 5 */ {8,   16, 32,   32, deflate_slow},
    ///* 6 */ {8,   16, 128, 128, deflate_slow}, -- Default
    ///* 7 */ {8,   32, 128, 256, deflate_slow},
    ///* 8 */ {32, 128, 258, 1024, deflate_slow},
    ///* 9 */ {32, 258, 258, 4096, deflate_slow} /* max compression */
    public void longestMatchInit(ManagedZLib.CompressionLevel level)
    {
        _actualWindowSize = 2L * (ulong)_windowSize;
        ClearHash(_hashHead); //TO-DO

        switch (level)
        {
            case ManagedZLib.CompressionLevel.DefaultCompression: // equivalent as level 6 in the config table
                _goodMatch = 8;
                MaxLazyMatch = 16;
                _niceMatch = 128;
                MaxChainLength = 128;
                break;

            case ManagedZLib.CompressionLevel.BestSpeed:
                _goodMatch = 4;
                MaxLazyMatch = 4;
                _niceMatch = 8;
                MaxChainLength = 4;
                break;

            case ManagedZLib.CompressionLevel.NoCompression:
                _goodMatch = 0;
                MaxLazyMatch = 0;
                _niceMatch = 0;
                MaxChainLength = 0;
                break;

            case ManagedZLib.CompressionLevel.BestCompression:
                _goodMatch = 32;
                MaxLazyMatch = 258;
                _niceMatch = 258;
                MaxChainLength = 4096;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level));
        }

        _strStart = 0;
        _blockStart = 0L;
        _lookahead = 0;
        _insert = 0;
        _matchLength = _prevLength = MinMatch - 1;
        _matchAvailable = false;
        _strHashIndex = 0;
    }

    private void ClearHash(Span<ushort> hash)
    {
        hash[_hashSize - 1] = ManagedZLib.NIL;
        hash.Slice(0, _hashSize - 1).Clear();
    }

    public uint LongestMatch(uint currHashHead) 
    {
        Debug.Assert(!_window.IsEmpty);
        uint chainLength = MaxChainLength;
        Span<byte> scan = _window.Slice((int)_strStart).Span; //Initial scan point
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

        Debug.Assert((ulong)_strStart <= (ulong)_actualWindowSize - MinLookahead, "need lookahead");

        do
        {
            Debug.Assert(currHashHead < _strStart, "no future");
            match = _window.Slice((int)currHashHead).Span;         
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
    public uint ReadBuffer(InputBuffer input, Span<byte> buffer, uint sizeRequested) 
    {
        uint Length = input._availInput;
        if (Length > sizeRequested) 
        {
            Length = sizeRequested; // All bytes requested, copied
        }
        if (Length < 0)
        { 
            return 0;  // No available input - Nothing is copied
        }

        input._availInput -= Length;

        // If inputBuffer length is less than sizeRequested, we copy inputBuffer length
        Span<byte> bytesToBeCopied = input._inputBuffer.Span.Slice((int)input._nextIn, (int)Length);
        Debug.Assert(buffer.Length > 0, buffer.Length.ToString() + " VS bytesCopied: " + bytesToBeCopied.Length.ToString());
        bytesToBeCopied.CopyTo(buffer);

        if (input._wrap == 1) //ZLib header
        {
            Adler32(buffer, Length);
        }
        else if (input._wrap == 2) // Gzip header
        {
            CRC32(buffer, Length);
        }

        input._nextIn += Length; //Moving index forward for future block copies
        input._totalInput += Length;

        return Length;
    }

    /* ===========================================================================
     * Slide the hash table when sliding the window down (could be avoided with 32
     * bit values at the expense of memory usage). We slide even when level == 0 to
     * keep the hash table consistent if we switch back to level > 0 later.
     */
    public void SlideHash()
    {
        int n, m;
        uint wsize = (uint)WindowSize();
        n = _hashSize;
        ushort[] p = new ushort[n];
        p = _hashHead!;
        do
        {
            m = p[--n];
            p[n] = (ushort)(m >= wsize ? m - wsize : ManagedZLib.NIL);
        } while (n > 0);
        n = (int)wsize;
        p = _prev!;
        do
        {
            m = p[--n];
            p[n] = (ushort)(m >= wsize ? m - wsize : ManagedZLib.NIL);
            /* If n is not on any hash chain, prev[n] is garbage but
             * its value will never be used.
             */
        } while (n > 0);
    }

    internal void ClearBytesUsed() => _bytesUsed = 0;
    public int MaxDistance() => _windowSize - MinLookahead;
    /// <summary>Add a byte to output window.</summary>
    public void Write(byte b)
    {
        Debug.Assert(_bytesUsed < _windowSize, "Can't add byte when window is full!");
        _window.Span[_lastIndex++] = b;
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
                Span<byte> sourceToBcopied = _window.Span.Slice(copyStart, length);
                //Copying into the look-ahead buffer (where the decompressed input is stored)
                //Array.Copy(_window, copyStart, _window, _lastIndex, length);
                sourceToBcopied.CopyTo(_window.Span.Slice(_lastIndex, length));
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
                    _window.Span[_lastIndex++] = _window.Span[copyStart++];
                }
            }
        }
        else
        {
            // Copy byte by byte
            while (length-- > 0)
            {
                _window.Span[_lastIndex++] = _window.Span[copyStart++];
                _lastIndex &= _windowMask;
                copyStart &= _windowMask;
            }
        }
    }

    /// <summary>
    /// Copy up to length of bytes from input directly.
    /// </summary>
    public int CopyFrom(InputBuffer input, int length) // I htink this is the as ReadBuffer - To check when refactoring
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

        Debug.Assert(_window.Span != null);
        if (length > spaceLeft) //Checking is within the boundaries
        {
            Debug.Assert(_lastIndex >= 0);
            Debug.Assert(spaceLeft >= 0);
            Debug.Assert(_lastIndex <= _window.Length - spaceLeft);
            // Copy the first part
            copied = input.CopyTo(_window.Span.Slice(_lastIndex, spaceLeft));

            if (copied == spaceLeft)
            {
                Debug.Assert((length - spaceLeft) >= 0);
                Debug.Assert(0 <= _window.Length - (length - spaceLeft));
                // Only try to copy the second part if we have enough bytes in input
                copied += input.CopyTo(_window.Span.Slice(0, length - spaceLeft));
            }
        }
        else
        {
            Debug.Assert(_lastIndex >= 0);
            Debug.Assert(length >= 0);
            Debug.Assert(_lastIndex <= _window.Length - length);
            // Only one copy is needed if there is no wrap around.
            copied = input.CopyTo(_window.Span.Slice(_lastIndex, length));

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
            usersOutput = usersOutput.Slice(0,_bytesUsed); /// -----COPIA MASO ESTO
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
            _window.Span.Slice(_windowSize - spaceLeft, spaceLeft).CopyTo(usersOutput);
            usersOutput = usersOutput.Slice(spaceLeft, copy_lastIndex);
        }
        _window.Slice(copy_lastIndex - usersOutput.Length, usersOutput.Length).Span.CopyTo(usersOutput);
        _bytesUsed -= copied;
        Debug.Assert(_bytesUsed >= 0, "check this function and find why we copied more bytes than we have");
        return copied;
    }

    /*
     Update a running Adler-32 checksum with the bytes buf[0..len-1] and
       return the updated checksum. An Adler-32 value is in the range of a 32-bit
       unsigned integer. If buf is Z_NULL, this function returns the required
       initial value for the checksum.

         An Adler-32 checksum is almost as reliable as a CRC-32 but can be computed
       much faster.

       Usage example:

         uLong adler = adler32(0L, Z_NULL, 0);

         while (read_buffer(buffer, length) != EOF) {
           adler = adler32(adler, buffer, length);
         }
         if (adler != original_adler) error();
    */
    public void Adler32(Span<byte> buffer, uint Length) 
    {
        _adler = 0;
        throw new NotImplementedException();
    }
    public void CRC32(Span<byte> buffer, uint Length)
    {
        _adler = 0;
        throw new NotImplementedException();
    }

    // For initializing values needed in DeflateResetKeep
    // Migth be deleted later
    public void Adler32() 
    {
        _adler = 0;
        throw new NotImplementedException();
    }
    public void CRC32()
    {
        _adler = 0;
        throw new NotImplementedException();
    }
    public void PutShortMSB(int header)
    {
        throw new NotImplementedException();
    }
}

