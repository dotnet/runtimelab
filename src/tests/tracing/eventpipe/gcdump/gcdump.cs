// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Tracing.Tests.Common;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Xunit;
using TestLibrary;

namespace Tracing.Tests.EventSourceError
{
    // Regression test for https://github.com/dotnet/runtime/issues/38639 
    public class GCDumpTest
    {
        private static bool _seenGCStart = false;
        private static bool _seenGCStop = false;
        private static int _bulkTypeCount = 0;
        private static int _bulkNodeCount = 0;
        private static int _bulkEdgeCount = 0;
        private static int _bulkRootEdgeCount = 0;
        private static int _bulkRootStaticVarCount = 0;
        private static readonly HeapDumpGcTracker _gcTracker = new HeapDumpGcTracker();
        private const int HeapNodeCount = 512;
        private const int QueuedRootCount = 4;
        private const int LargePayloadSize = 3 * 1024 * 1024;
        private static readonly bool _validateSatoriRoots = !Utilities.IsNativeAot &&
            GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object satori) && satori is true;
        private static readonly ManualResetEventSlim _finalizerBlocked = new ManualResetEventSlim();
        private static readonly ManualResetEventSlim _releaseFinalizer = new ManualResetEventSlim();
        private static readonly List<HeapDumpNode> _heapRoots = new List<HeapDumpNode>();
        private static readonly ConditionalWeakTable<DependentKey, DependentValue> _dependentRoots = new ConditionalWeakTable<DependentKey, DependentValue>();
        private static readonly DependentKey _dependentKey = new DependentKey();
        private static readonly LargeHeapRoots _largeRoots = new LargeHeapRoots();
        private static GCHandle _handleRoot;
        private static readonly Dictionary<ulong, string> _typeNames = new Dictionary<ulong, string>();
        private static readonly List<(ulong Address, ulong TypeID, ulong Size, int EdgeCount)> _nodes =
            new List<(ulong Address, ulong TypeID, ulong Size, int EdgeCount)>();
        private static readonly List<ulong> _edgeTargets = new List<ulong>();
        private static readonly HashSet<ulong> _finalizerRoots = new HashSet<ulong>();
        private static readonly HashSet<ulong> _handleRoots = new HashSet<ulong>();
        private static readonly List<(ulong Key, ulong Value)> _dependentEdges = new List<(ulong Key, ulong Value)>();

        private static readonly ulong GC_HeapDump_Keyword = 0x100000UL;
        private static ManualResetEvent _gcStopReceived = new ManualResetEvent(false);

        [ActiveIssue("System.Diagnostics.Process is not supported on wasm", TestPlatforms.Browser)]
        [ActiveIssue("Can't find file dotnet-diagnostic-{pid}-*-socket", typeof(PlatformDetection), nameof(PlatformDetection.IsMonoRuntime), nameof(PlatformDetection.IsRiscv64Process))]
        [SkipOnCoreClr("This test is sensitive to JIT optimizations.", RuntimeTestModes.AnyJitOptimizationStress)]
        [SkipOnCoreClr("Tracing tests routinely time out with JIT stress and GC stress.", RuntimeTestModes.AnyGCStress)]
        [Fact]
        public static int TestEntryPoint()
        {
            List<EventPipeProvider> providers = new List<EventPipeProvider>
            {
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime", eventLevel: EventLevel.Verbose, keywords: (long)ClrTraceEventParser.Keywords.GCHeapSnapshot)
            };

            try
            {
                if (_validateSatoriRoots)
                {
                    for (int i = 0; i < HeapNodeCount; i++)
                    {
                        _heapRoots.Add(new HeapDumpNode
                        {
                            Next = i == 0 ? null : _heapRoots[i - 1],
                            Payload = new byte[4096]
                        });
                    }
                    _handleRoot = GCHandle.Alloc(new HandleRoot());
                    _dependentRoots.Add(_dependentKey, new DependentValue());

                    // Keep finalizable objects queued throughout the snapshot.
                    CreateFinalizerBlocker();
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                    Assert.True(_finalizerBlocked.Wait(TimeSpan.FromSeconds(30)), "The finalizer did not start.");
                    CreateQueuedRoots();
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                }

                return IpcTraceTest.RunAndValidateEventCounts(_expectedEventCounts, _eventGeneratingAction, providers, 1024, _DoesRundownContainMethodEvents);
            }
            finally
            {
                _releaseFinalizer.Set();
                if (_handleRoot.IsAllocated)
                {
                    _handleRoot.Free();
                }
            }
        }

