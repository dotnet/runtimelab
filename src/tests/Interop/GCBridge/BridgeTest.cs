// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using Xunit;

public class Bridge
{
    public static List<Bridge> Roots;
    public List<object> Links;

    public unsafe Bridge()
    {
        Links = new List<object>();
        Roots.Add(this);
        IntPtr *pContext = (IntPtr*)NativeMemory.Alloc(((nuint)sizeof(void*)));
        GCHandle handle = JavaMarshal.CreateReferenceTrackingHandle(this, pContext);

        *pContext = GCHandle.ToIntPtr(handle);
    }
}

public class NonBridge
{
    public object Link;
}

public class NonBridge2 : NonBridge
{
    public object Link2;
}

public unsafe class GCBridgeTests 
{
    [DllImport("GCBridgeNative")]
    private static extern delegate* unmanaged<MarkCrossReferencesArgs*, void> GetMarkCrossReferencesFtn();

    [DllImport("GCBridgeNative")]
    private static extern void SetBridgeProcessingFinishCallback(delegate* unmanaged<MarkCrossReferencesArgs*, void> callback);

    static bool releaseHandles;
    static nuint sccsLen, ccrsLen;

    [UnmanagedCallersOnly]
    internal static unsafe void BridgeProcessingFinishCallback(MarkCrossReferencesArgs* mcr)
    {
        Console.WriteLine("Bridge processing finish SCCs {0}, CCRs {1}", mcr->ComponentCount, mcr->CrossReferenceCount);
        sccsLen = mcr->ComponentCount;
        ccrsLen = mcr->CrossReferenceCount;

        List<GCHandle> handlesToFree = new List<GCHandle>();

        if (releaseHandles)
        {
            for (nuint i = 0; i < mcr->ComponentCount; i++)
            {
                for (nuint j = 0; j < mcr->Components[i].Count; j++)
                {
                    IntPtr *pContext = (IntPtr*)mcr->Components[i].Contexts[j];
                    handlesToFree.Add(GCHandle.FromIntPtr(*pContext));
                    NativeMemory.Free(pContext);
                }
            }
        }

        JavaMarshal.FinishCrossReferenceProcessing(mcr, CollectionsMarshal.AsSpan<GCHandle>(handlesToFree));
    }

    [SkipOnCoreClr("This test is not compatible with GC stress.", RuntimeTestModes.AnyGCStress)]
    [Fact]
    public static void TestEntryPoint()
    {
        try
        {
            JavaMarshal.Initialize(GetMarkCrossReferencesFtn());
            SetBridgeProcessingFinishCallback(&BridgeProcessingFinishCallback);

            RunGraphTest(SimpleTest, 2, 1);

            RunGraphTest(NestedCycles, 2, 1);

            RunGraphTest(Spider, 3, 2);

            // expected result from mono implementation
            RunGraphTest(RandomLinks, 1993, 2695);
        }
        catch (PlatformNotSupportedException)
        {
            Console.WriteLine("GCBridge not supported on this platform");
        }
    }

    static void CheckWeakRefs(List<WeakReference> weakRefs, bool expectedAlive)
    {
        foreach (WeakReference weakRef in weakRefs)
        {
            if (expectedAlive)
            {
                Assert.NotNull(weakRef.Target);
                Assert.True(weakRef.IsAlive);
            }
            else
            {
                Assert.Null(weakRef.Target);
                Assert.False(weakRef.IsAlive);
            }
        }
    }

    private static void SetBPFinishArguments(bool rh)
    {
        releaseHandles = rh;
        sccsLen = 0;
        ccrsLen = 0;
    }

    static void RunGraphTest(Func<List<WeakReference>> buildGraph, nuint expectedSCCs, nuint expectedCCRs)
    {
        // Instead of relying on no-GC regions, detect GCs not induced by the test and retry.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (TryRunGraphTest(buildGraph, expectedSCCs, expectedCCRs))
            {
                return;
            }
        }

