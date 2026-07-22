// WriteBarrierBench - an isolated microbenchmark for the ZeroGC write
// barrier "ephemeral bounds" fix (see ZeroGCHeap::Initialize()).
//
// Every reference-type field/array-element store in managed code goes
// through the JIT-generated write barrier. Before the fix, ZeroGC told the
// EE that "everything is ephemeral" (ephemeral_low=1), which forces every
// single such store to additionally touch a card-table byte (and, on
// x64/arm64, a second card-bundle byte) - work ZeroGC never reads back,
// since it never collects or scans generationally. After the fix, the
// ephemeral range is empty, so the write barrier's very first branch
// ("is the stored reference below ephemeral_low?") is always taken and
// the card-table/card-bundle work is skipped entirely.
//
// This program isolates *only* that cost: each worker thread repeatedly
// reassigns a reference field on objects it exclusively owns (no cross-
// thread contention on the objects themselves - only the shared,
// process-wide card table can be touched, which is exactly what's being
// measured/eliminated). It reports:
//   1. A correctness check (single-threaded, deterministic) proving the
//      reference stores still round-trip correctly.
//   2. A multi-threaded throughput number (reference-field stores/sec)
//      that is directly comparable before/after the fix and across GC
//      modes.
using System.Diagnostics;
using System.Runtime;
using System.Text.Json;

int durationSeconds = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 15;
int threadCount = args.Length > 1 && int.TryParse(args[1], out var t) ? t : Environment.ProcessorCount;
int ringSize = args.Length > 2 && int.TryParse(args[2], out var r) ? r : 256;

string gcName = Environment.GetEnvironmentVariable("DOTNET_GCName")
    ?? Environment.GetEnvironmentVariable("COMPlus_GCName")
    ?? (GCSettings.IsServerGC ? "CoreCLR (Server)" : "CoreCLR (Workstation)");

Console.WriteLine($"# WriteBarrierBench starting: duration={durationSeconds}s threads={threadCount} ringSize={ringSize}");
Console.WriteLine($"# GC.Name={gcName}");

// --- Phase 1: correctness -------------------------------------------------
// Deterministic, single-threaded: build a ring of nodes, reassign every
// node's Next pointer to point two steps ahead N times, then verify every
// node ends up pointing exactly where expected. This exercises the same
// reference-store code path the throughput phase does, but in a way whose
// final state can be checked exactly for correctness (proving the write
// barrier fix does not corrupt stores/identity in any way).
bool correctnessPassed;
{
    const int n = 10_000;
    var nodes = new Node[n];
    for (int i = 0; i < n; i++) nodes[i] = new Node();
    for (int i = 0; i < n; i++) nodes[i].Next = nodes[(i + 1) % n];

    const int iterations = 50_000;
    int cursor = 0;
    for (int i = 0; i < iterations; i++)
    {
        int target = (cursor + 2) % n;
        nodes[cursor].Next = nodes[target]; // reference-type field store
        cursor = target;
    }

    // Final store for every node so we can verify the whole array, not
    // just the ones the cursor walk happened to touch last.
    for (int i = 0; i < n; i++) nodes[i].Next = nodes[(i + 3) % n];

    correctnessPassed = true;
    for (int i = 0; i < n; i++)
    {
        if (!ReferenceEquals(nodes[i].Next, nodes[(i + 3) % n]))
        {
            correctnessPassed = false;
            break;
        }
    }
}
Console.WriteLine($"# Correctness: {(correctnessPassed ? "PASS" : "FAIL")}");

// --- Phase 2: throughput ---------------------------------------------------
long[] perThreadOps = new long[threadCount];
var barrier = new Barrier(threadCount + 1);
var stopFlag = new StopFlag();
var threads = new Thread[threadCount];

for (int ti = 0; ti < threadCount; ti++)
{
    int threadIndex = ti;
    threads[ti] = new Thread(() =>
    {
        // Each thread owns its own private ring of objects - no other
        // thread ever reads or writes these references. This isolates the
        // write barrier's own per-store cost (and any shared card-table
        // cache-line traffic) from any other synchronization. Deliberately
        // small and accessed sequentially so the whole ring stays hot in
        // L1 cache: this isolates the write barrier's own instruction cost
        // (a handful of ALU ops + a card-table byte read/write) rather
        // than letting it be swamped by cache-miss latency from a large
        // working set, which is what a randomized access pattern measured.
        var ring = new Node[ringSize];
        for (int i = 0; i < ringSize; i++) ring[i] = new Node();

        long ops = 0;
        barrier.SignalAndWait();

        while (!stopFlag.Stop)
        {
            // Batch of stores between stop-flag checks to keep overhead low.
            for (int b = 0; b < 4096; b++)
            {
                for (int i = 0; i < ringSize; i++)
                {
                    ring[i].Next = ring[(i + 1) % ringSize]; // the reference-type store under test
                }
                ops += ringSize;
            }
        }

        perThreadOps[threadIndex] = ops;
    });
    threads[ti].IsBackground = true;
    threads[ti].Start();
}

barrier.SignalAndWait(); // release all worker threads together
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
    RingSize = ringSize,
    DurationSeconds = sw.Elapsed.TotalSeconds,
    TotalReferenceStores = totalOps,
    ReferenceStoresPerSecond = opsPerSecond,
    ReferenceStoresPerSecondPerThread = opsPerSecond / threadCount,
    CorrectnessPassed = correctnessPassed,
};

string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
Console.WriteLine("##RESULT##" + json);

internal class Node
{
    public Node? Next;
}

// A plain mutable-bool wrapper (not a struct field) so all worker threads
// observe the same StopFlag.Stop write - a volatile bool field would also
// work, but this keeps the closures simple.
internal class StopFlag
{
    public volatile bool Stop;
}

internal class BenchResult
{
    public string GcName { get; set; } = "";
    public int ThreadCount { get; set; }
    public int RingSize { get; set; }
    public double DurationSeconds { get; set; }
    public long TotalReferenceStores { get; set; }
    public double ReferenceStoresPerSecond { get; set; }
    public double ReferenceStoresPerSecondPerThread { get; set; }
    public bool CorrectnessPassed { get; set; }
}
