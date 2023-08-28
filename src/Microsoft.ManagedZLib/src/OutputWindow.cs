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
internal sealed class OutputWindow
{
    private uint WindowSize;
    private uint WindowMask;
    private byte[] _window; // The window is 2^n bytes where n is number of bits
    private uint _lastIndex; // Position to where we should write next byte
    private uint _bytesUsed; // Number of bytes in the output window that haven't been consumed yet.

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
        WindowSize = (uint)(1 << (int)windowBits); //logaritmic base 2
        WindowMask = WindowSize - 1;
        _window = new byte[WindowSize];
    }
    // With Deflate64 we can have up to a 65536 length as well as up to a 65538 distance. This means we need a Window that is at
    // least 131074 bytes long so we have space to retrieve up to a full 64kb in lookback and place it in our buffer without
    // overwriting existing data. OutputBuffer requires that the WindowSize be an exponent of 2, so we round up to 2^18.
    internal OutputWindow() //deflate64
    {
        WindowSize = 262144;
        WindowMask = 262143;
        _window = new byte[WindowSize];
    }
    internal void ClearBytesUsed()
    {
        _bytesUsed = 0;
    }
    /// <summary>Add a byte to output window.</summary>
    public void Write(byte b)
    {
        Debug.Assert(_bytesUsed < WindowSize, "Can't add byte when window is full!");
        _window[_lastIndex++] = b;
        _lastIndex &= WindowMask;
        ++_bytesUsed;
    }

    //Important part of LZ77 algorithm
    public void WriteLengthDistance(uint length, uint distance)
    {
        //Checking there's enough space for copying the output
        Debug.Assert((_bytesUsed + length) <= WindowSize, "No Enough space");

        // Move backwards distance bytes in the output stream,
        // and copy length bytes from this position to the output stream.
        _bytesUsed += length;
        uint copyStart = (_lastIndex - distance) & WindowMask;

        // Total space that would be taken by copying the length bytes
        uint border = WindowSize - length;
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
                _lastIndex &= WindowMask;
                copyStart &= WindowMask;
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
        length = Math.Min(Math.Min(length, (int)(WindowSize - _bytesUsed)), (int)input.AvailableBytes);
        uint copied;

        // We might need wrap around to copy all bytes.
        int spaceLeft = (int)(WindowSize - _lastIndex);
        if (length > spaceLeft) //Checking is within the boundaries
        {
            // Copy the first part
            copied = (uint)input.CopyTo(_window, (int)_lastIndex, spaceLeft);
            if (copied == spaceLeft)
            {
                // Only try to copy the second part if we have enough bytes in input
                copied += (uint)input.CopyTo(_window, 0, length - spaceLeft);
            }
        }
        else
        {
            // Only one copy is needed if there is no wrap around.
            copied = (uint)input.CopyTo(_window, (int)_lastIndex, length);
        }

        _lastIndex = (_lastIndex + copied) & WindowMask;
        _bytesUsed += copied;
        return (int)copied; 
    }

    /// <summary>Free space in output window.</summary>
    public uint FreeBytes => WindowSize - (uint)_bytesUsed;

    /// <summary>Bytes not consumed in output window.</summary>
    public uint AvailableBytes => _bytesUsed;

    /// <summary>Copy the decompressed bytes to output buffer.</summary>
    public int CopyTo(Span<byte> usersOutput)
    {
        uint copy_lastIndex;

        if (usersOutput.Length > _bytesUsed)
        {
            // We can copy all the decompressed bytes out
            copy_lastIndex = _lastIndex; //Last index auxiliar
            usersOutput = usersOutput.Slice(0,(int)_bytesUsed);
        }
        else
        {
            // Copy length of bytes
            copy_lastIndex = (_lastIndex - (uint)_bytesUsed + (uint)usersOutput.Length) & WindowMask;
        }

        uint copied = (uint)usersOutput.Length;

        int spaceLeft = (int)((uint)usersOutput.Length - copy_lastIndex);
        if (spaceLeft > 0)
        {
            // this means we need to copy two parts separately
            // copy the spaceLeft-bytes from the end of the output window
            _window.AsSpan((int)WindowSize - spaceLeft, spaceLeft).CopyTo(usersOutput);
            usersOutput = usersOutput.Slice(spaceLeft, (int)copy_lastIndex);
        }
        _window.AsSpan((int)(copy_lastIndex - (uint)usersOutput.Length), usersOutput.Length).CopyTo(usersOutput);
        Debug.Assert((_bytesUsed- copied) >= 0, "check this function and find why we copied more bytes than we have");
        _bytesUsed -= copied;
        return (int)copied;
    }
}

