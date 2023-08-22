// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using ZFlushCode = Microsoft.ManagedZLib.ManagedZLib.FlushCode;
using ZState = Microsoft.ManagedZLib.ManagedZLib.DeflateStates;
using ZBlockState = Microsoft.ManagedZLib.ManagedZLib.BlockState;
using System.Collections.Generic;

namespace Microsoft.ManagedZLib;

/// <summary>
/// Provides a wrapper around the ZLib compression API.
/// </summary>
internal class Deflater
{
    public const int NIL = 0; /* Tail of hash chains */

    //Min&Max match lengths.
    public const int MinMatch = 3;
    public const int MaxMatch = 258;
    public const int MinLookahead = MaxMatch + MinMatch + 1;

    private bool _isDisposed;

    private const int MinWindowBits = -15;  // WindowBits must be between -8..-15 to write no header, 8..15 for a
    private const int MaxWindowBits = 31;   // zlib header, or 24..31 for a GZip header
    private int _windowBits;
    private uint _litBufferSize;
    //private int _levelConfigTable;

    private int _wrap; //Default: Raw Deflate
    ZState _status;
    private CompressionLevel _compressionLevel;
    private readonly OutputWindow _output;
    private readonly InputBuffer _input;
    private readonly DeflateTrees _trees;
    public bool NeedsInput() => _input.NeedsInput();
    public bool HasSymNext() => _trees._symIndex !=0;
    public int MaxDistance() => _output._windowSize - MinLookahead;

    // Note, DeflateStream or the deflater do not try to be thread safe.
    // The lock is just used to make writing to unmanaged structures atomic to make sure
    // that they do not get inconsistent fields that may lead to an unmanaged memory violation.
    // To prevent *managed* buffer corruption or other weird behaviour users need to synchronise
    // on the stream explicitly.
    private object SyncLock => this;

    // This compLevel parameter is just the version user friendly
    internal Deflater(CompressionLevel compressionLevel, int windowBits)
    {
        ManagedZLib.CompressionLevel zlibCompressionLevel; // This is the one actually used in all the processings
        int memLevel;
        _input = new InputBuffer();
        switch (compressionLevel)
        {
            // See the note in ManagedZLib.CompressionLevel for the recommended combinations.
            case CompressionLevel.Optimal:
                zlibCompressionLevel = ManagedZLib.CompressionLevel.DefaultCompression;
                memLevel = ManagedZLib.Deflate_DefaultMemLevel;
                break;

            case CompressionLevel.Fastest:
                zlibCompressionLevel = ManagedZLib.CompressionLevel.BestSpeed;
                memLevel = ManagedZLib.Deflate_DefaultMemLevel;
                break;

            case CompressionLevel.NoCompression:
                zlibCompressionLevel = ManagedZLib.CompressionLevel.NoCompression;
                memLevel = ManagedZLib.Deflate_NoCompressionMemLevel;
                break;

            case CompressionLevel.SmallestSize:
                zlibCompressionLevel = ManagedZLib.CompressionLevel.BestCompression;
                memLevel = ManagedZLib.Deflate_DefaultMemLevel;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(compressionLevel));
        }
        _wrap = 0;
        _compressionLevel = compressionLevel;
        //Checking format of compression> Raw, Gzip or ZLib 
        // For Window and wrap flag initial configuration
        _windowBits = DeflateInit(windowBits);
        _output = new OutputWindow(_windowBits,memLevel, zlibCompressionLevel); //Setting window size and mask
        _output._method = (int)ManagedZLib.CompressionMethod.Deflated; //Deflated - only option
        _litBufferSize = 1U << (memLevel + 6); //16K by default
        _trees = new DeflateTrees(_output._pendingBuffer.Slice((int)_litBufferSize),_litBufferSize);
        _status = ZState.InitState;
        // Optimal is DefaultCompression which translates to level 6 in the configuration table
        // used for the algorithm that goes from 0 to 9.
        // The rest matches the int value in the enum.
        _output._level = (zlibCompressionLevel == ManagedZLib.CompressionLevel.DefaultCompression)? 6 : (int)zlibCompressionLevel;
        _output._strategy = (int)ManagedZLib.CompressionStrategy.DefaultStrategy; // Just the default
        // Setting variables for doing the matches
        DeflateReset();