        private static Dictionary<string, ExpectedEventCount> _expectedEventCounts = new Dictionary<string, ExpectedEventCount>()
        {
            // This space intentionally left blank
        };

        private static Action _eventGeneratingAction = () =>
        {
            Assert.True(_gcStopReceived.WaitOne(30000), "The diagnostic collection did not finish.");
            // NativeAOT dumps every blocking collection while the heap-dump keyword is enabled.
            if (!Utilities.IsNativeAot)
            {
                // The heap-dump keyword alone must not make ordinary collections produce another snapshot.
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            }
        };

        private static Func<EventPipeEventSource, Func<int>> _DoesRundownContainMethodEvents = (source) =>
        {
            source.Clr.GCStart += (GCStartTraceData data) =>
            {
                _seenGCStart = true;
                _gcTracker.GCStarted(data.Count);
            };

            source.Clr.TypeBulkType += (GCBulkTypeTraceData data) =>
            {
                _bulkTypeCount += data.Count;
                for (int i = 0; i < data.Count; i++)
                {
                    _typeNames[data.Values(i).TypeID] = data.Values(i).TypeName;
                }
            };

            source.Clr.GCBulkNode += delegate (GCBulkNodeTraceData data)
            {
                RecordHeapEvent();
                _bulkNodeCount += data.Count;
                for (int i = 0; i < data.Count; i++)
                {
                    GCBulkNodeValues node = data.Values(i);
                    _nodes.Add((node.Address, node.TypeID, node.Size, checked((int)node.EdgeCount)));
                }
            };

            source.Clr.GCBulkEdge += (GCBulkEdgeTraceData data) =>
            {
                RecordHeapEvent();
                _bulkEdgeCount += data.Count;
                for (int i = 0; i < data.Count; i++)
                {
                    _edgeTargets.Add(data.Values(i).Target);
                }
            };

            source.Clr.GCBulkRootEdge += (GCBulkRootEdgeTraceData data) =>
            {
                RecordHeapEvent();
                _bulkRootEdgeCount += data.Count;
                for (int i = 0; i < data.Count; i++)
                {
                    GCBulkRootEdgeValues root = data.Values(i);
                    if (root.GCRootKind == GCRootKind.Finalizer)
                    {
                        _finalizerRoots.Add(root.RootedNodeAddress);
                    }
                    else if (root.GCRootKind == GCRootKind.Handle)
                    {
                        _handleRoots.Add(root.RootedNodeAddress);
                    }
                }
            };

            source.Clr.GCBulkRootConditionalWeakTableElementEdge += (GCBulkRootConditionalWeakTableElementEdgeTraceData data) =>
            {
                RecordHeapEvent();
                for (int i = 0; i < data.Count; i++)
                {
                    _dependentEdges.Add((data.Values(i).GCKeyNodeID, data.Values(i).GCValueNodeID));
                }
            };

            source.Clr.GCBulkRootStaticVar += (GCBulkRootStaticVarTraceData data) =>
            {
                RecordHeapEvent();
                _bulkRootStaticVarCount += data.Count;
            };

            source.Clr.GCStop += (GCEndTraceData data) =>
            {
                _gcTracker.GCStopped(data.Count);
                if (_gcTracker.IsComplete)
                {
                    _seenGCStop = true;
                    _gcStopReceived.Set();
                }
            };

            return () => 
            {
                Console.WriteLine($"Heap dump: types={_bulkTypeCount}, nodes={_bulkNodeCount}, edges={_bulkEdgeCount}, roots={_bulkRootEdgeCount}, statics={_bulkRootStaticVarCount}.");
                Assert.False(_gcTracker.HasInvalidHeapEvent,
                    "Heap events must occur within the snapshot GC, and ordinary collections must not dump the heap.");
                // Hopefully it is low enough to be resilient to changes in the runtime
                // and high enough to catch issues. There should be between hundreds and thousands
                // for each, but the number is variable and the point of the test is to verify
                // that we get any events at all.
                
                if (_seenGCStart
                     && _seenGCStop
                     && _bulkTypeCount > 50
                     && _bulkNodeCount > 50
                     && _bulkEdgeCount > 50)
                {
                    // Native AOT hasn't yet implemented statics. Hence _bulkRootStaticVarCount is zero and _bulkRootEdgeCount can be low
                    if ((TestLibrary.Utilities.IsNativeAot && _bulkRootEdgeCount > 20) || (_bulkRootStaticVarCount > 50 && _bulkRootEdgeCount > 50))
                    {
                        if (_validateSatoriRoots)
                        {
                            ValidateSatoriRoots();
                        }
                        return 100;
                    }
                }

                Console.WriteLine($"Test failed due to missing GC heap events.");
                Console.WriteLine($"_seenGCStart =            {_seenGCStart}");
                Console.WriteLine($"_seenGCStop =             {_seenGCStop}");
                Console.WriteLine($"_bulkTypeCount =          {_bulkTypeCount}");
                Console.WriteLine($"_bulkNodeCount =          {_bulkNodeCount}");
                Console.WriteLine($"_bulkEdgeCount =          {_bulkEdgeCount}");
                Console.WriteLine($"_bulkRootEdgeCount =      {_bulkRootEdgeCount}");
                Console.WriteLine($"_bulkRootStaticVarCount = {_bulkRootStaticVarCount}");
                return -1;
            };
        };

