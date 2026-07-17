// ZeroGC benchmark ASP.NET Core (minimal API / Kestrel) app.
//
// Hosts a small set of endpoints that do realistic per-request allocation
// (JSON (de)serialization, string building, list/dictionary work), then
// drives itself with an in-process HTTP client for a fixed duration so the
// whole run (server + load generator) is a single, easily automated
// process. Reports the same BenchResult JSON shape as the console sample so
// both can be compared/plotted uniformly.

using System.Diagnostics;
using System.Runtime;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders(); // keep stdout free for the ##RESULT## line
builder.WebHost.UseUrls("http://127.0.0.1:0"); // let Kestrel pick a free port
var app = builder.Build();

// Per-request handler: simulates a small JSON API endpoint that builds a
// response object, serializes it, and does a bit of string/list work -
// representative gen0-heavy allocation churn for a typical web API.
app.MapGet("/api/work", (int size) =>
{
    var items = new List<WorkItem>(size);
    for (int i = 0; i < size; i++)
    {
        items.Add(new WorkItem(i, $"item-{i}-{Guid.NewGuid():N}", i * 1.5));
    }
    var payload = new WorkResponse(items.Count, items.Sum(x => x.Value), items.Take(5).ToArray());
    return Results.Json(payload);
});

app.MapGet("/api/ping", () => "pong");

app.Lifetime.ApplicationStarted.Register(() =>
{
    // Kick off the self-driving load generator once Kestrel is actually listening.
    _ = Task.Run(() => RunBenchmarkAndExitAsync(app));
});

app.Run();

static async Task RunBenchmarkAndExitAsync(WebApplication app)
{
    int durationSeconds = int.TryParse(Environment.GetEnvironmentVariable("ZEROGC_BENCH_DURATION_SECONDS"), out var d) ? d : 60;
    string label = Environment.GetEnvironmentVariable("ZEROGC_BENCH_LABEL") ?? "run";

    string gcName = Environment.GetEnvironmentVariable("DOTNET_GCName")
        ?? Environment.GetEnvironmentVariable("COMPlus_GCName")
        ?? (GCSettings.IsServerGC ? "CoreCLR (Server)" : "CoreCLR (Workstation)");

    var addresses = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
        .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
    string baseUrl = addresses?.Addresses.FirstOrDefault() ?? "http://127.0.0.1:5000";

    Console.WriteLine($"# ZeroGC-bench WebApi starting: duration={durationSeconds}s label={label} url={baseUrl}");
    Console.WriteLine($"# GC.Name={gcName}");

    using var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

    var rng = new Random(12345);
    long requests = 0;
    long errors = 0;
    var sw = Stopwatch.StartNew();
    var deadline = TimeSpan.FromSeconds(durationSeconds);
    var proc = Process.GetCurrentProcess();

    // Warm up JIT / connection pool before measuring.
    await client.GetStringAsync("/api/ping");

    while (sw.Elapsed < deadline)
    {
        try
        {
            int size = rng.Next(5, 50);
            var resp = await client.GetStringAsync($"/api/work?size={size}");
            requests++;
            if (resp.Length == -1) Console.WriteLine(resp); // never true; prevents dead-code elimination
        }
        catch
        {
            errors++;
        }

        // Throttle to a realistic sustained request rate (see ConsoleApp
        // sample for why: an unthrottled loop against a never-collecting GC
        // would commit unbounded memory very quickly).
        if (requests % 4 == 0)
        {
            await Task.Delay(1);
        }
    }

    sw.Stop();
    proc.Refresh();

    var result = new BenchResult
    {
        Label = label,
        DurationSeconds = sw.Elapsed.TotalSeconds,
        Operations = requests,
        OpsPerSecond = requests / sw.Elapsed.TotalSeconds,
        GcName = gcName,
        TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
        Gen0Collections = GC.CollectionCount(0),
        Gen1Collections = GC.CollectionCount(1),
        Gen2Collections = GC.CollectionCount(2),
        WorkingSetBytes = proc.WorkingSet64,
        PrivateBytes = proc.PrivateMemorySize64,
        PeakWorkingSetBytes = proc.PeakWorkingSet64,
        Errors = errors
    };

    var memInfo = GC.GetGCMemoryInfo();
    result.HeapSizeBytes = memInfo.HeapSizeBytes;
    result.TotalCommittedBytes = memInfo.TotalCommittedBytes;
    result.PauseTimePercentage = memInfo.PauseTimePercentage;

    string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
    Console.WriteLine("##RESULT##" + json);

    await app.StopAsync();
    Environment.Exit(0);
}

internal record WorkItem(int Id, string Name, double Value);
internal record WorkResponse(int Count, double Sum, WorkItem[] Sample);

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
    public long Errors { get; set; }
}
