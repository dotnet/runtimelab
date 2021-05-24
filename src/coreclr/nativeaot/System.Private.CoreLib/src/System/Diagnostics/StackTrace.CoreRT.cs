// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;

using Internal.DeveloperExperience;

namespace System.Diagnostics
{
    public partial class StackTrace
    {
        /// <summary>
        /// Construct a stack trace based on a subset of a precomputed array of IP addresses.
        /// </summary>
        /// <param name="ipAddresses">Array of IP addresses to use as the stack trace</param>
        /// <param name="startIndex">Starting index in the array to use</param>
        /// <param name="endIndex">Ending index in the array (one plus the last element)</param>
        /// <param name="needFileInfo">True when source file / line information is requested</param>
        internal StackTrace(IntPtr[] ipAddresses, int startIndex, int endIndex, bool needFileInfo)
        {
            InitializeForIpAddressArray(ipAddresses, startIndex, endIndex, needFileInfo);
        }

#if !TARGET_WASM
        /// <summary>
        /// Initialize the stack trace based on current thread and given initial frame index.
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        private void InitializeForCurrentThread(int skipFrames, bool needFileInfo)
        {
            const int SystemDiagnosticsStackDepth = 2;

            int frameCount = -RuntimeImports.RhGetCurrentThreadStackTrace(Array.Empty<IntPtr>());
            Debug.Assert(frameCount >= 0);
            IntPtr[] stackTrace = new IntPtr[frameCount];
            int trueFrameCount = RuntimeImports.RhGetCurrentThreadStackTrace(stackTrace);
            Debug.Assert(trueFrameCount == frameCount);
            if (trueFrameCount > 0 && IsRhGetCurrentThreadStackTraceFrame(stackTrace[0]))
            {
                skipFrames++;
            }
            InitializeForIpAddressArray(stackTrace, skipFrames + SystemDiagnosticsStackDepth, frameCount, needFileInfo);
        }
#endif

        /// <summary>
        /// Checks if ipAddress is for RhGetCurrentThreadStackTrace frame
        /// </summary>
        internal static bool IsRhGetCurrentThreadStackTraceFrame(IntPtr ipAddress)
        {
            return DeveloperExperience.Default.CreateStackTraceString(ipAddress, false).StartsWith("System.Runtime.RuntimeExports.RhGetCurrentThreadStackTrace(IntPtr[])", StringComparison.Ordinal);
        }

        /// <summary>
        /// Initialize the stack trace based on a given exception and initial frame index.
        /// </summary>
        private void InitializeForException(Exception exception, int skipFrames, bool needFileInfo)
        {
            IntPtr[] stackIPs = exception.GetStackIPs();
            InitializeForIpAddressArray(stackIPs, skipFrames, stackIPs.Length, needFileInfo);
        }

        /// <summary>
        /// Initialize the stack trace based on a given array of IP addresses.
        /// </summary>
        private void InitializeForIpAddressArray(IntPtr[] ipAddresses, int skipFrames, int endFrameIndex, bool needFileInfo)
        {
            int frameCount = (skipFrames < endFrameIndex ? endFrameIndex - skipFrames : 0);

            // Calculate true frame count upfront - we need to skip EdiSeparators which get
            // collapsed onto boolean flags on the preceding stack frame
            int outputFrameCount = 0;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                if (ipAddresses[frameIndex + skipFrames] != Exception.EdiSeparator)
                {
                    outputFrameCount++;
                }
            }

            if (outputFrameCount > 0)
            {
                _stackFrames = new StackFrame[outputFrameCount];
                int outputFrameIndex = 0;
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    IntPtr ipAddress = ipAddresses[frameIndex + skipFrames];
                    if (ipAddress != Exception.EdiSeparator)
                    {
                        _stackFrames[outputFrameIndex++] = new StackFrame(ipAddress, needFileInfo);
                    }
                    else if (outputFrameIndex > 0)
                    {
                        _stackFrames[outputFrameIndex - 1].SetIsLastFrameFromForeignExceptionStackTrace();
                    }
                }
                Debug.Assert(outputFrameIndex == outputFrameCount);
            }

            _numOfFrames = outputFrameCount;
            _methodsToSkip = 0;
        }

#if !TARGET_WASM
        internal void ToString(TraceFormat traceFormat, StringBuilder builder)
        {
            if (_stackFrames == null)
            {
                return;
            }

            foreach (StackFrame frame in _stackFrames)
            {
                frame.AppendToStackTrace(builder);
            }

            if (traceFormat == TraceFormat.Normal && builder.Length >= Environment.NewLine.Length)
                builder.Length -= Environment.NewLine.Length;
        }
#endif
    }
}
