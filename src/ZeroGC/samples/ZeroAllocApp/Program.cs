// ZeroGC benchmark app: a (near-)zero-allocation numeric compute workload.
//
// Unlike ConsoleApp/WebApi (which deliberately allocate to exercise the
// GC), this app repeatedly performs dense matrix multiplication entirely on
// buffers allocated ONCE at startup and reused for every iteration - no
// managed heap allocations occur in the hot loop itself. This models a
// class of real .NET workloads (numeric/ML kernels, codecs, low-latency
// trading, game-engine hot paths) that are written specifically to avoid
// allocating so GC pauses can't affect them.
//
// The point of comparing this workload across GC modes is the opposite of
// the other scenarios: when an app doesn't allocate, it shouldn't matter
// whether the GC never collects (ZeroGC) or collects normally (Workstation/
// Server) - there's nothing to collect either way, so all three modes
// should show near-identical throughput, ~0 collections, and flat memory.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime;
using System.Text.Json;

var meter = new Meter("ZeroGC.Bench");
var opsCounter = meter.CreateCounter<long>("operations", description: "Completed benchmark operations");

int durationSeconds = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 60;
string label = args.Length > 1 ? args[1] : "run";

string gcName = Environment.GetEnvironmentVariable("DOTNET_GCName")
    ?? Environment.GetEnvironmentVariable("COMPlus_GCName")
    ?? (GCSettings.IsServerGC ? "CoreCLR (Server)" : "CoreCLR (Workstation)");

Console.WriteLine($"# ZeroGC-bench ZeroAllocApp starting: duration={durationSeconds}s label={label}");
Console.WriteLine($"# GC.Name={gcName}");

// --- Fixed-size buffers, allocated once. Nothing below this point in the
// hot loop allocates on the managed heap. ---
const int N = 96; // 96x96 float matrices: cache-friendly, ~880K FLOPs/multiply
var a = new float[N * N];
var b = new float[N * N];
var c = new float[N * N];

var rng = new Random(12345);
for (int i = 0; i < a.Length; i++)
{
    a[i] = (float)rng.NextDouble();
    b[i] = (float)rng.NextDouble();
}

long ops = 0;
long checksumBits = 0; // accumulates a few result bits so the JIT can't dead-code-eliminate the multiply
var proc = Process.GetCurrentProcess();
long allocBytesStart = GC.GetAllocatedBytesForCurrentThread();

var sw = Stopwatch.StartNew();
var deadline = TimeSpan.FromSeconds(durationSeconds);

while (sw.Elapsed < deadline)
{
    MatMul(a, b, c, N);
    checksumBits += BitConverter.SingleToInt32Bits(c[(int)(ops % (N * N))]);
    ops++;
    opsCounter.Add(1);
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
    Checksum = checksumBits
};

var memInfo = GC.GetGCMemoryInfo();
result.HeapSizeBytes = memInfo.HeapSizeBytes;
result.TotalCommittedBytes = memInfo.TotalCommittedBytes;
result.PauseTimePercentage = memInfo.PauseTimePercentage;

string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
Console.WriteLine("##RESULT##" + json);

// Dense NxN matmul (i-k-j loop order for sequential access on the inner j
// loop). Clears the output buffer first (a fast memset, not an allocation)
// so accumulated values can't grow unbounded/overflow over a multi-minute
// run - this keeps every iteration's work identical and allocation-free.
static void MatMul(float[] a, float[] b, float[] c, int n)
{
    Array.Clear(c, 0, c.Length);
    for (int i = 0; i < n; i++)
    {
        int cRow = i * n;
        for (int k = 0; k < n; k++)
        {
            float aik = a[cRow + k];
            int bRow = k * n;
            for (int j = 0; j < n; j++)
            {
                c[cRow + j] += aik * b[bRow + j];
            }
        }
    }
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
    public long Checksum { get; set; }
}
