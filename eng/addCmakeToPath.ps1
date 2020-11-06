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

function DownloadCMake
{
  $downloadDir = (Split-Path $PSScriptRoot -Parent) + "\artifacts\download"
  $cmakeExtractPath = $downloadDir + "\cmake"

  $cmakeSearch = (Get-ChildItem -Path $cmakeExtractPath -Filter cmake.exe -Recurse -ErrorAction SilentlyContinue)
  if ($null -eq $cmakeSearch -or $cmakeSearch.Count -eq 0)
  {
    Write-Host "Downloading CMake"
    $cmakeZip = $downloadDir + "\cmake.zip"
    $cmakeUrl = "https://github.com/Kitware/CMake/releases/download/v3.18.4/cmake-3.18.4-win64-x64.zip"
    if (!(Test-Path $downloadDir)) { $throwAway = mkdir $downloadDir }
    if (!(Test-Path $cmakeZip))
    {
      Invoke-WebRequest -Uri $cmakeUrl -OutFile $cmakeZip
    }

    Write-Host "Extracting Cmake"
    if (!(Test-Path $cmakeExtractPath)) { $throwAway = mkdir $cmakeExtractPath }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($cmakeZip, $cmakeExtractPath)
    return (Get-ChildItem -Path $cmakeExtractPath -Filter cmake.exe -Recurse -ErrorAction SilentlyContinue).FullName
  }
  
  return $cmakeSearch.FullName
}

function LocateCMake
{
  $errorMsg = "CMake is a pre-requisite to build this repository but it was not found on the path. Please install CMake from https://cmake.org/download/ and ensure it is on your path."
  $inPathPath = (get-command cmake.exe -All -ErrorAction SilentlyContinue)
  if ($inPathPath -ne $null -or $inPathPath.Length -ge 1) {
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
    return DownloadCMake
  }

  return $newestCMakePath
}

function SetCMakePath
{
  $cmakePath = DownloadCMake
  $directory = Split-Path -Path $cmakePath
  Write-Host $directory
  echo "##vso[task.prependpath]$directory"
  $env:Path = "$directory;$env:Path"
  &{ cmake -version }
}

SetCMakePath