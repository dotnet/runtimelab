mkdir "%1" 2>nul
cd /D "%1"

git clone https://github.com/emscripten-core/emsdk.git

cd emsdk
rem checkout a known good version to avoid a random break when emscripten changes the top of tree.
git checkout 5ad9d72

powershell -NoProfile -NoLogo -ExecutionPolicy ByPass -command "& """%~dp0update-machine-certs.ps1""" %*"

set

dir C:\python3.7.0
call python emsdk.py install 2.0.12
if %errorlevel% NEQ 0 goto fail
call emsdk activate 2.0.8
if %errorlevel% NEQ 0 goto fail

exit /b 0

fail:
echo "Failed to install emscripten"
exit /b 1
