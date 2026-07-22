// ZeroGC benchmark app: a realistic "growing in-memory cache" service that
// naturally triggers long-running gen2 GCs through its own workload - no
// GC.Collect() is ever called by this program.
//
// This models a very common real-world pattern: a long-running service
// (e.g. a session store, product-catalog cache, feature store, or
// in-memory lookup table) that keeps accumulating long-lived entries over
// its lifetime while continuously handling "requests" that allocate
// ordinary short-lived garbage. As the cache grows, gen2's live object
// count grows with it, so the *same* CLR heuristics that decide when to
// promote/collect naturally produce gen2 collections that become
// progressively more expensive over the run - without this program ever
// forcing one. Any gen2 pauses observed here are 100% organic: the CLR's
// own allocation-budget/promotion heuristics decided to run them, exactly
// as they would in a production app suffering from an ever-growing cache.
//
// We *observe* (never induce) gen2 activity via two purely-informational,
// well documented APIs: GC.CollectionCount(2) (has a gen2 collection
// happened?) and GC.GetTotalPauseDuration() (cumulative wall-clock time
// spent paused for any GC, added in .NET 5). Sampling the delta of the
// latter whenever the former increments gives an approximate duration for
// each naturally-occurring gen2 event, with no side effects on the GC
// itself.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime;
using System.Text.Json;

var meter = new Meter("ZeroGC.Bench");
var opsCounter = meter.CreateCounter<long>("operations", description: "Completed benchmark operations");

int durationSeconds = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 60;
string label = args.Length > 1 ? args[1] : "run";
// Final size (MB) the long-lived cache grows to by the end of the run,
// ramped up roughly linearly over elapsed time (not allocated up-front) so
// early-run gen2 collections are cheap and late-run ones are expensive -
// exactly how a real, ever-growing production cache behaves over time.
int finalCacheTargetMB = args.Length > 2 && int.TryParse(args[2], out var mb) ? mb : 3000;
// Per-"request" transient allocation size in bytes, simulating ordinary
// short-lived garbage produced while handling a unit of work (building a
// response, a small buffer, etc.) - this is what supplies the gen0/gen1
// allocation pressure that drives natural promotion/collection.
int requestAllocBytes = args.Length > 3 && int.TryParse(args[3], out var rb) ? rb : 512;

string gcName = Environment.GetEnvironmentVariable("DOTNET_GCName")
    ?? Environment.GetEnvironmentVariable("COMPlus_GCName")
    ?? (GCSettings.IsServerGC ? "CoreCLR (Server)" : "CoreCLR (Workstation)");

Console.WriteLine($"# ZeroGC-bench GrowingCacheApp starting: duration={durationSeconds}s label={label} finalCacheTargetMB={finalCacheTargetMB} requestAllocBytes={requestAllocBytes}");
Console.WriteLine($"# GC.Name={gcName}");

// Long-lived cache: never evicted, grows for the whole run - the source of
// the ever-larger gen2 live set. ~136 bytes/entry (key + CacheEntry object
// header/fields + the string + the double[4]), used only to size the
// growth ramp; the real number is whatever the runtime actually needs.
const int ApproxBytesPerEntry = 136;
long targetEntries = (long)finalCacheTargetMB * 1024 * 1024 / ApproxBytesPerEntry;

var cache = new Dictionary<int, CacheEntry>(capacity: 1024);
var rng = new Random(12345);
long nextEntryIndex = 0;

long ops = 0;
long churnChecksum = 0;
var proc = Process.GetCurrentProcess();
long allocBytesStart = GC.GetAllocatedBytesForCurrentThread();

// Naturally-occurring gen2 pause observation state - purely passive, never
// forces or requests a collection.
long lastGen2Count = GC.CollectionCount(2);
double lastTotalPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds;
var observedGen2PausesMs = new List<double>();
var watchSw = Stopwatch.StartNew();

var sw = Stopwatch.StartNew();
var deadline = TimeSpan.FromSeconds(durationSeconds);