        Console.WriteLine("Skipped test {0} due to unexpected GCs", buildGraph.Method.Name);
    }

    static bool TryRunGraphTest(Func<List<WeakReference>> buildGraph, nuint expectedSCCs, nuint expectedCCRs)
    {
        Console.WriteLine("Start test {0}", buildGraph.Method.Name);
        List<WeakReference> weakRefs = BuildGraph(buildGraph);

        // Finish any GC in progress, then use the gen1 count to detect GCs not induced by the test.
        GC.Collect();
        int gcCount = GC.CollectionCount(1);

        // All objects produced by buildGraph are expected to be dead, so we can compute
        // the SCC graph.
        Console.WriteLine(" First GC");
        SetBPFinishArguments(false);
        Bridge.Roots = null;
        GC.Collect ();
        // The BP finish of first gc will not release any cross refs. We verify
        // that we computed the correct number of SCCs and CCRs for the object graph.

        Thread.Sleep (100);

        // BP might have finished or not at this point, WeakRef check should wait for
        // it to finish, so we can obtain the correct value of the weakref. All targets
        // should be alive because we haven't released any handles.
        CheckWeakRefs(weakRefs, true);

        // Read the results before checking that no other GC could have produced them.
        nuint sccs = sccsLen, ccrs = ccrsLen;
        if (GC.CollectionCount(1) != gcCount + 1)
        {
            ReleaseGraph(weakRefs);
            return false;
        }

        Assert.Equal(expectedSCCs, sccs);
        Assert.Equal(expectedCCRs, ccrs);

        Console.WriteLine(" Second GC");
        SetBPFinishArguments(true);
        GC.Collect ();
        if (GC.CollectionCount(1) != gcCount + 2)
        {
            ReleaseGraph(weakRefs);
            return false;
        }

        // The BP finish of first gc will release all cross refs. The bridge object graph
        // should be the same, since it is computed before the cross ref handles are released.

        // This should wait for bridge processing to finish, detecting that the bridge objects
        // are freed on the java/client side.
        CheckWeakRefs(weakRefs, false);
        Assert.Equal(expectedSCCs, sccsLen);
        Assert.Equal(expectedCCRs, ccrsLen);

        Console.WriteLine(" Third GC");
        SetBPFinishArguments(true);
        GC.Collect ();
        // During this GC, there are no cross ref handles anymore so no bridge objects to process

        // Make sure BP is finished before we start next test
        Thread.Sleep(1000);
        Assert.Equal((nuint)0, sccsLen);
        Assert.Equal((nuint)0, ccrsLen);

        Console.WriteLine("Finished test {0}", buildGraph.Method.Name);
        return true;
    }

    // Not inlined, so that unoptimized callers do not keep the roots alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static List<WeakReference> BuildGraph(Func<List<WeakReference>> buildGraph)
    {
        // Keep the graph alive while it is being built.
        Bridge.Roots = new List<Bridge>();
        return buildGraph();
    }

    static void ReleaseGraph(List<WeakReference> weakRefs)
    {
        Console.WriteLine(" Unexpected GC, releasing the graph");
        releaseHandles = true;
        while (weakRefs.Exists(w => w.IsAlive))
        {
            GC.Collect();
        }
    }

    // Simpler version of NestedCycles
    static List<WeakReference> SimpleTest()
    {
        Bridge b1 = new Bridge();
        Bridge b2 = new Bridge();

        NonBridge2 nb1 = new NonBridge2();
        NonBridge2 nb2 = new NonBridge2();

        List<WeakReference> weakRefs = new List<WeakReference>();
        weakRefs.Add(new WeakReference(b1));
        weakRefs.Add(new WeakReference(b2));

        b1.Links.Add(nb1);
        nb1.Link = nb2;
        nb2.Link = nb1;
        nb2.Link2 = b2;

        return weakRefs;
    }

    // Simulates a graph with two nested cycles that is produces by
    // the async state machine when `async Task M()` method gets its
    // continuation rooted by an Action held by RunnableImplementor
    // (ie. the task continuation is hooked through the SynchronizationContext
    // implentation and rooted only by Android bridge objects).
    static List<WeakReference> NestedCycles()
    {
        Bridge runnableImplementor = new Bridge();
        Bridge byteArrayOutputStream = new Bridge();

        List<WeakReference> weakRefs = new List<WeakReference>();
        weakRefs.Add(new WeakReference(runnableImplementor));
        weakRefs.Add(new WeakReference(byteArrayOutputStream));

        NonBridge2 action = new NonBridge2();
        NonBridge displayClass = new NonBridge();
        NonBridge2 asyncStateMachineBox = new NonBridge2();
        NonBridge2 asyncStreamWriter = new NonBridge2();

        runnableImplementor.Links.Add(action);
        action.Link = displayClass;
        action.Link2 = asyncStateMachineBox;
        displayClass.Link = action;
        asyncStateMachineBox.Link = asyncStreamWriter;
        asyncStateMachineBox.Link2 = action;
        asyncStreamWriter.Link = byteArrayOutputStream;
        asyncStreamWriter.Link2 = asyncStateMachineBox;

        return weakRefs;
    }

    static List<WeakReference> Spider()
    {
        const int L0_COUNT = 10000;
        const int L1_COUNT = 10000;
        const int EXTRA_LEVELS = 4;

        Bridge a = new Bridge();
        Bridge b = new Bridge();

        List<WeakReference> weakRefs = new List<WeakReference>();
        weakRefs.Add(new WeakReference(a));
        weakRefs.Add(new WeakReference(b));

        var l1 = new List<object>();
        for (int i = 0; i < L0_COUNT; ++i) {
            var l0 = new List<object>();
            l0.Add(a);
            l0.Add(b);
            l1.Add(l0);
        }
        var last_level = l1;
        for (int l = 0; l < EXTRA_LEVELS; ++l) {
            int j = 0;
            var l2 = new List<object>();
            for (int i = 0; i < L1_COUNT; ++i) {
                var tmp = new List<object>();
                tmp.Add(last_level [j++ % last_level.Count]);
                tmp.Add(last_level [j++ % last_level.Count]);
                l2.Add(tmp);
            }
            last_level = l2;
        }
        Bridge c = new Bridge();
        c.Links.Add(last_level);
        weakRefs.Add(new WeakReference(c));

        return weakRefs;
    }


    static List<WeakReference> RandomLinks()
    {
        const int OBJ_COUNT = 10000;
        const int LINK_COUNT = 2;
        const int EXTRAS_COUNT = 0;

        List<WeakReference> weakRefs = new List<WeakReference>();
        var list = new List<Bridge>();
        for (int i = 0; i < OBJ_COUNT; ++i)
        {
            var bridge = new Bridge();
            list.Add(bridge);
            weakRefs.Add(new WeakReference(bridge));
        }

        var r = new Random(100);
        for (int i = 0; i < OBJ_COUNT; ++i)
        {
            var n = list[i];
            for (int j = 0; j < LINK_COUNT; ++j)
                n.Links.Add(list[r.Next(OBJ_COUNT)]);
            for (int j = 0; j < EXTRAS_COUNT; ++j)
                n.Links.Add(j);
        }

        return weakRefs;
    }

}
