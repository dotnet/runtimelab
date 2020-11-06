Set-StrictMode -Version 'Latest'
$PSDefaultParameterValues['*:ErrorAction'] = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Root directory of the project.
$RootDir = Split-Path $PSScriptRoot -Parent
$NuGetPath = Join-Path $RootDir "nuget"

# Well-known location for clog packages.
$ClogDownloadUrl = "https://github.com/microsoft/CLOG/releases/download/v0.1.2"
$ClogVersion = "0.1.2"

function Install-ClogTool {
    param($ToolName)
    New-Item -Path $NuGetPath -ItemType Directory -Force | Out-Null
    $NuGetName = "$ToolName.$ClogVersion.nupkg"
    $NuGetFile = Join-Path $NuGetPath $NuGetName
    try {
        if (!(Test-Path $NuGetFile)) {
            Write-Host "Downloading $NuGetName"
            Invoke-WebRequest -Uri "$ClogDownloadUrl/$NuGetName" -OutFile $NuGetFile
        }
        $listOfTools = (dotnet tool list -g | findstr /spin /c:"$ToolName ")
        if ($null -eq $listOfTools)
        {
            Write-Host "Installing: $NuGetName"
            dotnet tool install --global --add-source $NuGetPath $ToolName
        }
        elseif ($null -eq ($listOfTools | findstr /spin /c:"$ClogVersion")){
            Write-Host "Updating: $NuGetName"
            dotnet tool update --global --add-source $NuGetPath $ToolName
        }
        if (!$?) { exit 1 }

    } catch {
        Write-Warning "Clog could not be installed. Building with logs will not work"
        Write-Warning $_
    }
}

Install-ClogTool "Microsoft.Logging.CLOG"
Install-ClogTool "Microsoft.Logging.CLOG2Text.Windows"
