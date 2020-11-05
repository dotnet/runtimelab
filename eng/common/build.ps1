[CmdletBinding(PositionalBinding=$false)]
Param(
  [string][Alias('c')]$configuration = "Debug",
  [string]$platform = $null,
  [string] $projects,
  [string][Alias('v')]$verbosity = "minimal",
  [string] $msbuildEngine = $null,
  [bool] $warnAsError = $true,
  [bool] $nodeReuse = $true,
  [bool] $useDefaultDotnetInstall = $false,
  [switch][Alias('r')]$restore,
  [switch] $deployDeps,
  [switch][Alias('b')]$build,
  [switch] $rebuild,
  [switch] $deploy,
  [switch][Alias('t')]$test,
  [switch] $integrationTest,
  [switch] $performanceTest,
  [switch] $sign,
  [switch] $pack,
  [switch] $publish,
  [switch] $clean,
  [switch][Alias('bl')]$binaryLog,
  [switch][Alias('nobl')]$excludeCIBinarylog,
  [switch] $ci,
  [switch] $prepareMachine,
  [string] $runtimeSourceFeed = '',
  [string] $runtimeSourceFeedKey = '',
  [switch] $help,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

# Unset 'Platform' environment variable to avoid unwanted collision in InstallDotNetCore.targets file
# some computer has this env var defined (e.g. Some HP)
if($env:Platform) {
  $env:Platform=""  
}
function Print-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -configuration <value>  Build configuration: 'Debug' or 'Release' (short: -c)"
  Write-Host "  -platform <value>       Platform configuration: 'x86', 'x64' or any valid Platform value to pass to msbuild"
  Write-Host "  -verbosity <value>      Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic] (short: -v)"
  Write-Host "  -binaryLog              Output binary log (short: -bl)"
  Write-Host "  -help                   Print help and exit"
  Write-Host ""

  Write-Host "Actions:"
  Write-Host "  -restore                Restore dependencies (short: -r)"
  Write-Host "  -build                  Build solution (short: -b)"
  Write-Host "  -rebuild                Rebuild solution"
  Write-Host "  -deploy                 Deploy built VSIXes"
  Write-Host "  -deployDeps             Deploy dependencies (e.g. VSIXes for integration tests)"
  Write-Host "  -test                   Run all unit tests in the solution (short: -t)"
  Write-Host "  -integrationTest        Run all integration tests in the solution"
  Write-Host "  -performanceTest        Run all performance tests in the solution"
  Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
  Write-Host "  -sign                   Sign build outputs"
  Write-Host "  -publish                Publish artifacts (e.g. symbols)"
  Write-Host "  -clean                  Clean the solution"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -projects <value>       Semi-colon delimited list of sln/proj's to build. Globbing is supported (*.sln)"
  Write-Host "  -ci                     Set when running on CI server"
  Write-Host "  -excludeCIBinarylog     Don't output binary log (short: -nobl)"
  Write-Host "  -prepareMachine         Prepare machine for CI run, clean up processes after build"
  Write-Host "  -warnAsError <value>    Sets warnaserror msbuild parameter ('true' or 'false')"
  Write-Host "  -msbuildEngine <value>  Msbuild engine to use to run build ('dotnet', 'vs', or unspecified)."
  Write-Host "  -useDefaultDotnetInstall <value> Use dotnet-install.* scripts from public location as opposed to from eng common folder"
  Write-Host ""

  Write-Host "Command line arguments not listed above are passed thru to msbuild."
  Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}

. $PSScriptRoot\tools.ps1

function InitializeCustomToolset {
  if (-not $restore) {
    return
  }

  $script = Join-Path $EngRoot 'restore-toolset.ps1'

  if (Test-Path $script) {
    . $script
  }
}

function GetCMakeVersions
{
  $items = @()
  $items += @(Get-ChildItem hklm:\SOFTWARE\Wow6432Node\Kitware -ErrorAction SilentlyContinue)
  $items += @(Get-ChildItem hklm:\SOFTWARE\Kitware -ErrorAction SilentlyContinue)
  return $items | where { $_.PSChildName.StartsWith("CMake") }
}

