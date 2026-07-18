<#
.SYNOPSIS
    Runs the ZeroGC benchmark suite across THREE GC configurations
    (Workstation GC, Server GC, ZeroGC) and five workloads: the two
    hand-rolled sample apps (ConsoleApp, WebApi) plus three GCPerfSim
    scenarios modeling more realistic allocation patterns (web-server
    request processing, a cache/large-object-heavy workload, and
    high-throughput transient object churn).

    For every (workload, GC mode) pair, the target process is launched with
    the right environment variables and `dotnet-counters collect` attaches
    to it for the whole run, capturing a real second-by-second time series
    (GC pause time, working set, committed bytes, heap size, collection
    counts, allocation rate, and - for ConsoleApp/WebApi - a custom
    "operations" throughput counter) via the standard "System.Runtime"
    EventCounters/Meters that ship in every .NET process. This works
    identically for ZeroGC because its IGCHeap counters
    (GC.CollectionCount/GetTotalAllocatedBytes/GetGCMemoryInfo) feed the same
    counters as the built-in GC.

    Writes results\results-full.json (summary + percentiles + downsampled
    time series per run) and then renders results\report.html.

.EXAMPLE
    .\run-benchmarks.ps1 -DurationSeconds 600
#>
param(
    [int]$DurationSeconds = 600,
    [string]$OutDir = "$PSScriptRoot\results",
    [string[]]$Scenarios = @("console", "webapi", "gcperfsim-webserver", "gcperfsim-cache", "gcperfsim-churn"),
    [string[]]$GcModes = @("workstation", "server", "zerogc"),
    [int]$CounterRefreshIntervalSeconds = 1
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$gcDll = Join-Path $root "native\obj\Release\ZeroGC.dll"
if (-not (Test-Path $gcDll)) { throw "ZeroGC.dll not built. Run native\build.ps1 first." }

$dotnetCounters = Get-Command dotnet-counters -ErrorAction SilentlyContinue
if (-not $dotnetCounters) {
    $candidate = Join-Path $env:USERPROFILE ".dotnet\tools\dotnet-counters.exe"
    if (Test-Path $candidate) { $dotnetCounters = $candidate } else { throw "dotnet-counters not found. Install via: dotnet tool install --global dotnet-counters" }
}
else { $dotnetCounters = $dotnetCounters.Source }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$rawDir = Join-Path $OutDir "raw"
New-Item -ItemType Directory -Force -Path $rawDir | Out-Null

# --- Workload definitions -----------------------------------------------
# GCPerfSim scenarios are bounded by total allocation (-tagb), not wall
# time: this is the key safety mechanism that lets us run for many minutes
# without risking machine memory, since ZeroGC never reclaims anything.
# Each scenario's -c (compute-between-allocations) value dominates wall
# time far more than GC pause overhead does, so all three GC modes finish
# in roughly comparable wall time even though ZeroGC has zero pause time.
# BaseTagbFor600s below was calibrated so the run takes ~600s under the
# regular Workstation GC; it's scaled linearly by -DurationSeconds.
function New-Scenario($Id, $DisplayName, $Kind, $CmdArgs = $null, $BaseTagbFor600s = 0) {
    [pscustomobject]@{ Id = $Id; DisplayName = $DisplayName; Kind = $Kind; Args = $CmdArgs; BaseTagbFor600s = $BaseTagbFor600s }
}

$allScenarios = @(
    (New-Scenario "console" "Console: mixed alloc-heavy CLI workload" "console"),
    (New-Scenario "webapi" "ASP.NET Core (Kestrel) minimal API" "webapi"),
    (New-Scenario "gcperfsim-webserver" "GCPerfSim: web-server request simulation" "gcperfsim" `
        "-tc 4 -tlgb 0.5 -sohsr 200-3000 -sohsi 15 -at simple -c 300000" 7.6),
    (New-Scenario "gcperfsim-cache" "GCPerfSim: cache / large-object-heavy workload" "gcperfsim" `
        "-tc 2 -tlgb 0.3 -lohar 100 -sohsr 500-4000 -sohsi 8 -lohsr 100000-300000 -lohsi 4 -at simple -c 300000" 5.3),
    (New-Scenario "gcperfsim-churn" "GCPerfSim: high-throughput transient object churn" "gcperfsim" `
        "-tc 8 -tlgb 0.1 -lohar 0 -sohsr 200-600 -sohsi 200 -at simple -c 300000" 3.4)
)

$gcModeDefs = @(
    [pscustomobject]@{ Id = "workstation"; DisplayName = "Workstation GC"; Env = @{ DOTNET_gcServer = "0" }; RemoveEnv = @("DOTNET_GCName") },
    [pscustomobject]@{ Id = "server";      DisplayName = "Server GC";      Env = @{ DOTNET_gcServer = "1" }; RemoveEnv = @("DOTNET_GCName") },
    [pscustomobject]@{ Id = "zerogc";      DisplayName = "ZeroGC";         Env = @{ DOTNET_GCName = "ZeroGC.dll" }; RemoveEnv = @("DOTNET_gcServer") }
)

$scenarioMap = @{}
foreach ($s in $allScenarios) { $scenarioMap[$s.Id] = $s }
$gcModeMap = @{}
foreach ($g in $gcModeDefs) { $gcModeMap[$g.Id] = $g }

# --- Helpers --------------------------------------------------------------

function Get-Percentile([double[]]$Values, [double]$Percentile) {
    if (-not $Values -or $Values.Count -eq 0) { return 0 }
    $sorted = $Values | Sort-Object
    $idx = [Math]::Ceiling($Percentile / 100.0 * $sorted.Count) - 1
    if ($idx -lt 0) { $idx = 0 }
    if ($idx -ge $sorted.Count) { $idx = $sorted.Count - 1 }
    return [double]$sorted[$idx]
}

function Get-Stats([double[]]$Values) {
    if (-not $Values -or $Values.Count -eq 0) {
        return [ordered]@{ Avg = 0; P50 = 0; P90 = 0; P99 = 0; Max = 0; Min = 0; Count = 0 }
    }
    $measure = $Values | Measure-Object -Average -Maximum -Minimum
    return [ordered]@{
        Avg   = $measure.Average
        P50   = Get-Percentile $Values 50
        P90   = Get-Percentile $Values 90
        P99   = Get-Percentile $Values 99
        Max   = $measure.Maximum
        Min   = $measure.Minimum
        Count = $Values.Count
    }
}

# Downsample a (t,v) series to at most $MaxPoints points (evenly spaced) so
# the final HTML report stays a reasonable size even for 600s/1Hz series.
function Get-Downsampled($Series, [int]$MaxPoints = 150) {
    if ($Series.Count -le $MaxPoints) { return $Series }
    $step = [Math]::Ceiling($Series.Count / [double]$MaxPoints)
    $out = @()
    for ($i = 0; $i -lt $Series.Count; $i += $step) { $out += , $Series[$i] }
    return $out
}

# Parses a dotnet-counters CSV export into a hashtable of
# metricKey -> @(@{T=<seconds-since-start>; V=<double>}, ...), sorted by time.
function Parse-CountersCsv([string]$CsvPath) {
    $metricMap = [ordered]@{
        "working_set"     = "dotnet.process.memory.working_set*"
        "pause_time"      = "dotnet.gc.pause.time*"
        "gen0_collections"= "*gc.collections*gen0*"
        "gen1_collections"= "*gc.collections*gen1*"
        "gen2_collections"= "*gc.collections*gen2*"
        "alloc_rate"      = "dotnet.gc.heap.total_allocated*"
        "committed_bytes" = "dotnet.gc.last_collection.memory.committed_size*"
        "operations"      = "operations*"
    }

    $rows = Import-Csv $CsvPath
    if (-not $rows -or $rows.Count -eq 0) { return @{} }

    $parsedRows = foreach ($r in $rows) {
        $ts = [datetime]::Parse($r.Timestamp, [System.Globalization.CultureInfo]::InvariantCulture)
        $val = 0.0
        [double]::TryParse($r.'Mean/Increment', [ref]$val) | Out-Null
        [pscustomobject]@{ Timestamp = $ts; Name = $r.'Counter Name'; Value = $val }
    }
    $t0 = ($parsedRows | Measure-Object -Property Timestamp -Minimum).Minimum

    $result = @{}
    foreach ($key in $metricMap.Keys) {
        $pattern = $metricMap[$key]
        $series = $parsedRows | Where-Object { $_.Name -like $pattern } | Sort-Object Timestamp |
            ForEach-Object { @{ T = [Math]::Round(($_.Timestamp - $t0).TotalSeconds, 1); V = $_.Value } }
        $result[$key] = @($series)
    }
    return $result
}

# Launches a workload process with per-child environment variables (NOT the
# parent shell's env, so dotnet-counters - itself a .NET app - never
# inherits e.g. DOTNET_GCName and fails to start), attaches dotnet-counters
# for the whole run duration, and returns stdout + parsed counter series.
function Invoke-MonitoredRun {
    param(
        [string]$ExePath,
        [string]$WorkingDirectory,
        [string]$Arguments,
        [hashtable]$ExtraEnv,
        [string[]]$RemoveEnvKeys,
        [string]$CsvBasePath,
        [int]$AttachDelayMs = 1500
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ExePath
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.Arguments = $Arguments
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    foreach ($k in $RemoveEnvKeys) { $psi.EnvironmentVariables.Remove($k) | Out-Null }
    foreach ($k in $ExtraEnv.Keys) { $psi.EnvironmentVariables[$k] = $ExtraEnv[$k] }

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
    $stderrTask = $proc.StandardError.ReadToEndAsync()
    Start-Sleep -Milliseconds $AttachDelayMs

    $dcArgs = @("collect", "-p", $proc.Id, "--refresh-interval", $CounterRefreshIntervalSeconds,
        "--format", "csv", "--counters", "System.Runtime,ZeroGC.Bench", "-o", $CsvBasePath)
    & $dotnetCounters @dcArgs *> (Join-Path $rawDir "dotnet-counters.log") 2>&1

    $proc.WaitForExit(60000) | Out-Null
    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result

    $csvPath = "$CsvBasePath.csv"
    $series = if (Test-Path $csvPath) { Parse-CountersCsv $csvPath } else { @{} }

    return [pscustomobject]@{
        StdOut = $stdout
        StdErr = $stderr
        Series = $series
        ExitCode = $proc.ExitCode
    }
}

function Get-ResultLineJson([string]$StdOut) {
    $line = ($StdOut -split "`n") | Where-Object { $_ -like "##RESULT##*" } | Select-Object -Last 1
    if (-not $line) { return $null }
    return ($line.Trim().Substring("##RESULT##".Length) | ConvertFrom-Json)
}

function Get-GcPerfSimStats([string]$StdOut) {
    function Num($name) {
        $m = [regex]::Match($StdOut, "$name`:\s*([0-9.]+)")
        if ($m.Success) { return [double]$m.Groups[1].Value }
        return 0
    }
    $collMatch = [regex]::Match($StdOut, "collection_counts:\s*\[([0-9,\s]+)\]")
    $gen0 = 0; $gen1 = 0; $gen2 = 0
    if ($collMatch.Success) {
        $parts = $collMatch.Groups[1].Value -split "," | ForEach-Object { $_.Trim() }
        if ($parts.Count -ge 3) { $gen0 = [int]$parts[0]; $gen1 = [int]$parts[1]; $gen2 = [int]$parts[2] }
    }
    return [pscustomobject]@{
        SohAllocatedBytes    = Num "sohAllocatedBytes"
        LohAllocatedBytes    = Num "lohAllocatedBytes"
        PohAllocatedBytes    = Num "pohAllocatedBytes"
        SecondsTaken         = Num "seconds_taken"
        Gen0Collections      = $gen0
        Gen1Collections      = $gen1
        Gen2Collections      = $gen2
        FinalTotalMemoryBytes = Num "final_total_memory_bytes"
        FinalHeapSizeBytes   = Num "final_heap_size_bytes"
        FinalFragmentationBytes = Num "final_fragmentation_bytes"
    }
}

# --- Main run loop ---------------------------------------------------------

$allRuns = @()
$runIndex = 0
$totalRuns = $Scenarios.Count * $GcModes.Count

foreach ($scenarioId in $Scenarios) {
    $scenario = $scenarioMap[$scenarioId]
    if (-not $scenario) { throw "Unknown scenario '$scenarioId'" }

    foreach ($gcModeId in $GcModes) {
        $gcMode = $gcModeMap[$gcModeId]
        if (-not $gcMode) { throw "Unknown GC mode '$gcModeId'" }

        $runIndex++
        $label = "$($scenario.Id)_$($gcMode.Id)"
        Write-Host "`n=== [$runIndex/$totalRuns] $($scenario.DisplayName) / $($gcMode.DisplayName) ===" -ForegroundColor Cyan

        $csvBase = Join-Path $rawDir $label
        $runStart = Get-Date

        switch ($scenario.Kind) {
            "console" {
                $publishDir = Join-Path $root "samples\ConsoleApp\publish"
                $run = Invoke-MonitoredRun -ExePath (Join-Path $publishDir "ConsoleApp.exe") -WorkingDirectory $publishDir `
                    -Arguments "$DurationSeconds $label" -ExtraEnv $gcMode.Env -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase
                $resultJson = Get-ResultLineJson $run.StdOut
                if (-not $resultJson) { Write-Host $run.StdOut; Write-Host $run.StdErr; throw "No ##RESULT## for $label" }
                $summary = [ordered]@{
                    OperationsTotal      = $resultJson.Operations
                    OpsPerSecondOverall  = $resultJson.OpsPerSecond
                    TotalAllocatedBytes  = $resultJson.TotalAllocatedBytes
                    Gen0Collections      = $resultJson.Gen0Collections
                    Gen1Collections      = $resultJson.Gen1Collections
                    Gen2Collections      = $resultJson.Gen2Collections
                    WorkingSetBytes      = $resultJson.WorkingSetBytes
                    PeakWorkingSetBytes  = $resultJson.PeakWorkingSetBytes
                    HeapSizeBytes        = $resultJson.HeapSizeBytes
                    TotalCommittedBytes  = $resultJson.TotalCommittedBytes
                    GcName               = $resultJson.GcName
                }
                $durationActual = $resultJson.DurationSeconds
            }
            "webapi" {
                $publishDir = Join-Path $root "samples\WebApi\publish"
                $env2 = @{} + $gcMode.Env
                $env2["ZEROGC_BENCH_DURATION_SECONDS"] = "$DurationSeconds"
                $env2["ZEROGC_BENCH_LABEL"] = $label
                $run = Invoke-MonitoredRun -ExePath (Join-Path $publishDir "WebApi.exe") -WorkingDirectory $publishDir `
                    -Arguments "" -ExtraEnv $env2 -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase -AttachDelayMs 3000
                $resultJson = Get-ResultLineJson $run.StdOut
                if (-not $resultJson) { Write-Host $run.StdOut; Write-Host $run.StdErr; throw "No ##RESULT## for $label" }
                $summary = [ordered]@{
                    OperationsTotal      = $resultJson.Operations
                    OpsPerSecondOverall  = $resultJson.OpsPerSecond
                    TotalAllocatedBytes  = $resultJson.TotalAllocatedBytes
                    Gen0Collections      = $resultJson.Gen0Collections
                    Gen1Collections      = $resultJson.Gen1Collections
                    Gen2Collections      = $resultJson.Gen2Collections
                    WorkingSetBytes      = $resultJson.WorkingSetBytes
                    PeakWorkingSetBytes  = $resultJson.PeakWorkingSetBytes
                    HeapSizeBytes        = $resultJson.HeapSizeBytes
                    TotalCommittedBytes  = $resultJson.TotalCommittedBytes
                    GcName               = $resultJson.GcName
                    Errors               = $resultJson.Errors
                }
                $durationActual = $resultJson.DurationSeconds
            }
            "gcperfsim" {
                $publishDir = Join-Path $root "samples\GCPerfSim\publish"
                $tagb = [Math]::Round($scenario.BaseTagbFor600s * ($DurationSeconds / 600.0), 3)
                $fullArgs = "$($scenario.Args) -tagb $tagb"
                $run = Invoke-MonitoredRun -ExePath (Join-Path $publishDir "GCPerfSim.exe") -WorkingDirectory $publishDir `
                    -Arguments $fullArgs -ExtraEnv $gcMode.Env -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase
                $stats = Get-GcPerfSimStats $run.StdOut
                $totalAlloc = $stats.SohAllocatedBytes + $stats.LohAllocatedBytes + $stats.PohAllocatedBytes
                $summary = [ordered]@{
                    OperationsTotal      = $null
                    OpsPerSecondOverall  = $(if ($stats.SecondsTaken -gt 0) { $totalAlloc / $stats.SecondsTaken / 1MB } else { 0 }) # MB/s alloc throughput proxy
                    TotalAllocatedBytes  = $totalAlloc
                    Gen0Collections      = $stats.Gen0Collections
                    Gen1Collections      = $stats.Gen1Collections
                    Gen2Collections      = $stats.Gen2Collections
                    WorkingSetBytes      = $null
                    PeakWorkingSetBytes  = $null
                    HeapSizeBytes        = $stats.FinalHeapSizeBytes
                    TotalCommittedBytes  = $null
                    GcName               = $gcMode.DisplayName
                    TagbUsed             = $tagb
                }
                $durationActual = $stats.SecondsTaken
                if ($run.Series.Count -eq 0) { Write-Host $run.StdOut; Write-Host $run.StdErr; throw "No counters captured for $label" }
            }
        }

        # Fill in working-set/committed-bytes summary from counters if the
        # scenario didn't already report it (GCPerfSim has no such field).
        if ($null -eq $summary.WorkingSetBytes -and $run.Series["working_set"]) {
            $wsVals = $run.Series["working_set"] | ForEach-Object { $_.V }
            if ($wsVals.Count -gt 0) { $summary.WorkingSetBytes = ($wsVals | Measure-Object -Maximum).Maximum; $summary.PeakWorkingSetBytes = $summary.WorkingSetBytes }
        }
        if ($null -eq $summary.TotalCommittedBytes -and $run.Series["committed_bytes"]) {
            $cbVals = $run.Series["committed_bytes"] | ForEach-Object { $_.V }
            if ($cbVals.Count -gt 0) { $summary.TotalCommittedBytes = ($cbVals | Measure-Object -Maximum).Maximum }
        }

        # Percentile/avg stats for the interesting time-series metrics.
        $pauseVals = @($run.Series["pause_time"] | ForEach-Object { $_.V * 100.0 }) # seconds/sec -> %
        $wsValsAll = @($run.Series["working_set"] | ForEach-Object { $_.V })
        $allocRateVals = @($run.Series["alloc_rate"] | ForEach-Object { $_.V / 1MB }) # MB/s
        $opsVals = @($run.Series["operations"] | ForEach-Object { $_.V })

        $elapsedWall = (Get-Date) - $runStart
        Write-Host ("  done in {0:N0}s wall (bench duration {1:N1}s), gen0/1/2={2}/{3}/{4}" -f `
            $elapsedWall.TotalSeconds, $durationActual, $summary.Gen0Collections, $summary.Gen1Collections, $summary.Gen2Collections)

        $allRuns += [pscustomobject]@{
            ScenarioId      = $scenario.Id
            ScenarioName    = $scenario.DisplayName
            GcModeId        = $gcMode.Id
            GcModeName      = $gcMode.DisplayName
            DurationSeconds = $durationActual
            Summary         = $summary
            PauseTimePctStats = (Get-Stats $pauseVals)
            WorkingSetStats   = (Get-Stats $wsValsAll)
            AllocRateMBStats  = (Get-Stats $allocRateVals)
            OpsPerSecStats    = (Get-Stats $opsVals)
            TimeSeries = [ordered]@{
                PauseTimePct = Get-Downsampled (@($run.Series["pause_time"] | ForEach-Object { @{ T = $_.T; V = [Math]::Round($_.V * 100.0, 4) } }))
                WorkingSetMB = Get-Downsampled (@($run.Series["working_set"] | ForEach-Object { @{ T = $_.T; V = [Math]::Round($_.V / 1MB, 2) } }))
                AllocRateMB  = Get-Downsampled (@($run.Series["alloc_rate"] | ForEach-Object { @{ T = $_.T; V = [Math]::Round($_.V / 1MB, 2) } }))
                OpsPerSec    = Get-Downsampled (@($run.Series["operations"] | ForEach-Object { @{ T = $_.T; V = [Math]::Round($_.V, 1) } }))
                Gen0PerSec   = Get-Downsampled (@($run.Series["gen0_collections"] | ForEach-Object { @{ T = $_.T; V = $_.V } }))
            }
        }

        # Save incrementally so a crash partway through doesn't lose earlier runs.
        $allRuns | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $OutDir "results-full.json") -Encoding UTF8
    }
}

Write-Host "`nAll $totalRuns runs complete." -ForegroundColor Green
Write-Host "Wrote $(Join-Path $OutDir 'results-full.json')" -ForegroundColor Green

& "$root\generate-report.ps1" -ResultsJson (Join-Path $OutDir "results-full.json") -OutHtml (Join-Path $OutDir "report.html")
