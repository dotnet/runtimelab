mkdir "%1" 2>nul
cd /D "%1"

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0install-llvm.ps1""" %*"
if %errorlevel% NEQ 0 goto fail

exit /b 0

fail:
echo "Failed to install llvm"
exit /b 1
