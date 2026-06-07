// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using System.Runtime.CompilerServices;

namespace System.Diagnostics
{
    public partial class StackFrame
    {
        private IntPtr[] _eips;
        private int _eipIndex;

        internal StackFrame(IntPtr[] eips, int eipIndex, bool needFileInfo)
        {
            InitializeForEip(eips, eipIndex, needFileInfo);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private unsafe void BuildStackFrameViaNativeUnwind(int frameIndex, bool needFileInfo)
        {
            frameIndex += 1; // We may have a value that has already overflown or will overflow.
            if (frameIndex < 0)
                frameIndex = int.MaxValue;

            // We have to parse the entire stack to get just one frame...
            int eipCount = RuntimeImports.RhpGetCurrentBrowserThreadStackTrace(0, Exception.ReportAllFramesAsJS());
            IntPtr[] eips = new IntPtr[eipCount];
            fixed (void* pEips = eips)
                RuntimeImports.RhpGetCurrentBrowserThreadStackTrace((nuint)pEips, Exception.ReportAllFramesAsJS());

            for (int eipIndex = 0, actualFrameIndex = 0; eipIndex < eipCount; actualFrameIndex++)
            {
                int length = Exception.GetBrowserFrameLengthInChunks(eips[eipIndex]);
                if (frameIndex == actualFrameIndex)
                {
                    // Trim the EIP array to avoid rooting the whole thing.
                    IntPtr[] justFrameEips = eips.AsSpan(eipIndex, length).ToArray();
                    InitializeForEip(justFrameEips, 0, needFileInfo);
                    return;
                }

                eipIndex += length;
            }

            // Frame info not found, build a dummy instance.
            InitializeForIpAddress(IntPtr.Zero, needFileInfo);
        }

        private void InitializeForEip(IntPtr[] eips, int eipIndex, bool needFileInfo)
        {
            IntPtr ip;
            IntPtr eip = eips[eipIndex];
            if (eip == Exception.EdiSeparator)
            {
                ip = Exception.EdiSeparator;
            }
            else
            {
                // We (have to) use the biased function index as IP because "0" is a valid function index.
                if (Exception.GetBrowserFrameInfoWithBias(eip, out int wasmFunctionIndexWithBias) is not 0 ||
                    Exception.IsValidBiasedWasmFunctionIndex(wasmFunctionIndexWithBias))
                {
                    _eips = eips;
                    _eipIndex = eipIndex;
                }
                ip = wasmFunctionIndexWithBias;
            }
            InitializeForIpAddress(ip, needFileInfo);
        }
    }
}
