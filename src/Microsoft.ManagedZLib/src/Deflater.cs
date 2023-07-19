// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection.Emit;
using System.Security;

using ZErrorCode = Microsoft.ManagedZLib.ManagedZLib.ErrorCode;
using ZFlushCode = Microsoft.ManagedZLib.ManagedZLib.FlushCode;

namespace Microsoft.ManagedZLib
{
    /// <summary>
    /// Provides a wrapper around the ZLib compression API.
    /// </summary>
    internal sealed class Deflater
    {
        //Vivi's note> MemoryHandle was for managing the pointers. Gone now
        private bool _isDisposed;
        private const int minWindowBits = -15;  // WindowBits must be between -8..-15 to write no header, 8..15 for a
        private const int maxWindowBits = 31;   // zlib header, or 24..31 for a GZip header
        private int _windowBits;
        private readonly OutputWindow _output;
        private readonly InputBuffer _input;
        public bool NeedsInput() => _input.NeedsInput();

        // Note, DeflateStream or the deflater do not try to be thread safe.
        // The lock is just used to make writing to unmanaged structures atomic to make sure
        // that they do not get inconsistent fields that may lead to an unmanaged memory violation.
        // To prevent *managed* buffer corruption or other weird behaviour users need to synchronise
        // on the stream explicitly.
        private object SyncLock => this;

        internal Deflater(CompressionLevel compressionLevel, int windowBits)
        {
            ManagedZLib.CompressionLevel zlibCompressionLevel;
            int memLevel;
            _windowBits = DeflateInit(windowBits); //Checking format of compression> Raw, Gzip or ZLib
            _output = new OutputWindow(_windowBits);
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

            ManagedZLib.CompressionStrategy strategy = ManagedZLib.CompressionStrategy.DefaultStrategy;

            DeflateInit2(zlibCompressionLevel, ManagedZLib.CompressionMethod.Deflated, windowBits, memLevel, strategy);


        }
        private int DeflateInit( int windowBits)
        {
            Debug.Assert(windowBits >= minWindowBits && windowBits <= maxWindowBits);
            //-15 to -1 or 0 to 47
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

        internal void SetInput(ReadOnlySpan<byte> inputBuffer)
        {
            Debug.Assert(NeedsInput(), "We have something left in previous input!");
            if (0 == inputBuffer.Length)
            {
                return;
            }
        }
        //Vivi's notes> This overloading might be repetitive
        internal void SetInput(Span<byte> inputBuffer, int count)
        {
            Debug.Assert(NeedsInput(), "We have something left in previous input!");
            Debug.Assert(inputBuffer != null);

            if (count == 0)
            {
                return;
            }

        }

        internal int GetDeflateOutput(byte[] outputBuffer)
        {
            Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
            Debug.Assert(!NeedsInput(), "GetDeflateOutput should only be called after providing input");

            int bytesRead = ReadDeflateOutput(outputBuffer, ZFlushCode.NoFlush);
            return bytesRead;

        }

        public int ReadDeflateOutput(Span<byte> outputBuffer, ZFlushCode flushCode)
        {
            Debug.Assert(outputBuffer.Length > 0); // This used to be nullable - Check behavior later
            int count = 0;
            lock (SyncLock)
            {
                //Here will copy from the output buffer to the stream
                // and deflate

                count = Deflate(flushCode);
                int bytesRead = outputBuffer.Length - _output.AvailableBytes;

                return bytesRead;
            }
        }

        internal int Finish(byte[] outputBuffer)
        {
            Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
            Debug.Assert(outputBuffer.Length > 0, "Can't pass in an empty output buffer!");

            int bytesRead = ReadDeflateOutput(outputBuffer, ZFlushCode.Finish); //We may to do byte
            return bytesRead; //return _state == DeflateState.StreamEnd; or DeflateState.Done;
        }

        /// <summary>
        /// Returns true if there was something to flush. Otherwise False.
        /// </summary>
        internal bool Flush(byte[] outputBuffer)
        {
            Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
            Debug.Assert(outputBuffer.Length > 0, "Can't pass in an empty output buffer!");
            Debug.Assert(NeedsInput(), "We have something left in previous input!");


            // Note: we require that NeedsInput() == true, i.e. that 0 == _zlibStream.AvailIn.
            // If there is still input left we should never be getting here; instead we
            // should be calling GetDeflateOutput.

            return ReadDeflateOutput(outputBuffer, ZFlushCode.SyncFlush) != 0;
        }



        private int Deflate(ZFlushCode flushCode)
        {

            return Deflate(flushCode); ; //Deflate + error checking (in progress)

        }
    }
}
