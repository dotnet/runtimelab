// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Diagnostics
{
    public partial class StackTrace
    {
        /// <summary>
        /// Initialize the stack trace based on current thread and given initial frame index.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private unsafe void InitializeForCurrentThread(int skipFrames, bool needFileInfo)
        {
            const int SystemDiagnosticsStackDepth = 2;

            int eipCount = RuntimeImports.RhpGetCurrentBrowserThreadStackTrace(0, Exception.ReportAllFramesAsJS());
            IntPtr[] eips = new IntPtr[eipCount];
            fixed (void* pEips = eips)
                RuntimeImports.RhpGetCurrentBrowserThreadStackTrace((nuint)pEips, Exception.ReportAllFramesAsJS());

            int skippedEipCount = Exception.SkipSystemFrames(eips, SystemDiagnosticsStackDepth);
            InitializeForIpAddressArray(eips, skippedEipCount, skipFrames, needFileInfo);
        }

        /// <summary>
        /// Initialize the stack trace based on a given exception and initial frame index.
        /// </summary>
        private void InitializeForException(Exception exception, int skipFrames, bool needFileInfo)
        {
            InitializeForIpAddressArray(exception.GetStackIPs(), 0, skipFrames, needFileInfo);
        }

        /// <summary>
        /// Initialize the stack trace based on a given array of encoded IP addresses (EIPs).
        /// </summary>
        private void InitializeForIpAddressArray(IntPtr[] eips, int skippedEipCount, int skipFrames, bool needFileInfo)
        {
            // Our callers may pass us values that have overflown.
            if (skipFrames < 0)
                skipFrames = int.MaxValue;

            // Calculate true frame count upfront - we need to skip EdiSeparators which get
            // collapsed onto boolean flags on the preceding stack frame
            int outputFrameCount = 0;
            for (int eipIndex = skippedEipCount, actualFrameIndex = 0; eipIndex < eips.Length; actualFrameIndex++)
            {
                IntPtr eip = eips[eipIndex];
                if (actualFrameIndex >= skipFrames && eip != Exception.EdiSeparator)
                {
                    outputFrameCount++;
                }

                eipIndex += Exception.GetBrowserFrameLengthInChunks(eip);
            }

            if (outputFrameCount > 0)
            {
                _stackFrames = new StackFrame[outputFrameCount];
                int outputFrameIndex = 0;
                for (int eipIndex = skippedEipCount, actualFrameIndex = 0; eipIndex < eips.Length; actualFrameIndex++)
                {
                    IntPtr eip = eips[eipIndex];
                    if (actualFrameIndex >= skipFrames)
                    {
                        if (outputFrameIndex >= outputFrameCount)
                        {
                            break;
                        }

                        if (eip != Exception.EdiSeparator)
                        {
                            _stackFrames[outputFrameIndex++] = new StackFrame(eips, eipIndex, needFileInfo);
                        }
                        else if (outputFrameIndex > 0)
                        {
                            _stackFrames[outputFrameIndex - 1].SetIsLastFrameFromForeignExceptionStackTrace();
                        }
                    }

                    eipIndex += Exception.GetBrowserFrameLengthInChunks(eip);
                }
                Debug.Assert(outputFrameIndex == outputFrameCount);
            }

            _numOfFrames = outputFrameCount;
            _methodsToSkip = 0;
        }
    }
}
