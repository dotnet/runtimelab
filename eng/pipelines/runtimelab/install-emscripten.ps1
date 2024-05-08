param(
    [string]$InstallDir
)

New-Item -ItemType Directory -Force -ErrorAction SilentlyContinue -Path (Split-Path -Path $InstallDir -Parent) -Name (Split-Path -Path $InstallDir -Leaf)

$ErrorActionPreference="Stop"

Set-Location -Path $InstallDir

git clone https://github.com/emscripten-core/emsdk.git

Set-Location -Path emsdk

# Checkout a specific commit to avoid unexpected issues
git checkout 37b85e9

python ./emsdk.py install 3.1.47

./emsdk activate 3.1.47

# Set a variable for later use (used in common/build.ps1)
Write-Host "##vso[task.setvariable variable=NATIVEAOT_CI_WASM_BUILD_EMSDK_PATH]$PWD"
