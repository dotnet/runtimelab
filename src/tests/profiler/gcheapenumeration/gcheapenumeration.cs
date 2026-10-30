// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Profiler.Tests
{
    [StructLayout(LayoutKind.Sequential)]
    struct ReferencePair
    {
        public object First;
        public int Padding;
        public object Second;
    }

    class ObjectReferenceGraph
    {
        public object[] Array = new object[] { new object(), null, new object() };
        public ReferencePair[] StructArray = new ReferencePair[]
        {
            new ReferencePair { First = new object(), Padding = 123, Second = null },
            new ReferencePair { First = new object(), Padding = 456, Second = new object() }
        };
        public object[] EmptyArray = new object[0];
        public object Missing = null;
    }

    class ObjectReferenceTrigger {}

    class GCHeapEnumerationTests
    {
        static readonly string ShouldSetMonitorGCEventMaskEnvVar = "Set_Monitor_GC_Event_Mask";
        static readonly Guid GCHeapEnumerationProfilerGuid = new Guid("8753F0E1-6D6D-4329-B8E1-334918869C15");
        static List<object> _rootObjects = new List<object>();
        static bool IsSatori => GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object value) && value is true;

        [DllImport("Profiler")]
        private static extern void EnumerateGCHeapObjectsWithoutProfilerRequestedRuntimeSuspension();

        [DllImport("Profiler")]
        private static extern void EnumerateGCHeapObjectsWithinProfilerRequestedRuntimeSuspension();

        public static int EnumerateObjectReferences()
        {
            var graph = new ObjectReferenceGraph();
            GC.KeepAlive(new ObjectReferenceTrigger());
            GC.KeepAlive(graph);
            return 100;
        }

        public static int EnumerateGCHeapObjectsSingleThreadNoPriorSuspension()
        {
            _rootObjects.Add(new CustomGCHeapObject());
            EnumerateGCHeapObjectsWithoutProfilerRequestedRuntimeSuspension();
            return 100;
        }

        public static int EnumerateGCHeapObjectsSingleThreadWithinProfilerRequestedRuntimeSuspension()
        {
            _rootObjects.Add(new CustomGCHeapObject());
            EnumerateGCHeapObjectsWithinProfilerRequestedRuntimeSuspension();
            return 100;
        }

        // Test invoking ProfToEEInterfaceImpl::EnumerateGCHeapObjects during non-profiler requested runtime suspension, e.g. during GC
        // ProfToEEInterfaceImpl::EnumerateGCHeapObjects should be invoked by the GarbageCollectionStarted callback
        public static int EnumerateGCHeapObjectsMultiThreadWithCompetingRuntimeSuspension()
        {
            _rootObjects.Add(new CustomGCHeapObject());
            GC.Collect();
            return 100;
        }

        public static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == nameof(EnumerateObjectReferences))
            {
                return RunProfilerTest(nameof(EnumerateObjectReferences), false, true) ? 100 : 101;
            }
            if (args.Length > 0 && args[0].Equals("RunTest", StringComparison.OrdinalIgnoreCase))
            {
                switch (args[1])
                {
                    case nameof(EnumerateObjectReferences):
                        return EnumerateObjectReferences();

                    case nameof(EnumerateGCHeapObjectsSingleThreadNoPriorSuspension):
                        return EnumerateGCHeapObjectsSingleThreadNoPriorSuspension();

                    case nameof(EnumerateGCHeapObjectsSingleThreadWithinProfilerRequestedRuntimeSuspension):
                        return EnumerateGCHeapObjectsSingleThreadWithinProfilerRequestedRuntimeSuspension();

                    case nameof(EnumerateGCHeapObjectsMultiThreadWithCompetingRuntimeSuspension):
                        return EnumerateGCHeapObjectsMultiThreadWithCompetingRuntimeSuspension();
                }
            }

            if (IsSatori)
            {
                // Profiler suspension does not provide the blocking-GC phase Satori needs for whole-heap walks.
                return RunProfilerTest(nameof(EnumerateObjectReferences), false, true) ? 100 : 101;
            }

            if (!RunProfilerTest(nameof(EnumerateGCHeapObjectsSingleThreadNoPriorSuspension), false))
            {
                return 101;
            }

            if (!RunProfilerTest(nameof(EnumerateGCHeapObjectsSingleThreadWithinProfilerRequestedRuntimeSuspension), false))
            {
                return 102;
            }

            if (!RunProfilerTest(nameof(EnumerateGCHeapObjectsMultiThreadWithCompetingRuntimeSuspension), true))
            {
                return 103;
            }

            return 100;
        }

        private static bool RunProfilerTest(string testName, bool shouldSetMonitorGCEventMask, bool referencesOnly = false)
        {
            try
            {
                return ProfilerTestRunner.Run(profileePath: System.Reflection.Assembly.GetExecutingAssembly().Location,
                                              testName: "GCHeapEnumeration",
                                              profilerClsid: GCHeapEnumerationProfilerGuid,
                                              profileeArguments: testName,
                                              envVars: new Dictionary<string, string>
                                              {
                                                {ShouldSetMonitorGCEventMaskEnvVar, shouldSetMonitorGCEventMask ? "TRUE" : "FALSE"},
                                                {"Test_Object_References_Only", referencesOnly ? "TRUE" : "FALSE"},
                                              }
                                              ) == 100;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return false;
        }

        class CustomGCHeapObject {}
    }
}
