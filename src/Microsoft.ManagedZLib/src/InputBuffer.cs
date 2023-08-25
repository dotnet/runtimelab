// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Dynamic;
using System.IO;

namespace Microsoft.ManagedZLib;

// This class can be used to read bits from an byte array quickly.
// Normally we get bits from 'bitBuffer' field and bitsInBuffer stores
// the number of bits available in 'BitBuffer'.
// When we used up the bits in bitBuffer, we will try to get byte from
// the byte array and copy the byte to appropriate position in bitBuffer.
//
// The byte array is not reused. We will go from 'start' to 'end'.
// When we reach the end, most read operations will return -1,
// which means we are running out of input.
public  class InputBuffer
{
    public ulong _totalInput; //Total input read so far
    public uint _availInput; // Number of available bytes from _nextIN
                             // When compressing, this should be 0 when
                             // running out of input to compress and 
                             // _nextIn is at the end of the input buffer
    public Memory<byte> _inputBuffer; // Input stream buffer
    private uint _bitBuffer;      // To quickly shift in this buffer
    private int _bitsInBuffer;    // #bits available in bitBuffer
    public int _wrap; //Default: Raw Deflate
    public uint _nextIn; // Index for next input byte to be copied from

    /// <summary>Total bits available in the input buffer.</summary>
    public int AvailableBits => _bitsInBuffer; //Used in getNextSymbol
    /// <summary>Total bytes available in the input buffer.</summary>


    //_totalInput, at the end of compression, should match this. It's going to be use
    // just in inflate.
    public int inputBufferSize => _inputBuffer.Length + (_bitsInBuffer / 8);

    /// <summary>Ensure that count bits are in the bit buffer.</summary>
    /// <param name="count">Can be up to 16.</param>
    /// <returns>Returns false if input is not sufficient to make this true.</returns>
    public bool EnsureBitsAvailable(int count)
    {
        Debug.Assert(0 < count && count <= 16, "count is invalid.");

        // Manual inlining to improve perf
        if (_bitsInBuffer < count)
        {
            if (NeedsInput())
            {
                return false;
            }

            // Insert a byte to bitbuffer
            _bitBuffer |= (uint)_inputBuffer.Span[0] << _bitsInBuffer;
            _inputBuffer = _inputBuffer.Slice(1);
            _bitsInBuffer += 8;

            if (_bitsInBuffer < count)
            {
                if (NeedsInput())
                {
                    return false;
                }
                // Insert a byte to bitbuffer
                _bitBuffer |= (uint)_inputBuffer.Span[0] << _bitsInBuffer;
                _inputBuffer = _inputBuffer.Slice(1);
                _bitsInBuffer += 8;
            }
        }

        return true;
    }

    /// <summary>
    /// This function will try to load 16 or more bits into bitBuffer.
    /// It returns whatever is contained in bitBuffer after loading.
    /// The main difference between this and GetBits is that this will
    /// never return -1. So the caller needs to check AvailableBits to
    /// see how many bits are available.
    /// </summary>
    public uint TryLoad16Bits()
    {
        if (_bitsInBuffer < 8)
        {
            if (_inputBuffer.Length > 1)
            {
                Span<byte> span = _inputBuffer.Span;
                _bitBuffer |= (uint)span[1] << (_bitsInBuffer + 8);
                _bitBuffer |= (uint)span[0] << _bitsInBuffer;
                _inputBuffer = _inputBuffer.Slice(2);
                _bitsInBuffer += 16;
            }
            else if (_inputBuffer.Length != 0)
            {
                _bitBuffer |= (uint)_inputBuffer.Span[0] << _bitsInBuffer;
                _inputBuffer = Memory<byte>.Empty;
                _bitsInBuffer += 8;
            }
        }
        else if (_bitsInBuffer < 16)
        {
            if (!_inputBuffer.IsEmpty)
            {
                _bitBuffer |= (uint)_inputBuffer.Span[0] << _bitsInBuffer;
                _inputBuffer = _inputBuffer.Slice(1);
                _bitsInBuffer += 8;
            }
        }

        return _bitBuffer;
    }

    private static uint GetBitMask(int count) => ((uint)1 << count) - 1;

    /// <summary>Gets count bits from the input buffer. Returns -1 if not enough bits available.</summary>
    public int GetBits(int count)
    {
        Debug.Assert(0 < count && count <= 16, "count is invalid.");

        if (!EnsureBitsAvailable(count))
        {
            return -1;
        }

        int result = (int)(_bitBuffer & GetBitMask(count));
        _bitBuffer >>= count;
        _bitsInBuffer -= count;
        return result;
    }

    /// <summary> 
    /// For copying the data on the Deflate blocks:
    /// Copies bytes from input buffer to output buffer.
    /// (As a Span) Copies length bytes from input buffer to output buffer starting at output[offset].
    /// You have to make sure, that the buffer is byte aligned. If not enough bytes are
    /// available, copies fewer bytes.
    /// </summary>
    /// <returns>Returns the number of bytes copied, 0 if no byte is available.</returns>
    public int CopyTo(Span<byte> output)
    {
        Debug.Assert(_bitsInBuffer % 8 == 0);

        // Copy the bytes in bitBuffer first.
        int bytesFromBitBuffer = 0;
        while (_bitsInBuffer > 0 && !output.IsEmpty)
        {
            output[0] = (byte)_bitBuffer;
            output = output.Slice(1);
            _bitBuffer >>= 8;
            _bitsInBuffer -= 8;
            bytesFromBitBuffer++;
        }

        if (output.IsEmpty)
        {
            return bytesFromBitBuffer;
        }

        int length = Math.Min(output.Length, _inputBuffer.Length);
        _inputBuffer.Slice(0, length).Span.CopyTo(output);
        _inputBuffer = _inputBuffer.Slice(length);
        return bytesFromBitBuffer + length;
    }

    /// <summary>
    /// Return true is all input bytes are used.
    /// This means the caller can call SetInput to add more input.
    /// </summary>
    public bool NeedsInput() => _availInput==0;

    /// <summary>
    /// Set the byte buffer to be processed.
    /// All the bits remained in bitbuffer will be processed before the new bytes.
    /// We don't clone the byte buffer here since it is expensive.
    /// The caller should make sure after a buffer is passed in, that
    /// it will not be changed before calling this function again.
    /// </summary>
    public void SetInput(Memory<byte> buffer)
    {
        if (NeedsInput())
        {
            _inputBuffer = buffer;
            _availInput = (uint)buffer.Length;
            _nextIn = 0;
            //AvailableBytes() is _inputBuffer.Length - nextIn
        }
    }

    /// <summary>Skip n bits in the buffer.</summary>
    public void SkipBits(int n)
    {
        Debug.Assert(_bitsInBuffer >= n, "No enough bits in the buffer, Did you call EnsureBitsAvailable?");
        _bitBuffer >>= n;
        _bitsInBuffer -= n;
    }

    /// <summary>Skips to the next byte boundary for byte alignment.</summary>
    public void SkipToByteBoundary()
    {
        _bitBuffer >>= (_bitsInBuffer % 8);
        _bitsInBuffer -= (_bitsInBuffer % 8);
    }
}

