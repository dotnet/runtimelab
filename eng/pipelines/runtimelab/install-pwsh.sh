#!/usr/bin/env bash

mkdir -p $1

cd $1

curl -L -o powershell.tar.gz https://github.com/PowerShell/PowerShell/releases/download/v7.3.12/powershell-7.3.12-linux-x64.tar.gz

# Create the target folder where powershell will be placed
mkdir powershell7

# Expand powershell to the target folder
tar zxf powershell.tar.gz -C powershell7

# Set execute permissions
chmod +x powershell7/pwsh

echo setting PATH $PATH:$1/powershell7
echo '##vso[task.setvariable variable=PATH]'$PATH:$1/powershell7
