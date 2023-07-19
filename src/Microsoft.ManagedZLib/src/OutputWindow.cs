// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using static Microsoft.ManagedZLib.ManagedZLib.ZLibStreamHandle;

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
    private int WindowSize; //Const pa hacerlo static para crear el arreglo de bytes.
    private int WindowMask;
    // With Deflate64 we can have up to a 65536 length as well as up to a 65538 distance. This means we need a Window that is at
    // least 131074 bytes long so we have space to retrieve up to a full 64kb in lookback and place it in our buffer without
    // overwriting existing data. OutputBuffer requires that the WindowSize be an exponent of 2, so we round up to 2^18.
    //private const int WindowSize64k = 262144; // -------- 64K
    //private const int WindowMask64k = 262143; //18bit

    private byte[] _window; // The window is 2^n bytes where n is number of bits
    private int _lastIndex;       // this is the position to where we should write next byte
    private int _bytesUsed; // The number of bytes in the output window which is not consumed.

    /// <summary>
    /// Initialized of the output buffer size with the window bits given.
    /// This will recieve the window bits and turn that into a base 2 number for the actual window byte size.
    /// The Output buffer is divided in 2 parts, depending in thetype of deflate, each one of 32K and 64K.
    ///     +---------+---------+
    ///     | 32K/64K | 32K/64K | Giving a total of 64K or 128K (aproximately) sliding window
    ///     +---------+---------+
    /// The decompressed input will go to one of the parts, while the other part will have the 
    /// </summary>
    internal OutputWindow(int windowBits)
    {
        WindowSize = 1 << windowBits; //logaritmic base 2 required - It's like 2^windowBits
        WindowMask = WindowSize - 1;
        _window = new byte[WindowSize];
    }
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
    public void WriteLengthDistance(int length, int distance)
    {
        //Checking there's enough space for copying
        Debug.Assert((_bytesUsed + length) <= WindowSize, "No Enough space");

        // move backwards distance bytes in the output stream,
        // and copy length bytes from this position to the output stream.
        _bytesUsed += length;
        int copyStart = (_lastIndex - distance) & WindowMask; // start position for coping.

        // Total - space that would be taken by copying the length bytes
        int border = WindowSize - length;
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
            // copy byte by byte
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
        // Remember: We are copying the data to the second portion of the window -look-ahead-
        // So bytesUsed might be the size of the first portion 32K or 64K, at this point.
        // AvailableBytes is usually the size of the underlying buffer of the stream (for DeflateStream)
        // This in particular is for copying the input respecting the I/O boundaries. 
        // It will lead us to either copy LEN bytes or just the amount available in the output window
        // (taking into account the byte boundaries)
        /// <summary> 
        /// Either how much input is available or how much free space in the output buffer we have. 
        /// length = Amount decompressed that we are trying to put in the output window
        /// </summary>
        length = Math.Min(Math.Min(length, WindowSize - _bytesUsed), input.AvailableBytes);
        int copied;

        // We might need wrap around to copy all bytes.
        int spaceLeft = WindowSize - _lastIndex;
        if (length > spaceLeft) //Checking is in the boundaries
        {
            // copy the first part
            copied = input.CopyTo(_window, _lastIndex, spaceLeft);
            if (copied == spaceLeft)
            {
                // only try to copy the second part if we have enough bytes in input
                copied += input.CopyTo(_window, 0, length - spaceLeft);
            }
        }
        else
        {
            // only one copy is needed if there is no wrap around.
            copied = input.CopyTo(_window, _lastIndex, length);
        }

        _lastIndex = (_lastIndex + copied) & WindowMask; //To keep it withting the window size boundary
        _bytesUsed += copied;
        return copied; 
    }

    /// <summary>Free space in output window.</summary>
    public int FreeBytes => WindowSize - _bytesUsed;

    /// <summary>Bytes not consumed in output window.</summary>
    public int AvailableBytes => _bytesUsed;

    // ReadInflateOutput
    /// <summary>Copy the decompressed bytes to output buffer.</summary>
    public int CopyTo(Span<byte> usersOutput)
    {
        int copy_lastIndex;

        if (usersOutput.Length > _bytesUsed)
        {
            // we can copy all the decompressed bytes out
            copy_lastIndex = _lastIndex; //Last index auxiliar
            usersOutput = usersOutput.Slice(0,_bytesUsed);
        }
        else
        {
            copy_lastIndex = (_lastIndex - _bytesUsed + usersOutput.Length) & WindowMask; // copy length of bytes
        }

        int copied = usersOutput.Length;

        int spaceLeft = usersOutput.Length - copy_lastIndex;
        if (spaceLeft > 0)
        {
            // this means we need to copy two parts separately
            // copy the spaceLeft bytes from the end of the output window
            _window.AsSpan(WindowSize - spaceLeft, spaceLeft).CopyTo(usersOutput);
            usersOutput = usersOutput.Slice(spaceLeft, copy_lastIndex);
        }
        _window.AsSpan(copy_lastIndex - usersOutput.Length, usersOutput.Length).CopyTo(usersOutput);
        _bytesUsed -= copied;
        Debug.Assert(_bytesUsed >= 0, "check this function and find why we copied more bytes than we have");
        return copied;
    }
}

