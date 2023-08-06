// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using static Microsoft.ManagedZLib.ManagedZLib;
using static Microsoft.ManagedZLib.ManagedZLib.ZLibStreamHandle;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Microsoft.ManagedZLib;

/// <summary>
/// Provides a wrapper around the ZLib decompression API.
/// </summary>
internal class Inflater
{
    private readonly OutputWindow _output;
    private readonly InputBuffer _input;

    private IHuffmanTree? _literalLengthTree;
    private IHuffmanTree? _distanceTree; 
    private IHuffmanTree? _codeLengthTree;

    private int _literalLengthCodeCount;
    private int _distanceCodeCount;
    private int _codeLengthCodeCount;

    private InflaterState _state;
    private BlockType _blockType;
    private int _finalByte;         // Check if it's final block
    private readonly byte[] _blockLengthBuffer = new byte[4]; //For LEN and NLEN(3.2.2 section in RFC1951) for uncompressed blocks
    private int _blockLength;

    // For decoding a compressed block
    // Alphabets used: Literals, length and distance
    // Extra bits for merging literal and length's alphabet
    private int _length; 
    private int _distanceCode;
    private int _extraBits;

    private int _loopCounter;
    private int _lengthCode;

    private int _codeArraySize;
    private readonly long _uncompressedSize;
    private long _currentInflatedCount;


    private readonly byte[] _codeList; // temporary array (with possibility of become a Span o Memory)
                                       // to store the code length for literal/Length and distance
    private readonly byte[] _codeLengthTreeCodeLength;

    private readonly bool _deflate64; //Whether it's 32K or 63K LZ77 window
    private int _decodeLimit = 258; // For 32K, it'll be 258. For 64K, it'll be 65536



    internal const int MinWindowBits = -15;              // WindowBits must be between -8..-15 to ignore the header, 8..15 for
    internal const int MaxWindowBits = 47;               // zlib headers, 24..31 for GZip headers, or 40..47 for either Zlib or GZip

    private bool _nonEmptyInput;                        // Whether there is any non empty input
    private readonly int _windowBits;                   // The WindowBits parameter passed to Inflater construction

    private bool _couldDecode;
    private object SyncLock => this;                    // Used to make writing to unmanaged structures atomic
    public bool NeedsInput() => _input.NeedsInput(); //For filling up the reference in InputBuffer class to DeflateStream's underlying stream
    public int AvailableOutput => _output.AvailableBytes;//This could be:  if we decide to make a struct instead of classes
                                                         //public int AvailableOutput => (int)_zlibStream.AvailOut;

    //-------------------- Bellow const tables used in decoding:
    // The base length for length-code 257 - 285.
    // The formula to get the real length for a length code is lengthBase[code - 257] + (value stored in extraBits)
    private static ReadOnlySpan<byte> LengthBase => new byte[]
    {
            3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51,
            59, 67, 83, 99, 115, 131, 163, 195, 227, 3
    };

    // Extra bits for length code 257 - 285.
    private static ReadOnlySpan<byte> ExtraLengthBits => new byte[]
    {
            0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3,
            3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 16
    }; // RFC1951 - Extra bits table

