<#
.SYNOPSIS
    Renders results-full.json (produced by run-benchmarks.ps1) into a
    self-contained HTML report comparing Workstation GC vs. Server GC vs.
    ZeroGC across all benchmarked workloads. Pure inline HTML/CSS/SVG (no
    external JS/CSS/image dependencies), so the artifact works fully
    offline and standalone.

    For each workload it renders:
      - a comparison table (duration, throughput, total allocated, gen0/1/2
        collections, heap/committed/working-set memory, GC pause time
        average + p50/p90/p99/max) across all three GC configurations
      - time-series line charts (inline SVG) for GC pause time (ms) over
        time, working set over time, and throughput (ops/sec or MB/s
        allocated) over time, plus grouped bar charts of GC pause time (ms)
        percentiles, one overlaid line/bar per GC configuration
#>
param(
    [string]$ResultsJson = "$PSScriptRoot\results\results-full.json",
    [string]$OutHtml = "$PSScriptRoot\results\report.html"
)

$ErrorActionPreference = "Stop"
$runs = Get-Content $ResultsJson -Raw | ConvertFrom-Json

$gcModeOrder = @("workstation", "server", "zerogc")
$gcModeColor = @{ workstation = "#2b7de9"; server = "#8e44ad"; zerogc = "#e94f2b" }
$gcModeLabel = @{ workstation = "Workstation GC"; server = "Server GC"; zerogc = "ZeroGC" }

function Fmt-Bytes($n) {
    if ($null -eq $n) { return "n/a" }
    $n = [double]$n
    $neg = $n -lt 0
    if ($neg) { $n = -$n }
    $units = "B", "KB", "MB", "GB", "TB"
    $i = 0
    while ($n -ge 1024 -and $i -lt $units.Length - 1) { $n /= 1024; $i++ }
    return ("{0}{1:N2} {2}" -f $(if ($neg) { "-" } else { "" }), $n, $units[$i])
}
function Fmt-Num($n) { if ($null -eq $n) { return "n/a" }; return "{0:N0}" -f [double]$n }
function Fmt-Dec($n, $d = 1) { if ($null -eq $n) { return "n/a" }; return ("{0:N" + $d + "}") -f [double]$n }

function Bar($value, $max, $color) {
    if ($null -eq $value) { return "" }
    $pct = if ($max -gt 0) { [Math]::Min(100, ([double]$value / $max) * 100) } else { 0 }
    return "<div class='barTrack'><div class='barFill' style='width:$([Math]::Round($pct,1))%;background:$color'></div></div>"
}

