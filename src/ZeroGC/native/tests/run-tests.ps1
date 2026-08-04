# run-tests.ps1 - Builds and runs ZeroGC's standalone native unit tests.
#
# These tests link directly against ZeroGCHeap.cpp/ZeroGCHandles.cpp/dllmain.cpp
# and call ZeroGCHeap's methods (Alloc, IsLargeObject, ...) directly, without
# needing a full CoreCLR host. This exists because some ZeroGC logic (e.g.
# IsLargeObject) has real call sites in dotnet/runtime that are unreachable in
# a normal Release build (gated behind USE_CHECKED_OBJECTREFS / _DEBUG, or the
# VERIFY_HEAP compile-time define), so running sample apps against a Release
# coreclr.dll - however long - never actually exercises that code path. These
# tests give direct, deterministic coverage instead.
#
# Usage:
#   .\run-tests.ps1 -RuntimeRepo C:\github\runtime
#
param(
    [string]$RuntimeRepo = "C:\github\runtime",
    [string]$VcVarsPath = ""
)

$ErrorActionPreference = "Stop"
$nativeSrc = Split-Path $PSScriptRoot -Parent
$testSrc = $PSScriptRoot
$rt = Join-Path $RuntimeRepo "src\coreclr"
$nativeRoot = Join-Path $RuntimeRepo "src\native"

if (-not (Test-Path $rt)) {
    throw "Could not find dotnet/runtime checkout at '$RuntimeRepo' (expected '$rt' to exist)."
}

if ([string]::IsNullOrEmpty($VcVarsPath)) {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\18\IntPreview\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
    )
    $VcVarsPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $VcVarsPath) {
        throw "Could not locate vcvars64.bat. Pass -VcVarsPath explicitly."
    }
}

$objDir = Join-Path $testSrc "obj"
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

$includes = @(
    $nativeSrc,
    (Join-Path $rt "gc"),
    (Join-Path $rt "gc\env"),
    (Join-Path $rt "inc"),
    $nativeRoot
) -join '" /I "'

$defs = @(
    "/DHOST_WINDOWS", "/DHOST_64BIT", "/DHOST_AMD64",
    "/DTARGET_WINDOWS", "/DTARGET_AMD64", "/DTARGET_64BIT", "/DBIT64",
    "/DWIN32", "/D_WIN32",
    "/DFEATURE_STANDALONE_GC", "/DBUILD_AS_STANDALONE",
    "/DFEATURE_MANUALLY_MANAGED_CARD_BUNDLES",
    "/D_CRT_SECURE_NO_WARNINGS", "/DUNICODE", "/D_UNICODE", "/DNDEBUG"
)

$sourceFiles = @("dllmain.cpp", "ZeroGCHeap.cpp", "ZeroGCHandles.cpp") | ForEach-Object { "`"$nativeSrc\$_`"" }
$sourceFiles += "`"$testSrc\test_islargeobject.cpp`""

$clCmd = "cl.exe /nologo /c /EHsc /std:c++17 /O2 /MD /guard:cf /guard:ehcont /I `"$includes`" $($defs -join ' ') /Fo:`"$objDir\\`" $($sourceFiles -join ' ')"
$objFiles = @("dllmain.obj", "ZeroGCHeap.obj", "ZeroGCHandles.obj", "test_islargeobject.obj") | ForEach-Object { "`"$objDir\$_`"" }
$outExe = Join-Path $objDir "test_islargeobject.exe"
$linkCmd = "link.exe /nologo /guard:cf /guard:ehcont /OUT:`"$outExe`" $($objFiles -join ' ') kernel32.lib advapi32.lib"

Write-Host "Building ZeroGC native unit tests..." -ForegroundColor Cyan
$output = & $env:ComSpec /c "call `"$VcVarsPath`" >nul 2>&1 && cd /d `"$testSrc`" && $clCmd 2>&1 && $linkCmd 2>&1"
$output | Write-Host

if (-not (Test-Path $outExe)) {
    throw "Build failed: $outExe was not produced."
}

Write-Host "Running test_islargeobject..." -ForegroundColor Cyan
& $outExe
if ($LASTEXITCODE -ne 0) {
    throw "test_islargeobject FAILED (exit code $LASTEXITCODE)."
}

Write-Host "All native unit tests passed." -ForegroundColor Green
