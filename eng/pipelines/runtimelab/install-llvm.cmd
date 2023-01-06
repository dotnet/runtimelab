mkdir "%1" 2>nul
cd /D "%1"

set RepoRoot=%2\

set buildConfig=%3
echo Building LLVM with config: %buildConfig%

set
:: Set CMakePath by evaluating the output from set-cmake-path.ps1
call "%RepoRoot%eng\native\init-vs-env.cmd" wasm || exit /b 1
echo Using CMake at "%CMakePath%"

set

REM There is no [C/c]hecked LLVM config, so change to Debug
if /I %buildConfig% EQU checked set buildConfig=Debug

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -File "%~dp0install-llvm.ps1" -buildConfig %buildConfig%
if %errorlevel% NEQ 0 goto fail

echo Setting LLVM_CMAKE_CONFIG to %1\llvm-15.0.6.src\build
echo ##vso[task.setvariable variable=LLVM_CMAKE_CONFIG]%1\llvm-15.0.6.src\build

exit /b 0

fail:
echo "Failed to install llvm"
exit /b 1
