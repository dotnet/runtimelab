#!/bin/bash

mkdir "$1" 2>/dev/null
cd "$1"

git clone https://github.com/emscripten-core/emsdk.git

cd emsdk
# Checkout a known good version to avoid a random break when emscripten changes the top of tree.
git checkout 37b85e9

python emsdk.py install 3.1.47 || exit 1
./emsdk activate 3.1.47 || exit 1

# We key off of this variable in the common/build.ps1 script.
echo "##vso[task.setvariable variable=NATIVEAOT_CI_WASM_BUILD_EMSDK_PATH]$PWD"
