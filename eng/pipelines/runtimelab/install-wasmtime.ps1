[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$InstallDir,
    [switch]$CI
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Set-Location $InstallDir

# Currently we need to use the daily releases for the debugging test to work.
$UsePreRelease = $true
$WasmtimeVersion = $UsePreRelease ? "dev" : "v26.0.1"

if ($IsWindows)
{
    $WasmtimeBaseName = "wasmtime-$WasmtimeVersion-x86_64-windows"
    $WasmtimeArchive = "$WasmtimeBaseName.zip"
}
else
{
    $WasmtimeBaseName = "wasmtime-$WasmtimeVersion-x86_64-linux"
    $WasmtimeArchive = "$WasmtimeBaseName.tar.xz"
}

Invoke-WebRequest -Uri https://github.com/bytecodealliance/wasmtime/releases/download/$WasmtimeVersion/$WasmtimeArchive -OutFile $WasmtimeArchive
if ($IsWindows)
{
    Expand-Archive -LiteralPath $WasmtimeArchive -DestinationPath .
}
else
{
    tar -xf $WasmtimeArchive
}

if ($CI)
{
    Write-Host "Adding to PATH: '$pwd/$WasmtimeBaseName'"
    Write-Output "##vso[task.prependpath]$pwd/$WasmtimeBaseName"
}
