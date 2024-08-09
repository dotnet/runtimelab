$WasiSdkVersion = 24

# Verify that we're not behind upstream (and allow us to be ahead).
$UpstreamWasiSdkVersion = Get-Content $PSScriptRoot/../../../src/mono/wasi/wasi-sdk-version.txt
if ($WasiSdkVersion -lt [int]$UpstreamWasiSdkVersion)
{
    Write-Error "Upstream WASI SDK version is $UpstreamWasiSdkVersion; update `$WasiSdlVersion (currently $WasiSdkVersion)!"
    exit
}

$WasiSdkHost = "x86_64-windows"
$WasiSdkDirName = "wasi-sdk-$WasiSdkVersion.0-$WasiSdkHost"
$WasiSdkGzFile = "$WasiSdkDirName.tar.gz"
Invoke-WebRequest -Uri "https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-$WasiSdkVersion/$WasiSdkGzFile" -OutFile $WasiSdkGzFile

tar -xzf $WasiSdkGzFile
mv $WasiSdkDirName wasi-sdk

# The upstream build expects this sentinel to exist, otherwise it tries to use a provisioned SDK.
$WasiSdkVersion > wasi-sdk/"VERSION$("$WasiSdkVersion".ToUpper())"
