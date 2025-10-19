# When publishing packages in CI, the full closure has duplicates. For example,
# both win-x64/wasi and win-x64/browser produce the "runtime.win-x64.*" package,
# Similarly with win-x64/wasi and linux-x64/wasi producing "runtime.wasi-wasm.*".
# The publishing system does not tolerate duplicates, so we remove them before
# the upload step. This allows us to still have all the packages available for
# testing on all CI platforms.
[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$Config,
    [string]$HostArch,
    [string]$TargetOS,
    [string]$TargetArch
)
$ErrorActionPreference="Stop"

$RepoRoot = [IO.Path]::GetFullPath("../../..", $PSScriptRoot)
if (!(Test-Path $RepoRoot/LICENSE.TXT))
{
    Write-Error "Unexpected repository root: '$RepoRoot'"
}

# Must be kept in sync with "upload-intermediate-artifacts-step.yml".
$PackagesPath = Join-Path $RepoRoot "artifacts" "packages" $Config "Shipping"

if (!(Test-Path $PackagesPath))
{
    Write-Host "'$PackagesPath' does not exist, exiting"
    exit
}

$BuildHostRid = [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
$HostRid = $BuildHostRid.Substring(0, $BuildHostRid.LastIndexOf("-")) + "-" + $HostArch
$PublishTargetPackages = $HostRid -eq "win-x64"
if (!$PublishTargetPackages)
{
    $TargetRid = "$TargetOS-$TargetArch"
    $TargetPackagePattern = "runtime.$TargetRid.Microsoft.DotNet.ILCompiler.LLVM.*.nupkg"
    Write-Host "Removing ${TargetPackagePattern}:"
    Get-ChildItem $PackagesPath/$TargetPackagePattern | Write-Host
    Remove-Item -Force $PackagesPath/$TargetPackagePattern
}

$PublishHostPackages = $TargetOS -eq "wasi"
if (!$PublishHostPackages)
{
    $HostPackagePattern = "runtime.$HostRid.Microsoft.DotNet.ILCompiler.LLVM.*.nupkg"
    Write-Host "Removing ${HostPackagePattern}:"
    Get-ChildItem $PackagesPath/$HostPackagePattern | Write-Host
    Remove-Item -Force $PackagesPath/$HostPackagePattern
}

Write-Host "Final package set:"
Get-ChildItem $PackagesPath/* | Write-Host

