// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Diagnostics.Tracing
{
    [EventSource(Guid = "E13C0D23-CCBC-4E12-931B-D9CC2EEE27E4", Name = EventSourceName)]
    [EventSourceAutoGenerate]
    internal sealed partial class NativeRuntimeEventSource : EventSource
    {
        internal const string EventSourceName = "Microsoft-Windows-DotNETRuntime";
        public static readonly NativeRuntimeEventSource Log = new NativeRuntimeEventSource();

        // Parameterized constructor to block initialization and ensure the EventSourceGenerator is creating the default constructor
        // as you can't make a constructor partial.
        private NativeRuntimeEventSource(int _) { }

        public class Keywords
        {
            public const EventKeywords ThreadingKeyword = (EventKeywords)0x10000;
            public const EventKeywords ThreadTransferKeyword = (EventKeywords)0x80000000;
        }

        [NonEvent]
        internal static void LogThreadPoolWorkerThreadStart(uint ActiveWorkerThreadCount, uint RetiredWorkerThreadCount, ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolWorkerThreadStop(uint ActiveWorkerThreadCount, uint RetiredWorkerThreadCount, ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolWorkerThreadWait(uint ActiveWorkerThreadCount, uint RetiredWorkerThreadCount, ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolWorkerThreadAdjustmentSample(double Throughput, ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolWorkerThreadAdjustmentAdjustment(double AverageThroughput, uint NewWorkerThreadCount, NativeRuntimeEventSource.ThreadAdjustmentReasonMap Reason, ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolWorkerThreadAdjustmentStats(
            double Duration,
            double Throughput,
            double ThreadPoolWorkerThreadWait,
            double ThroughputWave,
            double ThroughputErrorEstimate,
            double AverageThroughputErrorEstimate,
            double ThroughputRatio,
            double Confidence,
            double NewControlSetting,
            ushort NewThreadWaveMagnitude,
            ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolIOEnqueue(
            IntPtr NativeOverlapped,
            IntPtr Overlapped,
            bool MultiDequeues,
            ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolIODequeue(
            IntPtr NativeOverlapped,
            IntPtr Overlapped,
            ushort ClrInstanceID) { }

        [NonEvent]
        internal static void LogThreadPoolWorkingThreadCount(
            uint Count,
            ushort ClrInstanceID
        ) { }
    }
}
