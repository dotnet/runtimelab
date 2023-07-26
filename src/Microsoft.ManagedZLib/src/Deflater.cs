// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Reflection.Emit;
using System.Security;

using ZErrorCode = Microsoft.ManagedZLib.ManagedZLib.ErrorCode;
using ZFlushCode = Microsoft.ManagedZLib.ManagedZLib.FlushCode;

namespace Microsoft.ManagedZLib
{
    /// <summary>
    /// Provides a wrapper around the ZLib compression API.
    /// </summary>
    internal class Deflater
    {
        public const int NIL = 0; /* Tail of hash chains */
        // States - Might replace later for flags
        public const int InitState = 42;
        public const int GZipState = 57;
        public const int BusyState = 113; //defalte->Finished -- using just Finished() might be enough

        //Min&Max match lengths.
        public const int MinMatch = 3;
        public const int MaxMatch = 258;
        public const int MinLookahead = MaxMatch + MinMatch + 1;

        private bool _isDisposed;
        bool _flushDone = false;
        private const int MinWindowBits = -15;  // WindowBits must be between -8..-15 to write no header, 8..15 for a
        private const int MaxWindowBits = 31;   // zlib header, or 24..31 for a GZip header
        private int _windowBits;
        private int _strHashIndex;
        
        private int _wrap; //Default: Raw Deflate
        int _status;

        private DeflaterState _state; //Class with states - See if its suitable to have a class like with the inflater
        private readonly OutputWindow _output;
        private readonly InputBuffer _input;
        public bool NeedsInput() => _input.NeedsInput();
        public bool BlockDone() => _flushDone == true;
        // This is when inputBuffer is empty, all processed and the output buffer is not.
        //It's the start of the finish.
        //public bool NeedJustOutput() => _deflater.NeedsInput() && ((Deflater)_buffer?.Length != 0);
        public bool Finished() => _state == DeflaterState.Done; //Change this to all the checks done in DeflateEnd() -
                                                                //To see if it ahs finished the state machine
        public int MaxDistance() => _output._windowSize - MinLookahead; 

        // Note, DeflateStream or the deflater do not try to be thread safe.
        // The lock is just used to make writing to unmanaged structures atomic to make sure
        // that they do not get inconsistent fields that may lead to an unmanaged memory violation.
        // To prevent *managed* buffer corruption or other weird behaviour users need to synchronise
        // on the stream explicitly.
        private object SyncLock => this;

        internal Deflater(CompressionLevel compressionLevel, int windowBits)
        {
            _wrap = 0;
            ManagedZLib.CompressionLevel zlibCompressionLevel;
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
            _windowBits = DeflateInit(windowBits); //Checking format of compression> Raw, Gzip or ZLib
            _output = new OutputWindow(_windowBits,memLevel); //Setting window size and mask
            _status = InitState;
            _strHashIndex = 0;
            ManagedZLib.CompressionStrategy strategy = ManagedZLib.CompressionStrategy.DefaultStrategy;

            DeflateInit2(zlibCompressionLevel, ManagedZLib.CompressionMethod.Deflated, windowBits, memLevel, strategy);


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
            // No estoy segura de por que debemos guardar el estado del anterior flush,
            // pero copiare exactamente como esta
            ZFlushCode olfFlush = flushCode;
            //Se copia el stream pero ese ya esta implicito en nuestras variables globales
            // Se checa que es stream sea valido
            // Y no estamos en el finished stated

            //Write the header
            if (_status == InitState && _wrap == 0) {
                _status = BusyState;
            }
            if (_status == InitState) //ZLib
            {
                //ZLib header
                int header = ((int)ManagedZLib.CompressionMethod.Deflated + ((_windowBits - 8) << 4)) << 8;
                // Check the compression level and more
            }
            if (_status == GZipState){ }//Gzip header


            if (flushCode != ZFlushCode.NoFlush && Finished()) { 
            }

            return Deflate(flushCode); ; //Deflate + error checking (in progress)

        }

        public bool DeflateSlow(Span<byte> buffer, ZFlushCode flushCode) 
        {

            return false;
        }

        public bool DeflateFast(Span<byte> buffer, ZFlushCode flushCode)
        {
            uint hashHead; //Head of the hash chain - index
            //bool blockFlush; //Set if current block must be flushed

            while (true)
            {
                /* Make sure that we always have enough lookahead, except
                 * at the end of the input file. We need MAX_MATCH bytes
                 * for the next match, plus MIN_MATCH bytes to insert the
                 * string following the next match.
                 */
                if (_output._lookahead == MinMatch) 
                {
                    if (_output._lookahead < MinLookahead && flushCode == ManagedZLib.FlushCode.NoFlush)
                    {
                        return NeedsInput();
                    }
                    if (_output._lookahead == 0) break; /* flush the current block */
                }
                hashHead = NIL; //hash head starts with tail's value - empty hash
                if (_output._lookahead >= MinMatch) {
                    hashHead = _output.InsertString(_output._strStart);
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
                    //_output.CheckMatch(s, s->strstart, s->match_start, s->match_length); //Aqui creo que no le pasas nada
                    // le terminaras pasando el flush, el resto ya lo tiene la clase
                    _tr_tally_dist(s, s->strstart - s->match_start, s->match_length - MIN_MATCH, bflush);

                    _output._lookahead -= _output._matchLength;
                }


            }

            return false;
        }
        
    }
}
