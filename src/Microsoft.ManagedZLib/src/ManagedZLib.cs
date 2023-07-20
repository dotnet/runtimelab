﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.ManagedZLib;

//Vivi's notes: I'll keep this class until the bare basics are met, like Raw In/Deflate for handling Deflate Blocks (RFC1951).
// So far it seems to not be needed anymore, since all is being handled by Inflator and Deflator.
public static class ManagedZLib
{
    public enum FlushCode : int // For knowing how much and when to produce output. Mainly applicable to Deflater.
    {
        NoFlush = 0,
        SyncFlush = 2,
        Finish = 4,
        Block = 5
    }

    public enum ErrorCode : int //For error checking - Can be replaced by using exceptions
    {
        Ok = 0,
        StreamEnd = 1,
        StreamError = -2,
        DataError = -3,
        MemError = -4,
        BufError = -5,
        VersionError = -6
    }

    public enum BlockType // For inflate (RFC1951 deflate format)
    {
        Uncompressed = 0,
        Static = 1, //Fixed
        Dynamic = 2
    }
    /// <summary>
    /// <p>ZLib can accept any integer value between 0 and 9 (inclusive) as a valid compression level parameter:
    /// 1 gives best speed, 9 gives best compression, 0 gives no compression at all (the input data is simply copied a block at a time).
    /// <code>CompressionLevel.DefaultCompression</code> = -1 requests a default compromise between speed and compression
    /// (currently equivalent to level 6).</p>
    ///
    /// <p><strong>How to choose a compression level:</strong></p>
    ///
    /// <p>The names <code>NoCompression</code>, <code>BestSpeed</code>, <code>DefaultCompression</code>, <code>BestCompression</code> are taken over from
    /// the corresponding ZLib definitions, which map to our public NoCompression, Fastest, Optimal, and SmallestSize respectively.</p>
    /// <p><em>Optimal Compression:</em></p>
    /// <p><code>ZLibNative.CompressionLevel compressionLevel = ZLibNative.CompressionLevel.DefaultCompression;</code> <br />
    ///    <code>int windowBits = 15;  // or -15 if no headers required</code> <br />
    ///    <code>int memLevel = 8;</code> <br />
    ///    <code>ZLibNative.CompressionStrategy strategy = ZLibNative.CompressionStrategy.DefaultStrategy;</code> </p>
    ///
    ///<p><em>Fastest compression:</em></p>
    ///<p><code>ZLibNative.CompressionLevel compressionLevel = ZLibNative.CompressionLevel.BestSpeed;</code> <br />
    ///   <code>int windowBits = 15;  // or -15 if no headers required</code> <br />
    ///   <code>int memLevel = 8; </code> <br />
    ///   <code>ZLibNative.CompressionStrategy strategy = ZLibNative.CompressionStrategy.DefaultStrategy;</code> </p>
    ///
    /// <p><em>No compression (even faster, useful for data that cannot be compressed such some image formats):</em></p>
    /// <p><code>ZLibNative.CompressionLevel compressionLevel = ZLibNative.CompressionLevel.NoCompression;</code> <br />
    ///    <code>int windowBits = 15;  // or -15 if no headers required</code> <br />
    ///    <code>int memLevel = 7;</code> <br />
    ///    <code>ZLibNative.CompressionStrategy strategy = ZLibNative.CompressionStrategy.DefaultStrategy;</code> </p>
    ///
    /// <p><em>Smallest Size Compression:</em></p>
    /// <p><code>ZLibNative.CompressionLevel compressionLevel = ZLibNative.CompressionLevel.BestCompression;</code> <br />
    ///    <code>int windowBits = 15;  // or -15 if no headers required</code> <br />
    ///    <code>int memLevel = 8;</code> <br />
    ///    <code>ZLibNative.CompressionStrategy strategy = ZLibNative.CompressionStrategy.DefaultStrategy;</code> </p>
    /// </summary>
    public enum CompressionLevel : int
    {
        NoCompression = 0,
        BestSpeed = 1,
        DefaultCompression = -1,
        BestCompression = 9
    }

    /// <summary>
    /// <p><strong>From the ZLib manual:</strong></p>
    /// <p><code>CompressionStrategy</code> is used to tune the compression algorithm.<br />
    /// </summary>public enum CompressionStrategy : int
    public enum CompressionStrategy : int
    {
        DefaultStrategy = 0
    }

    /// <summary>
    /// In version 1.2.3, ZLib provides on the <code>Deflated</code>-<code>CompressionMethod</code>.
    /// </summary>
    public enum CompressionMethod : int
    {
        Deflated = 8 //Default compression method - deflate
    }
    // Raw deflate is actually the more basic format for defalte and inflate. The other ones like GZip and ZLib have a wrapper around
    // the data/deflate block.
    /// <summary>
    /// <p><strong>From the ZLib manual:</strong></p>
    /// <p>ZLib's <code>windowBits</code> parameter is the base two logarithm of the window size (the size of the history buffer).
    /// It should be in the range 8..15 for this version of the library. Larger values of this parameter result in better compression
    /// at the expense of memory usage. The default value is 15 if deflateInit is used instead.<br /></p>
    /// <strong>Note</strong>:
    /// <code>windowBits</code> can also be -8..-15 for raw deflate. In this case, -windowBits determines the window size.
    /// <code>Deflate</code> will then generate raw deflate data with no ZLib header or trailer, and will not compute an adler32 check value.<br />
    /// <p>See also: How to choose a compression level (in comments to <code>CompressionLevel</code>.</p>
    /// </summary>
    public const int Deflate_DefaultWindowBits = -15; // Legal values are 8..15 and -8..-15. 15 is the window size,
                                                      // negative val causes deflate to produce raw deflate data (no zlib header).

