mkdir "%1" 2>nul
cd /D "%1"

echo Installing Wasmer

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -File "%~dp0install-wasmer.ps1"
if %errorlevel% NEQ 0 goto fail

echo Setting WASMER_EXECUTABLE to %1\wasmer\bin\wasmer.exe
echo ##vso[task.setvariable variable=WASMER_EXECUTABLE]%1\wasmer\bin\wasmer.exe

exit /b 0

fail:
echo "Failed to install wasmer"
exit /b 1
