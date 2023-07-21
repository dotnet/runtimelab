$InstallPath = $Args[0]
$NodeJSVersion = "v18.16.0"
$NodeJSInstallName = "node-$NodeJSVersion-win-x64"
$NodeJSZipName = "$NodeJSInstallName.zip"

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

Expand-Archive -LiteralPath "$InstallPath\$NodeJSInstallName.zip" -DestinationPath $InstallPath -Force

$NodeJSExePath = "$InstallPath\$NodeJSInstallName\node.exe"
if (!(Test-Path $NodeJSExePath))
{
    Write-Error "Did not find NodeJS at: '$NodeJSExePath'"
    exit 1
}

Write-Host Setting NODEJS_EXECUTABLE to $NodeJSExePath
Write-Host "##vso[task.setvariable variable=NODEJS_EXECUTABLE]$NodeJSExePath"
