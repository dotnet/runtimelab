[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$InstallDir,
    [switch]$CI
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Set-Location $InstallDir

$WasmtimeVersion = "v21.0.1"
$WasmtimeFolderName = "wasmtime-$WasmtimeVersion-x86_64-windows"
Invoke-WebRequest -Uri https://github.com/bytecodealliance/wasmtime/releases/download/v21.0.1/$WasmtimeFolderName.zip -OutFile wasmtime.zip
Expand-Archive -LiteralPath wasmtime.zip -DestinationPath .

if ($CI)
{
    Write-Output "##vso[task.prependpath]$pwd/$WasmtimeFolderName"
}