        private static void RecordHeapEvent()
        {
            _gcTracker.HeapEvent();
            if (_gcTracker.IsComplete)
            {
                _seenGCStop = true;
                _gcStopReceived.Set();
            }
        }

        private static void ValidateSatoriRoots()
        {
            HashSet<ulong> graphNodes = NodesOfType(nameof(HeapDumpNode)).ToHashSet();
            Assert.Equal(HeapNodeCount, graphNodes.Count);
            foreach (ulong address in NodesOfType(nameof(QueuedRoot)))
            {
                Assert.Contains(address, _finalizerRoots);
            }
            Assert.Equal(QueuedRootCount, NodesOfType(nameof(QueuedRoot)).Count());
            Assert.Contains(Assert.Single(NodesOfType(nameof(HandleRoot))), _handleRoots);

            ulong key = Assert.Single(NodesOfType(nameof(DependentKey)));
            ulong value = Assert.Single(NodesOfType(nameof(DependentValue)));
            Assert.Contains((key, value), _dependentEdges);

            ulong largeRoots = Assert.Single(NodesOfType(nameof(LargeHeapRoots)));
            int edgeIndex = 0;
            int graphEdges = 0;
            foreach ((ulong Address, ulong TypeID, ulong Size, int EdgeCount) node in _nodes)
            {
                if (graphNodes.Contains(node.Address))
                {
                    Assert.InRange(node.EdgeCount, 1, 2);
                    for (int i = 0; i < node.EdgeCount; i++)
                    {
                        if (graphNodes.Contains(_edgeTargets[edgeIndex + i]))
                        {
                            graphEdges++;
                        }
                    }
                }
                if (node.Address == largeRoots)
                {
                    Assert.Equal(2, node.EdgeCount);
                    for (int i = 0; i < node.EdgeCount; i++)
                    {
                        ulong target = _edgeTargets[edgeIndex + i];
                        Assert.True(_nodes.Any(n => n.Address == target && n.Size >= LargePayloadSize),
                            $"Huge or pinned object {target:x} is missing from the dump.");
                    }
                }
                edgeIndex = checked(edgeIndex + node.EdgeCount);
            }
            Assert.Equal(_edgeTargets.Count, edgeIndex);
            Assert.Equal(HeapNodeCount - 1, graphEdges);
            Console.WriteLine($"Validated {HeapNodeCount} graph nodes, {QueuedRootCount} finalizer roots, a handle root, a dependent handle, and huge/pinned objects.");
        }

        private static IEnumerable<ulong> NodesOfType(string name)
        {
            ulong[] typeIds = _typeNames.Where(t => t.Value.EndsWith(name, StringComparison.Ordinal)).Select(t => t.Key).ToArray();
            Assert.True(typeIds.Length == 1, $"Expected one type named {name}, found {typeIds.Length}.");

            return _nodes.Where(n => n.TypeID == typeIds[0]).Select(n => n.Address);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CreateFinalizerBlocker()
        {
            _ = new FinalizerBlocker();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CreateQueuedRoots()
        {
            for (int i = 0; i < QueuedRootCount; i++)
            {
                _ = new QueuedRoot();
            }
        }

        private sealed class HeapDumpNode
        {
            public HeapDumpNode Next;
            public byte[] Payload;
        }

        private sealed class HandleRoot {}
        private sealed class DependentKey {}
        private sealed class DependentValue {}
        private sealed class QueuedRoot
        {
            ~QueuedRoot() {}
        }

        private sealed class FinalizerBlocker
        {
            ~FinalizerBlocker()
            {
                _finalizerBlocked.Set();
                _releaseFinalizer.Wait();
            }
        }

        private sealed class LargeHeapRoots
        {
            public byte[] Large = new byte[LargePayloadSize];
            public byte[] Pinned = GC.AllocateArray<byte>(LargePayloadSize, pinned: true);
        }
    }

}
