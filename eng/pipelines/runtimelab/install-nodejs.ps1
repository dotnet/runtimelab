$InstallPath = $Args[0]
$NodeJSVersion = "v18.16.0"
$NodeJSInstallName = "node-$NodeJSVersion-win-x64"
$NodeJSZipName = "$NodeJSInstallName.zip"

if (!(Test-Path $InstallPath))
{
    mkdir $InstallPath
}
Invoke-WebRequest -Uri "https://nodejs.org/dist/$NodeJSVersion/$NodeJSZipName" -OutFile "$InstallPath\$NodeJSZipName"

Expand-Archive -LiteralPath "$InstallPath\$NodeJSInstallName.zip" -DestinationPath $InstallPath -Force

$NodeJSExePath = "$InstallPath\$NodeJSInstallName\node.exe"
if (!(Test-Path $NodeJSExePath))
{
    Write-Error "Did not find NodeJS at: '$NodeJSExePath'"
    exit 1
}

Write-Host Setting NODEJS_EXECUTABLE to $NodeJSExePath
Write-Host "##vso[task.setvariable variable=NODEJS_EXECUTABLE]$NodeJSExePath"