while (sw.Elapsed < deadline)
{
    // Simulate handling one "request": allocate ordinary short-lived
    // garbage representing per-request work (building a response buffer,
    // a small temporary object, etc.).
    var reqBuf = new byte[requestAllocBytes];
    reqBuf[0] = (byte)(ops & 0xFF);
    churnChecksum += reqBuf[0];

    // Grow the long-lived cache in proportion to elapsed time, simulating
    // a real service whose in-memory cache/session store keeps
    // accumulating entries over its lifetime - a common, realistic source
    // of gradually worsening GC pauses in long-running processes.
    double targetProgress = Math.Min(sw.Elapsed.TotalSeconds / durationSeconds, 1.0);
    long targetEntriesNow = (long)(targetEntries * targetProgress);
    while (nextEntryIndex < targetEntriesNow)
    {
        cache[(int)nextEntryIndex] = new CacheEntry
        {
            Payload = "entry-" + nextEntryIndex.ToString("x8"),
            Values = new[] { rng.NextDouble(), rng.NextDouble(), rng.NextDouble(), rng.NextDouble() }
        };
        nextEntryIndex++;
    }

    ops++;
    opsCounter.Add(1);

    // Throttle to a realistic per-request rate (matches ConsoleApp's
    // pacing) instead of allocating as fast as the CPU allows. This keeps
    // total lifetime allocation volume bounded to something a process can
    // actually hold in memory for the full run under ZeroGC (which never
    // reclaims ANY allocation, including this transient request churn),
    // while still supplying enough sustained gen0/gen1 pressure under
    // Workstation/Server GC to naturally promote the ever-growing cache
    // into gen2 over time.
    Thread.Sleep(1);

    // Passive observation only, every ~50ms: did a gen2 collection happen
    // since we last checked? If so, attribute the incremental total pause
    // duration to it. Nothing here triggers, requests, or waits for a GC.
    if (watchSw.Elapsed.TotalMilliseconds >= 50)
    {
        long gen2Now = GC.CollectionCount(2);
        if (gen2Now != lastGen2Count)
        {
            double totalPauseNow = GC.GetTotalPauseDuration().TotalMilliseconds;
            double delta = totalPauseNow - lastTotalPauseMs;
            if (delta > 0) observedGen2PausesMs.Add(delta);
            lastTotalPauseMs = totalPauseNow;
            lastGen2Count = gen2Now;
        }
        watchSw.Restart();
    }
}

sw.Stop();
proc.Refresh();

long allocBytesEnd = GC.GetAllocatedBytesForCurrentThread();

var result = new BenchResult
{
    Label = label,
    DurationSeconds = sw.Elapsed.TotalSeconds,
    Operations = ops,
    OpsPerSecond = ops / sw.Elapsed.TotalSeconds,
    GcName = gcName,
    TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
    BytesAllocatedThisThreadDuringRun = allocBytesEnd - allocBytesStart,
    Gen0Collections = GC.CollectionCount(0),
    Gen1Collections = GC.CollectionCount(1),
    Gen2Collections = GC.CollectionCount(2),
    WorkingSetBytes = proc.WorkingSet64,
    PrivateBytes = proc.PrivateMemorySize64,
    PeakWorkingSetBytes = proc.PeakWorkingSet64,
    FinalCacheEntryCount = nextEntryIndex,
    Checksum = churnChecksum
};

var memInfo = GC.GetGCMemoryInfo();
result.HeapSizeBytes = memInfo.HeapSizeBytes;
result.TotalCommittedBytes = memInfo.TotalCommittedBytes;
result.PauseTimePercentage = memInfo.PauseTimePercentage;

if (observedGen2PausesMs.Count > 0)
{
    result.ObservedGen2Events = observedGen2PausesMs.Count;
    result.ObservedGen2PauseAvgMs = observedGen2PausesMs.Average();
    result.ObservedGen2PauseMaxMs = observedGen2PausesMs.Max();
    result.ObservedGen2PauseMinMs = observedGen2PausesMs.Min();
    result.ObservedGen2PauseSamplesMs = observedGen2PausesMs;
}

string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
Console.WriteLine("##RESULT##" + json);

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
    public long BytesAllocatedThisThreadDuringRun { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public long WorkingSetBytes { get; set; }
    public long PrivateBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public long HeapSizeBytes { get; set; }
    public long TotalCommittedBytes { get; set; }
    public double PauseTimePercentage { get; set; }
    public long FinalCacheEntryCount { get; set; }
    public int ObservedGen2Events { get; set; }
    public double ObservedGen2PauseAvgMs { get; set; }
    public double ObservedGen2PauseMaxMs { get; set; }
    public double ObservedGen2PauseMinMs { get; set; }
    public List<double>? ObservedGen2PauseSamplesMs { get; set; }
    public long Checksum { get; set; }
}
