param(
    $InstallDir
    $CI
)

$ErrorActionPreference="Stop"

New-Item -ItemType Directory -Force -Path $InstallDir

Set-Location -Path $InstallDir

git clone https://github.com/emscripten-core/emsdk.git

Set-Location -Path emsdk

# Checkout a specific commit to avoid unexpected issues
git checkout ca7b40ae222a2d8763b6ac845388744b0e57cfb7
./emsdk install 3.1.56
./emsdk activate 3.1.56


if ($CI)
{
    Write-Host "Setting EMSDK to '$env:EMSDK'"
    Write-Output "##vso[task.setvariable variable=EMSDK]$env:EMSDK"
}

