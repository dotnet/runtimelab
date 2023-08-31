// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    private int WindowSize;
    private int WindowMask;
    private Memory<byte> _window; // The window is 2^n bytes where n is number of bits
    private int _lastIndex; // Position to where we should write next byte
    private int _bytesUsed; // Number of bytes in the output window that haven't been consumed yet.

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
        WindowSize = 1 << windowBits; //logaritmic base 2
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
        _window.Span[_lastIndex++] = b;
        _lastIndex &= WindowMask;
        ++_bytesUsed;
    }

    //Important part of LZ77 algorithm
    public void WriteLengthDistance(int length, int distance)
    {
        //Checking there's enough space for copying the output
        Debug.Assert((_bytesUsed + length) <= WindowSize, "No Enough space");

        // Move backwards distance bytes in the output stream,
        // and copy length bytes from this position to the output stream.
        _bytesUsed += length;
        int copyStart = (_lastIndex - distance) & WindowMask;

        // Total space that would be taken by copying the length bytes
        int border = WindowSize - length;
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
        length = Math.Min(Math.Min(length, WindowSize - _bytesUsed), (int)input.AvailableBytes);
        int copied;

        // We might need wrap around to copy all bytes.
        int spaceLeft = WindowSize - _lastIndex;

        Debug.Assert(_window.Span != null);
        if (length > spaceLeft) //Checking is within the boundaries
        {
            // Copy the first part
            Debug.Assert(_lastIndex >= 0);
            Debug.Assert(spaceLeft >= 0);
            Debug.Assert(_lastIndex <= _window.Length - spaceLeft);
            copied = input.CopyTo(_window.Slice(_lastIndex, spaceLeft));
            if (copied == spaceLeft)
            {
                // Only try to copy the second part if we have enough bytes in input
                Debug.Assert((length - spaceLeft) >= 0);
                Debug.Assert(0 <= _window.Length - (length - spaceLeft));
                copied += input.CopyTo(_window.Slice(0, length - spaceLeft));
            }
        }
        else
        {
            // Only one copy is needed if there is no wrap around.
            Debug.Assert(_lastIndex >= 0);
            Debug.Assert(length >= 0);
            Debug.Assert(_lastIndex <= _window.Length - length);
            copied = input.CopyTo(_window.Slice(_lastIndex, length));

        }

        _lastIndex = (_lastIndex + copied) & WindowMask;
        _bytesUsed += copied;
        return copied;
    }

    /// <summary>Free space in output window.</summary>
    public int FreeBytes => WindowSize - _bytesUsed;

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
            usersOutput = usersOutput.Slice(0, _bytesUsed);
        }
        else
        {
            // Copy length of bytes
            copy_lastIndex = (_lastIndex - _bytesUsed + usersOutput.Length) & WindowMask;
        }

        int copied = usersOutput.Length;

        int spaceLeft = usersOutput.Length - copy_lastIndex;
        if (spaceLeft > 0)
        {
            // this means we need to copy two parts separately
            // copy the spaceLeft-bytes from the end of the output window
            _window.Span.Slice(WindowSize - spaceLeft, spaceLeft).CopyTo(usersOutput);
            usersOutput = usersOutput.Slice(spaceLeft, copy_lastIndex);
        }
        _window.Span.Slice(copy_lastIndex - usersOutput.Length, usersOutput.Length).CopyTo(usersOutput);
        _bytesUsed -= copied;
        Debug.Assert(_bytesUsed >= 0, "check this function and find why we copied more bytes than we have");
        return copied;
    }
}