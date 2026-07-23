#!/usr/bin/env bash
# build-linux.sh - Builds libZeroGC.so (standalone CoreCLR GC) on Linux using clang++ directly.
#
# Mirrors build.ps1 (the Windows build), but invokes clang++ instead of cl.exe/link.exe
# and defines the Unix-side host/target macros instead of the Windows ones.
#
# Requires:
#   - clang++ (or g++) with C++17 support.
#   - A local clone of dotnet/runtime, used ONLY as a read-only header/reference
#     dependency (nothing under it is built or modified by this script).
#
# Usage:
#   ./build-linux.sh [--runtime-repo /path/to/runtime] [--configuration Release|Debug] [--output-subdir -net11]
#
set -euo pipefail

RUNTIME_REPO="/mnt/c/github/runtime"
CONFIGURATION="Release"
CXX="${CXX:-clang++}"
# Optional suffix appended to the "Configuration" obj subfolder name (e.g.
# "-net11"), mirroring build.ps1's -OutputSubDir - lets callers building
# against multiple, ABI-incompatible reference runtime checkouts (net10.0 GA
# vs net11.0 preview) avoid clobbering each other's output when run back to
# back against the same native/ source tree.
OUTPUT_SUBDIR=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --runtime-repo) RUNTIME_REPO="$2"; shift 2 ;;
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        --output-subdir) OUTPUT_SUBDIR="$2"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RT="$RUNTIME_REPO/src/coreclr"
NATIVE_ROOT="$RUNTIME_REPO/src/native"

if [[ ! -d "$RT" ]]; then
    echo "Could not find dotnet/runtime checkout at '$RUNTIME_REPO' (expected '$RT' to exist)." >&2
    exit 1
fi

OBJ_DIR="$SRC_DIR/obj-linux/$CONFIGURATION$OUTPUT_SUBDIR"
mkdir -p "$OBJ_DIR"

INCLUDES=(
    "-I$SRC_DIR"
    "-I$RT/gc"
    "-I$RT/gc/env"
    "-I$RT/inc"
    "-I$NATIVE_ROOT"
)

DEFS=(
    -DHOST_UNIX -DHOST_64BIT -DHOST_AMD64
    -DTARGET_UNIX -DTARGET_AMD64 -DTARGET_64BIT -DBIT64
    -DFEATURE_STANDALONE_GC -DBUILD_AS_STANDALONE
    -DFEATURE_MANUALLY_MANAGED_CARD_BUNDLES
)

if [[ "$CONFIGURATION" == "Release" ]]; then
    DEFS+=(-DNDEBUG)
    OPT_FLAGS=(-O2)
else
    DEFS+=(-D_DEBUG)
    OPT_FLAGS=(-O0 -g)
fi

SOURCE_FILES=("$SRC_DIR/dllmain.cpp" "$SRC_DIR/ZeroGCHeap.cpp" "$SRC_DIR/ZeroGCHandles.cpp")
OUT_SO="$OBJ_DIR/libZeroGC.so"

echo "Compiling ZeroGC ($CONFIGURATION) for Linux..."
# Security mitigation required for any Microsoft-shipped native binary
# (mirrors dotnet/runtime's eng/native/configurecompiler.cmake, which sets
# -fstack-protector / -fstack-protector-strong for GCC/Clang builds).
SEC_FLAGS=(-fstack-protector-strong)
"$CXX" -shared -fPIC -fvisibility=hidden -std=c++17 -pthread \
    "${OPT_FLAGS[@]}" "${SEC_FLAGS[@]}" "${INCLUDES[@]}" "${DEFS[@]}" \
    -o "$OUT_SO" "${SOURCE_FILES[@]}"

if [[ ! -f "$OUT_SO" ]]; then
    echo "Build failed: $OUT_SO was not produced." >&2
    exit 1
fi

echo "Built: $OUT_SO"
