<#
.SYNOPSIS
    Runs the ZeroGC benchmark suite: ConsoleApp + WebApi samples, each under
    the regular (default) CoreCLR GC and under ZeroGC, for a configurable
    duration per run. Collects the ##RESULT## JSON line each app prints and
    renders results.json + report.html (self-contained, no external deps).

.EXAMPLE
    .\run-benchmarks.ps1 -DurationSeconds 180
#>
param(
    [int]$DurationSeconds = 180,
    [string]$OutDir = "$PSScriptRoot\results"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$consolePublish = Join-Path $root "samples\ConsoleApp\publish"
$webApiPublish = Join-Path $root "samples\WebApi\publish"
$gcDll = Join-Path $root "native\obj\Release\ZeroGC.dll"

if (-not (Test-Path $gcDll)) { throw "ZeroGC.dll not built. Run native\build.ps1 first." }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Copy-Item $gcDll $consolePublish -Force
Copy-Item $gcDll $webApiPublish -Force

function Get-ResultLine($lines) {
    $line = $lines | Where-Object { $_ -like "##RESULT##*" } | Select-Object -Last 1
    if (-not $line) { throw "No ##RESULT## line found in output:`n$($lines -join "`n")" }
    return $line.Substring("##RESULT##".Length) | ConvertFrom-Json
}

function Run-Console([string]$label, [bool]$useZeroGc) {
    Write-Host "=== ConsoleApp / $label (duration=${DurationSeconds}s, ZeroGC=$useZeroGc) ===" -ForegroundColor Cyan
    Push-Location $consolePublish
    try {
        if ($useZeroGc) { $env:DOTNET_GCName = "ZeroGC.dll" } else { Remove-Item Env:\DOTNET_GCName -ErrorAction SilentlyContinue }
        $out = & .\ConsoleApp.exe $DurationSeconds $label 2>&1
        $out | ForEach-Object { Write-Host "  $_" }
        return Get-ResultLine $out
    }
    finally { Pop-Location }
}

function Run-WebApi([string]$label, [bool]$useZeroGc) {
    Write-Host "=== WebApi / $label (duration=${DurationSeconds}s, ZeroGC=$useZeroGc) ===" -ForegroundColor Cyan
    Push-Location $webApiPublish
    try {
        if ($useZeroGc) { $env:DOTNET_GCName = "ZeroGC.dll" } else { Remove-Item Env:\DOTNET_GCName -ErrorAction SilentlyContinue }
        $env:ZEROGC_BENCH_DURATION_SECONDS = "$DurationSeconds"
        $env:ZEROGC_BENCH_LABEL = $label
        $out = & .\WebApi.exe 2>&1
        $out | ForEach-Object { Write-Host "  $_" }
        return Get-ResultLine $out
    }
    finally { Pop-Location }
}

$results = [ordered]@{
    ConsoleRegular = Run-Console "console-regular" $false
    ConsoleZeroGC  = Run-Console "console-zerogc"  $true
    WebApiRegular  = Run-WebApi  "webapi-regular"   $false
    WebApiZeroGC   = Run-WebApi  "webapi-zerogc"    $true
}

Remove-Item Env:\DOTNET_GCName -ErrorAction SilentlyContinue
Remove-Item Env:\ZEROGC_BENCH_DURATION_SECONDS -ErrorAction SilentlyContinue
Remove-Item Env:\ZEROGC_BENCH_LABEL -ErrorAction SilentlyContinue

$resultsJsonPath = Join-Path $OutDir "results.json"
$results | ConvertTo-Json -Depth 5 | Set-Content -Path $resultsJsonPath -Encoding UTF8
Write-Host "Wrote $resultsJsonPath" -ForegroundColor Green

& "$root\generate-report.ps1" -ResultsJson $resultsJsonPath -OutHtml (Join-Path $OutDir "report.html")
