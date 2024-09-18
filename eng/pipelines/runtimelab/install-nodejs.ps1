$InstallPath = $Args[0]
$NodeJSVersion = "v20.2.0"

if (!(Test-Path variable:global:IsWindows))
{
    $IsWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
}

if ($IsWIndows)
{
    $NodeJSInstallName = "node-$NodeJSVersion-win-x64"
    $NodeJSZipName = "$NodeJSInstallName.zip"
}
else
{
    $NodeJSInstallName = "node-$NodeJSVersion-linux-x64"
    $NodeJSZipName = "$NodeJSInstallName.tar.xz"
}

if (!(Test-Path $InstallPath))
{
    mkdir $InstallPath
}

$RetryCount = 10
$RetryInterval = 10

while ($RetryCount -gt 0)
{
    try
    {
        $RetryCount--
        Invoke-WebRequest -Uri "https://nodejs.org/dist/$NodeJSVersion/$NodeJSZipName" -OutFile "$InstallPath\$NodeJSZipName"
        break
    }
    catch [System.Net.WebException]
    {
        Write-Host "Invoke-WebRequest failed with: $_"
        Write-Host "Retrying in $RetryInterval seconds; $RetryCount retries remaining"
        Start-Sleep -Seconds $RetryInterval
    }
}

if ($RetryCount -le 0)
{
    Write-Host "All retries exhausted; exiting with error"
    exit 1
}

if ($IsWindows)
{
    Expand-Archive -LiteralPath "$InstallPath\$NodeJSZipName" -DestinationPath $InstallPath -Force
    $NodeJSExePath = "$InstallPath\$NodeJSInstallName\node.exe"
    $NpmExePath = "$InstallPath\$NodeJSInstallName\npm.cmd"
}
else
{
    tar xJf $InstallPath/$NodeJSZipName -C $InstallPath
    $NodeJSExePath = "$InstallPath/$NodeJSInstallName/bin/node"
    $NpmExePath = "$InstallPath/$NodeJSInstallName/bin/npm"
}

if (!(Test-Path $NodeJSExePath))
{
    Write-Error "Did not find NodeJS at: '$NodeJSExePath'"
    exit 1
}

if (!(Test-Path $NpmExePath))
{
    Write-Error "Did not find NPM at: '$NpmExePath'"
    exit 1
}

Write-Host Setting NODEJS_EXECUTABLE to $NodeJSExePath
Write-Host "##vso[task.setvariable variable=NODEJS_EXECUTABLE]$NodeJSExePath"

Write-Host Setting NPM_EXECUTABLE to $NpmExePath
Write-Host "##vso[task.setvariable variable=NPM_EXECUTABLE]$NpmExePath"