    // The base distance for distance code 0 - 31
    // The real distance for a distance code is  distanceBasePosition[code] + (value stored in extraBits)
    private static ReadOnlySpan<ushort> DistanceBasePosition => new ushort[]
    {
            1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513,
            769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577, 32769, 49153
    }; //Vivi's notes> This come from RFC1951
    // code lengths for code length alphabet is stored in following order
    private static ReadOnlySpan<byte> CodeOrder => new byte[] { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

    private static ReadOnlySpan<byte> StaticDistanceTreeTable => new byte[]
    {
            0x00, 0x10, 0x08, 0x18, 0x04, 0x14, 0x0c, 0x1c, 0x02, 0x12, 0x0a, 0x1a,
            0x06, 0x16, 0x0e, 0x1e, 0x01, 0x11, 0x09, 0x19, 0x05, 0x15, 0x0d, 0x1d,
            0x03, 0x13, 0x0b, 0x1b, 0x07, 0x17, 0x0f, 0x1f
    };
    //Setting the windows bits
    private static int InflateInit(int windowBits)
    {
        Debug.Assert(windowBits >= MinWindowBits && windowBits <= MaxWindowBits);
        //-15 to -1 or 0 to 47
        return (windowBits < 0) ? -windowBits : windowBits &= 15;
    }

    /// <summary>
    /// Initialized the Inflater with the given windowBits size
    /// </summary>
    internal Inflater(int windowBits, long uncompressedSize = -1)
    {
        _input = new InputBuffer();
        // Error checking
        _windowBits = InflateInit(windowBits);
        // Initializing window size according the type of deflate (window limits - 32k or 64k)
        // This has mainly: Output Window, Index last position (Where in window bytes array) and BytesUsed (As the quantity)
        _output = _deflate64? new OutputWindow() : new OutputWindow(_windowBits);
        _codeList = new byte[IHuffmanTree.MaxLiteralTreeElements + IHuffmanTree.MaxDistTreeElements];
        _codeLengthTreeCodeLength = new byte[IHuffmanTree.NumberOfCodeLengthTreeElements];
        _nonEmptyInput = false;
        _couldDecode = false; //After finishing decoding
        //Initial state of the state machine - Checking BFinal bit
        _state = InflaterState.ReadingBFinal; // BFINAL - First bit of the block
        _uncompressedSize = uncompressedSize;
    }

    // Possibility of branching out from 32K to 64K as the output window limit depending on the bool
    //Maybe it should be an enum since these are all the possible types in archie
    //{ Stored = 0x0, Deflate = 0x8, Deflate64 = 0x9, BZip2 = 0xC, LZMA = 0xE }
    internal Inflater(bool deflate64, int windowBits, long uncompressedSize = -1) : this(windowBits, uncompressedSize)
    {
        _deflate64= deflate64;
        _decodeLimit = 65536; //64K window
        // With Deflate64 we can have up to a 64kb length, so we ensure at least that much space is available
        // in the OutputWindow to avoid overwriting previous unflushed output data.
    }


    /// <summary>
    /// Returns true if the end of the stream has been reached.
    /// </summary>
    public bool Finished() =>  _state == InflaterState.Done || _state == InflaterState.VerifyingFooter;

    public int Inflate(Span<byte> buffer) 
    {
        // If Inflate is called on an invalid or unready inflater, return 0 to indicate no bytes have been read.
        if (buffer.Length == 0)
            return 0;

        int bytesRead = InflateVerified(buffer);

        Debug.Assert(buffer != null, "Can't pass in a null output buffer!");

        return bytesRead;
    }

    public int InflateVerified(Span<byte> bufferBytes)
    {

        int bytesRead = 0;
        // This division of _uncompressedSize is for GZip
        // For Raw Inflate, it is not necessary to compare the inflate count (bytes read so far)
        // with anything else, besides cheching is a valid number fr either finish the loop or
        // refill the buffer that refers to the underlying deflate stream buffer. (Default size: 8192)
        do
        {
            int copied = 0;
            if (_uncompressedSize == -1) //Initial data reading
            {
                //For Raw Inflate, this is suppose to enter this
                copied = ReadOutput(bufferBytes);
            }
            else
            {
                // This might be specially for GZip - Since it's the only one changing _uncompressedSize
                // through the constructor -it's a readonly field-
                if (_uncompressedSize > _currentInflatedCount)
                {
                    int newLength = (int)Math.Min(bufferBytes.Length, _uncompressedSize - _currentInflatedCount);
                    bufferBytes = bufferBytes.Slice(newLength);
                    copied = ReadOutput(bufferBytes);
                    _currentInflatedCount += copied;
                }
                else
                {
                    //Done reading input
                    _state = InflaterState.Done;
                    _output.ClearBytesUsed(); //The window end up being clean - _bytesUsed = 0
                }  
            }
            // Before actually add the bytes read to the local variable, 
            // we check if the value is valid 
            if (copied > 0)
            {
                bufferBytes = bufferBytes.Slice(copied);
                bytesRead += copied;
            }
            if (bufferBytes.IsEmpty)
            {
                // filled in the bytes buffer - We reached the end
                break;
            }
        } while (!Finished()&& _couldDecode) ; //Will return 0 when more input is need

        return bytesRead;
    }
    
    private int ReadOutput(Span<byte> outputBytes) {
        // Before the state machine of inflater starts, we need to check the type of inflation done (Raw, Gzip or Zlib)
        // To know if besides the first bits is the raw deflate block, any additional header processing is needed
        // or if at the end, we are doing additional error checkings.

        //Inflate state machine
        _couldDecode = Decode();

        //Final copying of the uncompressed data
        // Keeps looping until the decom
        
        //bytesRead
        return _output.CopyTo(outputBytes);
    }

    internal bool IsGzipStream() => _windowBits >= 24 && _windowBits <= 31;

    public bool NonEmptyInput() => _nonEmptyInput;

    //With sanity checks
    public void SetInput(byte[] inputBuffer, int startIndex, int count)
    {
        Debug.Assert(_input.NeedsInput(), "We have something left in previous input!");
        Debug.Assert(inputBuffer != null);
        Debug.Assert(startIndex >= 0 && count >= 0 && count + startIndex <= inputBuffer.Length);

        SetInput(inputBuffer.AsMemory(startIndex, count));
    }

    public void SetInput(Memory<byte> inputBuffer)
    {
        Debug.Assert(_input.NeedsInput(), "We have something left in previous input!");

        if (inputBuffer.IsEmpty)
            return;

        lock (SyncLock)
        {
            //Literally just setting the input -Compressed data gotten through the DeflateStream constructor-
            // to the _input's buffer (Memory<byte> in inputBuffer.cs _buffer field)
            _input.SetInput(inputBuffer);
            _nonEmptyInput = true;
        }
    }


    private bool Decode()
    {
        bool EndOfBlock = false;
        bool result;
        
        // For checking later, to add some extra checks here, the ones done by ReadOutput and ResetStreamForLeftoverInput() 
        // ResetStreamForLeftoverInput() for GZip and ZLib scenarios that behave differently than raw inflate.
        // ResetStreamForLeftoverInput() checks if it's a GZpin member
        
        //* --- For GZip and ZLib, there are more checks needed for their headers and trailers.
        //* [Reference from Mark Adler's repo: inflate.h
        /* State transitions
            Process header:
                HEAD -> (gzip) or (zlib) or (raw)
                (gzip) -> FLAGS -> TIME -> OS -> EXLEN -> EXTRA -> NAME -> COMMENT ->
                            HCRC -> TYPE
                (zlib) -> DICTID or TYPE
                DICTID -> DICT -> TYPE
                (raw) -> TYPEDO
            Read deflate blocks:
                    TYPE -> TYPEDO -> STORED or TABLE or LEN_ or CHECK
                    STORED -> COPY_ -> COPY -> TYPE
                    TABLE -> LENLENS -> CODELENS -> LEN_
                    LEN_ -> LEN
            Read deflate codes in fixed or dynamic block:
                        LEN -> LENEXT or LIT or TYPE
                        LENEXT -> DIST -> DISTEXT -> MATCH -> LEN
                        LIT -> LEN
            Process trailer:
                CHECK -> LENGTH -> DONE
        */
        if (Finished())
        {
            return true;
        }
        
        //Read header of deflate blocks (HEAD)
        if (_state == InflaterState.ReadingBFinal)
        {
            if (!_input.EnsureBitsAvailable(1)) //CanRead()
                return false;

            _finalByte = _input.GetBits(1);      // BFinal
            _state = InflaterState.ReadingBType; // Next state - next 2 bits in the header           
        }
        if (_state == InflaterState.ReadingBType)
        { 
            if (!_input.EnsureBitsAvailable(2)) // Need 2 bits - Error check
            {
                _state = InflaterState.ReadingBType; //Returns to first state - Error check
                return false;
            }
            // Types (2 bits): 00-No compression, 01-Fixed Huff, 10-Dynamic, 11-Reserved. 
            _blockType = (BlockType)_input.GetBits(2);
            if (_blockType == BlockType.DynamicTrees) //Type = Dynamic Huffman codes
            {
                _state = InflaterState.ReadingNumLitCodes;
            }
            else if (_blockType == BlockType.StaticTrees) //Type = Fixed Huffman codes
            {
                _literalLengthTree = IHuffmanTree.StaticLiteralLengthTree;
                _distanceTree = IHuffmanTree.StaticDistanceTree;
                _state = InflaterState.DecodeTop;
            }
            else if (_blockType == BlockType.Uncompressed) //Type = Stored with no compression
            {
                _state = InflaterState.UncompressedAligning;
            }
            else
            {
                throw new InvalidDataException("UnknownBlockType - Unknown block type. Stream might be corrupted.");
            }
        }
        // Depending on the type, we will go to the methods for decoding each
        if (_blockType == BlockType.DynamicTrees)
        {
            if (_state < InflaterState.DecodeTop)
            {
                // For decoding a literal (char/match) in a compressed block
                result = DecodeDynamicBlockHeader();
            }
            else
            {
                result = DecodeBlock(out EndOfBlock); // this can returns true when output is full
            }
        }
        else if (_blockType == BlockType.StaticTrees)
        {
            result = DecodeBlock(out EndOfBlock);
        }
        else if (_blockType == BlockType.Uncompressed)
        {
            result = DecodeUncompressedBlock(out EndOfBlock);
        }
        else
        {
            throw new InvalidDataException("UnknownBlockType - Unknown block type. Stream might be corrupted.");
        }

        //
        // If we reached the end of the block and the block we were decoding had
        // bfinal=1 (final block)
        //
        if (EndOfBlock && (_finalByte != 0))
        {
            _state = InflaterState.Done;
        }
        return result;
    }

    // Decoding algorithm (RFC1951) for the actual compressed data per Deflate block
    // GZip member checking should have been done before this.
    private bool DecodeBlock(out bool end_of_block_code_seen)
    {
        end_of_block_code_seen = false;
        // A little bit faster than frequently accessing the property
        int freeBytes = _output.FreeBytes;
        while (freeBytes > _decodeLimit)
        {
            int symbol;
            switch (_state)
            {
                case InflaterState.DecodeTop:
                    // Decode an element from the literal tree

                    Debug.Assert(_literalLengthTree != null);
                    // TODO: optimize this!!!
                    symbol = _literalLengthTree.GetNextSymbol(_input);
                    if (symbol < 0)
                    {
                        // running out of input
                        return false;
                    }

                    if (symbol < 256)
                    {
                        // literal
                        _output.Write((byte)symbol);
                        --freeBytes;
                    }
                    else if (symbol == 256)
                    {
                        // end of block
                        end_of_block_code_seen = true;
                        // Reset state
                        _state = InflaterState.ReadingBFinal;
                        return true;
                    }
                    else
                    {
                        // length/distance pair
                        symbol -= 257;     // length code started at 257
                        if (symbol < 8)
                        {
                            symbol += 3;   // match length = 3,4,5,6,7,8,9,10
                            _extraBits = 0;
                        }
                        else if (!_deflate64 && symbol == 28) //deflateType is 64k
                        {
                            // extra bits for code 285 is 0
                            symbol = 258;             // code 285 means length 258
                            _extraBits = 0;
                        }
                        else
                        {
                            if ((uint)symbol >= ExtraLengthBits.Length)
                            {
                                throw new InvalidDataException("GenericInvalidData - Found invalid data while decoding.");
                            }
                            _extraBits = ExtraLengthBits[symbol];
                            Debug.Assert(_extraBits != 0, "We handle other cases separately!");
                        }
                        _length = symbol;
                        goto case InflaterState.HaveInitialLength;
                    }
                    break;

                case InflaterState.HaveInitialLength:
                    if (_extraBits > 0)
                    {
                        _state = InflaterState.HaveInitialLength;
                        int bits = _input.GetBits(_extraBits);
                        if (bits < 0)
                        {
                            return false;
                        }

                        if (_length < 0 || _length >= LengthBase.Length)
                        {
                            throw new InvalidDataException("GenericInvalidData - Found invalid data while decoding.");
                        }
                        _length = LengthBase[_length] + bits;
                    }
                    _state = InflaterState.HaveFullLength;
                    goto case InflaterState.HaveFullLength;

                case InflaterState.HaveFullLength:
                    if (_blockType == BlockType.DynamicTrees)
                    {
                        Debug.Assert(_distanceTree != null);
                        _distanceCode = _distanceTree.GetNextSymbol(_input);
                    }
                    else
                    {
                        // get distance code directly for static block
                        _distanceCode = _input.GetBits(5);
                        if (_distanceCode >= 0)
                        {
                            _distanceCode = StaticDistanceTreeTable[_distanceCode];
                        }
                    }

                    if (_distanceCode < 0)
                    {
                        // running out input
                        return false;
                    }

                    _state = InflaterState.HaveDistCode;
                    goto case InflaterState.HaveDistCode;

                case InflaterState.HaveDistCode:
                    // To avoid a table lookup we note that for distanceCode > 3,
                    // extra_bits = (distanceCode-2) >> 1
                    int offset;
                    if (_distanceCode > 3)
                    {
                        _extraBits = (_distanceCode - 2) >> 1;
                        int bits = _input.GetBits(_extraBits);
                        if (bits < 0)
                        {
                            return false;
                        }
                        offset = DistanceBasePosition[_distanceCode] + bits;
                    }
                    else
                    {
                        offset = _distanceCode + 1;
                    }

                    _output.WriteLengthDistance(_length, offset);
                    freeBytes -= _length;
                    _state = InflaterState.DecodeTop;
                    break;

                default:
                    Debug.Fail("check why we are here!");
                    throw new InvalidDataException("UnknownState - Decoder is in some unknown state. This might be caused by corrupted data.");
            }
        }

        return true;
    }

    // Format of Non-compressed blocks (BTYPE=00) - RFC1951 spec
    private bool DecodeUncompressedBlock(out bool end_of_block)
    {
        end_of_block = false;
        while (true)
        {
            switch (_state)
            {
                case InflaterState.UncompressedAligning: // initial state when calling this function
                                                         // we must skip to a byte boundary
                    _input.SkipToByteBoundary();
                    _state = InflaterState.UncompressedByte1;
                    goto case InflaterState.UncompressedByte1;

                case InflaterState.UncompressedByte1:   // decoding block length
                case InflaterState.UncompressedByte2:
                case InflaterState.UncompressedByte3:
                case InflaterState.UncompressedByte4:
                    int bits = _input.GetBits(8);
                    if (bits < 0)
                    {
                        return false;
                    }

                    _blockLengthBuffer[_state - InflaterState.UncompressedByte1] = (byte)bits;
                    if (_state == InflaterState.UncompressedByte4)
                    {
                        _blockLength = _blockLengthBuffer[0] + ((int)_blockLengthBuffer[1]) * 256;
                        int blockLengthComplement = _blockLengthBuffer[2] + ((int)_blockLengthBuffer[3]) * 256;

                        // make sure complement matches
                        if ((ushort)_blockLength != (ushort)(~blockLengthComplement))
                        {
                            throw new InvalidDataException("InvalidBlockLength - Block length does not match with its complement.");
                        }
                    }

                    _state += 1;
                    break;

                case InflaterState.DecodingUncompressed: // copying block data

                    // Directly copy bytes from input to output.
                    int bytesCopied = _output.CopyFrom(_input, _blockLength);
                    _blockLength -= bytesCopied;

                    if (_blockLength == 0)
                    {
                        // Done with this block, need to re-init bit buffer for next block
                        _state = InflaterState.ReadingBFinal;
                        end_of_block = true;
                        return true;
                    }

                    // We can fail to copy all bytes for two reasons:
                    //    Running out of Input
                    //    running out of free space in output window
                    if (_output.FreeBytes == 0)
                    {
                        return true;
                    }

                    return false;

                default:
                    Debug.Fail("check why we are here!");
                    throw new InvalidDataException("UnknownState - Decoder is in some unknown state.This might be caused by corrupted data.");
            }
        }
    }
    // Format of Compression with dynamic Huffman codes (BTYPE=10)
    // Dynamic Block header - RFC1951
    private bool DecodeDynamicBlockHeader()
    {
        switch (_state)
        {
            case InflaterState.ReadingNumLitCodes:
                _literalLengthCodeCount = _input.GetBits(5);
                if (_literalLengthCodeCount < 0)
                {
                    return false;
                }
                _literalLengthCodeCount += 257;
                _state = InflaterState.ReadingNumDistCodes;
                goto case InflaterState.ReadingNumDistCodes;

            case InflaterState.ReadingNumDistCodes:
                _distanceCodeCount = _input.GetBits(5);
                if (_distanceCodeCount < 0)
                {
                    return false;
                }
                _distanceCodeCount += 1;
                _state = InflaterState.ReadingNumCodeLengthCodes;
                goto case InflaterState.ReadingNumCodeLengthCodes;

            case InflaterState.ReadingNumCodeLengthCodes:
                _codeLengthCodeCount = _input.GetBits(4);
                if (_codeLengthCodeCount < 0)
                {
                    return false;
                }
                _codeLengthCodeCount += 4;
                _loopCounter = 0;
                _state = InflaterState.ReadingCodeLengthCodes;
                goto case InflaterState.ReadingCodeLengthCodes;

            case InflaterState.ReadingCodeLengthCodes:
                while (_loopCounter < _codeLengthCodeCount)
                {
                    int bits = _input.GetBits(3);
                    if (bits < 0)
                    {
                        return false;
                    }
                    _codeLengthTreeCodeLength[CodeOrder[_loopCounter]] = (byte)bits;
                    ++_loopCounter;
                }

                for (int i = _codeLengthCodeCount; i < CodeOrder.Length; i++)
                {
                    _codeLengthTreeCodeLength[CodeOrder[i]] = 0;
                }

                // create huffman tree for code length
                _codeLengthTree = new IHuffmanTree(_codeLengthTreeCodeLength);
                _codeArraySize = _literalLengthCodeCount + _distanceCodeCount;
                _loopCounter = 0; // reset loop count

                _state = InflaterState.ReadingTreeCodesBefore;
                goto case InflaterState.ReadingTreeCodesBefore;

            case InflaterState.ReadingTreeCodesBefore:
            case InflaterState.ReadingTreeCodesAfter:
                while (_loopCounter < _codeArraySize)
                {
                    if (_state == InflaterState.ReadingTreeCodesBefore)
                    {
                        Debug.Assert(_codeLengthTree != null);
                        if ((_lengthCode = _codeLengthTree.GetNextSymbol(_input)) < 0)
                        {
                            return false;
                        }
                    }

                    // The alphabet for code lengths is as follows:
                    //  0 - 15: Represent code lengths of 0 - 15
                    //  16: Copy the previous code length 3 - 6 times.
                    //  The next 2 bits indicate repeat length
                    //         (0 = 3, ... , 3 = 6)
                    //      Example:  Codes 8, 16 (+2 bits 11),
                    //                16 (+2 bits 10) will expand to
                    //                12 code lengths of 8 (1 + 6 + 5)
                    //  17: Repeat a code length of 0 for 3 - 10 times.
                    //    (3 bits of length)
                    //  18: Repeat a code length of 0 for 11 - 138 times
                    //    (7 bits of length)
                    if (_lengthCode <= 15)
                    {
                        _codeList[_loopCounter++] = (byte)_lengthCode;
                    }
                    else
                    {
                        int repeatCount;
                        if (_lengthCode == 16)
                        {
                            if (!_input.EnsureBitsAvailable(2))
                            {
                                _state = InflaterState.ReadingTreeCodesAfter;
                                return false;
                            }

                            if (_loopCounter == 0)
                            {
                                // can't have "prev code" on first code
                                throw new InvalidDataException();
                            }

                            byte previousCode = _codeList[_loopCounter - 1];
                            repeatCount = _input.GetBits(2) + 3;

                            if (_loopCounter + repeatCount > _codeArraySize)
                            {
                                throw new InvalidDataException();
                            }

                            for (int j = 0; j < repeatCount; j++)
                            {
                                _codeList[_loopCounter++] = previousCode;
                            }
                        }
                        else if (_lengthCode == 17)
                        {
                            if (!_input.EnsureBitsAvailable(3))
                            {
                                _state = InflaterState.ReadingTreeCodesAfter;
                                return false;
                            }

                            repeatCount = _input.GetBits(3) + 3;

                            if (_loopCounter + repeatCount > _codeArraySize)
                            {
                                throw new InvalidDataException();
                            }

                            for (int j = 0; j < repeatCount; j++)
                            {
                                _codeList[_loopCounter++] = 0;
                            }
                        }
                        else
                        {
                            // code == 18
                            if (!_input.EnsureBitsAvailable(7))
                            {
                                _state = InflaterState.ReadingTreeCodesAfter;
                                return false;
                            }

                            repeatCount = _input.GetBits(7) + 11;

                            if (_loopCounter + repeatCount > _codeArraySize)
                            {
                                throw new InvalidDataException();
                            }

                            for (int j = 0; j < repeatCount; j++)
                            {
                                _codeList[_loopCounter++] = 0;
                            }
                        }
                    }
                    _state = InflaterState.ReadingTreeCodesBefore; // we want to read the next code.
                }
                break;

            default:
                Debug.Fail("check why we are here!");
                throw new InvalidDataException("UnknownState - Decoder is in some unknown state.This might be caused by corrupted data.");
        }

        byte[] literalTreeCodeLength = new byte[IHuffmanTree.MaxLiteralTreeElements];
        byte[] distanceTreeCodeLength = new byte[IHuffmanTree.MaxDistTreeElements];

        // Create literal and distance tables
        Array.Copy(_codeList, literalTreeCodeLength, _literalLengthCodeCount);
        Array.Copy(_codeList, _literalLengthCodeCount, distanceTreeCodeLength, 0, _distanceCodeCount);

        // Make sure there is an end-of-block code, otherwise how could we ever end?
        if (literalTreeCodeLength[IHuffmanTree.EndOfBlockCode] == 0)
        {
            throw new InvalidDataException();
        }

        _literalLengthTree = new IHuffmanTree(literalTreeCodeLength);
        _distanceTree = new IHuffmanTree(distanceTreeCodeLength);
        _state = InflaterState.DecodeTop;
        return true;
    }

}
