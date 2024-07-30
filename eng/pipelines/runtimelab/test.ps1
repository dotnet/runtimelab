[CmdletBinding(PositionalBinding=$false)]
param(
    [switch]$CI
)

$ErrorActionPreference="Stop"

if ($CI) {
    $RepoDir = Split-path $PSScriptRoot | Split-Path | Split-Path
       Write-Host $RepoDir

    bash -c "build_arch=amd64 compiler=clang source $RepoDir/eng/common/native/init-compiler.sh && set | grep -e CC -e CXX" |
      ForEach-Object {
       # Split the "<name>=<value>" line into the variable's name and value.
       $name, $value = $_ -split '=', 2
       # Define it as a process-level environment variable in PowerShell.
       Set-Content ENV:$name $value
       Write-Host $name $value
     }
}
