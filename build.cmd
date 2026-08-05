@echo off
REM ZeroGC.rs is a native Rust cdylib crate - there is no managed (dotnet/runtime)
REM code to restore/build here, so this simply drives `cargo build` instead of
REM Arcade's eng\common\Build.ps1 (msbuild) orchestrator.
REM Recognizes the -c/--configuration <Debug|Release> flag used by CI
REM (see eng\pipelines\common\templates\build-job.yml); all other Arcade-style
REM flags (-ci, -restore, -pack, -test, /p:...) are accepted and ignored.
setlocal enabledelayedexpansion
set CONFIG=Release

:parse
if "%~1"=="" goto :afterparse
if /I "%~1"=="-c" (
    set CONFIG=%~2
    shift
    shift
    goto :parse
)
if /I "%~1"=="--configuration" (
    set CONFIG=%~2
    shift
    shift
    goto :parse
)
shift
goto :parse

:afterparse
set MANIFEST=%~dp0src\ZeroGC.rs\Cargo.toml
if /I "%CONFIG%"=="Release" (
    cargo build --release --manifest-path "%MANIFEST%"
) else (
    cargo build --manifest-path "%MANIFEST%"
)
exit /b %ErrorLevel%