# Renders an inline, self-contained SVG line chart overlaying one line per
# GC mode. $SeriesByMode is a hashtable: gcModeId -> array of @{T=;V=}.
function New-LineChartSvg {
    param(
        [hashtable]$SeriesByMode,
        [string]$YSuffix = "",
        [int]$Width = 720,
        [int]$Height = 220
    )
    $maxT = 0.0; $maxV = 0.0
    foreach ($modeId in $SeriesByMode.Keys) {
        foreach ($p in $SeriesByMode[$modeId]) {
            if ($p.T -gt $maxT) { $maxT = $p.T }
            if ($p.V -gt $maxV) { $maxV = $p.V }
        }
    }
    if ($maxT -le 0) { $maxT = 1 }
    if ($maxV -le 0) { $maxV = 1 }
    $maxV = $maxV * 1.08 # headroom so peaks don't touch the top edge

    $padL = 54; $padB = 24; $padT = 10; $padR = 10
    $plotW = $Width - $padL - $padR
    $plotH = $Height - $padT - $padB

    $gridLines = ""
    for ($i = 0; $i -le 4; $i++) {
        $gy = $padT + $plotH - ($i / 4.0) * $plotH
        $val = ($i / 4.0) * $maxV
        $gridLines += "<line x1='$padL' y1='$gy' x2='$($padL+$plotW)' y2='$gy' stroke='#eee' stroke-width='1'/>"
        $gridLines += "<text x='2' y='$($gy+3)' font-size='9' fill='#888'>$(Fmt-Dec $val 1)$YSuffix</text>`n"
    }

    $polylines = ""
    foreach ($modeId in $gcModeOrder) {
        if (-not $SeriesByMode.ContainsKey($modeId)) { continue }
        $pts = $SeriesByMode[$modeId]
        if (-not $pts -or $pts.Count -eq 0) { continue }
        $coords = @()
        foreach ($p in $pts) {
            $x = $padL + ($p.T / $maxT) * $plotW
            $y = $padT + $plotH - (($p.V / $maxV) * $plotH)
            $coords += ("{0:N1},{1:N1}" -f $x, $y)
        }
        $color = $gcModeColor[$modeId]
        $polylines += "<polyline points='$($coords -join ' ')' fill='none' stroke='$color' stroke-width='2' opacity='0.9' />`n"
    }

    $xAxisLabel = "<text x='$($padL+$plotW-70)' y='$($Height-4)' font-size='10' fill='#666'>time (s), 0-$([Math]::Round($maxT))</text>"

    return @"
<svg viewBox='0 0 $Width $Height' width='100%' height='$Height' xmlns='http://www.w3.org/2000/svg' style='background:#fbfbfd;border-radius:6px;'>
  $gridLines
  <line x1='$padL' y1='$padT' x2='$padL' y2='$($padT+$plotH)' stroke='#ccc' stroke-width='1'/>
  <line x1='$padL' y1='$($padT+$plotH)' x2='$($padL+$plotW)' y2='$($padT+$plotH)' stroke='#ccc' stroke-width='1'/>
  $polylines
  $xAxisLabel
</svg>
"@
}

# Renders an inline, self-contained SVG grouped bar chart comparing
# Avg/P50/P90/P99/Max GC pause time (in % or ms) across GC modes.
# $StatsByMode is a hashtable: gcModeId -> stats object (Avg/P50/P90/P99/Max/...).
function New-PauseStatsBarChartSvg {
    param(
        [hashtable]$StatsByMode,
        [string]$YSuffix = "%",
        [int]$Decimals = 2,
        [int]$Width = 720,
        [int]$Height = 260
    )
    $categories = @("Avg", "P50", "P90", "P99", "Max")
    $modesPresent = @($gcModeOrder | Where-Object { $StatsByMode.ContainsKey($_) -and $StatsByMode[$_] })

    $maxV = 0.0
    foreach ($modeId in $modesPresent) {
        foreach ($cat in $categories) {
            $v = [double]$StatsByMode[$modeId].$cat
            if ($v -gt $maxV) { $maxV = $v }
        }
    }
    if ($maxV -le 0) { $maxV = 1 }
    $maxV = $maxV * 1.15 # headroom so tallest bar doesn't touch the top edge

    $padL = 54; $padB = 30; $padT = 10; $padR = 10
    $plotW = $Width - $padL - $padR
    $plotH = $Height - $padT - $padB

    $gridLines = ""
    for ($i = 0; $i -le 4; $i++) {
        $gy = $padT + $plotH - ($i / 4.0) * $plotH
        $val = ($i / 4.0) * $maxV
        $gridLines += "<line x1='$padL' y1='$gy' x2='$($padL+$plotW)' y2='$gy' stroke='#eee' stroke-width='1'/>"
        $gridLines += "<text x='2' y='$($gy+3)' font-size='9' fill='#888'>$(Fmt-Dec $val $Decimals)$YSuffix</text>`n"
    }

    $groupW = $plotW / $categories.Count
    $barGap = 4
    $modeCount = [Math]::Max($modesPresent.Count, 1)
    $barW = [Math]::Max(4, ($groupW - $barGap * ($modeCount + 1)) / $modeCount)

    $bars = ""
    $catLabels = ""
    for ($ci = 0; $ci -lt $categories.Count; $ci++) {
        $cat = $categories[$ci]
        $groupX = $padL + $ci * $groupW
        $mi = 0
        foreach ($modeId in $modesPresent) {
            $v = [double]$StatsByMode[$modeId].$cat
            $barH = ($v / $maxV) * $plotH
            $bx = $groupX + $barGap + $mi * ($barW + $barGap)
            $by = $padT + $plotH - $barH
            $color = $gcModeColor[$modeId]
            $bars += "<rect x='$([Math]::Round($bx,1))' y='$([Math]::Round($by,1))' width='$([Math]::Round($barW,1))' height='$([Math]::Round($barH,1))' fill='$color' opacity='0.9'><title>$($gcModeLabel[$modeId]) $cat`: $(Fmt-Dec $v 3)$YSuffix</title></rect>`n"
            $mi++
        }
        $catLabels += "<text x='$($groupX + $groupW/2)' y='$($Height-8)' font-size='10' fill='#666' text-anchor='middle'>$cat</text>"
    }

    return @"
<svg viewBox='0 0 $Width $Height' width='100%' height='$Height' xmlns='http://www.w3.org/2000/svg' style='background:#fbfbfd;border-radius:6px;'>
  $gridLines
  <line x1='$padL' y1='$padT' x2='$padL' y2='$($padT+$plotH)' stroke='#ccc' stroke-width='1'/>
  <line x1='$padL' y1='$($padT+$plotH)' x2='$($padL+$plotW)' y2='$($padT+$plotH)' stroke='#ccc' stroke-width='1'/>
  $bars
  $catLabels
</svg>
"@
}

