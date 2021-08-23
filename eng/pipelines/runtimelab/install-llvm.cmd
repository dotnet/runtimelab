mkdir "%1" 2>nul
cd /D "%1"

set RepoRoot=%2\

set
:: Set CMakePath by evaluating the output from set-cmake-path.ps1
call "%RepoRoot%eng\native\init-vs-env.cmd wasm" || exit /b 1
echo Using CMake at "%CMakePath%"

set

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0install-llvm.ps1""" %*"
if %errorlevel% NEQ 0 goto fail

echo setting LLVM_CMAKE_CONFIG to %1\llvm-11.0.0.src\build
echo "##vso[task.setvariable variable=LLVM_CMAKE_CONFIG]%1\llvm-11.0.0.src\build"

exit /b 0

fail:
echo "Failed to install llvm"
exit /b 1
