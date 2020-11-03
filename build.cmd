@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\Build.ps1""" -warnAsError:$false -restore -build %*"
exit /b %ErrorLevel%