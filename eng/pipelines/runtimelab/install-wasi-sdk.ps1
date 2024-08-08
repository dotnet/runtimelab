[CmdletBinding(PositionalBinding=$false)]
param(
    $InstallDir,
    [switch]$CI
)

Set-Location -Path $InstallDir

if ($IsWindows)
{
    $WasiTar = "wasi-sdk-22.0.m-mingw64.tar.gz"
    $WasiFolder = "wasi-sdk-22.0+m"
}
else
{
    $WasiTar = "wasi-sdk-22.0-linux.tar.gz"
    $WasiFolder = "wasi-sdk-22.0"
}

$ProgressPreference = SilentlyContinue
Invoke-WebRequest -Uri https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-22/$WasiTar -OutFile $WasiTar

tar -xzf $WasiTar
mv $WasiFolder wasi-sdk

# Temporary WASI-SDK 22 workaround: Until
# https://github.com/WebAssembly/wasi-libc/issues/501 is addressed, we copy
# pthread.h from the wasm32-wasi-threads include directory to the wasm32-wasi
# include directory.  See https://github.com/dotnet/runtimelab/issues/2598 for
# the issue to remove this workaround once WASI-SDK 23 is released.

cp wasi-sdk/share/wasi-sysroot/include/wasm32-wasi-threads/pthread.h wasi-sdk/share/wasi-sysroot/include/wasm32-wasi/

if ($CI)
{
    Write-Host "Setting WASI_SDK_PATH to '$InstallDir/wasi-sdk'"
    Write-Output "##vso[task.setvariable variable=WASI_SDK_PATH]$InstallDir/wasi-sdk"
}
