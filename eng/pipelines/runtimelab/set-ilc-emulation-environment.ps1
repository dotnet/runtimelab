[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$Arch
)
$ErrorActionPreference="Stop"

if ($IsWindows)
{
    Write-Error "Windows ILC emulation not implemented"
}
else
{
    $QemuArch = $Arch -eq "arm64" ? "aarch64" : $Arch
    $IlcLauncher = "qemu-$QemuArch -L $env:ROOTFS_DIR"
    Write-Host "Setting `$(IlcLauncher) to '$IlcLauncher'"
    Write-Host "Setting `$(IlcHostArch) to '$Arch'"
    Write-Host "##vso[task.setvariable variable=IlcLauncher]$IlcLauncher"
    Write-Host "##vso[task.setvariable variable=IlcHostArch]$Arch"
}
