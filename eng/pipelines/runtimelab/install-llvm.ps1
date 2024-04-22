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
    $LlvmProjectTag = "llvmorg-18.1.3"
    $DepthOption = if ($CI) {"--depth","1"} {}
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
    mkdir $BuildDirName -Force

    $BuildDirPath = "$pwd/$BuildDirName"
    $SourceDirName = "$pwd/llvm"
    popd

    $CmakeConfigureCommandLine = "-G", "Visual Studio 17 2022", "-DLLVM_INCLUDE_BENCHMARKS=OFF", "-Thost=x64"
    $CmakeConfigureCommandLine += "-S", $SourceDirName, "-B", $BuildDirPath
    if ($Config -eq "Release")
    {
        $LlvmConfig = "Release"
        $CmakeConfigureCommandLine += "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded"
    }
    else
    {
        $LlvmConfig = "Debug"
        $CmakeConfigureCommandLine += "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreadedDebug"
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
        $LlvmCmakeConfigEnvVarName = if ($Config -eq "Release") {"LLVM_CMAKE_CONFIG_RELEASE"} {"LLVM_CMAKE_CONFIG_DEBUG"}
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
