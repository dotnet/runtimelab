@if not defined _echo @echo off
rem
rem This file invokes cmake and generates the build system for windows.

setlocal

set argC=0
for %%x in (%*) do Set /A argC+=1

if %argC% lss 4 GOTO :USAGE
if %1=="/?" GOTO :USAGE

setlocal enabledelayedexpansion
set basePath=%~dp0
set __repoRoot=%~dp0..\..\
:: remove quotes
set "basePath=%basePath:"=%"
:: remove trailing slash
if %basePath:~-1%==\ set "basePath=%basePath:~0,-1%"

set __SourceDir=%1
set __IntermediatesDir=%2
set __VSVersion=%3
set __Arch=%4
set __CmakeGenerator=Visual Studio
set __UseEmcmake=0
if /i "%__Ninja%" == "1" (
    set __CmakeGenerator=Ninja
) else (
    if /i NOT "%__Arch%" == "wasm" (
        if /i "%__VSVersion%" == "vs2019" (set __CmakeGenerator=%__CmakeGenerator% 16 2019)
        if /i "%__VSVersion%" == "vs2017" (set __CmakeGenerator=%__CmakeGenerator% 15 2017)

        if /i "%__Arch%" == "x64" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A x64)
        if /i "%__Arch%" == "arm" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A ARM)
        if /i "%__Arch%" == "arm64" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A ARM64)
        if /i "%__Arch%" == "x86" (set __ExtraCmakeParams=%__ExtraCmakeParams% -A Win32)
    ) else (
        set __CmakeGenerator=NMake Makefiles
    )
)
echo gen-buildsys %__Arch%
if /i "%__Arch%" == "wasm" (
    if "%EMSDK%" == "" (
       echo Error: Should set EMSDK environment variable pointing to emsdk root.
       exit /B 1
    )

    if /i "%CMAKE_BUILD_TYPE%" == "debug" (
        set __ExtraCmakeParams=%__ExtraCmakeParams% -g -O0
    )
    set __ExtraCmakeParams=%__ExtraCmakeParams% "-DCMAKE_BUILD_TYPE=%CMAKE_BUILD_TYPE%" "-DCMAKE_TOOLCHAIN_FILE=%EMSDK%/upstream/emscripten/cmake/Modules/Platform/Emscripten.cmake" -DCLR_CMAKE_TARGET_ARCH=wasm -DCLR_CMAKE_TARGET_ARCH_WASM=1 -DCLR_CMAKE_HOST_ARCH=Windows_NT -DCLR_CMAKE_HOST_OS=Emscripten -DRUNTIME_FLAVOR=CoreClr -DCLR_CMAKE_HOST_UNIX_WASM=1 "-DCLR_ENG_NATIVE_DIR=%__repoRoot%\eng\native" "-DCMAKE_REPO_ROOT=%__repoRoot%" -DCLR_CMAKE_KEEP_NATIVE_SYMBOLS=1
    set __UseEmcmake=1
) else (
    set __ExtraCmakeParams=%__ExtraCmakeParams%  "-DCMAKE_SYSTEM_VERSION=10.0"
)

:loop
if [%5] == [] goto end_loop
set __ExtraCmakeParams=%__ExtraCmakeParams% %5
shift
goto loop
:end_loop

set __ExtraCmakeParams="-DCMAKE_INSTALL_PREFIX=%__CMakeBinDir%" "-DCLR_CMAKE_HOST_ARCH=%__Arch%" %__ExtraCmakeParams%

set __CmdLineOptionsUpToDateFile=%__IntermediatesDir%\cmake_cmd_line.txt
set __CMakeCmdLineCache=
if not "%__ConfigureOnly%" == "1" (
    REM MSBuild can't reload from a CMake reconfigure during build correctly, so only do this
    REM command-line up to date check for non-VS generators.
    if not "%__CmakeGenerator%" == "Visual Studio" (
        if exist "%__CmdLineOptionsUpToDateFile%" (
            set /p __CMakeCmdLineCache=<"%__CmdLineOptionsUpToDateFile%"
            REM Strip the extra space from the end of the cached command line
            if "!__ExtraCmakeParams!" == "!__CMakeCmdLineCache:~0,-1!" (
                echo The CMake command line is the same as the last run. Skipping running CMake.
                exit /B 0
            ) else (
                echo The CMake command line differs from the last run. Running CMake again.
                echo %__ExtraCmakeParams% > %__CmdLineOptionsUpToDateFile%
            )
        ) else (
            echo %__ExtraCmakeParams% > %__CmdLineOptionsUpToDateFile%
        )
    )
)

if /i "%__UseEmcmake%" == "1" (
    emcmake "%CMakePath%" %__ExtraCmakeParams% --no-warn-unused-cli -G "%__CmakeGenerator%" -B %__IntermediatesDir% -S %__SourceDir% 
) else (
    "%CMakePath%" %__ExtraCmakeParams% --no-warn-unused-cli -G "%__CmakeGenerator%" -B %__IntermediatesDir% -S %__SourceDir%
)
endlocal
exit /B %errorlevel%

:USAGE
  echo "Usage..."
  echo "gen-buildsys.cmd <path to top level CMakeLists.txt> <path to location for intermediate files> <VSVersion> <arch>"
  echo "Specify the path to the top level CMake file - <ProjectK>/src/NDP"
  echo "Specify the VSVersion to be used - VS2017 or VS2019"
  EXIT /B 1
