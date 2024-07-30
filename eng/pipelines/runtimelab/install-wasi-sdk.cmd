mkdir "%1" 2>nul
cd /D "%1"

echo Installing Wasi SDK

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -File "%~dp0install-wasi-sdk.ps1" -InstallDir .
if %errorlevel% NEQ 0 goto fail

echo Setting WASI_SDK_PATH to %1\wasi-sdk
echo ##vso[task.setvariable variable=WASI_SDK_PATH]%1\wasi-sdk

exit /b 0

fail:
echo "Failed to install wasi sdk"
exit /b 1
