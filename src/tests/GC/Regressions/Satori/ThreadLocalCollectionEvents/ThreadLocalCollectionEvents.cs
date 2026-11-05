// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Tracing.Tests.Common;
using Xunit;

public static class ThreadLocalCollectionEvents
{
    private const int RegionSize = 2 * 1024 * 1024;
    private const int AllocationLimit = 32 * 1024 * 1024;
    private const int RegionAdvanceAllocationSize = 4 * 1024;
    private static object? s_published;

    public static bool IsSatori =>
        GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? value) && value is true;

    [ConditionalTheory(typeof(ThreadLocalCollectionEvents), nameof(IsSatori))]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public static void EscapedByteTotals(int scenario)
    {
        ValidateSourceManifest();
        var providers = new List<EventPipeProvider>
        {
            new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Verbose, 1),
            new EventPipeProvider("Satori-EscapedBytes-Test", EventLevel.Informational)
        };
        var expected = new Dictionary<string, ExpectedEventCount>
        {
            { "Microsoft-Windows-DotNETRuntime", -1 },
            { "Satori-EscapedBytes-Test", -1 }
        };
        using var probe = new EscapeProbe();

        Assert.Equal(100, IpcTraceTest.RunAndValidateEventCounts(
            expected,
            () => RunOnFreshThread(() => GenerateEscapedByteEvents(scenario, probe)),
            providers,
            16,
            source => ValidateEscapedByteEvents(source, scenario, probe)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GenerateEscapedByteEvents(int scenario, EscapeProbe probe)
    {
        // Warm the marker before acquiring the region so its initialization cannot escape test objects.
        EscapedByteMarkers.Log.Begin(0);
        EscapedByteMarkers.Log.End();
        EscapedByteMarkers.Log.Pulse();
        // The first wait on a thread allocates thread-local state that escapes, so make it before acquiring the region.
        probe.Ready.WaitOne(1);
        var retained = new byte[RegionSize / RegionAdvanceAllocationSize][];
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        // With the small finalization queue of debug builds, finalization after a collection can overflow and
        // the finalizer thread then runs collections of its own. Let it finish before the region is acquired.
        GC.WaitForPendingFinalizers();
        byte[] anchor = AcquireThreadLocalRegion();
        anchor = AcquireUnescapedRegion(anchor, retained, probe);
        nuint region = RegionFor(anchor);
        object[]? graph = null;

        try
        {
            if (scenario == 1)
            {
                graph = new object[3];
                byte[] first = new byte[1024];
                byte[] second = new byte[2048];
                if (RegionFor(first) != region || RegionFor(second) != region)
                {
                    throw new InvalidOperationException("The published graph must stay in the tracked region.");
                }

                graph[0] = first;
                graph[1] = first;
                graph[2] = second;
                s_published = graph;
            }
            else if (scenario == 2)
            {
                AllocateFinalizableGraph(region);
            }

            EscapedByteMarkers.Log.Begin(2);
            AllocateTransientInRegion(anchor, RegionSize, graph);
        }
        finally
        {
            EscapedByteMarkers.Log.End();
            s_published = null;
        }

        GC.KeepAlive(anchor);
        GC.KeepAlive(graph);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte[] AcquireUnescapedRegion(byte[] anchor, byte[][] retained, EscapeProbe probe)
    {
        // Reused Gen0 regions escape their pre-existing survivors. Find an actually empty
        // escape set rather than assuming that generation zero implies zero escaped bytes.
        for (int attempt = 0; attempt < AllocationLimit / RegionSize; attempt++)
        {
            probe.Ready.Reset();
            EscapedByteMarkers.Log.Begin(1);
            AllocateTransientInRegion(anchor, RegionSize / 2);
            // Live readers may hold a partial block until more events arrive.
            // Keep the stream moving without allocating into the region under test.
            long deadline = Environment.TickCount64 + 30_000;
            while (!probe.Ready.WaitOne(10) && Environment.TickCount64 < deadline)
            {
                for (int i = 0; i < 512; i++)
                {
                    EscapedByteMarkers.Log.Pulse();
                }
            }

            if (!probe.Ready.WaitOne(0))
            {
                throw new InvalidOperationException("The region probe did not receive a local collection event.");
            }

            Volatile.Read(ref probe.Failure)?.Throw();
            if (Volatile.Read(ref probe.EscapedBytes) == 0)
            {
                return anchor;
            }

            EscapedByteMarkers.Log.End();
            nuint region = RegionFor(anchor);
            bool advanced = false;
            for (int i = 0; i < retained.Length; i++)
            {
                // Pointer-free objects of 8 KB or more can use the large allocator
                // when the regular region is fragmented. Smaller objects advance it.
                byte[] buffer = new byte[RegionAdvanceAllocationSize];
                if (RegionFor(buffer) != region)
                {
                    anchor = GC.GetGeneration(buffer) == 0 ? buffer : AcquireThreadLocalRegion();
                    advanced = true;
                    break;
                }

                retained[i] = buffer;
            }

            if (!advanced)
            {
                throw new InvalidOperationException("Could not advance past the reused region.");
            }

            Array.Clear(retained);
        }

        throw new InvalidOperationException("Could not acquire a region with zero escaped bytes.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AllocateTransientInRegion(byte[] anchor, int bytes, object[]? graph = null)
    {
        nuint region = RegionFor(anchor);
        for (int allocated = 0; allocated < bytes; allocated += 1024)
        {
            byte[] buffer = new byte[1024];
            if (RegionFor(buffer) != region)
            {
                throw new InvalidOperationException("The diagnostic workload must stay in the tracked region.");
            }

            if (graph is not null)
            {
                // Re-publishing the same graph must not count it again.
                s_published = graph;
            }

            GC.KeepAlive(buffer);
        }

        GC.KeepAlive(anchor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AllocateFinalizableGraph(nuint region)
    {
        var value = new FinalizableGraph();
        if (GC.GetGeneration(value) != 0 || RegionFor(value.Buffer) != region)
        {
            throw new InvalidOperationException("The finalizable graph must stay in the tracked region.");
        }

        GC.KeepAlive(value);
    }

    private static Func<int> ValidateEscapedByteEvents(EventPipeEventSource source, int scenario, EscapeProbe probe)
    {
        int activeThread = -1;
        int scopes = 0;
        bool calibrating = false;
        bool probeDelivered = false;
        var samples = new List<(uint Count, ulong Before, ulong After)>();
        string[] payloadNames =
        {
            "ClrInstanceID", "Count", "Collected", "OccupancyBefore", "OccupancyAfter",
            "DurationNs", "EscapedBytesBefore", "EscapedBytesAfter"
        };
        Type[] payloadTypes =
        {
            typeof(ushort), typeof(uint), typeof(bool), typeof(ulong), typeof(ulong),
            typeof(double), typeof(ulong), typeof(ulong)
        };
        bool schemaReported = false;

        source.Dynamic.All += data =>
        {
            try
            {
                ProcessEvent(data);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Reader failed: {data.ProviderName}/{(int)data.ID}v{data.Version}, " +
                    $"names=[{string.Join(",", data.PayloadNames)}], raw={Convert.ToHexString(data.EventData())}: {exception}");
                Volatile.Write(ref probe.Failure, ExceptionDispatchInfo.Capture(exception));
                probe.Ready.Set();
                throw;
            }
        };

        return () =>
        {
            Assert.Equal(0, source.EventsLost);
            Assert.Equal(1, scopes);
            Assert.Equal(-1, activeThread);
            Assert.True(samples.Count >= 3, $"Expected repeated local collections; saw {samples.Count}.");

            ulong expected = scenario switch
            {
                0 => 0,
                1 => ArrayBytes(3, IntPtr.Size) + ArrayBytes(1024, 1) + ArrayBytes(2048, 1),
                2 => (ulong)(3 * IntPtr.Size) + ArrayBytes(1024, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };
            int growthEvents = 0;
            uint previousCount = 0;
            foreach ((uint count, ulong before, ulong after) in samples)
            {
                Assert.True(count > previousCount, "Collection records must not be duplicated.");
                previousCount = count;
                Assert.Equal(expected, after);
                if (scenario == 2 && growthEvents == 0)
                {
                    Assert.Equal(0UL, before);
                    growthEvents++;
                }
                else
                {
                    Assert.Equal(expected, before);
                }
            }

            Assert.Equal(scenario == 2 ? 1 : 0, growthEvents);
            Console.WriteLine($"EscapedByteTotals scenario={scenario} PASS: workerEvents={samples.Count}, " +
                $"expectedAfter={expected}, firstBefore={samples[0].Before}, growthEvents={growthEvents}, " +
                $"firstCount={samples[0].Count}, lastCount={previousCount}, drops={source.EventsLost}");
            return 100;
        };

        void ProcessEvent(TraceEvent data)
        {
            if (data.ProviderName == "Satori-EscapedBytes-Test")
            {
                if ((int)data.ID == 1)
                {
                    int phase = (int)data.PayloadValue(0);
                    if (phase != 0)
                    {
                        activeThread = data.ThreadID;
                        calibrating = phase == 1;
                        probeDelivered = false;
                        if (!calibrating)
                        {
                            scopes++;
                        }
                    }
                }
                else if ((int)data.ID == 2 && data.ThreadID == activeThread)
                {
                    activeThread = -1;
                }

                return;
            }

            if (data.ProviderName != "Microsoft-Windows-DotNETRuntime" ||
                (int)data.ID != 239 || data.ThreadID != activeThread)
            {
                return;
            }

            Assert.Equal(0, (int)data.Version);
            Assert.Equal(new Guid("e13c0d23-ccbc-4e12-931b-d9cc2eee27e4"), data.ProviderGuid);
            Assert.Equal(5, (int)data.Level);
            Assert.Equal(1UL, (ulong)data.Keywords);
            if (data.PayloadNames.Length != 0)
            {
                Assert.Equal(payloadNames, data.PayloadNames);
                for (int i = 0; i < payloadTypes.Length; i++)
                {
                    Assert.Equal(payloadTypes[i], data.PayloadValue(i).GetType());
                }
            }

            // When wire field definitions are absent, use the embedded build-source
            // manifest as an out-of-band contract, not as evidence of wire names or types.
            byte[] bytes = data.EventData();
            const int PayloadBytes = sizeof(ushort) + sizeof(uint) + sizeof(int) + 4 * sizeof(ulong) + sizeof(double);
            Assert.Equal(PayloadBytes, bytes.Length);
            ReadOnlySpan<byte> payload = bytes;
            _ = BinaryPrimitives.ReadUInt16LittleEndian(payload);
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(2));
            Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(6)));
            ulong occupancyBefore = BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(10));
            ulong occupancyAfter = BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(18));
            double duration = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(26)));
            Assert.True(occupancyAfter <= occupancyBefore);
            Assert.True(double.IsFinite(duration) && duration >= 0);
            ulong before = BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(34));
            ulong after = BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(42));
            Assert.True(before <= after && before <= occupancyBefore && after <= occupancyAfter);
            if (!schemaReported)
            {
                schemaReported = true;
                Console.WriteLine($"239v0 schema: source manifest verified; wire field names/types " +
                    $"{(data.PayloadNames.Length == 0 ? "unavailable" : "verified")}; " +
                    $"rawBytes={bytes.Length}; firstRaw={Convert.ToHexString(bytes)}");
            }
            if (calibrating)
            {
                if (!probeDelivered)
                {
                    probeDelivered = true;
                    Console.WriteLine($"Region probe: count={count}, before={before}, after={after}");
                    Volatile.Write(ref probe.EscapedBytes, (long)after);
                    probe.Ready.Set();
                }

                return;
            }

            samples.Add((count, before, after));
        }
    }

    private static void ValidateSourceManifest()
    {
        using Stream manifest = typeof(ThreadLocalCollectionEvents).Assembly.GetManifestResourceStream("SatoriEventManifest")
            ?? throw new InvalidOperationException("The build-source event manifest is missing.");
        XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events";
        XElement provider = XDocument.Load(manifest).Descendants(ns + "provider")
            .Single(p => (string?)p.Attribute("name") == "Microsoft-Windows-DotNETRuntime");
        Assert.Equal(new Guid("e13c0d23-ccbc-4e12-931b-d9cc2eee27e4"), Guid.Parse((string)provider.Attribute("guid")!));
        Assert.Equal("0x1", (string?)provider.Element(ns + "keywords")!.Elements(ns + "keyword")
            .Single(k => (string?)k.Attribute("name") == "GCKeyword").Attribute("mask"));
        string[] expected =
        {
            "ClrInstanceID:win:UInt16", "Count:win:UInt32", "Collected:win:Boolean",
            "OccupancyBefore:win:UInt64", "OccupancyAfter:win:UInt64", "DurationNs:win:Double",
            "EscapedBytesBefore:win:UInt64", "EscapedBytesAfter:win:UInt64"
        };
        XElement definition = provider.Element(ns + "events")!.Elements(ns + "event")
            .Single(e => (string?)e.Attribute("value") == "239");
        Assert.Equal("0", (string?)definition.Attribute("version"));
        Assert.Equal("win:Verbose", (string?)definition.Attribute("level"));
        Assert.Equal("GCKeyword", (string?)definition.Attribute("keywords"));
        XElement template = provider.Element(ns + "templates")!.Elements(ns + "template")
            .Single(t => (string?)t.Attribute("tid") == (string?)definition.Attribute("template"));
        string[] fields = template.Elements(ns + "data")
            .Select(f => $"{f.Attribute("name")!.Value}:{f.Attribute("inType")!.Value}").ToArray();
        Assert.Equal(expected, fields);
    }

    private static ulong ArrayBytes(int length, int elementSize)
    {
        ulong alignment = (ulong)IntPtr.Size;
        return ((ulong)(3 * IntPtr.Size + length * elementSize) + alignment - 1) & ~(alignment - 1);
    }

    private sealed class FinalizableGraph
    {
        internal readonly byte[] Buffer = new byte[1024];

        ~FinalizableGraph() => GC.KeepAlive(Buffer);
    }

    private sealed class EscapeProbe : IDisposable
    {
        internal readonly ManualResetEvent Ready = new(false);
        internal long EscapedBytes;
        internal ExceptionDispatchInfo? Failure;

        public void Dispose() => Ready.Dispose();
    }

    [EventSource(Name = "Satori-EscapedBytes-Test")]
    private sealed class EscapedByteMarkers : EventSource
    {
        internal static readonly EscapedByteMarkers Log = new();

        [Event(1, Level = EventLevel.Informational)]
        public void Begin(int measured) => WriteEvent(1, measured);

        [Event(2, Level = EventLevel.Informational)]
        public void End() => WriteEvent(2);

        [Event(3, Level = EventLevel.Informational)]
        public void Pulse() => WriteEvent(3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte[] AcquireThreadLocalRegion()
    {
        for (int allocated = 0; allocated < AllocationLimit; allocated += RegionAdvanceAllocationSize)
        {
            byte[] buffer = new byte[RegionAdvanceAllocationSize];
            if (GC.GetGeneration(buffer) == 0)
            {
                return buffer;
            }
        }

        throw new InvalidOperationException("Could not acquire the requested escape-tracked region.");
    }

    // Region alignment is part of Satori's object-address layout; no private header offsets are needed.
    private static unsafe nuint RegionFor(byte[] buffer)
    {
        fixed (byte* address = buffer)
        {
            return (nuint)address & ~(nuint)(RegionSize - 1);
        }
    }

    private static void RunOnFreshThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });

        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
