// ZeroGC benchmark console app.
//
// Runs a mixed allocation workload (small short-lived objects, some medium
// "survivor-like" objects kept alive in a list, and occasional large object
// heap allocations) for a fixed duration, then reports throughput and GC
// counters to stdout as a single JSON line so the harness can parse it.

using System.Diagnostics;
using System.Runtime;
using System.Text.Json;

int durationSeconds = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 60;
string label = args.Length > 1 ? args[1] : "run";

// AppContext doesn't expose the resolved standalone GC name to managed code,
// so report the DOTNET_GCName knob directly (this is exactly how the bench
// harness selects ZeroGC vs. the built-in GC in the first place).
string gcName = Environment.GetEnvironmentVariable("DOTNET_GCName")
    ?? Environment.GetEnvironmentVariable("COMPlus_GCName")
    ?? (GCSettings.IsServerGC ? "CoreCLR (Server)" : "CoreCLR (Workstation)");

Console.WriteLine($"# ZeroGC-bench ConsoleApp starting: duration={durationSeconds}s label={label}");
Console.WriteLine($"# GC.Name={gcName}");

var survivors = new List<byte[]>();
var rng = new Random(12345);
long ops = 0;
var sw = Stopwatch.StartNew();
var deadline = TimeSpan.FromSeconds(durationSeconds);

var proc = Process.GetCurrentProcess();

while (sw.Elapsed < deadline)
{
    // Small short-lived allocations (typical gen0 churn).
    for (int i = 0; i < 200; i++)
    {
        var small = new byte[rng.Next(16, 256)];
        small[0] = (byte)i;
        ops++;
    }

    // Some strings / small objects too.
    for (int i = 0; i < 50; i++)
    {
        var s = new string('x', rng.Next(8, 64));
        ops++;
        if (s.Length == -1) Console.WriteLine(s); // never true; keeps JIT from eliding the alloc
    }

    // Occasionally keep something alive (simulates real app state growth).
    if (ops % 5000 == 0)
    {
        survivors.Add(new byte[rng.Next(1024, 4096)]);
    }

    // Occasional large object heap allocation.
    if (ops % 20000 == 0)
    {
        var large = new byte[rng.Next(90_000, 200_000)];
        large[0] = 1;
        ops++;
    }

    // Throttle to a realistic sustained allocation rate. A GC that never
    // reclaims memory would otherwise commit many tens of GB per minute at
    // an unthrottled rate, which is unrepresentative of real workloads and
    // risks exhausting machine memory during a multi-minute comparison run.
    Thread.Sleep(1);
}

sw.Stop();
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
    SurvivorListCount = survivors.Count
};

var memInfo = GC.GetGCMemoryInfo();
result.HeapSizeBytes = memInfo.HeapSizeBytes;
result.TotalCommittedBytes = memInfo.TotalCommittedBytes;
result.PauseTimePercentage = memInfo.PauseTimePercentage;

string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
Console.WriteLine("##RESULT##" + json);

GC.KeepAlive(survivors);

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
    public int SurvivorListCount { get; set; }
}
