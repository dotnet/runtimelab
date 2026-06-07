[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$CloneDir = $null,
    [ValidateSet("Debug","Release","Checked")][string[]]$Configs = @("Debug","Release"),
    [string]$Path = "llvm-project",
    [string]$Arch = $null,
    [switch]$CI,
    [switch]$NoClone,
    [switch]$NoBuild
)
$ErrorActionPreference="Stop"

if (!(gcm git -ErrorAction SilentlyContinue))
{
   Write-Error "Unable to find 'git' in PATH"
}
if (!(gcm cmake -ErrorAction SilentlyContinue))
{
   Write-Error "Unable to find 'cmake' in PATH"
}

if ($CloneDir)
{
    New-Item -ItemType Directory -Path $CloneDir -Force
    Set-Location -Path $CloneDir
}

if (!$Arch)
{
    $Arch = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
}

$LlvmProjectTag = "llvmorg-18.1.3"
if ($NoClone)
{
    if (!(Test-Path $Path))
    {
        Write-Error "$Path repository not present in the current directory"
    }

    pushd $Path
    git checkout $LlvmProjectTag
    popd
    if ($LastExitCode -ne 0)
    {
        Write-Error "git checkout failed (fetch the missing tag with 'git fetch --all --tags')"
    }
}
else
{
    $DepthOption = if ($CI) {"--depth","1"} else {}
    git clone https://github.com/llvm/llvm-project $Path --branch $LlvmProjectTag $DepthOption
}

# There is no [C/c]hecked LLVM config, so change to Debug
foreach ($Config in $Configs | % { if ($_ -eq "Checked") { "Debug" } else { $_ } } | Select-Object -Unique)
{
    pushd $Path
    $BuildDirName = "build-$($Config.ToLower())"
    New-Item -ItemType Directory $BuildDirName -Force

    $BuildDirPath = "$pwd/$BuildDirName"
    $SourceDirName = "$pwd/llvm"
    popd

    $LlvmConfig = $Config -eq "Release" ? "Release" : "Debug"
    $CmakeConfigureCommandLine =
        "-G", ($IsWindows ? "Visual Studio 18 2026" : "Unix Makefiles"),
        "-S", $SourceDirName,
        "-B", $BuildDirPath,
        "-DLLVM_INCLUDE_BENCHMARKS=OFF",
        "-DLLVM_ENABLE_TERMINFO=0",
        "-DLLVM_TARGETS_TO_BUILD=WebAssembly",
        "-DCMAKE_BUILD_TYPE=$LlvmConfig"
    if ($IsWindows)
    {
        $RuntimeLibrary = $LlvmConfig -eq "Release" ? "MultiThreaded" : "MultiThreadedDebug"
        $CmakeConfigureCommandLine += "-DCMAKE_MSVC_RUNTIME_LIBRARY=$RuntimeLibrary", "-Thost=x64"
    }
    elseif ($env:ROOTFS_DIR)
    {
        # This logic was copied from gen-buildsys.sh. Maybe we should just call it here directly?
        $RepoRoot = Split-Path $PSScriptRoot | Split-Path | Split-Path
        # We're assuming here that the sysroot is coming from our CI docker images.
        $TargetTriple = (Get-ChildItem $env:ROOTFS_DIR/usr/lib/gcc).Name

        bash -c "build_arch=$Arch compiler=clang source $RepoRoot/eng/common/native/init-compiler.sh && set | grep -e CC -e CXX -e LDFLAGS" |
            ForEach-Object {
                # Split the "<name>=<value>" line into the variable's name and value.
                $name, $value = $_ -Split '=', 2
                # Define it as a process-level environment variable in PowerShell.
                Set-Content ENV:$name $value
            }
        $env:TARGET_BUILD_ARCH = $Arch # Used by toolchain.cmake
        $CmakeConfigureCommandLine +=
            "-DCMAKE_TOOLCHAIN_FILE=$RepoRoot/eng/common/cross/toolchain.cmake",
            "-DLLVM_HOST_TRIPLE=$TargetTriple",
            "-DCMAKE_ASM_COMPILER_VERSION=18.1.2" # Workaround for https://gitlab.kitware.com/cmake/cmake/-/issues/22995.
    }

    Write-Host "Invoking CMake configure: 'cmake $CmakeConfigureCommandLine'"
    cmake @CmakeConfigureCommandLine || Write-Error "CMake configure failed"

    if (!$NoBuild)
    {
        $CmakeBuildCommandLine =
            "--build", $BuildDirPath,
            "--parallel", $([Environment]::ProcessorCount),
            "--config", $LlvmConfig,
            "--target", "LLVMCore", "LLVMBitWriter"

        Write-Host "Invoking Cmake build: 'cmake $CmakeBuildCommandLine'"
        cmake @CmakeBuildCommandLine || Write-Error "CMake build failed"
    }

    $LlvmCmakeConfigPath = "$BuildDirPath/lib/cmake/llvm"
    if ($CI)
    {
        $LlvmCmakeConfigEnvVarName = "LLVM_CMAKE_CONFIG"
    }
    else
    {
        $LlvmCmakeConfigEnvVarName = $Config -eq "Release" ? "LLVM_CMAKE_CONFIG_RELEASE" : "LLVM_CMAKE_CONFIG_DEBUG"
    }

    Write-Host "Setting $LlvmCmakeConfigEnvVarName to '$LlvmCmakeConfigPath'"
    if ($CI)
    {
        Write-Output "##vso[task.setvariable variable=$LlvmCmakeConfigEnvVarName]$LlvmCmakeConfigPath"
        # We need LLVM_DIR for Linux
        Write-Output "##vso[task.setvariable variable=LLVM_DIR]$LlvmCmakeConfigPath"
    }
    else
    {
        [Environment]::SetEnvironmentVariable($LlvmCmakeConfigEnvVarName, $LlvmCmakeConfigPath, "Process")
        if (![Environment]::GetEnvironmentVariable($LlvmCmakeConfigEnvVarName, "User"))
        {
            Write-Host "Also setting $LlvmCmakeConfigEnvVarName to '$LlvmCmakeConfigPath' for the user"
            [Environment]::SetEnvironmentVariable($LlvmCmakeConfigEnvVarName, $LlvmCmakeConfigPath, "User")
        }
    }
}
