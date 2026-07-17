<#
.SYNOPSIS
    Renders results.json (produced by run-benchmarks.ps1) into a
    self-contained HTML report comparing ZeroGC vs. the regular CoreCLR GC
    across the console and ASP.NET Core samples. Pure HTML/CSS (no external
    JS/CSS dependencies), so the artifact works fully offline.
#>
param(
    [string]$ResultsJson = "$PSScriptRoot\results\results.json",
    [string]$OutHtml = "$PSScriptRoot\results\report.html"
)

$ErrorActionPreference = "Stop"
$data = Get-Content $ResultsJson -Raw | ConvertFrom-Json

function Fmt-Bytes($n) {
    $n = [double]$n
    $units = "B","KB","MB","GB","TB"
    $i = 0
    while ($n -ge 1024 -and $i -lt $units.Length - 1) { $n /= 1024; $i++ }
    return "{0:N2} {1}" -f $n, $units[$i]
}
function Fmt-Num($n) { return "{0:N0}" -f [double]$n }
function Fmt-Dec($n, $d = 1) { return ("{0:N" + $d + "}") -f [double]$n }

function Bar($value, $max, $color) {
    $pct = if ($max -gt 0) { [Math]::Min(100, ($value / $max) * 100) } else { 0 }
    return "<div class='barTrack'><div class='barFill' style='width:$([Math]::Round($pct,1))%;background:$color'></div></div>"
}

function Section($title, $regular, $zerogc) {
    $maxAlloc = [Math]::Max($regular.TotalAllocatedBytes, $zerogc.TotalAllocatedBytes)
    $maxCommitted = [Math]::Max($regular.TotalCommittedBytes, $zerogc.TotalCommittedBytes)
    $maxWs = [Math]::Max($regular.WorkingSetBytes, $zerogc.WorkingSetBytes)
    $maxOps = [Math]::Max($regular.OpsPerSecond, $zerogc.OpsPerSecond)
    $maxGen0 = [Math]::Max($regular.Gen0Collections, $zerogc.Gen0Collections)
    $maxGen0 = [Math]::Max($maxGen0, 1)

    return @"
<section>
  <h2>$title</h2>
  <table class="cmp">
    <thead>
      <tr><th>Metric</th><th>Regular GC ($($regular.GcName))</th><th></th><th>ZeroGC ($($zerogc.GcName))</th><th></th></tr>
    </thead>
    <tbody>
      <tr>
        <td>Duration (s)</td>
        <td>$(Fmt-Dec $regular.DurationSeconds 1)</td><td></td>
        <td>$(Fmt-Dec $zerogc.DurationSeconds 1)</td><td></td>
      </tr>
      <tr>
        <td>Operations / Requests</td>
        <td>$(Fmt-Num $regular.Operations)</td><td></td>
        <td>$(Fmt-Num $zerogc.Operations)</td><td></td>
      </tr>
      <tr>
        <td>Throughput (ops/sec)</td>
        <td>$(Fmt-Dec $regular.OpsPerSecond 1)$(Bar $regular.OpsPerSecond $maxOps '#2b7de9')</td><td></td>
        <td>$(Fmt-Dec $zerogc.OpsPerSecond 1)$(Bar $zerogc.OpsPerSecond $maxOps '#e94f2b')</td><td></td>
      </tr>
      <tr>
        <td>Total allocated bytes</td>
        <td>$(Fmt-Bytes $regular.TotalAllocatedBytes)$(Bar $regular.TotalAllocatedBytes $maxAlloc '#2b7de9')</td><td></td>
        <td>$(Fmt-Bytes $zerogc.TotalAllocatedBytes)$(Bar $zerogc.TotalAllocatedBytes $maxAlloc '#e94f2b')</td><td></td>
      </tr>
      <tr>
        <td>Gen0 collections</td>
        <td>$($regular.Gen0Collections)$(Bar $regular.Gen0Collections $maxGen0 '#2b7de9')</td><td></td>
        <td>$($zerogc.Gen0Collections)$(Bar $zerogc.Gen0Collections $maxGen0 '#e94f2b')</td><td></td>
      </tr>
      <tr>
        <td>Gen1 collections</td>
        <td>$($regular.Gen1Collections)</td><td></td>
        <td>$($zerogc.Gen1Collections)</td><td></td>
      </tr>
      <tr>
        <td>Gen2 collections</td>
        <td>$($regular.Gen2Collections)</td><td></td>
        <td>$($zerogc.Gen2Collections)</td><td></td>
      </tr>
      <tr>
        <td>GC heap size (GetGCMemoryInfo)</td>
        <td>$(Fmt-Bytes $regular.HeapSizeBytes)</td><td></td>
        <td>$(Fmt-Bytes $zerogc.HeapSizeBytes)</td><td></td>
      </tr>
      <tr>
        <td>Total committed bytes</td>
        <td>$(Fmt-Bytes $regular.TotalCommittedBytes)$(Bar $regular.TotalCommittedBytes $maxCommitted '#2b7de9')</td><td></td>
        <td>$(Fmt-Bytes $zerogc.TotalCommittedBytes)$(Bar $zerogc.TotalCommittedBytes $maxCommitted '#e94f2b')</td><td></td>
      </tr>
      <tr>
        <td>Process working set</td>
        <td>$(Fmt-Bytes $regular.WorkingSetBytes)$(Bar $regular.WorkingSetBytes $maxWs '#2b7de9')</td><td></td>
        <td>$(Fmt-Bytes $zerogc.WorkingSetBytes)$(Bar $zerogc.WorkingSetBytes $maxWs '#e94f2b')</td><td></td>
      </tr>
      <tr>
        <td>Peak working set</td>
        <td>$(Fmt-Bytes $regular.PeakWorkingSetBytes)</td><td></td>
        <td>$(Fmt-Bytes $zerogc.PeakWorkingSetBytes)</td><td></td>
      </tr>
      <tr>
        <td>GC pause time %</td>
        <td>$(Fmt-Dec $regular.PauseTimePercentage 3)%</td><td></td>
        <td>$(Fmt-Dec $zerogc.PauseTimePercentage 3)%</td><td></td>
      </tr>
    </tbody>
  </table>
</section>
"@
}

$genDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$consoleSection = Section "Console app benchmark" $data.ConsoleRegular $data.ConsoleZeroGC
$webApiSection = Section "ASP.NET Core (Kestrel minimal API) benchmark" $data.WebApiRegular $data.WebApiZeroGC

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>ZeroGC vs Regular GC - Benchmark Report</title>
<style>
  body { font-family: Segoe UI, Arial, sans-serif; margin: 2rem; background: #f7f8fa; color: #1a1a1a; }
  h1 { margin-bottom: 0.2rem; }
  .subtitle { color: #666; margin-top: 0; }
  section { background: #fff; border-radius: 8px; padding: 1.2rem 1.5rem; margin-bottom: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
  h2 { margin-top: 0; border-bottom: 2px solid #eee; padding-bottom: 0.5rem; }
  table.cmp { width: 100%; border-collapse: collapse; }
  table.cmp td, table.cmp th { padding: 0.5rem 0.7rem; text-align: left; vertical-align: middle; }
  table.cmp thead th { border-bottom: 2px solid #ddd; font-size: 0.85rem; color: #555; }
  table.cmp tbody tr:nth-child(odd) { background: #fafbfc; }
  table.cmp td:first-child { font-weight: 600; width: 14rem; }
  .barTrack { display:inline-block; width: 140px; height: 10px; background: #eee; border-radius: 5px; margin-left: 0.6rem; vertical-align: middle; overflow:hidden; }
  .barFill { height: 100%; border-radius: 5px; }
  .legend span { display:inline-block; margin-right:1.5rem; }
  .legend .sw { display:inline-block; width:12px;height:12px;border-radius:3px;margin-right:0.4rem;vertical-align:middle; }
  .callout { background:#fff8e6; border:1px solid #f0d98c; border-radius:6px; padding:1rem 1.2rem; margin-bottom:1.5rem; }
  code { background:#f0f0f0; padding:0.1rem 0.35rem; border-radius:4px; }
  footer { color:#888; font-size:0.85rem; margin-top:2rem; }
</style>
</head>
<body>
  <h1>ZeroGC vs. Regular CoreCLR GC</h1>
  <p class="subtitle">Generated $genDate on this machine. Same throttled workload shape run against both GC configurations.</p>

  <div class="legend">
    <span><span class="sw" style="background:#2b7de9"></span>Regular GC (default Workstation/Server GC)</span>
    <span><span class="sw" style="background:#e94f2b"></span>ZeroGC (allocate-only, never-collect standalone GC)</span>
  </div>

  <div class="callout">
    <strong>What is ZeroGC?</strong> ZeroGC is a custom standalone CoreCLR GC (loaded via
    <code>DOTNET_GCName=ZeroGC.dll</code>) modeled after the "Epsilon"/"no-op" GC idea (and the
    author's earlier <a href="https://github.com/kkokosa/UpsilonGC">UpsilonGC</a> experiment): it
    implements the full <code>IGCHeap</code>/<code>IGCHandleManager</code> ABI, but its allocator is a
    simple bump-pointer arena that <em>never</em> collects, compacts, or reclaims memory - every
    object allocated for the lifetime of the process stays resident. It exposes the same
    <code>GC.CollectionCount</code>, <code>GC.GetTotalAllocatedBytes</code>, and
    <code>GC.GetGCMemoryInfo()</code> counters as the real GC, so apps and tools observe 0
    collections and monotonically growing memory instead of an error.
  </div>

  $consoleSection
  $webApiSection

  <div class="callout">
    <strong>How to read this:</strong> throughput (ops/sec) should be similar between the two GCs
    for a given workload, since both throttle to the same target request/operation rate - the
    interesting difference is in <em>collections</em> (ZeroGC always reports 0 across all
    generations) and <em>memory growth</em> (ZeroGC's committed/working-set bytes grow roughly
    linearly with total bytes allocated, since nothing is ever reclaimed, whereas the regular GC's
    footprint stays roughly flat thanks to periodic gen0/1/2 collections).
  </div>

  <footer>
    ZeroGC source: <code>src/ZeroGC/native</code> in this repository. Benchmark harness:
    <code>src/ZeroGC/run-benchmarks.ps1</code> / <code>generate-report.ps1</code>.
    Raw JSON: <code>results.json</code> in the same folder as this report.
  </footer>
</body>
</html>
"@

$html | Set-Content -Path $OutHtml -Encoding UTF8
Write-Host "Wrote $OutHtml" -ForegroundColor Green
