mkdir "%1" 2>nul
cd /D "%1"

set RepoRoot=%2\

set
:: Set CMakePath by evaluating the output from set-cmake-path.ps1
call "%RepoRoot%src\coreclr\setup_vs_tools.cmd" || exit /b 1
for /f "delims=" %%a in ('powershell -NoProfile -ExecutionPolicy ByPass "& ""%RepoRoot%eng\native\set-cmake-path.ps1"""') do %%a
echo Using CMake at "%CMakePath%"

set

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0install-llvm.ps1""" %*"
if %errorlevel% NEQ 0 goto fail

exit /b 0

fail:
echo "Failed to install llvm"
exit /b 1
