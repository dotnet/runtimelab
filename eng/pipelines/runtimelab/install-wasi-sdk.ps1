param(
    $InstallDir,
    $HostArch = $null,
    [switch]$CI
)

$WasiSdkVersion = 25
Set-Location -Path $InstallDir
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Verify that we're not behind upstream (and allow us to be ahead).
$UpstreamWasiSdkVersion = Get-Content $PSScriptRoot/../../../src/mono/wasi/wasi-sdk-version.txt
if ($WasiSdkVersion -lt [int]$UpstreamWasiSdkVersion)
{
    Write-Error "Upstream WASI SDK version is $UpstreamWasiSdkVersion; update `$WasiSdkVersion (currently $WasiSdkVersion)!"
    exit
}

if (!$HostArch)
{
    $HostArch = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
}
$WasiSdkHostArch = switch ($HostArch)
{
    x64 { "x86_64" }
    default { "$HostArch".ToLowerInvariant() }
}

$WasiSdkHost = $IsWindows ? "$WasiSdkHostArch-windows" : "$WasiSdkHostArch-linux"
if ($WasiSdkHost -eq "arm64-windows")
{
    # TODO-LLVM: remove once we updgrade to WASI SDK 27.0 that supports win-arm64. For now we rely on emulation.
    $WasiSdkHost = "x86_64-windows"
}
$WasiSdkDirName = "wasi-sdk-$WasiSdkVersion.0-$WasiSdkHost"
$WasiSdkGzFile = "$WasiSdkDirName.tar.gz"

Invoke-WebRequest -Uri https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-$WasiSdkVersion/$WasiSdkGzFile -OutFile $WasiSdkGzFile

tar -xzf $WasiSdkGzFile
del $WasiSdkGzFile
mv $WasiSdkDirName wasi-sdk

# The upstream build expects this sentinel to exist, otherwise it tries to use a provisioned SDK.
$WasiSdkVersion > wasi-sdk/"WASI-SDK-VERSION-$WasiSdkVersion.0"

if ($CI)
{
    Write-Host "Setting WASI_SDK_PATH to '$InstallDir/wasi-sdk'"
    Write-Output "##vso[task.setvariable variable=WASI_SDK_PATH]$InstallDir/wasi-sdk"
}
