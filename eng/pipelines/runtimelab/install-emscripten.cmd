mkdir "%1" 2>nul
cd /D "%1"

git clone https://github.com/emscripten-core/emsdk.git

cd emsdk
rem Checkout a known good version to avoid a random break when emscripten changes the top of tree.
git checkout b4fd475

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0update-machine-certs.ps1""" %*"

call python emsdk.py install 3.1.23
if %errorlevel% NEQ 0 goto fail
call emsdk activate 3.1.23
if %errorlevel% NEQ 0 goto fail

rem We key off of this variable in the common/build.ps1 script.
echo Setting NATIVEAOT_CI_WASM_BUILD_EMSDK_PATH to %cd%
echo ##vso[task.setvariable variable=NATIVEAOT_CI_WASM_BUILD_EMSDK_PATH]%cd%

exit /b 0

fail:
echo "Failed to install emscripten"
exit /b 1
