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
    [string[]]$Scenarios = @("console", "webapi", "gcperfsim-webserver", "gcperfsim-cache", "gcperfsim-churn", "zeroalloc", "dotllm-serve", "dotllm-serve-1_5b", "growing-cache", "gcperfsim-mt-throughput"),
    [string[]]$GcModes = @("workstation", "server", "zerogc"),
    [int]$CounterRefreshIntervalSeconds = 1,
    [string]$DotLlmModelFile = "$env:USERPROFILE\.dotllm\models\QuantFactory\SmolLM-135M-GGUF\SmolLM-135M.Q4_K_M.gguf",
    [int]$DotLlmPort = 8099,
    [string]$DotLlmLargeModelFile = "$env:USERPROFILE\.dotllm\models\Qwen\Qwen2.5-1.5B-Instruct-GGUF\qwen2.5-1.5b-instruct-q4_k_m.gguf",
    [int]$DotLlmLargePort = 8100
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

# dotllm (https://github.com/kkokosa/dotLLM) is only required for the
# "dotllm-serve"/"dotllm-serve-1_5b" scenarios - real-world, (near-)zero-
# allocation LLM inference servers at two different model sizes/context
# lengths. Resolved lazily so the rest of the suite still runs on machines
# that don't have it installed.
$dotLlmExe = $null
if ($Scenarios -contains "dotllm-serve" -or $Scenarios -contains "dotllm-serve-1_5b") {
    $dotLlmCmd = Get-Command dotllm -ErrorAction SilentlyContinue
    if ($dotLlmCmd) { $dotLlmExe = $dotLlmCmd.Source }
    else {
        $candidate = Join-Path $env:USERPROFILE ".dotnet\tools\dotllm.exe"
        if (Test-Path $candidate) { $dotLlmExe = $candidate }
    }
    if (-not $dotLlmExe) {
        throw "dotllm not found but a dotllm-serve* scenario was requested. Install via: dotnet tool install -g DotLLM.Cli --prerelease"
    }
    if ($Scenarios -contains "dotllm-serve" -and -not (Test-Path $DotLlmModelFile)) {
        throw "dotLLM model file not found at '$DotLlmModelFile'. Pull it first: dotllm model pull QuantFactory/SmolLM-135M-GGUF --file SmolLM-135M.Q4_K_M.gguf"
    }
    if ($Scenarios -contains "dotllm-serve-1_5b" -and -not (Test-Path $DotLlmLargeModelFile)) {
        throw "dotLLM 1.5B model file not found at '$DotLlmLargeModelFile'. Pull it first: dotllm model pull Qwen/Qwen2.5-1.5B-Instruct-GGUF --file qwen2.5-1.5b-instruct-q4_k_m.gguf"
    }
    # For GC-plugin loading (DOTNET_GCName=ZeroGC.dll), CoreCLR resolves the
    # plugin relative to the app's own directory - not the apphost.exe's
    # folder in ~/.dotnet/tools, but the actual managed assembly's directory
    # under the global tool's .store layout. Copy ZeroGC.dll there so the
    # zerogc GC mode can load for this scenario too.
    $dotLlmAppDll = Get-ChildItem (Join-Path $env:USERPROFILE ".dotnet\tools\.store\dotllm.cli") -Recurse -Filter "DotLLM.Cli.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($dotLlmAppDll) {
        Copy-Item $gcDll -Destination $dotLlmAppDll.DirectoryName -Force
    }
}

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
        "-tc 8 -tlgb 0.1 -lohar 0 -sohsr 200-600 -sohsi 200 -at simple -c 300000" 3.4),
    # --- Zero-alloc scenarios: the point isn't to stress the GC, it's the
    # opposite - when an app allocates almost nothing, the choice of GC
    # (including never collecting at all) shouldn't matter. These two
    # scenarios demonstrate that Workstation/Server/ZeroGC all perform
    # near-identically when there's nothing to collect.
    (New-Scenario "zeroalloc" "Console: near-zero-allocation numeric compute (matrix multiply)" "zeroalloc"),
    (New-Scenario "dotllm-serve" "dotLLM: zero-alloc-inference HTTP server (github.com/kkokosa/dotLLM)" "dotllm-serve"),
    # A second, heavier dotLLM scenario: a real ~1GB quantized 1.5B-parameter
    # model (Qwen2.5-1.5B-Instruct, Q4_K_M) fed a few-hundred-token prompt
    # per request instead of a handful of words. Bigger weights (more model
    # state resident during inference) and a much longer prefill exercise a
    # meaningfully different allocation/compute profile than the tiny
    # 135M/short-prompt scenario above, while still being a genuinely
    # near-zero-managed-alloc inference workload (same NativeMemory-backed
    # tensor path).
    (New-Scenario "dotllm-serve-1_5b" "dotLLM: 1.5B model (~1GB, Q4_K_M), few-hundred-token context, zero-alloc-inference HTTP server" "dotllm-serve-large"),
    # A cache/session-store-style service that keeps accumulating long-lived
    # entries over its lifetime while handling ordinary "requests" that
    # allocate short-lived garbage - a very common real-world pattern (an
    # ever-growing in-memory cache/session store). Every gen2 collection
    # observed here is 100% naturally triggered by the CLR's own
    # allocation-budget/promotion heuristics; this program never calls
    # GC.Collect(). As the cache grows, gen2's live-object count grows with
    # it, so pauses under Workstation/Server GC naturally get longer and
    # longer over the run (ZeroGC, which never collects, stays at 0ms).
    (New-Scenario "growing-cache" "Console: growing in-memory cache with real request workload (naturally escalating gen2 GCs)" "growingcache" "4000 512"),
    # A deliberately GC-bound "raw concurrent allocation throughput" burst:
    # 16 threads (matched to this machine's 16 logical cores) allocating as
    # fast as possible with NO compute delay between allocations (`-c 0`),
    # unlike the other GCPerfSim scenarios above which all use `-c 300000`
    # and are therefore compute-bound (allocation is a small fraction of
    # wall time, which is why Workstation and Server GC show no measurable
    # throughput gap in those scenarios). With `-c 0`, allocation/collection
    # cost dominates wall time, exposing Server GC's per-core heaps/parallel
    # GC threads: it clears ~30GB roughly 3-4x faster than Workstation GC's
    # single heap. This scenario intentionally runs as a short, sub-30-second
    # burst (scaled by BaseTagbFor600s, same as the other GCPerfSim
    # scenarios) rather than a full ~600s run, so it stays memory-safe for
    # ZeroGC (which retains 100% of everything ever allocated) while still
    # producing hundreds of gen0 and dozens of gen1/gen2 collections to
    # measure. It also reveals a second, independent finding: ZeroGC's
    # simple mutex/interlocked-bump allocator (see README) is NOT tuned for
    # heavy multi-threaded contention, so despite doing zero GC work it runs
    # roughly as slow as Workstation GC here - "never collecting" doesn't
    # automatically mean "fastest" once allocator scalability matters.
    (New-Scenario "gcperfsim-mt-throughput" "GCPerfSim: 16-thread concurrent allocation throughput burst (GC-bound, no compute delay)" "gcperfsim" `
        "-tc 16 -tlgb 0.3 -sohsr 100-2000 -sohsi 50 -lohar 0 -at simple -c 0" 45.0)
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

# Monitors a long-running HTTP server process for scenarios where the
# harness (not the target app) controls when the run ends. Key difference
# from Invoke-MonitoredRun: dotnet-counters is invoked with --duration so it
# auto-stops and flushes a valid CSV on its own after a fixed time span,
# regardless of whether the server is still alive. The server process is
# force-killed only AFTER dotnet-counters has already exited cleanly -
# killing it first would make dotnet-counters throw (ServerNotAvailableException)
# and write no CSV at all.
#
# Load is driven by a background job that repeatedly POSTs small inference
# requests and records one completed-request count per wall-clock second;
# that per-second count is returned as a synthetic "operations" series in
# exactly the same {T=seconds; V=count} shape Parse-CountersCsv produces,
# so it plugs into the existing stats/percentile/downsampling pipeline
# unmodified.
function Invoke-MonitoredServerRun {
    param(
        [string]$ExePath,
        [string]$WorkingDirectory,
        [string]$Arguments,
        [hashtable]$ExtraEnv,
        [string[]]$RemoveEnvKeys,
        [string]$CsvBasePath,
        [string]$ReadyUrl,
        [string]$RequestUrl,
        [string]$RequestBodyJson,
        [int]$RunSeconds,
        [int]$ReadyTimeoutSeconds = 90
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

    # Wait for the server to actually accept requests (model load + warm-up
    # can take several seconds) before starting the timed measurement window.
    $ready = $false
    $readyDeadline = (Get-Date).AddSeconds($ReadyTimeoutSeconds)
    while ((Get-Date) -lt $readyDeadline) {
        if ($proc.HasExited) { break }
        try {
            Invoke-RestMethod -Uri $ReadyUrl -Method Post -Body $RequestBodyJson -ContentType "application/json" -TimeoutSec 10 | Out-Null
            $ready = $true
            break
        }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) {
        if (-not $proc.HasExited) { $proc.Kill() }
        throw "Server at $ReadyUrl did not become ready within $ReadyTimeoutSeconds s (stdout: $($stdoutTask.Result); stderr: $($stderrTask.Result))"
    }

    # Background load-driver job: sequential requests (dotLLM processes them
    # one at a time anyway), recording a completed-request count per second.
    $job = Start-Job -ScriptBlock {
        param($Url, $BodyJson, $DurationSec)
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $perSecond = @{}
        while ($sw.Elapsed.TotalSeconds -lt $DurationSec) {
            try {
                Invoke-RestMethod -Uri $Url -Method Post -Body $BodyJson -ContentType "application/json" -TimeoutSec 30 | Out-Null
                $sec = [int][Math]::Floor($sw.Elapsed.TotalSeconds)
                if ($perSecond.ContainsKey($sec)) { $perSecond[$sec] = $perSecond[$sec] + 1 } else { $perSecond[$sec] = 1 }
            }
            catch { Start-Sleep -Milliseconds 200 }
        }
        return $perSecond
    } -ArgumentList $RequestUrl, $RequestBodyJson, $RunSeconds

    # dotnet-counters --duration makes it stop and flush a complete CSV on
    # its own after this span, independent of the server's lifetime. Give it
    # a little slack over the load-driver duration so the last few seconds
    # of load are fully captured.
    $durSpan = [TimeSpan]::FromSeconds($RunSeconds + 5)
    $durArg = "{0:00}:{1:00}:{2:00}:{3:00}" -f $durSpan.Days, $durSpan.Hours, $durSpan.Minutes, $durSpan.Seconds
    $dcArgs = @("collect", "-p", $proc.Id, "--refresh-interval", $CounterRefreshIntervalSeconds,
        "--format", "csv", "--counters", "System.Runtime,ZeroGC.Bench", "-o", $CsvBasePath, "--duration", $durArg)
    & $dotnetCounters @dcArgs *> (Join-Path $rawDir "dotnet-counters.log") 2>&1

    # dotnet-counters has now exited cleanly on its own - safe to reap the
    # load-driver job and finally kill the server.
    Wait-Job -Job $job -Timeout 30 | Out-Null
    $perSecondCounts = Receive-Job -Job $job -ErrorAction SilentlyContinue
    Remove-Job -Job $job -Force -ErrorAction SilentlyContinue

    if (-not $proc.HasExited) { $proc.Kill() }
    $proc.WaitForExit(15000) | Out-Null

    $csvPath = "$CsvBasePath.csv"
    $series = if (Test-Path $csvPath) { Parse-CountersCsv $csvPath } else { @{} }

    # Build a synthetic "operations" series (requests completed per second)
    # in the same {T=seconds-since-start; V=value} shape used elsewhere.
    $opsSeries = @()
    if ($perSecondCounts) {
        foreach ($sec in ($perSecondCounts.Keys | Sort-Object)) {
            $opsSeries += @{ T = [double]$sec; V = [double]$perSecondCounts[$sec] }
        }
    }
    $series["operations"] = $opsSeries
    $totalOps = ($perSecondCounts.Values | Measure-Object -Sum).Sum
    if (-not $totalOps) { $totalOps = 0 }

    return [pscustomobject]@{
        Series   = $series
        TotalOps = $totalOps
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
                # gcperfsim-mt-throughput is a short (single-digit-to-tens-of-
                # seconds) GC-bound burst rather than a ~600s run like the
                # other GCPerfSim scenarios, so the default 1500ms attach
                # delay can eat too much of the run for dotnet-counters to
                # capture any samples (especially under Server GC, the
                # fastest mode here) - use a much smaller attach delay.
                $attachDelayMs = if ($scenario.Id -eq "gcperfsim-mt-throughput") { 300 } else { 1500 }
                $run = Invoke-MonitoredRun -ExePath (Join-Path $publishDir "GCPerfSim.exe") -WorkingDirectory $publishDir `
                    -Arguments $fullArgs -ExtraEnv $gcMode.Env -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase -AttachDelayMs $attachDelayMs
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
            "zeroalloc" {
                $publishDir = Join-Path $root "samples\ZeroAllocApp\publish"
                $run = Invoke-MonitoredRun -ExePath (Join-Path $publishDir "ZeroAllocApp.exe") -WorkingDirectory $publishDir `
                    -Arguments "$DurationSeconds $label" -ExtraEnv $gcMode.Env -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase
                $resultJson = Get-ResultLineJson $run.StdOut
                if (-not $resultJson) { Write-Host $run.StdOut; Write-Host $run.StdErr; throw "No ##RESULT## for $label" }
                $summary = [ordered]@{
                    OperationsTotal      = $resultJson.Operations
                    OpsPerSecondOverall  = $resultJson.OpsPerSecond
                    TotalAllocatedBytes  = $resultJson.TotalAllocatedBytes
                    BytesAllocatedThisThreadDuringRun = $resultJson.BytesAllocatedThisThreadDuringRun
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
            "dotllm-serve" {
                $reqBody = '{"model":"SmolLM-135M.Q4_K_M.gguf","prompt":"The capital of France is","max_tokens":16}'
                $baseUrl = "http://localhost:$DotLlmPort/v1/completions"
                $env2 = @{} + $gcMode.Env
                $svrRun = Invoke-MonitoredServerRun -ExePath $dotLlmExe -WorkingDirectory $root `
                    -Arguments "serve `"$DotLlmModelFile`" --port $DotLlmPort --no-browser --no-ui" `
                    -ExtraEnv $env2 -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase `
                    -ReadyUrl $baseUrl -RequestUrl $baseUrl -RequestBodyJson $reqBody -RunSeconds $DurationSeconds
                $run = [pscustomobject]@{ Series = $svrRun.Series }
                $summary = [ordered]@{
                    OperationsTotal      = $svrRun.TotalOps
                    OpsPerSecondOverall  = $(if ($DurationSeconds -gt 0) { $svrRun.TotalOps / $DurationSeconds } else { 0 })
                    TotalAllocatedBytes  = $null
                    Gen0Collections      = $null
                    Gen1Collections      = $null
                    Gen2Collections      = $null
                    WorkingSetBytes      = $null
                    PeakWorkingSetBytes  = $null
                    HeapSizeBytes        = $null
                    TotalCommittedBytes  = $null
                    GcName               = $gcMode.DisplayName
                }
                $durationActual = $DurationSeconds
            }
            "dotllm-serve-large" {
                # A few-hundred-token prompt (~300 tokens as tokenized by the
                # model) about GC generations, followed by a short question -
                # long enough prefill to meaningfully exercise the model's
                # attention/KV-cache path over the tiny short-prompt scenario
                # above, while still completing in well under a minute per
                # request on CPU so a single benchmark run stays practical.
                $longPrompt = @'
You are a helpful assistant. Consider the following technical passage about garbage collection in modern managed runtimes, then answer the question at the end.

Garbage collection (GC) is an automatic memory management technique used by many programming language runtimes, including the Common Language Runtime (CLR) that powers .NET. The core idea is to relieve developers from the burden of manually tracking and freeing memory. Instead, the runtime periodically identifies objects that are no longer reachable from any root reference - such as local variables, static fields, or CPU registers - and reclaims the memory they occupy. Generational garbage collectors, like the one used in CoreCLR, divide the managed heap into multiple generations based on object lifetime. Generation 0 contains newly allocated objects. Generation 1 acts as a buffer between short-lived and long-lived objects. Generation 2 holds long-lived objects. The Large Object Heap holds objects larger than 85,000 bytes. Because most objects die young, this generational hypothesis lets a collector focus most of its work on generation 0, dramatically reducing average pause times. However, generation 2 collections, which must trace the entire live object graph reachable from the root set, can still take a long time when the retained heap is large. This is why long-running services with big in-memory caches sometimes see occasional, but very noticeable, garbage collection pauses.

Question: Summarize the tradeoff between generation 0 and generation 2 collections in one sentence.
'@
                $reqBody = (@{ model = "qwen2.5-1.5b-instruct-q4_k_m.gguf"; prompt = $longPrompt; max_tokens = 64 } | ConvertTo-Json -Compress)
                $baseUrl = "http://localhost:$DotLlmLargePort/v1/completions"
                $env2 = @{} + $gcMode.Env
                $svrRun = Invoke-MonitoredServerRun -ExePath $dotLlmExe -WorkingDirectory $root `
                    -Arguments "serve `"$DotLlmLargeModelFile`" --port $DotLlmLargePort --no-browser --no-ui" `
                    -ExtraEnv $env2 -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase `
                    -ReadyUrl $baseUrl -RequestUrl $baseUrl -RequestBodyJson $reqBody -RunSeconds $DurationSeconds
                $run = [pscustomobject]@{ Series = $svrRun.Series }
                $summary = [ordered]@{
                    OperationsTotal      = $svrRun.TotalOps
                    OpsPerSecondOverall  = $(if ($DurationSeconds -gt 0) { $svrRun.TotalOps / $DurationSeconds } else { 0 })
                    TotalAllocatedBytes  = $null
                    Gen0Collections      = $null
                    Gen1Collections      = $null
                    Gen2Collections      = $null
                    WorkingSetBytes      = $null
                    PeakWorkingSetBytes  = $null
                    HeapSizeBytes        = $null
                    TotalCommittedBytes  = $null
                    GcName               = $gcMode.DisplayName
                }
                $durationActual = $DurationSeconds
            }
            "growingcache" {
                $publishDir = Join-Path $root "samples\GrowingCacheApp\publish"
                $run = Invoke-MonitoredRun -ExePath (Join-Path $publishDir "GrowingCacheApp.exe") -WorkingDirectory $publishDir `
                    -Arguments "$DurationSeconds $label $($scenario.Args)" -ExtraEnv $gcMode.Env -RemoveEnvKeys $gcMode.RemoveEnv -CsvBasePath $csvBase -AttachDelayMs 3000
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
                    FinalCacheEntryCount   = $resultJson.FinalCacheEntryCount
                    ObservedGen2Events     = $resultJson.ObservedGen2Events
                    ObservedGen2PauseAvgMs = $resultJson.ObservedGen2PauseAvgMs
                    ObservedGen2PauseMaxMs = $resultJson.ObservedGen2PauseMaxMs
                    ObservedGen2PauseMinMs = $resultJson.ObservedGen2PauseMinMs
                    ObservedGen2PauseSamplesMs = $resultJson.ObservedGen2PauseSamplesMs
                }
                $durationActual = $resultJson.DurationSeconds
            }
        }

        # Fill in allocation/collection-count summary fields from the raw
        # counters series for scenarios (server-based ones) that can't
        # self-report these via a ##RESULT## line.
        if ($null -eq $summary.TotalAllocatedBytes -and $run.Series["alloc_rate"]) {
            $allocRawVals = @($run.Series["alloc_rate"] | ForEach-Object { $_.V })
            if ($allocRawVals.Count -gt 0) { $summary.TotalAllocatedBytes = ($allocRawVals | Measure-Object -Sum).Sum }
        }
        if ($null -eq $summary.Gen0Collections -and $run.Series["gen0_collections"]) {
            $summary.Gen0Collections = [int](@($run.Series["gen0_collections"] | ForEach-Object { $_.V }) | Measure-Object -Sum).Sum
        }
        if ($null -eq $summary.Gen1Collections -and $run.Series["gen1_collections"]) {
            $summary.Gen1Collections = [int](@($run.Series["gen1_collections"] | ForEach-Object { $_.V }) | Measure-Object -Sum).Sum
        }
        if ($null -eq $summary.Gen2Collections -and $run.Series["gen2_collections"]) {
            $summary.Gen2Collections = [int](@($run.Series["gen2_collections"] | ForEach-Object { $_.V }) | Measure-Object -Sum).Sum
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
        $pauseMsVals = @($run.Series["pause_time"] | ForEach-Object { $_.V * 1000.0 }) # seconds/sec, ~1s sample window -> ms of pause in that window
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
            PauseTimeMsStats  = (Get-Stats $pauseMsVals)
            WorkingSetStats   = (Get-Stats $wsValsAll)
            AllocRateMBStats  = (Get-Stats $allocRateVals)
            OpsPerSecStats    = (Get-Stats $opsVals)
            TimeSeries = [ordered]@{
                PauseTimePct = Get-Downsampled (@($run.Series["pause_time"] | ForEach-Object { @{ T = $_.T; V = [Math]::Round($_.V * 100.0, 4) } }))
                PauseTimeMs  = Get-Downsampled (@($run.Series["pause_time"] | ForEach-Object { @{ T = $_.T; V = [Math]::Round($_.V * 1000.0, 3) } }))
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
