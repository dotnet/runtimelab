[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$InstallDir,
    [switch]$CI
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Set-Location $InstallDir

$UsePreRelease = $false
$WasmtimeVersion = $UsePreRelease ? "dev" : "v31.0.0"

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
