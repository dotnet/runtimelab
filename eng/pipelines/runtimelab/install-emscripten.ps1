param(
    $InstallDir
)

$ErrorActionPreference="Stop"

New-Item -ItemType Directory -Force -Path $InstallDir

Set-Location -Path $InstallDir

git clone https://github.com/emscripten-core/emsdk.git

Set-Location -Path emsdk

# Checkout a specific commit to avoid unexpected issues
git checkout c18280c

./emsdk install 3.1.54

./emsdk activate 3.1.54

# Set a variable for later use (used in common/build.ps1)
Write-Host "##vso[task.setvariable variable=NATIVEAOT_CI_WASM_BUILD_EMSDK_PATH]$PWD"
