#!/usr/bin/env bash

ScriptDir="$(cd "$(dirname "$0")"; pwd -P)"
ArtifactsDir="$1"
RepoRoot="$2"
BuildArch="$3"
TargetArch="$4"
BuildType="${5:-Release}"

# Check that we have enough arguments
if [ $# -lt 4 ]; then
    echo "Usage: $(basename $0) ArtifactsDir RepoRoot BuildArch TargetArch [BuildType]"
    exit 1
fi

if [ "$TargetArch" != "$BuildArch" ]; then
    echo "Error: Cross-building of objwriter is not supported"
    exit 1
fi

cd "$ArtifactsDir" || exit 1
PatchApplied=0

if [ ! -d llvm ]; then
    # Clone the LLVM repo
    git clone --depth 1 -b release_50 https://github.com/llvm-mirror/llvm.git || exit 1
    cd llvm || exit 1
else
    # Check whether the current diff is the same as the patch
    cd llvm || exit 1
    mkdir -p build
    DiffFile="build/llvm_$RANDOM.patch"
    git diff --full-index >"$DiffFile" || exit 1
    cmp -s "$DiffFile" "$ScriptDir/llvm.patch"
    if [ $? -eq 0 ]; then
        # The current diff is the same as the patch
        rm "$DiffFile"
        PatchApplied=1
    else
        echo "LLVM changes are saved to $PWD/$DiffFile and overwritten with $ScriptDir/llvm.patch"
    fi
fi

if [ "$PatchApplied" -ne 1 ]; then
    # Clean the tree and apply the patch
    git checkout -- . || exit 1
    git apply "$ScriptDir/llvm.patch" || exit 1
fi

# Configure and build objwriter
mkdir -p "build/$TargetArch"
cd "build/$TargetArch" || exit 1

# Do not use the -S and -B options to support older CMake versions
cmake ../../ \
    -DCMAKE_TOOLCHAIN_FILE="$ScriptDir/toolchain.cmake" \
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
