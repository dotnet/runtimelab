#!/usr/bin/env bash

ScriptDir="$(cd "$(dirname "$0")"; pwd -P)"
ArtifactsDir="$1"
RepoRoot="$2"
BuildType="${3:-Release}"

# Clone the LLVM repo
pushd "$ArtifactsDir" || exit 1
if [ ! -d llvm ]; then
  git clone --depth 1 -b release_50 https://github.com/llvm-mirror/llvm.git || exit 1
fi

# Clean the tree and apply the patch
cd llvm && git checkout -- . || exit 1
git apply "$ScriptDir/llvm.patch" || exit 1

# Configure and build
[ -d build ] || mkdir build
cd build || exit 1

cmake ../ \
  -DCMAKE_BUILD_TYPE="$BuildType" \
  -DCMAKE_INSTALL_PREFIX=install \
  -DLLVM_BUILD_TOOLS=0 \
  -DLLVM_ENABLE_TERMINFO=0 \
  -DLLVM_INCLUDE_UTILS=0 \
  -DLLVM_INCLUDE_RUNTIMES=0 \
  -DLLVM_INCLUDE_EXAMPLES=0 \
  -DLLVM_INCLUDE_TESTS=0 \
  -DLLVM_INCLUDE_DOCS=0 \
  -DLLVM_TARGETS_TO_BUILD="AArch64;ARM;X86" \
  -DLLVM_EXTERNAL_OBJWRITER_SOURCE_DIR="$ScriptDir" \
  -DCORECLR_INCLUDE_DIR="${RepoRoot}src/coreclr/inc" \
  || exit 1

cmake --build . --config "$BuildType" --target objwriter -j 10 || exit 1
