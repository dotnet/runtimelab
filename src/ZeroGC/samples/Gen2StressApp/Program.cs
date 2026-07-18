// ZeroGC benchmark app: deliberately triggers long, fully-blocking gen2
// (full) garbage collections - the scenario the other benchmarks in this
// suite don't exercise well, since they're tuned to keep run duration
// comparable across GC modes rather than to maximize a single pause.
//
// Strategy:
//   1. Build up a large, long-lived object graph at startup: millions of
//      small interconnected objects (a Dictionary-backed "cache") plus a
//      set of large (LOH) byte-array chunks. None of this is ever released,
//      so every full gen2 collection's mark phase has to trace the whole
//      graph and its sweep/compaction phase has to deal with the whole
//      heap - this is what makes gen2 pauses expensive (mark cost scales
//      with live object *count*, not just live bytes).
//   2. Disable background/concurrent GC (see Gen2StressApp.csproj's
//      <ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>) so
//      every gen2 collection is a plain, fully-blocking stop-the-world
//      collection rather than mostly running concurrently with only a
//      short blocking suspend - this is what makes the *whole* collection,
//      not just a brief phase of it, count as "pause time".
//   3. In a steady-state loop, keep allocating small transient garbage
//      (so natural generational promotion/collections still occur) and,
//      every few seconds, explicitly force a blocking, compacting gen2
//      collection (`GC.Collect(2, Forced, blocking: true, compacting:
//      true)`), timing it precisely with a Stopwatch. Both the natural and
//      forced collections show up in dotnet-counters' pause-time counter
//      for the report's charts; the forced ones are also reported directly
//      in the ##RESULT## JSON so their true duration is captured exactly
//      (a per-second sampling window can under- or over-attribute a pause
//      that straddles a sample boundary).
//
// Under ZeroGC, GarbageCollect() is a documented no-op (see
// native/ZeroGCHeap.cpp) - so the exact same forced-collection calls that
// take hundreds of ms under Workstation/Server GC should return in
// microseconds under ZeroGC, while retained memory just keeps growing
// (nothing is ever actually reclaimed).

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime;
using System.Text.Json;

var meter = new Meter("ZeroGC.Bench");
var opsCounter = meter.CreateCounter<long>("operations", description: "Completed benchmark loop iterations");

int durationSeconds = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 60;
string label = args.Length > 1 ? args[1] : "run";
int retainedTargetMB = args.Length > 2 && int.TryParse(args[2], out var t) ? t : 3000;
int forcedGcIntervalSeconds = args.Length > 3 && int.TryParse(args[3], out var iv) ? iv : 5;

string gcName = Environment.GetEnvironmentVariable("DOTNET_GCName")
    ?? Environment.GetEnvironmentVariable("COMPlus_GCName")
    ?? (GCSettings.IsServerGC ? "CoreCLR (Server)" : "CoreCLR (Workstation)");

Console.WriteLine($"# ZeroGC-bench Gen2StressApp starting: duration={durationSeconds}s retainedTargetMB={retainedTargetMB} forcedGcIntervalSeconds={forcedGcIntervalSeconds} label={label}");
Console.WriteLine($"# GC.Name={gcName} IsServerGC={GCSettings.IsServerGC} LatencyMode={GCSettings.LatencyMode}");

// --- Build phase: a large, long-lived object graph that never gets
// released, split roughly 60% "SOH cache" (many small interconnected
// objects - stresses mark-phase object count) / 40% "LOH chunks" (large
// byte arrays - stresses sweep/compaction over raw bytes). ---
var buildSw = Stopwatch.StartNew();

long sohTargetBytes = (long)(retainedTargetMB * 1024L * 1024L * 0.6);
long lohTargetBytes = (long)(retainedTargetMB * 1024L * 1024L * 0.4);

// Each CacheEntry (~40 bytes) + its nested string (~40 bytes) + its nested
// double[4] (~48 bytes) is roughly 130 bytes of live, reference-laden data
// per cache key - deliberately several small objects per entry rather than
// one bigger one, since that's what makes the mark phase expensive.
const int approxBytesPerEntry = 130;
long entryCount = sohTargetBytes / approxBytesPerEntry;

var cache = new Dictionary<int, CacheEntry>((int)Math.Min(entryCount, int.MaxValue / 2));
var rng = new Random(12345);
for (long i = 0; i < entryCount; i++)
{
    cache[(int)i] = new CacheEntry
    {
        Payload = "entry-" + i.ToString("x8"),
        Values = new[] { rng.NextDouble(), rng.NextDouble(), rng.NextDouble(), rng.NextDouble() }
    };
}