    /// <summary>
    /// <p><strong>From the ZLib manual:</strong></p>
    /// <p>ZLib's <code>windowBits</code> parameter is the base two logarithm of the window size (the size of the history buffer).
    /// It should be in the range 8..15 for this version of the library. Larger values of this parameter result in better compression
    /// at the expense of memory usage. The default value is 15 if deflateInit is used instead.<br /></p>
    /// </summary>
    public const int ZLib_DefaultWindowBits = 15;

    /// <summary>
    /// <p>Zlib's <code>windowBits</code> parameter is the base two logarithm of the window size (the size of the history buffer).
    /// For GZip header encoding, <code>windowBits</code> should be equal to a value between 8..15 (to specify Window Size) added to
    /// 16. The range of values for GZip encoding is therefore 24..31.
    /// <strong>Note</strong>:
    /// The GZip header will have no file name, no extra data, no comment, no modification time (set to zero), no header crc, and
    /// the operating system will be set based on the OS that the ZLib library was compiled to. <code>ZStream.adler</code>
    /// is a crc32 instead of an adler32.</p>
    /// </summary>
    public const int GZip_DefaultWindowBits = 31;

    /// <summary>
    /// <p><strong>From the ZLib manual:</strong></p>
    /// <p>The <code>memLevel</code> parameter specifies how much memory should be allocated for the internal compression state.
    /// <code>memLevel</code> = 1 uses minimum memory but is slow and reduces compression ratio; <code>memLevel</code> = 9 uses maximum
    /// memory for optimal speed. The default value is 8.</p>
    /// <p>See also: How to choose a compression level (in comments to <code>CompressionLevel</code>.</p>
    /// </summary>
    public const int Deflate_DefaultMemLevel = 8;     // Memory usage by deflate. Legal range: [1..9]. 8 is ZLib default.
                                                      // More is faster and better compression with more memory usage.
    public const int Deflate_NoCompressionMemLevel = 7;

    public const byte GZip_Header_ID1 = 31;
    public const byte GZip_Header_ID2 = 139;

    /**
     * Do not remove the nested typing of types inside of <code>System.IO.Compression.ZLibNative</code>.
     * This was done on purpose to:
     *
     * - Achieve the right encapsulation in a situation where <code>ZLibNative</code> may be compiled division-wide
     *   into different assemblies that wish to consume <code>System.IO.Compression.Native</code>. Since <code>internal</code>
     *   scope is effectively like <code>public</code> scope when compiling <code>ZLibNative</code> into a higher
     *   level assembly, we need a combination of inner types and <code>private</code>-scope members to achieve
     *   the right encapsulation.
     *
     * - Achieve late dynamic loading of <code>System.IO.Compression.Native.dll</code> at the right time.
     *   The native assembly will not be loaded unless it is actually used since the loading is performed by a static
     *   constructor of an inner type that is not directly referenced by user code.
     *
     *   In Dev12 we would like to create a proper feature for loading native assemblies from user-specified
     *   directories in order to PInvoke into them. This would preferably happen in the native interop/PInvoke
     *   layer; if not we can add a Framework level feature.
     */

    /// <summary>
    /// The <code>ZLibStreamHandle</code> could be a <code>CriticalFinalizerObject</code> rather than a
    /// <code>SafeHandleMinusOneIsInvalid</code>. This would save an <code>IntPtr</code> field since
    /// <code>ZLibStreamHandle</code> does not actually use its <code>handle</code> field.
    /// Instead it uses a <code>private ZStream zStream</code> field which is the actual handle data
    /// structure requiring critical finalization.
    /// However, we would like to take advantage if the better debugability offered by the fact that a
    /// <em>releaseHandleFailed MDA</em> is raised if the <code>ReleaseHandle</code> method returns
    /// <code>false</code>, which can for instance happen if the underlying ZLib <code>XxxxEnd</code>
    /// routines return an failure error code.
    /// </summary>
    public sealed class ZLibStreamHandle
    {
        public ErrorCode DeflateInit2_(CompressionLevel level, int windowBits, int memLevel, CompressionStrategy strategy)
        {
            //This would have gone to a PInvoke
            return ErrorCode.Ok;
        }


        public ErrorCode Deflate(FlushCode flush)
        {
            // This would have gone to a PInvoke
            return ErrorCode.Ok;
        }


        public ErrorCode DeflateEnd()
        {
            // This would have gone to a PInvoke
            return ErrorCode.Ok;
        }

        public ErrorCode InflateInit2_(int windowBits)
        {
            // This would have gone to a PInvoke
            return ErrorCode.Ok;
        }


        public ErrorCode Inflate(FlushCode flush)
        {
            // This would have gone to a PInvoke for the native version of ZLib inflate
            return ErrorCode.Ok;
        }


        public ErrorCode InflateEnd()
        {
            return ErrorCode.Ok;
        }
        
    }
}
