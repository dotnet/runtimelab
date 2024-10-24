#!/usr/bin/env bash

function InstallWorkloads {
    # Check if dotnet is installed
    if ! command -v dotnet &> /dev/null
    then
        echo "dotnet could not be found. Aborting..."
        exit 1
    else
        echo "dotnet is installed at $(command -v dotnet)."
    fi

    dotnet workload install maccatalyst

    if [[ $? != 0 ]]; then
        Write-PipelineTelemetryError -category 'InitializeToolset' "Failed to install workloads."
        ExitWithExitCode 1
    fi

    echo "Workloads installed successfully."

    return 0
}

InstallWorkloads