const int lohChunkSize = 200_000; // > 85,000 bytes -> goes straight to LOH
int lohChunkCount = (int)(lohTargetBytes / lohChunkSize);
var lohChunks = new List<byte[]>(lohChunkCount);
for (int i = 0; i < lohChunkCount; i++)
{
    var chunk = new byte[lohChunkSize];
    rng.NextBytes(chunk.AsSpan(0, Math.Min(chunk.Length, 4096))); // touch pages, avoid the JIT/runtime eliding a fully-zero fill
    lohChunks.Add(chunk);
}

buildSw.Stop();
long retainedObjectCount = cache.Count * 3L /* Dictionary entry + string + double[] */ + lohChunks.Count;
Console.WriteLine($"# Build phase done in {buildSw.Elapsed.TotalSeconds:F1}s: {cache.Count:N0} cache entries, {lohChunks.Count:N0} LOH chunks (~{retainedObjectCount:N0} live objects)");

// --- Steady-state churn + periodic forced full GC ---
var forcedPauseMsSamples = new List<double>();
long ops = 0;
long churnChecksum = 0;
var sw = Stopwatch.StartNew();
var deadline = TimeSpan.FromSeconds(durationSeconds);
var nextForcedGc = TimeSpan.FromSeconds(forcedGcIntervalSeconds);

while (sw.Elapsed < deadline)
{
    // Small transient garbage so natural gen0/1(/occasionally 2) promotion
    // and collection still happens between forced collections.
    var churn = new byte[2048];
    churn[0] = (byte)(ops & 0xFF);
    churnChecksum += churn[0];
    ops++;
    opsCounter.Add(1);

    if (sw.Elapsed >= nextForcedGc)
    {
        var gcSw = Stopwatch.StartNew();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        gcSw.Stop();
        forcedPauseMsSamples.Add(gcSw.Elapsed.TotalMilliseconds);
        nextForcedGc += TimeSpan.FromSeconds(forcedGcIntervalSeconds);
    }

    Thread.Sleep(1);
}

sw.Stop();
var proc = Process.GetCurrentProcess();
proc.Refresh();

var result = new BenchResult
{
    Label = label,
    DurationSeconds = sw.Elapsed.TotalSeconds,
    Operations = ops,
    OpsPerSecond = ops / sw.Elapsed.TotalSeconds,
    GcName = gcName,
    TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
    Gen0Collections = GC.CollectionCount(0),
    Gen1Collections = GC.CollectionCount(1),
    Gen2Collections = GC.CollectionCount(2),
    WorkingSetBytes = proc.WorkingSet64,
    PrivateBytes = proc.PrivateMemorySize64,
    PeakWorkingSetBytes = proc.PeakWorkingSet64,
    RetainedObjectCount = retainedObjectCount,
    RetainedTargetMB = retainedTargetMB,
    ForcedGen2Collections = forcedPauseMsSamples.Count,
    ForcedGen2PauseAvgMs = forcedPauseMsSamples.Count > 0 ? forcedPauseMsSamples.Average() : 0,
    ForcedGen2PauseMaxMs = forcedPauseMsSamples.Count > 0 ? forcedPauseMsSamples.Max() : 0,
    ForcedGen2PauseMinMs = forcedPauseMsSamples.Count > 0 ? forcedPauseMsSamples.Min() : 0,
    ForcedGen2PauseSamplesMs = forcedPauseMsSamples,
    Checksum = churnChecksum
};

var memInfo = GC.GetGCMemoryInfo();
result.HeapSizeBytes = memInfo.HeapSizeBytes;
result.TotalCommittedBytes = memInfo.TotalCommittedBytes;
result.PauseTimePercentage = memInfo.PauseTimePercentage;

string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
Console.WriteLine("##RESULT##" + json);

// Keep the retained graph alive (and out of the JIT's dead-code-elimination
// reach) all the way to the end.
GC.KeepAlive(cache);
GC.KeepAlive(lohChunks);

internal class CacheEntry
{
    public string Payload { get; set; } = "";
    public double[] Values { get; set; } = Array.Empty<double>();
}

internal class BenchResult
{
    public string Label { get; set; } = "";
    public double DurationSeconds { get; set; }
    public long Operations { get; set; }
    public double OpsPerSecond { get; set; }
    public string GcName { get; set; } = "";
    public long TotalAllocatedBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public long WorkingSetBytes { get; set; }
    public long PrivateBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public long HeapSizeBytes { get; set; }
    public long TotalCommittedBytes { get; set; }
    public double PauseTimePercentage { get; set; }
    public long RetainedObjectCount { get; set; }
    public int RetainedTargetMB { get; set; }
    public int ForcedGen2Collections { get; set; }
    public double ForcedGen2PauseAvgMs { get; set; }
    public double ForcedGen2PauseMaxMs { get; set; }
    public double ForcedGen2PauseMinMs { get; set; }
    public List<double> ForcedGen2PauseSamplesMs { get; set; } = new();
    public long Checksum { get; set; }
}
