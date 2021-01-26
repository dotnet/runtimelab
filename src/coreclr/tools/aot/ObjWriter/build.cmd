@echo off
setlocal

:: Clone the LLVM repo
pushd %1 || exit /b 1
if not exist llvm (
  git clone -b release_50 https://github.com/llvm-mirror/llvm.git || exit /b 1
)

:: Clean the tree and apply the patch
cd llvm && git restore . || exit /b 1
git apply "%~dp0llvm.patch" || exit /b 1

:: Copy ObjWriter files
robocopy /mir "%~dp0\" tools\ObjWriter
if %ErrorLevel% geq 8 exit /b %ErrorLevel%

:: Configure and build
if not exist build mkdir build
cd build || exit /b 1

:: Set CMakePath by evaluating the output from set-cmake-path.ps1
call "%2src\coreclr\setup_vs_tools.cmd" || exit /b 1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& ""%2eng\native\set-cmake-path.ps1"""') do %%a
echo Using CMake at "%CMakePath%"

"%CMakePath%" ../ -DCMAKE_BUILD_TYPE=Release -DLLVM_OPTIMIZED_TABLEGEN=1 -DHAVE_POSIX_SPAWN=0 -DLLVM_ENABLE_PIC=1 -DLLVM_BUILD_TESTS=0 -DLLVM_ENABLE_DOXYGEN=0 -DLLVM_INCLUDE_DOCS=0 -DLLVM_INCLUDE_TESTS=0 -DLLVM_TARGETS_TO_BUILD="ARM;X86;AArch64" -DCMAKE_INSTALL_PREFIX=install || exit /b 1
"%CMakePath%" --build . -j 10 --config Release --target install || exit /b 1
