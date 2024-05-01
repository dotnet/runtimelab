[CmdletBinding(PositionalBinding=$false)]
param(
    [ValidateSet("Debug","Release")][string[]]$Configs = @("Debug","Release"),
    [switch]$CI,
    [switch]$NoClone,
    [switch]$NoBuild
)

$ErrorActionPreference="Stop"

if (!(gcm git -ErrorAction SilentlyContinue))
{
   Write-Error "Unable to find 'git' in PATH"
   exit 1
}
if (!(gcm cmake -ErrorAction SilentlyContinue))
{
   Write-Error "Unable to find 'cmake' in PATH"
   exit 1
}

if (!$NoClone)
{
    $LlvmProjectTag = "llvmorg-17.0.4"
    $DepthOption = if ($CI) {"--depth","1"} else {}
    git clone https://github.com/llvm/llvm-project --branch $LlvmProjectTag $DepthOption
}
elseif (!(Test-Path llvm-project))
{
    Write-Error "llvm-project repository not present in the current directory"
    exit 1
}

foreach ($Config in $Configs)
{
    pushd llvm-project
    $BuildDirName = "build-$($Config.ToLower())"
    if ($IsWindows)
    {
        mkdir $BuildDirName -Force
    }
    else
    {
        mkdir $BuildDirName --parents
    }

    $BuildDirPath = "$pwd/$BuildDirName"
    $SourceDirName = "$pwd/llvm"
    popd

    if ($IsWindows)
    {
        $generator="Visual Studio 17 2022"
    }
    else
    {
        $generator="Ninja"
    }

    $CmakeConfigureCommandLine = "-G", "$generator", "-DLLVM_INCLUDE_BENCHMARKS=OFF"
    $CmakeConfigureCommandLine += "-S", $SourceDirName, "-B", $BuildDirPath
    if ($Config -eq "Release")
    {
        $LlvmConfig = "Release"
	if ($IsWindows)
        {
            $CmakeConfigureCommandLine += "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded", "-Thost=x64"
        }
    }
    else
    {
        $LlvmConfig = "Debug"
	if ($IsWindows)
        {
            $CmakeConfigureCommandLine += "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreadedDebug", "-Thost=x64"
        }
    }
    $CmakeConfigureCommandLine += "-DCMAKE_BUILD_TYPE=$LlvmConfig"

    Write-Host "Invoking CMake configure: 'cmake $CmakeConfigureCommandLine'"
    cmake @CmakeConfigureCommandLine

    if (!$NoBuild)
    {
        Write-Host "Invoking CMake --build"
        cmake --build $BuildDirPath --config $LlvmConfig --target LLVMCore
        cmake --build $BuildDirPath --config $LlvmConfig --target LLVMBitWriter
    }

    $LlvmCmakeConfigPath = "$BuildDirPath/lib/cmake/llvm"
    if ($CI)
    {
        $LlvmCmakeConfigEnvVarName = "LLVM_CMAKE_CONFIG"
    }
    else
    {
        $LlvmCmakeConfigEnvVarName = if ($Config -eq "Release") {"LLVM_CMAKE_CONFIG_RELEASE"} else {"LLVM_CMAKE_CONFIG_DEBUG"}
    }

    Write-Host "Setting $LlvmCmakeConfigEnvVarName to '$LlvmCmakeConfigPath'"
    if ($CI)
    {
        Write-Output "##vso[task.setvariable variable=$LlvmCmakeConfigEnvVarName]$LlvmCmakeConfigPath"
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