        //Might not be necessary - constructors do everything that DelfateInit2 used to do
        DeflateInit2(zlibCompressionLevel, ManagedZLib.CompressionMethod.Deflated, windowBits, memLevel, _output._strategy);
    }
    private int DeflateInit( int windowBits)
    {
        _wrap = 1; // To check which type of wrapper are we checking
                     // (0) No wrapper/Raw, (1) ZLib, (2)GZip
        Debug.Assert(windowBits >= MinWindowBits && windowBits <= MaxWindowBits);
        //Check input stream is not null
        ////-15 to -1 or 0 to 47
        if (windowBits < 0)
        {//Raw deflate - Suppress ZLib Wrapper
            _wrap = 0;
            windowBits = -windowBits;
        } else if (windowBits > 15) {  //GZip
            _wrap = 2;
            windowBits -= 16; //
        }                                      /// What's this (bellow):
        _input._wrap = _wrap;
        if (windowBits == 8) windowBits = 9;  /* until 256-byte window bug fixed */
        return (windowBits < 0) ? -windowBits : windowBits &= 15;
    }
    //This will use asserts instead of the ZLibNative error checking
    private void DeflateInit2(ManagedZLib.CompressionLevel level,
        ManagedZLib.CompressionMethod method,
        int windowBits,
        int memLevel,
        ManagedZLib.CompressionStrategy strategy) { 

        //Set everything up + error checking if needed - Might merge with DeflateInit later.
    }
    // DeflateReset(): Sets some initial values.
    // ATTENTION: This method to be reviewed/re-valuated in refactoring stage**
    // I moved some initializations to DeflateTrees and OutputWindow constructors
    // I'd need to put them back if this is going to be called else where again, 
    // besides just in Deflaters constructor. 
    // More so, if this is not called again, I can totally do this initializations
    // in the respective constructors if I'm not doing them already.
    public void DeflateReset()
    {
        _input._totalInput = _output._totalOutput = 0;
        _output._dataType = DeflateTrees.Unknown;
        _output._pendingBufferBytes = 0;
        _output._pendingOut = _output._pendingBuffer;
        _output._pendingOutIndex = _output._pendingBuffIndex = 0;

        if (_wrap < 0)
        {
            _wrap = -_wrap; /* was made negative by deflate(..., Z_FINISH); */
        }

        _status = (_wrap == 2) ? ZState.GZipState : ZState.InitState;

        // This will modify _adler like:
        //_adler = (_wrap == 2)? _output.Adler32() : _output.CRC32()
        // But I think I'll do both void and put _adler as a field of the class
        //if (_wrap == 2)
        //    _output.Adler32();
        //else
        //    _output.CRC32();

        _output.lastFlush = ZFlushCode.GenOutput; // Just used when necessary
                                                  // More may impact on compression ratio
    }
    ~Deflater()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing) {
               // Dispose(); //TBD
               // Just in case ArrayPool is used (not likely so far).
            }
            _isDisposed = true;
        }
    }

    internal void SetInput(Memory<byte> inputBuffer)
    {
        Debug.Assert(NeedsInput(), "We have something left in previous input!");
        if (0 == inputBuffer.Length)
        {
            return;
        }
        _input.SetInput(inputBuffer);
    }

    internal int GetDeflateOutput(byte[] outputBuffer)
    {
        Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
        Debug.Assert(!NeedsInput(), "GetDeflateOutput should only be called after providing input");
        int bytesRead = ReadDeflateOutput(outputBuffer, ZFlushCode.NoFlush, out _);
        return bytesRead;

    }

    public int ReadDeflateOutput(byte[] outputBuffer, ZFlushCode flushCode, out bool finishCode) // It threw an error when doing the conversion from byte[] to Span<byte>
                                                                                  // So I'll change to Memory<byte>
    {
        Debug.Assert(outputBuffer.Length > 0); // This used to be nullable - Check behavior later

        lock (SyncLock)
        {
            _output._output = outputBuffer; //Find a way to not having to allocate this
            _output._availableOutput = outputBuffer.Length;
            _output._nextOut = 0;

            // It is either returning the bytesRead or just throwing an error
            int bytesRead = Deflate(flushCode, out finishCode);
            //int bytesRead = _output.Buffer.Length - _output.AvailableBytes; // outputBuffer.Length = _output.Buffer.Length 
            return bytesRead;
        }
    }

    internal int Finish(byte[] outputBuffer, out bool finishCode)
    {
        Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
        Debug.Assert(outputBuffer.Length > 0, "Can't pass in an empty output buffer!");

        int bytesRead = ReadDeflateOutput(outputBuffer, ZFlushCode.Finish, out finishCode); 
        return bytesRead;
    }

    /// <summary>
    /// Returns true if there was something to flush. Otherwise False.
    /// </summary>
    internal int Flush(byte[] outputBuffer, out bool isFinished)
    {
        Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
        Debug.Assert(outputBuffer.Length > 0, "Can't pass in an empty output buffer!");
        Debug.Assert(NeedsInput(), "We have something left in previous input!");
        
        return ReadDeflateOutput(outputBuffer, ZFlushCode.SyncFlush, out isFinished);
    }
    // Checking is a valid state
    public bool DeflateStateCheck()
    {
        if (_output == null || _input == null)
        {
            return false;
        }
        return true;
    }

    private int Deflate(ZFlushCode flushCode, out bool finishCode)
    {
        // C Zlib returning values porting interpretation:
        // ERR - Possible Debug.Assert statement
        // OK - return a substraction 
        // bytesRead = outputBuffer.Length - _output.AvailableBytes

        ZFlushCode oldFlush = flushCode;

        oldFlush = _output.lastFlush;
        _output.lastFlush = flushCode;

        // Error checking:
        // Flush as much pending output as possible
        if (_output._pendingBufferBytes != 0)
        {
            FlushPending();
            if (_output._availableOutput == 0)
            {
                /* Since avail_out is 0, deflate will be called again with
                 * more output space, but possibly with both pending and
                 * avail_in equal to zero. There won't be anything to do,
                 * but this is not an error situation so make sure we
                 * return OK instead of BUF_ERROR at next call of deflate:
                 */
                _output.lastFlush = ZFlushCode.GenOutput;
                finishCode = false;
                return _output._output.Length - _output._availableOutput; // OK
            }
        }

        // User must not provide more input after the first FINISH:
        if (_input.AvailableBytes == 0)
        {
            Debug.Assert(flushCode != ZFlushCode.Finish);
        }
        //Debug.Assert(flushCode != ZFlushCode.Finish || _input.AvailableBytes != 0);

        //Write the header
        if (_status == ZState.InitState && _wrap == 0)
        {
            _status = ZState.BusyState;
        }
        if (_status == ZState.InitState) //ZLib
        {
            uint levelFlags;
            //ZLib header
            int header = ((int)ManagedZLib.CompressionMethod.Deflated + ((_windowBits - 8) << 4)) << 8;
            // Check the compression level and more
            if (_output._level < 6) 
            {
                levelFlags = 1;
            }
            else if (_output._level == 6)
            {
                levelFlags = 2;
            }
            else
            {
                levelFlags = 3;
            }
            header |= (int) (levelFlags << 6);
            if (_output._strStart != 0) header |= ManagedZLib.PresetDict;
            header += 31 - (header % 31);

            _trees.PutShortMSB(_output, (uint)header);

            /* Save the adler32 of the preset dictionary: */
            if (_output._strStart != 0)
            {
                //putShortMSB(s, (uInt)(strm->adler >> 16));
                //putShortMSB(s, (uInt)(strm->adler & 0xffff));
            }
            _output.Adler32(); // modifies _output._adler
            _status = ZState.BusyState;

            /* Compression must start with an empty pending buffer */
            FlushPending();
            if (_output._pendingBufferBytes != 0)
            {
                _output.lastFlush = ZFlushCode.GenOutput;
                finishCode = false;
                return _output._output.Length - _output._availableOutput; // OK
            }

        }
        if (_status == ZState.GZipState){ }//Gzip header

        if (_status == ZState.ExtraState) { } //Gzip: Start of bytes to update crc

        if (_status == ZState.NameState) { } // GZip: Gzip file name

        if (_status == ZState.CommentState) { } // GZip: Gzip comment

        if (_status == ZState.HCRCState) {
            //Process header [..]
            _status = ZState.BusyState;
            //  Compression must start with an empty pending buffer */
            FlushPending();
            if (_output._pendingBufferBytes != 0)
            {
                _output.lastFlush = ZFlushCode.GenOutput;
                finishCode = false;
                return _output._output.Length - _output._availableOutput; // OK
            }
        } // GZip: GZip header CRC

        // --------- Most basic deflate operation starts here --------
        // For regular DeflateStream scenarios (Raw Deflate) all of the above should be ignore
        // and it should go from InitState to BusyState directly
        // Leading to here, after the error checking

        // Start a new block or continue the current one.
        if (_input.AvailableBytes != 0 || _output._lookahead != 0 ||
            (flushCode != ZFlushCode.NoFlush && _status != ZState.FinishState))
        {
            ZBlockState blockState;
            // level = 0 is No compression
            if (_output._level == 0)
            {
                blockState = DeflateStored(flushCode); // TO-DO
            }
            else
            {
                // Compression table
                switch (_compressionLevel) //maybe set on longMatchInit
                {
                    // See the note in ManagedZLib.CompressionLevel for the recommended combinations.
                    case CompressionLevel.Optimal:
                        // This should be an in between, since the level of compressions
                        // goes from 0 to 9, I'll guess (I'll make sure later on) this is
                        // _output.level = 4 --- DeflateSlow() - lazy matches
                        blockState = DeflateSlow(flushCode);
                        break;

                    case CompressionLevel.Fastest:
                        // _output.level = 1 --- DeflateFastest() - no lazy matches - max speed
                        blockState = DeflateFast(flushCode);
                        break;

                    case CompressionLevel.NoCompression: 
                        // _output.level = 0 --- DeflateStore() - Store only
                        blockState = DeflateStored(flushCode);
                        break;

                    case CompressionLevel.SmallestSize:
                        // _output.level = 9 --- DeflateSlow() - Max compression - the slowest
                        blockState = DeflateSlow(flushCode);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(_compressionLevel));
                }
            }
            if (blockState == ZBlockState.FinishStarted || blockState == ZBlockState.FinishDone)
            {
                _status = ZState.FinishState;
            }
            if (blockState == ZBlockState.NeedMore || blockState == ZBlockState.FinishStarted)
            {
                if (_output._availableOutput == 0)
                {
                    _output.lastFlush = ZFlushCode.GenOutput; /* avoid BUF_ERROR next call, see above */
                }
                finishCode = false;
                return _output._output.Length - _output._availableOutput; // OK
                /* If flush != Z_NO_FLUSH && avail_out == 0, the next call
                 * of deflate should use the same flush parameter to make sure
                 * that the flush is complete. So we don't have to output an
                 * empty block here, this will be done at next call. This also
                 * ensures that for a very small output buffer, we emit at most
                 * one empty block.
                 */
            }
            if (blockState == ZBlockState.BlockDone)
            {
                if (flushCode != ZFlushCode.Block)
                { /* FULL_FLUSH or SYNC_FLUSH */
                    _trees.TreeStoredBlock(_output, Span<byte>.Empty, 0L, last: false);
                }
                FlushPending();
                if (_output._availableOutput == 0)
                {
                    _output.lastFlush = ZFlushCode.GenOutput; /* avoid BUF_ERROR at next call, see above */
                    finishCode = false;
                    return _output._output.Length - _output._availableOutput; // OK
                }
            }

        }

        if (flushCode != ZFlushCode.Finish)
        {
            finishCode = false;
            return _output._output.Length - _output._availableOutput; // OK
        }
        if (_wrap <= 0) {
            finishCode = true;
            return _output._output.Length - _output._availableOutput; //STREAM END: GZip
        }
        if (_wrap == 2) 
        { } // Gzip: Write the trailer
        else
        {
            _trees.PutShortMSB(_output, (uint)_output._adler >> 16);
            _trees.PutShortMSB(_output, (uint)_output._adler & 0xffff);
        }
        // If avail_out is zero, the application will call deflate again
        // to flush the rest.
        FlushPending();

        // write the trailer only once!
        if (_wrap > 0)
        {
            _wrap = -_wrap;
        }
        if (_output._pendingBufferBytes != 0)
        {
            // Raw deflate bytesRead
            finishCode = false;
            return _output._output.Length - _output._availableOutput; // OK
        }
        // bytesRead + extra GZip operations
        finishCode = true;
        return _output._output.Length - _output._availableOutput; // STREAM_END 

    }

    private ZBlockState DeflateStored(ZFlushCode flushCode)
    {
        // Smallest worthy block size when not flushing or finishing. By default
        // this is 32K.This can be as small as 507 bytes for memLevel == 1.For
        // large input and output buffers, the stored block size will be larger.
        ulong minBlock = (ulong)Math.Min((ulong)_output._penBufferSize - 5, (ulong)_output._windowSize);
        /* Copy as many min_block or larger stored blocks directly to next_out as
         * possible. If flushing, copy the remaining available input to next_out as
         * stored blocks, if there is enough space.
         */
        uint len, left, have;
        bool last = false;
        uint used = _input._availInput; // or inputBuffer length - nextIn
        do
        {
            /* Set len to the maximum size block that we can copy directly with the
             * available input data and output space. Set left to how much of that
             * would be copied from what's left in the window.
             */
            len = ManagedZLib.MaxStored;       /* maximum deflate stored block length */
            have = ((uint)_trees._bitsValid + 42) >> 3;         /* number of header bytes */
            if (_output._availableOutput < have)          /* need room for header */
                break;
            /* maximum stored block length that will fit in avail_out: */
            have = (uint)_output._availableOutput - have;
            left = _output._strStart - (uint)_output._blockStart;    /* bytes left in window */
            if (len > left + _input._availInput)
            {
                len = left + _input._availInput;     /* limit len to the input */
            }   
            if (len > have)
            {
                len = have;                         /* limit len to the output */
            }
            /* If the stored block would be less than min_block in length, or if
             * unable to copy all of the available input when flushing, then try
             * copying to the window and the pending buffer instead. Also don't
             * write an empty block when flushing -- deflate() does that.
             */
            if (len < minBlock && ((len == 0 && flushCode != ZFlushCode.Finish) ||
                                    flushCode == ZFlushCode.NoFlush ||
                                    len != left + _input._availInput))
            {
                break;
            }

            /* Make a dummy stored block in pending to get the header bytes,
             * including any pending bits. This also updates the debugging counts.
             */
            last = flushCode == ZFlushCode.Finish && len == left + _input._availInput ? true : false;
            _trees.TreeStoredBlock(_output, Span<byte>.Empty, 0L, last);

            /* Replace the lengths in the dummy stored block with len. */
            _output._pendingBuffer.Span[(int)_output._pendingBufferBytes - 4] = (byte)len;
            _output._pendingBuffer.Span[(int)_output._pendingBufferBytes - 3] = (byte)(len >> 8);
            _output._pendingBuffer.Span[(int)_output._pendingBufferBytes - 2] = (byte)(~len);
            _output._pendingBuffer.Span[(int)_output._pendingBufferBytes - 1] = (byte)(~len >> 8);

            /* Write the stored block header bytes. */
            FlushPending();

            /* Copy uncompressed bytes from the window to (C-ZLib's next_out)_output.Buffer 
             * using nextOut as the starting index. */
            if (left != 0)
            {
                if (left > len)
                {
                    left = len;
                }

                Span<byte> source = _output._window.Span.Slice((int)_output._blockStart, (int)left);
                Span<byte> dest = _output._output.Span.Slice((int)_output._nextOut,(int)left);
                source.CopyTo(dest);

                _output._nextOut += left;
                _output._availableOutput -= (int)left;
                _output._totalOutput += left;
                _output._blockStart += left;
                len -= left;
            }
            // Copy uncompressed bytes directly from next_in to next_out, updating
            // the check value.
            if (len != 0)
            {
                Span<byte> dest = _output._output.Span.Slice((int)_output._nextOut, (int)len);
                // ATTENTION: this returns uint - bytesRead
                _output.ReadBuffer(_input,dest, len);
                _output._nextOut += len;
                _output._availableOutput -= (int)len;
                _output._totalOutput += len;
            }

        } while (last == false);
        // Update the sliding window with the last s->w_size bytes of the copied
        // data, or append all of the copied data to the existing window if less
        // than s->w_size bytes were copied. Also update the number of bytes to
        // insert in the hash tables, in the event that deflateParams() switches to
        // a non-zero compression level.
        used -= _input._availInput;      /* number of input bytes directly copied */
        if (used != 0)
        {
            /* If any input was used, then no unused input remains in the window,
             * therefore s->block_start == s->strstart.
             */
            if (used >= _output._windowSize)
            {    /* supplant the previous history */
                _trees._matchesInBlock = 2;         /* clear hash */
                Span<byte> source = _input._inputBuffer.Span.Slice((int)(_input._nextIn-(uint)_output._windowSize),_output._windowSize);
                //source.CopyTo(_output._window.Span.Slice(0, _output._windowSize));
                source.CopyTo(_output._window.Span);
                _output._strStart = (uint)_output._windowSize;
                _output._insert = _output._strStart;
            }
            else
            {
                if (_output._actualWindowSize - _output._strStart <= used)
                {
                    /* Slide the window down. */
                    _output._strStart -= (uint)_output._windowSize;
                    Memory<byte> temp = _output._window.Slice(_output._windowSize, (int)_output._strStart);
                    temp.CopyTo(_output._window);
                    if (_trees._matchesInBlock < 2)
                        _trees._matchesInBlock++;   /* add a pending slide_hash() */
                    if (_output._insert > _output._strStart)
                        _output._insert = _output._strStart;
                }
                // TO-DO porting
                // (usually this would be a CopyTo method between Memory<byte> types)
                Span<byte> source = _input._inputBuffer.Span.Slice((int)(_input._nextIn - (uint)used), _output._windowSize);
                Span<byte> destination = _output._window.Span.Slice((int)_output._strStart);
                //source.CopyTo(_output._window.Span.Slice(0, _output._windowSize));
                source.CopyTo(destination);
                _output._strStart += used;
                _output._insert += Math.Min(used, (uint)_output._windowSize - _output._insert);
            }
            _output._blockStart = _output._strStart;
        }
        if (_output._highWater < _output._strStart)
            _output._highWater = _output._strStart;

        // If the last block was written to next_out, then done.
        if (last)
        {
            return ZBlockState.FinishDone;
        }
        /* If flushing and all input has been consumed, then done. */
        if (flushCode != ZFlushCode.NoFlush && flushCode != ZFlushCode.Finish &&
            _input._availInput == 0 && (long)_output._strStart == _output._blockStart)
        {
            return ZBlockState.BlockDone;
        }

        // Fill the window with any remaining input.
        have = (uint)_output._actualWindowSize - _output._strStart;
        if (_input._availInput > have && _output._blockStart >= (long)_output._windowSize)
        {
            /* Slide the window down. */
            _output._blockStart -= _output._windowSize;
            _output._strStart -= (uint)_output._windowSize;
            Memory<byte> temp = _output._window.Slice(_output._windowSize, 
                (int)_output._strStart);
            temp.CopyTo(_output._window);
            if (_trees._matchesInBlock < 2)
                _trees._matchesInBlock++;           /* add a pending slide_hash() */
            have += (uint)_output._windowSize;          /* more space now */
            if (_output._insert > _output._strStart)
                _output._insert = _output._strStart;
        }
        if (have > _input._availInput)
            have = _input._availInput;
        if (have != 0)
        {
            // ATTENTION: this returns uint - bytesRead
            _output.ReadBuffer(_input, _output._window.Span.Slice((int)_output._strStart), have);
            _output._strStart += have;
            _output._insert += Math.Min(have, (uint)_output._windowSize - _output._insert);
        }
        if (_output._highWater < _output._strStart)
        {
            _output._highWater = _output._strStart;
        }

        /* There was not enough avail_out to write a complete worthy or flushed
         * stored block to next_out. Write a stored block to pending instead, if we
         * have enough input for a worthy block, or if flushing and there is enough
         * room for the remaining input as a stored block in the pending buffer.
         */
        have = (uint)(_trees._bitsValid + 42) >> 3;         /* number of header bytes */
        /* maximum stored block length that will fit in pending: */
        have = Math.Min((uint)_output._penBufferSize - have, ManagedZLib.MaxStored);
        minBlock = Math.Min(have, (uint)_output._windowSize);
        left = _output._strStart - (uint)_output._blockStart;

        if (left >= minBlock ||
            ((left != 0 || flushCode == ZFlushCode.Finish) 
            && flushCode != ZFlushCode.NoFlush &&
             _input._availInput == 0 && left <= have))
        {
            len = Math.Min(left, have);
            last = (flushCode == ZFlushCode.Finish && _input._availInput == 0 &&
                   len == left) ? true : false;
            _trees.TreeStoredBlock(_output, _output._window.Span.Slice((int)_output._blockStart), len, last);
            _output._blockStart += len;
            FlushPending();
        }

        // We've done all we can with the available input and output.
        return last ? ZBlockState.FinishStarted : ZBlockState.NeedMore;
    }

    // Compress as much as possible from the input stream, return the current
    // block state.
    // This function does not perform lazy evaluation of matches and inserts
    // new strings in the dictionary only for unmatched strings or for short
    // matches. It is used only for the fast compression options.
    public ZBlockState DeflateFast(ZFlushCode flushCode) // TO-DO
    {
        uint hashHead; //Head of the hash chain - index
        bool blockFlush; //Set if current block must be flushed

        while (true)
        {
            /* Make sure that we always have enough lookahead, except
             * at the end of the input file. We need MAX_MATCH bytes
             * for the next match, plus MIN_MATCH bytes to insert the
             * string following the next match.
             */
            if (_output._lookahead < MinMatch) 
            {
                _output.FillWindow(_input); // ------------------------------------------------------------------Pending to implement
                if (_output._lookahead < MinLookahead && flushCode == ZFlushCode.NoFlush)
                {
                    return ZBlockState.NeedMore;
                }
                if (_output._lookahead == 0) break; /* flush the current block */
            }

            hashHead = NIL; //hash head starts with tail's value - empty hash
            if (_output._lookahead >= MinMatch) {
                hashHead = _output.InsertString();
            }

            //Find the longest match, discarding those <= prev_length.
            //At this point we have always match_length < MIN_MATCH
            if (hashHead != NIL && _output._strStart - hashHead <= MaxDistance())
            {
                /* To simplify the code, we prevent matches with the string
                 * of window index 0 (in particular we have to avoid a match
                 * of the string with itself at the start of the input file).
                 */
                _output._matchLength = _output.LongestMatch(hashHead);// Sets _output._matchStart and eventually the _lookahead
            }
            if (_output._matchLength >= MinMatch)
            {
                
                blockFlush = _trees.TreeTallyDist(_output._strStart - _output._matchStart, _output._matchLength - MinMatch);
                int x = 1;
                if (_trees._symIndex == 49149)
                {
                    x = 0;
                }if (x == 0)
                {
                    Console.WriteLine("khe");
                }
                _output._lookahead -= _output._matchLength;

                if (_output._matchLength <= _output.MaxInsertLength() && 
                    _output._lookahead >= MinMatch)
                {
                    _output._matchLength--;
                    do
                    {
                        _output._strStart++;
                        // _strStart never exceeds _windowSize-MaxMatch, so there
                        // are always MinMatch bytes ahead.
                        hashHead = _output.InsertString();
                    }
                    while (--_output._matchLength != 0);
                    _output._strStart++;
                }
                else 
                {
                    _output._strStart += _output._matchLength;
                    _output._matchLength = 0;

                    Debug.Assert(((int)_output._strStart) > 0); // Checking overflow due to the casting
                    _output._strHashIndex = _output.Window((int)_output._strStart);

                    Debug.Assert(((int)_output._strStart+1) > 0);
                    _output.UpdateHash(_output.Window((int)_output._strStart+1));
                }
            }
            else
            {
                // Not match, output a literal byte
                blockFlush = _trees.TreeTallyLit(_output.Window((int)_output._strStart));
                int x = 1;
                if (_trees._symIndex == 49149)
                {
                    x = 0;
                }
                if (x == 0)
                {
                    Console.WriteLine("khe");
                }
                _output._lookahead--;
                _output._strStart++;
            }

            if (blockFlush)
            {
                FlushBlock(last: false);
            }
        }
        _output._insert = (_output._strStart < (MinMatch - 1))? 
            _output._strStart : MinMatch - 1;

        if ( flushCode == ZFlushCode.Finish) 
        {
            // DONE STATE
            FlushBlock(last: true);
            if (_output._availableOutput == 0)
                return ZBlockState.FinishStarted;
            return ZBlockState.FinishDone; 
        }

        if (_trees._symIndex != 0) 
        {
            FlushBlock(last : false);
            if (_output._availableOutput == 0)
                return ZBlockState.NeedMore;
        }

        return ZBlockState.BlockDone;
    }
    public ZBlockState DeflateSlow(ZFlushCode flushCode)
    {
        //throw new NotImplementedException(); // TO-DO
        uint hashHead; //Head of the hash chain - index
        bool blockFlush; //Set if current block must be flushed

        while (true)
        {
            /* Make sure that we always have enough lookahead, except
             * at the end of the input file. We need MAX_MATCH bytes
             * for the next match, plus MIN_MATCH bytes to insert the
             * string following the next match.
             */
            if (_output._lookahead < MinLookahead)   //------------------
            {
                _output.FillWindow(_input); // ------------------------------------------------------------------Pending to implement
                if (_output._lookahead < MinLookahead && flushCode == ZFlushCode.NoFlush)
                {
                    return ZBlockState.NeedMore;
                }
                if (_output._lookahead == 0) break; /* flush the current block */
            }

            hashHead = NIL; //hash head starts with tail's value - empty hash
            if (_output._lookahead >= MinMatch)
            {
                hashHead = _output.InsertString(); //------------------
            }

            // Find the longest match, discarding those <= prev_length.
            _output._prevLength = _output._matchLength;
            _output._prevMatch = _output._matchStart;
            _output._matchLength = MinMatch - 1;

            //Find the longest match, discarding those <= prev_length.
            //At this point we have always match_length < MIN_MATCH
            if (hashHead != NIL && _output._prevLength < _output.MaxLazyMatch
                && _output._strStart - hashHead <= MaxDistance())
            {
                /* To simplify the code, we prevent matches with the string
                 * of window index 0 (in particular we have to avoid a match
                 * of the string with itself at the start of the input file).
                 */
                _output._matchLength = _output.LongestMatch(hashHead);
                // Sets _output._matchStart and eventually the _lookahead
            }
            if (_output._prevLength >= MinMatch && _output._matchLength <= _output._prevLength)
            {
                uint maxInsert = _output._strStart + _output._lookahead - MinMatch;
                //Do not insert strings in hash table beyond this.

                blockFlush = _trees.TreeTallyDist(_output._strStart - 1 - _output._prevMatch,
                    _output._prevLength - MinMatch);                                              //------------------------------

                /* Insert in hash table all strings up to the end of the match.
                 * strstart - 1 and strstart are already inserted. If there is not
                 * enough lookahead, the last two strings are not inserted in
                 * the hash table.
                 */
                _output._lookahead -= _output._prevLength - 1;
                _output._prevLength -= 2;                   
                do
                {
                    if (++_output._strStart <= maxInsert)
                    {
                        //We just care about inserting in hash - Mot retreiving hashHead
                        _output.InsertString();
                    }
                }
                while (--_output._prevLength != 0); //resetting prev length
                // This must have been resetted
                Debug.Assert(_output._prevLength == 0);
                // Reset everything for looking for a new match
                _output._matchAvailable = false;
                _output._matchLength = MinMatch - 1;
                _output._strStart++;

                if (blockFlush)
                {
                    FlushBlock(last: false);
                }
            }
            else if (_output._matchAvailable)
            {
                //If there was no match at the previous position, output a
                //single literal.If there was a match but the current match
                //is longer, truncate the previous match to a single literal.
                blockFlush = _trees.TreeTallyLit(_output.Window((int)_output._strStart - 1));
                if (blockFlush)
                {
                    FlushBlock(last: false);
                }
                _output._strStart++;
                _output._lookahead--;
                if (_output._availableOutput == 0)
                {
                    return ZBlockState.NeedMore;
                }
            }
            else
            {
                // There is no previous match to compare with, wait for
                // the next step to decide.
                _output._matchAvailable = true;
                _output._strStart++;
                _output._lookahead--;
            }
        }
        Debug.Assert(flushCode != ZFlushCode.NoFlush, "no flush?");
        if (_output._matchAvailable)
        {
            blockFlush = _trees.TreeTallyLit(_output.Window((int)_output._strStart - 1));
            _output._matchAvailable = false;
        }

        _output._insert = (_output._strStart < MinMatch - 1) ? 
            _output._strStart : MinMatch - 1;

        if (flushCode == ZFlushCode.Finish)
        {
            // DONE STATE
            FlushBlock(last: true);
            if (_output._availableOutput == 0)
                return ZBlockState.FinishStarted;
            return ZBlockState.FinishDone;
        }

        if (_trees._symIndex != 0)
        {
            FlushBlock(last: false);
            if (_output._availableOutput == 0)
                return ZBlockState.NeedMore;
        }

        return ZBlockState.BlockDone;
    }

    /* =========================================================================
     * Flush as much pending output as possible. All deflate() output, except for
     * some deflate_stored() output, goes through this function so some
     * applications may wish to modify it to avoid allocating a large
     * strm->next_out buffer and copying into it. (See also read_buf()).
     */
    public void FlushPending()
    {
        uint len;

        _trees.FlushBits(_output);
        len = (uint)_output._pendingBufferBytes;
        if (len > _output._availableOutput) len = (uint)_output._availableOutput;
        if (len == 0) return;

        // s->pending_out  += len;
        Memory<byte> source = _output._pendingOut.Slice((int)_output._pendingOutIndex, (int)len); // TO-DO extra check in case it overflows the int casting
        source.CopyTo(_output._output.Slice((int)_output._nextOut,(int)len)); // output = NextOut

        _output._totalOutput += len;
        _output._pendingOutIndex += len;
        _output._nextOut += len;
        _output._availableOutput -= (int)len;
        _output._pendingBufferBytes -= len;

        if (_output._pendingBufferBytes == 0)
        {
            _output._pendingOut = _output._pendingBuffer;
            _output._pendingOutIndex = _output._pendingBuffIndex;
        }
    }
    // Same but force premature exit if necessary.
    public void FlushBlock(bool last)
    {
        FlushBlockOnly(last);
    }

    // Flush the current block, with given end-of-file flag.
    public void FlushBlockOnly(bool last) // last = end-of-file
    {
        if (_output._blockStart >= 0)
        {
            Memory<byte> buffer = _output._window.Slice((int)_output._blockStart);
            _trees.FlushBlock(_output, buffer,
            (ulong)(_output._strStart - _output._blockStart),
            ref last);
        }
        else
        {
            Memory<byte> buffer = Memory<byte>.Empty;
            _trees.FlushBlock(_output, buffer,
            (ulong)(_output._strStart - _output._blockStart),
            ref last);

        }
        _output._blockStart = _output._strStart;
        FlushPending();
    }

}

