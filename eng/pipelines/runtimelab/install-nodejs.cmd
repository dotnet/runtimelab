@echo OFF
echo Installing NodeJS

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0install-nodejs.ps1""" %*"

if %errorlevel% NEQ 0 (
  echo Failed to install NodeJS
  exit /b 1
)
