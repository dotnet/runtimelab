mkdir "%1" 2>nul
cd /D "%1"

set RepoRoot=%2\
set LlvmBuildConfig=%3
echo Building LLVM with config: %LlvmBuildConfig%

:: Set CMakePath by evaluating the output from set-cmake-path.ps1
call "%RepoRoot%eng\native\init-vs-env.cmd" wasm || exit /b 1
echo Using CMake at "%CMakePath%"
set PATH=%PATH%;%CMakePath%

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -File "%~dp0install-llvm.ps1" -Configs %LlvmBuildConfig% -CI
if %errorlevel% NEQ 0 goto fail
exit /b 0

fail:
echo "Failed to install llvm"
exit /b 1
