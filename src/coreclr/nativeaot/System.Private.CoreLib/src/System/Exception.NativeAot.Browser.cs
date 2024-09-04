// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    public partial class Exception
    {
        private const int WasmFunctionIndexBias = 2;
        private static int s_reportAllFramesAsJS;

        private IntPtr[] _eips;
        private int _eipConsumedCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private unsafe void AppendStack(IntPtr ip, bool isFirstFrame, bool isFirstRethrowFrame)
        {
            Debug.Assert(!isFirstRethrowFrame || isFirstFrame);

            if (isFirstRethrowFrame)
            {
                // A rethrow means we should already have a valid stack trace data array on our hands. Just use it.
                // The right 'catch' frame has already been appended by the previous dispatch - below should logically
                // hold... but the stack trace could have been truncated, or is JS-only, so we can't assert it.
                // Debug.Assert(IsWasmFrame(_eips[_eipConsumedCount - 2], out lastCatchIP) && ip == lastCatchIP);
                // Q: perhaps we should re-initialize the array in the above case then, to supply more frames?
                // A: "Resupplying" frames could be a valid strategy for all calls to this function, if we detect
                //    truncated input. But it could lead to confusing user experience: missing frames instead
                //    of the current 'small number of frames; disable truncation!'. So we leave this be for now.
                Debug.Assert(_eips != null);
                return;
            }

            // On browser, we do stack trace handling for exceptions in two phases:
            // 1. Initialize the whole stack trace - that's all the JS API can offer, and set the cursor to "zero"
            //    (no frames visible as yet).
            //
            if (isFirstFrame)
            {
                // Our implementation of "RhGetCurrentThreadStackTrace" caches the JS stack trace with this pattern,
                // so, while we do have to parse the trace twice, we avoid a complex memory allocation contract.
                int eipCount = RuntimeImports.RhpGetCurrentBrowserThreadStackTrace(0, ReportAllFramesAsJS());
                IntPtr[] allEips = new IntPtr[eipCount];
                fixed (void* pEips = allEips)
                    RuntimeImports.RhpGetCurrentBrowserThreadStackTrace((nuint)pEips, ReportAllFramesAsJS());

                // Skip the following frames:
                //  AppendStack (this frame)
                //  AppendExceptionStackFrame
                //  DispatchException
                //  RhpThrowEx
                int skipppedEipCount = SkipSystemFrames(allEips, skipCount: 4);

                // Q: could we use "_corDbgStackTrace" and "_idxFirstFreeStackTraceEntry" directly?
                // A: yes, but it would require careful consideration of the interaction with all other mutation
                //    sources. Case to consider: filter calling "RestoreDispatchState".
                _eips = allEips;
                _eipConsumedCount = skipppedEipCount;
            }

            // 2. Advance the cursor until it reaches the handling frame (or top of the stack, in case of an unhandled
            //    exception).
            //
            // A couple of things to consider here.
            // 1. The stack trace data could be imprecise, due to, for example, engine-level inlining. Note, however,
            //    that in practice the JS engines are **very** good at preserving traces, even for inlined functions.
            //    This is fortunate for us because the various "skip N frames" places that exists can continue to
            //    function reliably even in the absense of WASM-level noinline annotation.
            // 2. The stack trace data could be truncated (default NodeJS behavior).
            // Both of these cases cause the frames to be missing from the trace, which means we will append "too much".
            // That is mostly ok - our goal is to not append _too little_.
            //
            // The format of the Browser's "IP array" is a little complex. This is due to the requirements of storing
            // native frames (strings copied verbatim from JS) "inline". Concretely, we can have the following values
            // for "IP"s:
            // 1. 0 - unknown/null frame. Takes two chunks to be the same size as a WASM frame.
            // 2. 1 - 'EdiSeparator', as used elsewhere.
            // 3. [2..int.MaxValue] - biased WASM function index. We have to bias the index because function index zero
            //    (and one) is a valid value, although it is probably impossible to construct a useful WASM binary that
            //    has it as a defined function (recall WASM indices start with imports). This index is followed by
            //    the function offset (file-relative, as reported by JS).
            // 4. < 0 - encoded UTF16 strings that represent native frames. The encoding is as follows:
            //    [(1 << 31) | 'length' in UTF16 code points]
            //    [...] - the UTF16 code points themselves, tightly packed (the last 'chunk' may be padded with zeroes).
            //
            IntPtr[] eips = _eips;
            int index = _eipConsumedCount;
            while (index < eips.Length)
            {
                IntPtr eip = eips[index++];
                Debug.Assert(eip != EdiSeparator); // Should never appear in data from JS.
                AppendStackIP(eip, false);

                int jsFrameLength = GetBrowserFrameInfoWithBias(eip, out int wasmFunctionIndexWithBias);
                if (jsFrameLength is 0)
                {
                    AppendStackIP(eips[index++], false); // Append the function offset.

                    if (wasmFunctionIndexWithBias == ip)
                    {
                        // We have found the frame of interest. Note how function indices are not actually
                        // unique identifiers of frames due to recursion. However, it works out because we
                        // establish a new virtual unwind frame on entry to each function with EH, and then
                        // call this method for each said frame.
                        break;
                    }
                }
                else
                {
                    int jsFrameLengthInChunks = GetJSFrameLengthInChunks(jsFrameLength);
                    for (int i = 0; i < jsFrameLengthInChunks; i++)
                    {
                        AppendStackIP(eips[index++], false);
                    }
                }
            }

            Debug.Assert(index <= eips.Length);
            _eipConsumedCount = index;
        }

        // The WASM binary may be modified post-link (e.g. by wasm-opt), which will invalidate our stack trace metadata.
        // To handle this scenario gracefully, we fall back to using 'JS' frames for everything in that case. Obviously,
        // this breaks all of managed stack trace features, but at least it allows ToString() to function and give back
        // theoretically symbolicatable traces.
        internal static unsafe int ReportAllFramesAsJS()
        {
            int reportAllFramesAsJS = s_reportAllFramesAsJS;
            if (reportAllFramesAsJS is 0)
            {
                // To increase our chances of detecting post-link modification, the canary method is placed at the very
                // end of the code section. Of course, this is not 100% reliable. E. g. it won't detect something like
                // simple reordering of the functions (with the canary staying in place). That is acceptable.
                delegate*<int> pGetCanary = &RuntimeImports.RhpGetStackTraceIpCanary;
                int expectedIp = GetBiasedWasmFunctionIndex(pGetCanary());
                int actualIp = RuntimeImports.RhpGetBiasedWasmFunctionIndexForFunctionPointer((nuint)pGetCanary);
                s_reportAllFramesAsJS = reportAllFramesAsJS = (expectedIp == actualIp ? 0 : 1) + 1;
            }

            return reportAllFramesAsJS - 1;
        }

        internal static int SkipSystemFrames(IntPtr[] eips, int skipCount)
        {
            // Skip only if the stack trace is intact (we did not fall back to native frames). We could be
            // even more precise here by comparing actual function indices, but the need for that has not
            // yet been demonstrated.
            int skipppedEipCount = 0;
            for (int i = 0; i < skipCount; i++)
            {
                IntPtr eip = eips[skipppedEipCount];
                GetBrowserFrameInfoWithBias(eip, out int wasmFunctionIndexWithBias);
                if (!IsValidBiasedWasmFunctionIndex(wasmFunctionIndexWithBias))
                {
                    skipppedEipCount = 0;
                    break;
                }

                skipppedEipCount += GetBrowserFrameLengthInChunks(eip);
            }

            return skipppedEipCount;
        }

        // Keep the parsing code in sync with "RhpGetCurrentBrowserThreadStackTrace" in "StackTrace.Browser.cpp".
        internal static int GetBrowserFrameInfoWithoutBias(nint eip, out int wasmFunctionIndex)
        {
            int jsFrameLength = GetBrowserFrameInfoWithBias(eip, out wasmFunctionIndex);
            if (jsFrameLength is 0)
            {
                wasmFunctionIndex = GetUnbiasedWasmFunctionIndex(wasmFunctionIndex);
            }

            return jsFrameLength;
        }

        internal static int GetBrowserFrameInfoWithBias(nint eip, out int wasmFunctionIndexWithBias)
        {
            Debug.Assert(eip != EdiSeparator);
            int actualEip = (int)eip; // Only the lower 32 bits are significant.
            if (actualEip > 0)
            {
                wasmFunctionIndexWithBias = actualEip;
                return 0;
            }

            wasmFunctionIndexWithBias = 0;
            return -actualEip; // Note that unknown frames turn into zero lengths here.
        }

        internal static int GetBrowserFrameLengthInChunks(nint eip)
        {
            if (eip == EdiSeparator)
            {
                return 1;
            }
            int jsFrameLength = GetBrowserFrameInfoWithBias(eip, out _);
            if (jsFrameLength != 0)
            {
                return 1 + GetJSFrameLengthInChunks(jsFrameLength);
            }
            return 2;
        }

        internal static ReadOnlySpan<char> GetJSFrame(IntPtr[] eips, int eipIndex)
        {
            int jsFrameLength = GetBrowserFrameInfoWithBias(eips[eipIndex], out _);
            if (jsFrameLength is not 0)
            {
                ReadOnlySpan<char> data = MemoryMarshal.Cast<IntPtr, char>(eips.AsSpan(eipIndex + 1));
                ReadOnlySpan<char> jsFrame = data.Slice(0, jsFrameLength);
                return jsFrame;
            }
            return [];
        }

        internal static int GetWasmFunctionOffset(nint[] eips, int eipIndex)
        {
            Debug.Assert(GetBrowserFrameInfoWithBias(eips[eipIndex], out int wasmFunctionIndexWithBias) is 0 &&
                         IsValidBiasedWasmFunctionIndex(wasmFunctionIndexWithBias));
            return (int)eips[eipIndex + 1];
        }

        internal static bool IsValidBiasedWasmFunctionIndex(int wasmFunctionIndexWithBias) => wasmFunctionIndexWithBias >= WasmFunctionIndexBias;

        internal static int GetBiasedWasmFunctionIndex(int wasmFunctionIndex) => wasmFunctionIndex + WasmFunctionIndexBias;

        internal static int GetUnbiasedWasmFunctionIndex(int wasmFunctionIndexWithBias)
        {
            Debug.Assert(EdiSeparator + 1 == WasmFunctionIndexBias);
            return wasmFunctionIndexWithBias - WasmFunctionIndexBias;
        }

        private static int GetJSFrameLengthInChunks(int jsFrameLength) => (2 * jsFrameLength + (nint.Size - 1)) / nint.Size;
    }
}
