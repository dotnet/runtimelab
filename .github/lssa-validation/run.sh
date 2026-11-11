#!/usr/bin/env bash
set -euo pipefail

root=$PWD
work="$RUNNER_TEMP/lssa-3053"
mkdir -p "$work/repro" "$EVIDENCE"
stage=initialization
result="No compiler verdict"
report() {
    local status=$?
    {
        printf '## LSSA #3053: %s\n\n' "$result"
        printf -- '- Revision: `%s`\n- Last stage: %s\n- Exit status: %s\n' "$(git -C "$root" rev-parse HEAD)" "$stage" "$status"
        printf '\nThe report artifact includes available build logs, the raw compiler dump, and the checker output.\n'
        if [[ -s "$EVIDENCE/checker.txt" ]]; then
            printf '\n```text\n'
            cat "$EVIDENCE/checker.txt"
            printf '```\n'
        fi
    } > "$EVIDENCE/report.md"
}
trap report EXIT

logged() {
    local log=$1
    shift
    if "$@" > "$EVIDENCE/$log.log" 2>&1; then
        tail -n 10 "$EVIDENCE/$log.log"
    else
        local status=$?
        tail -n 100 "$EVIDENCE/$log.log"
        return "$status"
    fi
}

stage="LLVM 18.1.3 setup"
logged llvm pwsh -NoProfile -File eng/pipelines/runtimelab/install-llvm.ps1 -CloneDir "$work" -Configs Release -CI
export LLVM_CMAKE_CONFIG="$work/llvm-project/build-release/lib/cmake/llvm"
export LLVM_CMAKE_CONFIG_DEBUG="$LLVM_CMAKE_CONFIG"
export LLVM_DIR="$LLVM_CMAKE_CONFIG"

stage="Emscripten setup"
logged emsdk pwsh -NoProfile -File eng/pipelines/runtimelab/install-emscripten.ps1 -InstallDir "$work"
set +u
source "$work/emsdk/emsdk_env.sh"
set -u

stage="Debug host LLVM JIT and ILC build"
logged host ./build.sh clr.llvmjit+clr.aot -a x64 -c Debug
stage="Debug browser-wasm runtime and libraries build"
logged wasm ./build.sh clr.aot+libs -a wasm -os browser -c Debug
stage="Package the browser target and x64 compiler"
logged target-pack ./build.sh nativeaot.packages -a wasm -os browser -c Debug
logged host-pack ./build.sh libs+nativeaot.packages -a x64 -c Debug

stage="Prepare optimized repro"
export DOTNET_ROOT="$root/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export MSBuildEnableWorkloadResolver=false
export NUGET_PACKAGES="$work/nuget"
python3 "$root/.github/lssa-validation/prepare.py" "$root" "$work/repro"
cd "$work/repro"
logged restore dotnet restore --configfile NuGet.Config
logged publish dotnet publish --no-restore -c Release

stage="Replay ILC with the patched Debug LLVM JIT"
rsp=$(find obj -name '*.ilc.rsp' -print -quit)
test -n "$rsp"
jit=$(find "$root/artifacts/bin/coreclr" -path '*/linux.x64.Debug/*' -name 'libclrjit_universal_llvm32_x64.so' -print -quit)
ilc=$(find "$root/artifacts/bin/coreclr" -path '*/linux.x64.Debug/ilc/ilc' -type f -print -quit)
test -x "$ilc"
test -f "$jit"
cp "$rsp" "$EVIDENCE/repro.ilc.rsp"
printf 'ILC: %s\nJIT: %s\n' "$ilc" "$jit" > "$EVIDENCE/tool-paths.txt"
sha256sum "$ilc" "$jit" >> "$EVIDENCE/tool-paths.txt"
if ! logged patched-ilc "$ilc" "@$rsp" --jitpath "$jit" --codegenopt JitDump=Problem; then
    result="Patched compiler invocation failed"
    exit 1
fi
stage="Check the PHI and block-entry liveness evidence"
if python3 "$root/.github/lssa-validation/check.py" "$EVIDENCE/patched-ilc.log" | tee "$EVIDENCE/checker.txt"; then
    result="Patched compiler passed the repro"
else
    result="Repro failed or did not exercise the expected PHI"
    exit 1
fi
