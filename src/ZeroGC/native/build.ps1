# build.ps1 - Builds ZeroGC.dll (standalone CoreCLR GC) using cl.exe/link.exe directly.
#
# Requires:
#   - Visual Studio (or Build Tools) with the "Desktop development with C++" workload.
#   - A local clone of dotnet/runtime, used ONLY as a read-only header/reference
#     dependency (nothing under it is built or modified by this script).
#
# Usage:
#   .\build.ps1 -RuntimeRepo C:\github\runtime -Configuration Release
#
param(
    [string]$RuntimeRepo = "C:\github\runtime",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$VcVarsPath = ""
)

$ErrorActionPreference = "Stop"
$src = $PSScriptRoot
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

$objDir = Join-Path $src "obj\$Configuration"
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

$includes = @(
    $src,
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
    "/D_CRT_SECURE_NO_WARNINGS", "/DUNICODE", "/D_UNICODE"
)
if ($Configuration -eq "Release") {
    $defs += "/DNDEBUG"
    $optFlags = "/O2 /MD"
}
else {
    $defs += "/D_DEBUG"
    $optFlags = "/Od /MDd /Zi"
}

$sourceFiles = @("dllmain.cpp", "ZeroGCHeap.cpp", "ZeroGCHandles.cpp") | ForEach-Object { "`"$src\$_`"" }

$clCmd = "cl.exe /nologo /c /EHsc /std:c++17 $optFlags /I `"$includes`" $($defs -join ' ') /Fo:`"$objDir\\`" $($sourceFiles -join ' ')"
$objFiles = @("dllmain.obj", "ZeroGCHeap.obj", "ZeroGCHandles.obj") | ForEach-Object { "`"$objDir\$_`"" }
$outDll = Join-Path $objDir "ZeroGC.dll"
$linkCmd = "link.exe /nologo /DLL /OUT:`"$outDll`" $($objFiles -join ' ') kernel32.lib advapi32.lib"

Write-Host "Compiling ZeroGC ($Configuration)..." -ForegroundColor Cyan
$output = & $env:ComSpec /c "call `"$VcVarsPath`" >nul 2>&1 && cd /d `"$src`" && $clCmd 2>&1 && $linkCmd 2>&1"
$output | Write-Host

if (-not (Test-Path $outDll)) {
    throw "Build failed: $outDll was not produced."
}

Write-Host "Built: $outDll" -ForegroundColor Green
