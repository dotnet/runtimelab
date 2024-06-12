[CmdletBinding(PositionalBinding=$false)]
param(
    [string]$CloneDir = $null,
    [ValidateSet("Debug","Release","Checked")][string[]]$Configs = @("Debug","Release"),
    [switch]$CI,
    [switch]$NoClone,
    [switch]$NoBuild
)

$ErrorActionPreference="Stop"

# Set IsWindows if the version of Powershell does not already have it.
if (!(Test-Path variable:global:IsWindows)) 
{
    $IsWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
}
if (!(gcm git -ErrorAction SilentlyContinue))
{
   Write-Error "Unable to find 'git' in PATH"
}
if (!(gcm cmake -ErrorAction SilentlyContinue))
{
   Write-Error "Unable to find 'cmake' in PATH"
}

if ($CloneDir -ne $null)
{
    New-Item -ItemType Directory -Path $CloneDir -Force
    Set-Location -Path $CloneDir
}

$LlvmProjectTag = "llvmorg-18.1.3"
if ($NoClone)
{
    if (!(Test-Path llvm-project))
    {
        Write-Error "llvm-project repository not present in the current directory"
    }

    pushd llvm-project
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
    git clone https://github.com/llvm/llvm-project --branch $LlvmProjectTag $DepthOption
}

# There is no [C/c]hecked LLVM config, so change to Debug
foreach ($Config in $Configs | % { if ($_ -eq "Checked") { "Debug" } else { $_ } } | Select-Object -Unique)
{
    pushd llvm-project
    $BuildDirName = "build-$($Config.ToLower())"
    New-Item -ItemType Directory $BuildDirName -Force

    $BuildDirPath = "$pwd/$BuildDirName"
    $SourceDirName = "$pwd/llvm"
    popd

    if ($IsWindows)
    {
        $CmakeGenerator = "Visual Studio 17 2022"
    }
    else
    {
        $CmakeGenerator = "Unix Makefiles"
    }

    $CmakeConfigureCommandLine = "-G", "$CmakeGenerator", "-DLLVM_INCLUDE_BENCHMARKS=OFF"
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
    if ($LastExitCode -ne 0)
    {
        Write-Error "CMake configure failed"
    }

    if (!$NoBuild)
    {
        Write-Host "Invoking CMake --build"
        cmake --build $BuildDirPath --config $LlvmConfig --target LLVMCore LLVMBitWriter
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