function GetCMakeInfo($regKey)
{
  try {
    $version = [System.Version] $regKey.PSChildName.Split(' ')[1]
  }
  catch {
    return $null
  }
  $itemProperty = Get-ItemProperty $regKey.PSPath;
  if (Get-Member -inputobject $itemProperty -name "InstallDir" -Membertype Properties) {
    $cmakeDir = $itemProperty.InstallDir
  }
  else {
    $cmakeDir = $itemProperty.'(default)'
  }
  $cmakePath = [System.IO.Path]::Combine($cmakeDir, "artifacts\cmake.exe")
  if (![System.IO.File]::Exists($cmakePath)) {
    return $null
  }
  return @{'version' = $version; 'path' = $cmakePath}
}

function LocateCMake
{
  $errorMsg = "CMake is a pre-requisite to build this repository but it was not found on the path. Please install CMake from https://cmake.org/download/ and ensure it is on your path."
  $inPathPath = (get-command cmake.exe -ErrorAction SilentlyContinue)
  if ($inPathPath -ne $null) {
    # Resolve the first version of CMake if multiple commands are found
    if ($inPathPath.Length -gt 1) {
      return $inPathPath[0].Path
    }
    return $inPathPath.Path
  }
  # Let us hope that CMake keep using their current version scheme
  $validVersions = @()
  foreach ($regKey in GetCMakeVersions) {
    $info = GetCMakeInfo($regKey)
    if ($info -ne $null) {
      $validVersions += @($info)
    }
  }
  $newestCMakePath = ($validVersions |
    Sort-Object -property @{Expression={$_.version}; Ascending=$false} |
    select -first 1).path
  if ($newestCMakePath -eq $null) {
    Throw $errorMsg
  }
  return $newestCMakePath
}

function SetCMakePath
{
  $path = LocateCMake
  $path = Split-Path -Path $path
  Write-Host $path
  $env:Path += ";" + $path
}

function Build {
  $toolsetBuildProj = InitializeToolset
  InitializeCustomToolset

  $bl = if ($binaryLog) { '/bl:' + (Join-Path $LogDir 'Build.binlog') } else { '' }
  $platformArg = if ($platform) { "/p:Platform=$platform" } else { '' }

  if ($projects) {
    # Re-assign properties to a new variable because PowerShell doesn't let us append properties directly for unclear reasons.
    # Explicitly set the type as string[] because otherwise PowerShell would make this char[] if $properties is empty.
    [string[]] $msbuildArgs = $properties
    
    # Resolve relative project paths into full paths 
    $projects = ($projects.Split(';').ForEach({Resolve-Path $_}) -join ';')
    
    $msbuildArgs += "/p:Projects=$projects"
    $properties = $msbuildArgs
  }

  SetCMakePath

  MSBuild $toolsetBuildProj `
    $bl `
    $platformArg `
    /p:Configuration=$configuration `
    /p:RepoRoot=$RepoRoot `
    /p:Restore=$restore `
    /p:DeployDeps=$deployDeps `
    /p:Build=$build `
    /p:Rebuild=$rebuild `
    /p:Deploy=$deploy `
    /p:Test=$test `
    /p:Pack=$pack `
    /p:IntegrationTest=$integrationTest `
    /p:PerformanceTest=$performanceTest `
    /p:Sign=$sign `
    /p:Publish=$publish `
    @properties
}

try {
  if ($clean) {
    if (Test-Path $ArtifactsDir) {
      Remove-Item -Recurse -Force $ArtifactsDir
      Write-Host 'Artifacts directory deleted.'
    }
    exit 0
  }

  if ($help -or (($null -ne $properties) -and ($properties.Contains('/help') -or $properties.Contains('/?')))) {
    Print-Usage
    exit 0
  }

  if ($ci) {
    if (-not $excludeCIBinarylog) {
      $binaryLog = $true
    }
    $nodeReuse = $false
  }

  if ($restore) {
    InitializeNativeTools
  }

  Build
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'InitializeToolset' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
