#!/usr/bin/env bash

ScriptPath="$(cd "$(dirname "$0")"; pwd -P)"

# Clone the LLVM repo
pushd "$1" || exit 1
if [ ! -d llvm ]; then
  git clone -b release_50 https://github.com/llvm-mirror/llvm.git || exit 1
fi

# Clean the tree and apply the patch
cd llvm && git checkout -- . || exit 1
git apply "$ScriptPath/llvm.patch" || exit 1

# Add ObjWriter files
rsync -av --delete "$ScriptPath/" tools/ObjWriter || exit 1

# Configure and build
[ -d build ] || mkdir build
cd build || exit 1

cmake ../ -DCMAKE_BUILD_TYPE=Release -DLLVM_OPTIMIZED_TABLEGEN=1 -DHAVE_POSIX_SPAWN=0 -DLLVM_ENABLE_PIC=1 -DLLVM_BUILD_TESTS=0 -DLLVM_ENABLE_DOXYGEN=0 -DLLVM_INCLUDE_DOCS=0 -DLLVM_INCLUDE_TESTS=0 -DLLVM_TARGETS_TO_BUILD="ARM;X86;AArch64" -DCMAKE_INSTALL_PREFIX=install || exit 1
cmake --build . -j 10 --config Release --target install || exit 1