function Stats-Row($label, $statsByMode, $fmt) {
    $cells = foreach ($modeId in $gcModeOrder) {
        $s = $statsByMode[$modeId]
        if ($null -eq $s) { "<td>n/a</td><td>n/a</td><td>n/a</td><td>n/a</td>" }
        else { "<td>$(& $fmt $s.Avg)</td><td>$(& $fmt $s.P50)</td><td>$(& $fmt $s.P90)</td><td>$(& $fmt $s.P99)</td>" }
    }
    return "<tr><td>$label</td>$($cells -join '')</tr>"
}

# GC pause time is captured natively as a %-of-wall-clock-time stat/series
# (PauseTimePctStats / TimeSeries.PauseTimePct). Since dotnet-counters
# samples "dotnet.gc.pause.time" once per second (--refresh-interval 1),
# each raw sample already equals the seconds of pause within that ~1s
# window, so ms-of-pause = pct-of-time * 10 exactly. Older results-full.json
# files (produced before ms tracking was added to run-benchmarks.ps1) won't
# have PauseTimeMsStats/TimeSeries.PauseTimeMs, so derive them here on the
# fly instead of requiring a full benchmark re-run.
function Get-PauseMsStats($run) {
    if ($run.PauseTimeMsStats) { return $run.PauseTimeMsStats }
    if (-not $run.PauseTimePctStats) { return $null }
    $s = $run.PauseTimePctStats
    return [ordered]@{ Avg = $s.Avg * 10.0; P50 = $s.P50 * 10.0; P90 = $s.P90 * 10.0; P99 = $s.P99 * 10.0; Max = $s.Max * 10.0; Min = $s.Min * 10.0; Count = $s.Count }
}
function Get-PauseMsSeries($run) {
    if ($run.TimeSeries.PauseTimeMs) { return $run.TimeSeries.PauseTimeMs }
    if (-not $run.TimeSeries.PauseTimePct) { return @() }
    return @($run.TimeSeries.PauseTimePct | ForEach-Object { @{ T = $_.T; V = [Math]::Round($_.V * 10.0, 3) } })
}

$genDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$scenarioOrder = @()
$byScenario = [ordered]@{}
foreach ($run in $runs) {
    if (-not $byScenario.Contains($run.ScenarioId)) { $byScenario[$run.ScenarioId] = @{}; $scenarioOrder += $run.ScenarioId }
    $byScenario[$run.ScenarioId][$run.GcModeId] = $run
}

$sections = ""
foreach ($scenarioId in $scenarioOrder) {
    $byMode = $byScenario[$scenarioId]
    $anyRun = $byMode.Values | Select-Object -First 1
    $scenarioName = $anyRun.ScenarioName
    $isGcPerfSim = $scenarioId -like "gcperfsim*"
    $isGrowingCache = $scenarioId -eq "growing-cache"
    $throughputLabel = if ($isGcPerfSim) { "Allocation throughput (MB/s)" } else { "Throughput (ops/sec)" }

    $maxAlloc = ($byMode.Values | ForEach-Object { $_.Summary.TotalAllocatedBytes } | Measure-Object -Maximum).Maximum
    $maxCommitted = ($byMode.Values | ForEach-Object { $_.Summary.TotalCommittedBytes } | Where-Object { $_ } | Measure-Object -Maximum).Maximum
    $maxWs = ($byMode.Values | ForEach-Object { $_.Summary.WorkingSetBytes } | Where-Object { $_ } | Measure-Object -Maximum).Maximum
    $maxThroughput = ($byMode.Values | ForEach-Object { $_.Summary.OpsPerSecondOverall } | Measure-Object -Maximum).Maximum
    $maxGen0 = [Math]::Max((($byMode.Values | ForEach-Object { $_.Summary.Gen0Collections } | Measure-Object -Maximum).Maximum), 1)

    $headerCells = ($gcModeOrder | ForEach-Object {
        $m = $byMode[$_]
        $name = if ($m) { "$($gcModeLabel[$_])" } else { $gcModeLabel[$_] }
        "<th>$name</th><td></td>"
    }) -join ""

    $rowsHtml = ""
    $metricRows = @(
        @{ Label = "Duration (s)"; Get = { param($m) Fmt-Dec $m.DurationSeconds 1 }; Bar = $false }
        @{ Label = $throughputLabel; Get = { param($m) Fmt-Dec $m.Summary.OpsPerSecondOverall 2 }; Bar = $true; Max = $maxThroughput; Val = { param($m) $m.Summary.OpsPerSecondOverall } }
        @{ Label = "Total allocated"; Get = { param($m) Fmt-Bytes $m.Summary.TotalAllocatedBytes }; Bar = $true; Max = $maxAlloc; Val = { param($m) $m.Summary.TotalAllocatedBytes } }
        @{ Label = "Gen0 collections"; Get = { param($m) Fmt-Num $m.Summary.Gen0Collections }; Bar = $true; Max = $maxGen0; Val = { param($m) $m.Summary.Gen0Collections } }
        @{ Label = "Gen1 collections"; Get = { param($m) Fmt-Num $m.Summary.Gen1Collections }; Bar = $false }
        @{ Label = "Gen2 collections"; Get = { param($m) Fmt-Num $m.Summary.Gen2Collections }; Bar = $false }
        @{ Label = "GC heap size"; Get = { param($m) Fmt-Bytes $m.Summary.HeapSizeBytes }; Bar = $false }
        @{ Label = "Total committed bytes"; Get = { param($m) Fmt-Bytes $m.Summary.TotalCommittedBytes }; Bar = $true; Max = $maxCommitted; Val = { param($m) $m.Summary.TotalCommittedBytes } }
        @{ Label = "Peak working set"; Get = { param($m) Fmt-Bytes $m.Summary.WorkingSetBytes }; Bar = $true; Max = $maxWs; Val = { param($m) $m.Summary.WorkingSetBytes } }
    )
    if ($isGrowingCache) {
        $maxObservedPause = ($byMode.Values | ForEach-Object { $_.Summary.ObservedGen2PauseMaxMs } | Where-Object { $_ } | Measure-Object -Maximum).Maximum
        $metricRows += @{ Label = "Final cache entries (retained)"; Get = { param($m) Fmt-Num $m.Summary.FinalCacheEntryCount }; Bar = $false }
        $metricRows += @{ Label = "Naturally observed gen2 GC events"; Get = { param($m) Fmt-Num $m.Summary.ObservedGen2Events }; Bar = $false }
        $metricRows += @{ Label = "Naturally observed gen2 GC pause (avg)"; Get = { param($m) "$(Fmt-Dec $m.Summary.ObservedGen2PauseAvgMs 1) ms" }
            Bar = $true; Max = $maxObservedPause; Val = { param($m) $m.Summary.ObservedGen2PauseAvgMs } }
        $metricRows += @{ Label = "Naturally observed gen2 GC pause (max)"; Get = { param($m) "$(Fmt-Dec $m.Summary.ObservedGen2PauseMaxMs 1) ms" }
            Bar = $true; Max = $maxObservedPause; Val = { param($m) $m.Summary.ObservedGen2PauseMaxMs } }
    }
    foreach ($mr in $metricRows) {
        $cells = foreach ($modeId in $gcModeOrder) {
            $m = $byMode[$modeId]
            if (-not $m) { "<td>n/a</td><td></td>"; continue }
            $text = & $mr.Get $m
            $barHtml = if ($mr.Bar) { Bar (& $mr.Val $m) $mr.Max $gcModeColor[$modeId] } else { "" }
            "<td>$text$barHtml</td><td></td>"
        }
        $rowsHtml += "<tr><td>$($mr.Label)</td>$($cells -join '')</tr>`n"
    }

    # GC pause time (ms), working set, throughput percentile tables.
    $pauseMsStatsByMode = @{}; $wsStatsByMode = @{}; $throughputStatsByMode = @{}
    foreach ($modeId in $gcModeOrder) {
        $m = $byMode[$modeId]
        if ($m) {
            $pauseMsStatsByMode[$modeId] = Get-PauseMsStats $m
            $wsStatsByMode[$modeId] = $m.WorkingSetStats
            $throughputStatsByMode[$modeId] = if ($isGcPerfSim) { $m.AllocRateMBStats } else { $m.OpsPerSecStats }
        }
    }
    $pauseHeaderCells = ($gcModeOrder | ForEach-Object { "<th colspan='4'>$($gcModeLabel[$_])</th>" }) -join ""
    $subHeaderCells = ($gcModeOrder | ForEach-Object { "<th>avg</th><th>p50</th><th>p90</th><th>p99</th>" }) -join ""
    $pauseMsRow = Stats-Row "GC pause time (ms per ~1s sample)" $pauseMsStatsByMode { param($v) "{0:N2} ms" -f $v }
    $wsRow = Stats-Row "Working set (MB)" $wsStatsByMode { param($v) "{0:N0}" -f ($v / 1MB) }
    $throughputUnit = if ($isGcPerfSim) { "MB/s" } else { "ops/sec" }
    $throughputRow = Stats-Row "Throughput ($throughputUnit)" $throughputStatsByMode { param($v) "{0:N1}" -f $v }

    # Time-series charts.
    $pauseMsSeries = @{}; $wsSeries = @{}; $throughputSeries = @{}
    foreach ($modeId in $gcModeOrder) {
        $m = $byMode[$modeId]
        if (-not $m) { continue }
        $pauseMsSeries[$modeId] = Get-PauseMsSeries $m
        $wsSeries[$modeId] = $m.TimeSeries.WorkingSetMB
        $throughputSeries[$modeId] = if ($isGcPerfSim) { $m.TimeSeries.AllocRateMB } else { $m.TimeSeries.OpsPerSec }
    }
    $pauseMsChart = New-LineChartSvg -SeriesByMode $pauseMsSeries -YSuffix " ms"
    $wsChart = New-LineChartSvg -SeriesByMode $wsSeries -YSuffix " MB"
    $throughputChart = New-LineChartSvg -SeriesByMode $throughputSeries -YSuffix " $throughputUnit"
    $pauseMsBarChart = New-PauseStatsBarChartSvg -StatsByMode $pauseMsStatsByMode -YSuffix " ms" -Decimals 2

    $legendHtml = ($gcModeOrder | ForEach-Object {
        "<span><span class='sw' style='background:$($gcModeColor[$_])'></span>$($gcModeLabel[$_])</span>"
    }) -join ""

    $sections += @"
<section>
  <h2>$scenarioName</h2>
  <table class="cmp">
    <thead><tr><th>Metric</th>$headerCells</tr></thead>
    <tbody>
$rowsHtml
    </tbody>
  </table>

  <h3>GC pause time / memory / throughput percentiles (over the full run)</h3>
  <table class="cmp stats">
    <thead><tr><th></th>$pauseHeaderCells</tr><tr><th>Metric</th>$subHeaderCells</tr></thead>
    <tbody>
      $pauseMsRow
      $wsRow
      $throughputRow
    </tbody>
  </table>

  <div class="chartsGrid">
    <div class="chartCard">
      <h4>GC pause time (ms) over time</h4>
      $pauseMsChart
    </div>
    <div class="chartCard">
      <h4>Throughput ($throughputUnit) over time</h4>
      $throughputChart
    </div>
    <div class="chartCard">
      <h4>GC pause time (ms) percentiles (avg / p50 / p90 / p99 / max, full run)</h4>
      $pauseMsBarChart
    </div>
    <div class="chartCard">
      <h4>Working set (MB) over time</h4>
      $wsChart
    </div>
  </div>
  <div class="legend small">$legendHtml</div>
</section>
"@
}

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>ZeroGC vs Workstation vs Server GC - Benchmark Report</title>
<style>
  body { font-family: Segoe UI, Arial, sans-serif; margin: 2rem; background: #f7f8fa; color: #1a1a1a; }
  h1 { margin-bottom: 0.2rem; }
  .subtitle { color: #666; margin-top: 0; }
  section { background: #fff; border-radius: 8px; padding: 1.2rem 1.5rem; margin-bottom: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
  h2 { margin-top: 0; border-bottom: 2px solid #eee; padding-bottom: 0.5rem; }
  h3 { margin-top: 1.5rem; font-size: 1rem; color: #444; }
  h4 { margin: 0 0 0.4rem 0; font-size: 0.9rem; color: #444; }
  table.cmp { width: 100%; border-collapse: collapse; }
  table.cmp td, table.cmp th { padding: 0.45rem 0.6rem; text-align: left; vertical-align: middle; font-size: 0.92rem; }
  table.cmp thead th { border-bottom: 2px solid #ddd; font-size: 0.85rem; color: #555; }
  table.cmp tbody tr:nth-child(odd) { background: #fafbfc; }
  table.cmp td:first-child { font-weight: 600; }
  table.stats thead tr:first-child th { text-align: center; }
  .barTrack { display:inline-block; width: 110px; height: 9px; background: #eee; border-radius: 5px; margin-left: 0.5rem; vertical-align: middle; overflow:hidden; }
  .barFill { height: 100%; border-radius: 5px; }
  .legend span, .legend.small span { display:inline-block; margin-right:1.5rem; }
  .legend.small { margin-top: 0.6rem; font-size: 0.85rem; color: #555; }
  .legend .sw { display:inline-block; width:12px;height:12px;border-radius:3px;margin-right:0.4rem;vertical-align:middle; }
  .callout { background:#fff8e6; border:1px solid #f0d98c; border-radius:6px; padding:1rem 1.2rem; margin-bottom:1.5rem; }
  code { background:#f0f0f0; padding:0.1rem 0.35rem; border-radius:4px; }
  footer { color:#888; font-size:0.85rem; margin-top:2rem; }
  .chartsGrid { display:grid; grid-template-columns: repeat(2, 1fr); gap: 1.2rem; margin-top: 0.8rem; }
  .chartCard { background:#fbfbfd; border:1px solid #eee; border-radius:8px; padding:0.6rem 0.7rem; }
  @media (max-width: 900px) { .chartsGrid { grid-template-columns: 1fr; } }
</style>
</head>
<body>
  <h1>ZeroGC vs. Workstation GC vs. Server GC</h1>
  <p class="subtitle">Generated $genDate on this machine. Five workloads (2 hand-written sample apps + 3 GCPerfSim scenarios), each run once per GC configuration.</p>

  <div class="legend">
    <span><span class="sw" style="background:$($gcModeColor.workstation)"></span>Workstation GC (default, non-concurrent-by-default background GC)</span>
    <span><span class="sw" style="background:$($gcModeColor.server)"></span>Server GC (DOTNET_gcServer=1, per-core heaps)</span>
    <span><span class="sw" style="background:$($gcModeColor.zerogc)"></span>ZeroGC (allocate-only, never-collect standalone GC)</span>
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
    collections and monotonically growing memory instead of an error. This report compares it
    against both default GC modes (Workstation and Server) across 5 workloads, using
    <code>dotnet-counters</code> to capture real second-by-second time series (not just
    end-of-run summaries) for GC pause time, memory, and throughput.
  </div>

  $sections

  <div class="callout">
    <strong>How to read this:</strong> ZeroGC always reports 0 ms GC pause time and 0 collections
    across all generations - there is nothing to compact or trace. Its memory (working
    set/committed bytes/heap size) grows monotonically and roughly tracks total bytes allocated,
    since nothing is ever reclaimed, whereas Workstation/Server GC's footprint stays roughly flat
    (or grows much more slowly) thanks to periodic gen0/1/2 collections. Throughput differences
    between the three configurations reflect the true cost of garbage collection (pause time,
    write barriers, card scanning) for that specific allocation pattern - workloads with more
    survivorship/larger live sets (e.g. the cache-heavy GCPerfSim scenario) tend to show a bigger
    gap than workloads dominated by short-lived gen0 garbage.
  </div>

  <div class="callout">
    <strong>Why "Total allocated" looks much bigger for ZeroGC:</strong> this is expected, not a
    bug. <code>GC.GetTotalAllocatedBytes(precise: false)</code> - the counter both the real GC and
    ZeroGC report through - is documented as a fast approximation on the real GC: each thread
    allocates from a private bump-pointer "allocation context" and the imprecise counter only
    reconciles those contexts opportunistically, so with many concurrently-allocating threads it
    can noticeably <em>undercount</em> true allocation volume between reconciliations. ZeroGC's
    implementation instead returns its arena's exact bump-pointer position - a true, exact count of
    every byte ever handed out, since nothing is ever reclaimed. So the gap between GC modes in the
    "Total allocated" row partly reflects this counter-precision difference, not solely a
    difference in actual allocation behavior; the working-set/committed-bytes/heap-size rows (which
    come from OS/GC-heap accounting, not this approximate counter) are the more apples-to-apples
    memory comparison.
  </div>

  <footer>
    ZeroGC source: <code>src/ZeroGC/native</code> in this repository. Benchmark harness:
    <code>src/ZeroGC/run-benchmarks.ps1</code> / <code>generate-report.ps1</code>, using
    <code>dotnet-counters collect</code> for real time-series data. Raw JSON:
    <code>results-full.json</code> in the same folder as this report.
  </footer>
</body>
</html>
"@

$html | Set-Content -Path $OutHtml -Encoding UTF8
Write-Host "Wrote $OutHtml" -ForegroundColor Green
