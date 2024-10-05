[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$InstallDir,
    [switch]$CI
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Set-Location $InstallDir

$WasmtimeVersion = "v25.0.1"

if (!(Test-Path variable:global:IsWindows))
{
    $IsWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
}

if ($IsWIndows)
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
if ($IsWIndows)
{
    Expand-Archive -LiteralPath $WasmtimeArchive -DestinationPath .
}
else
{
    New-Item -ItemType Directory -Force -Path $WasmtimeBaseName
    tar -xf $WasmtimeArchive -C $WasmtimeBaseName
}

if ($CI)
{
    Write-Host "Setting WASMTIME_EXECUTABLE to '$pwd/$WasmtimeBaseName/$WasmtimeBaseName/wasmtime'"
    Write-Output "##vso[task.setvariable variable=WASMTIME_EXECUTABLE]$pwd/$WasmtimeBaseName/$WasmtimeBaseName/wasmtime"
    Write-Output "##vso[task.prependpath]$pwd/$WasmtimeBaseName"
}
