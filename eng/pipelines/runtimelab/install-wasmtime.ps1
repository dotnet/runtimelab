[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$InstallDir,
    [switch]$CI
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Set-Location $InstallDir

$WasmtimeVersion = "v21.0.1"
if ($IsWIndows)
{
    $WasmtimeBaseName = "wasmtime-$WasmtimeVersion-x86_64-windows"
    $WasmtimeArchive = $WasmtimeBaseName.zip
}
else
{
    $WasmtimeBaseName = "wasmtime-$WasmtimeVersion-x86_64-linux"
    $WasmtimeArchive = $WasmtimeFolderName.tar.gz
}

Invoke-WebRequest -Uri https://github.com/bytecodealliance/wasmtime/releases/download/v21.0.1/$WasmtimeArchive -OutFile $WasmtimeArchive
if ($IsWIndows)
{
    Expand-Archive -LiteralPath $WasmtimeArchive -DestinationPath .
}
else
{
    New-Item -ItemType Directory -Force -Path wasmtime
    tar -xzf $WasmtimeArchive -C wasmtime
}

if ($CI)
{
    Write-Output "##vso[task.prependpath]$pwd/$WasmtimeFolderName"
}
