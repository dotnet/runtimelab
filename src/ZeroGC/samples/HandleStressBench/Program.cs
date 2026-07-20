// HandleStressBench - an isolated microbenchmark for the ZeroGC handle
// store fix (see ZeroGCHandleStore in native/ZeroGCHandles.cpp).
//
// Before the fix, every GCHandle.Alloc/Free (and anything built on top of
// handles under the hood - WeakReference, interop pinning,
// ConditionalWeakTable, etc.) serialized on a single global
// CRITICAL_SECTION guarding the handle free list - the exact same
// contention shape the original arena bump allocator had before it was
// switched to per-thread arenas. After the fix, the free list is a
// lock-free Treiber stack (CAS push/pop), so concurrent handle churn from
// independent threads no longer serializes on a single lock.
//
// This program isolates *only* that cost: each worker thread repeatedly
// allocates and frees GCHandles as fast as possible, with no other shared
// state between threads. It reports:
//   1. A correctness check (every handle's Target must round-trip to
//      exactly the object it was allocated for, and IsAllocated/Free must
//      behave as documented).
//   2. A multi-threaded throughput number (handle alloc+free pairs/sec)
//      directly comparable before/after the fix and across GC modes.
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

int durationSeconds = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 15;
int threadCount = args.Length > 1 && int.TryParse(args[1], out var t) ? t : Environment.ProcessorCount;

string gcName = Environment.GetEnvironmentVariable("DOTNET_GCName")
    ?? Environment.GetEnvironmentVariable("COMPlus_GCName")
    ?? (GCSettings.IsServerGC ? "CoreCLR (Server)" : "CoreCLR (Workstation)");

Console.WriteLine($"# HandleStressBench starting: duration={durationSeconds}s threads={threadCount}");
Console.WriteLine($"# GC.Name={gcName}");

// --- Phase 1: correctness ---------------------------------------------------
bool correctnessPassed = true;
{
    const int n = 20_000;
    var handles = new GCHandle[n];
    var payloads = new object[n];
    for (int i = 0; i < n; i++)
    {
        payloads[i] = new Payload { Id = i };
        handles[i] = GCHandle.Alloc(payloads[i], GCHandleType.Normal);
    }
    for (int i = 0; i < n; i++)
    {
        if (!handles[i].IsAllocated || !ReferenceEquals(handles[i].Target, payloads[i]))
        {
            correctnessPassed = false;
            break;
        }
    }
    // Free every other handle, then verify freed ones report not-allocated
    // and the remaining ones are still intact - this exercises the
    // free-list push/pop path (recycling) under the exact same load the
    // fix targets.
    for (int i = 0; i < n; i += 2) handles[i].Free();
    for (int i = 0; i < n; i++)
    {
        if (i % 2 == 0)
        {
            if (handles[i].IsAllocated) { correctnessPassed = false; break; }
        }
        else
        {
            if (!handles[i].IsAllocated || !ReferenceEquals(handles[i].Target, payloads[i]))
            {
                correctnessPassed = false;
                break;
            }
        }
    }
    // Re-allocate into the now-freed slots (forces slot recycling from the
    // free list) and verify those round-trip correctly too.
    for (int i = 0; i < n; i += 2)
    {
        payloads[i] = new Payload { Id = i + 1_000_000 };
        handles[i] = GCHandle.Alloc(payloads[i], GCHandleType.Normal);
    }
    for (int i = 0; i < n; i++)
    {
        if (!handles[i].IsAllocated || !ReferenceEquals(handles[i].Target, payloads[i]))
        {
            correctnessPassed = false;
            break;
        }
    }
    for (int i = 0; i < n; i++) handles[i].Free();
}
Console.WriteLine($"# Correctness: {(correctnessPassed ? "PASS" : "FAIL")}");

// --- Phase 2: throughput -----------------------------------------------------
long[] perThreadOps = new long[threadCount];
var barrier = new Barrier(threadCount + 1);
var stopFlag = new StopFlag();
var threads = new Thread[threadCount];

for (int ti = 0; ti < threadCount; ti++)
{
    int threadIndex = ti;
    threads[ti] = new Thread(() =>
    {
        long ops = 0;
        var obj = new Payload { Id = threadIndex };
        barrier.SignalAndWait();

        while (!stopFlag.Stop)
        {
            for (int b = 0; b < 2048; b++)
            {
                // The operation under test: an Alloc immediately followed
                // by a Free - the same alloc/free churn pattern real apps
                // exhibit for short-lived interop handles, WeakReferences
                // created and dropped per-request, etc.
                var h = GCHandle.Alloc(obj, GCHandleType.Normal);
                h.Free();
                ops++;
            }
        }

        perThreadOps[threadIndex] = ops;
    });
    threads[ti].IsBackground = true;
    threads[ti].Start();
}

barrier.SignalAndWait();
var sw = Stopwatch.StartNew();
Thread.Sleep(TimeSpan.FromSeconds(durationSeconds));
stopFlag.Stop = true;
foreach (var th in threads) th.Join();
sw.Stop();

long totalOps = perThreadOps.Sum();
double opsPerSecond = totalOps / sw.Elapsed.TotalSeconds;

var result = new BenchResult
{
    GcName = gcName,
    ThreadCount = threadCount,
    DurationSeconds = sw.Elapsed.TotalSeconds,
    TotalHandleAllocFreePairs = totalOps,
    HandleOpsPerSecond = opsPerSecond,
    HandleOpsPerSecondPerThread = opsPerSecond / threadCount,
    CorrectnessPassed = correctnessPassed,
};

string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
Console.WriteLine("##RESULT##" + json);

internal class Payload
{
    public int Id;
}

internal class StopFlag
{
    public volatile bool Stop;
}

internal class BenchResult
{
    public string GcName { get; set; } = "";
    public int ThreadCount { get; set; }
    public double DurationSeconds { get; set; }
    public long TotalHandleAllocFreePairs { get; set; }
    public double HandleOpsPerSecond { get; set; }
    public double HandleOpsPerSecondPerThread { get; set; }
    public bool CorrectnessPassed { get; set; }
}
