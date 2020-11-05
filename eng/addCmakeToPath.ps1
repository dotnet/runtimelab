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
    Throw $errorMsg
  }
  return $newestCMakePath
}

function SetCMakePath
{
  $path = LocateCMake
  $path = Split-Path -Path $path
  Write-Host $path
  Write-Host "##vso[task.prependpath]$path"
  &{ cmake.exe -version }
}

SetCMakePath