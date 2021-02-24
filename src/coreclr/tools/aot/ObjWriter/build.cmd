@echo off
setlocal

set "ScriptDir=%~dp0"
set "ArtifactsDir=%~1"
set "RepoRoot=%~2"
set "BuildType=%~3"
if "%BuildType%"=="" set "BuildType=RelWithDebInfo"

:: Clone the LLVM repo
pushd "%ArtifactsDir%" || exit /b 1
if not exist llvm (
  git clone --depth 1 -b release_50 https://github.com/llvm-mirror/llvm.git || exit /b 1
)

:: Clean the tree and apply the patch
cd llvm && git restore . || exit /b 1
git apply "%ScriptDir%llvm.patch" || exit /b 1

:: Set CMakePath by evaluating the output from set-cmake-path.ps1
call "%RepoRoot%src\coreclr\setup_vs_tools.cmd" || exit /b 1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& ""%RepoRoot%eng\native\set-cmake-path.ps1"""') do %%a
echo Using CMake at "%CMakePath%"

:: Configure and build
if not exist build mkdir build
cd build || exit /b 1

"%CMakePath%" ../ ^
  -DCMAKE_BUILD_TYPE=%BuildType% ^
  -DCMAKE_INSTALL_PREFIX=install ^
  -DLLVM_BUILD_TOOLS=0 ^
  -DLLVM_ENABLE_TERMINFO=0 ^
  -DLLVM_INCLUDE_UTILS=0 ^
  -DLLVM_INCLUDE_RUNTIMES=0 ^
  -DLLVM_INCLUDE_EXAMPLES=0 ^
  -DLLVM_INCLUDE_TESTS=0 ^
  -DLLVM_INCLUDE_DOCS=0 ^
  -DLLVM_TARGETS_TO_BUILD="AArch64;ARM;X86" ^
  -DLLVM_EXTERNAL_OBJWRITER_SOURCE_DIR="%ScriptDir%\" ^
  -DCORECLR_INCLUDE_DIR="%RepoRoot%src\coreclr\inc" ^
  || exit /b 1

"%CMakePath%" --build . --config %BuildType% --target objwriter -j 10 || exit /b 1
