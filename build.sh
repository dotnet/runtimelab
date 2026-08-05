#!/usr/bin/env bash
# ZeroGC.rs is a native Rust cdylib crate - there is no managed (dotnet/runtime)
# code to restore/build here, so this simply drives `cargo build` instead of
# Arcade's eng/common/build.sh (msbuild) orchestrator.
# Recognizes the -c/--configuration <Debug|Release> flag used by CI (see
# eng/pipelines/common/templates/build-job.yml); all other Arcade-style flags
# (-ci, -restore, -pack, -test, /p:...) are accepted and ignored.
set -euo pipefail

source="${BASH_SOURCE[0]}"
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

config=Release
while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      config="$2"
      shift 2
      ;;
    *)
      shift
      ;;
  esac
done

manifest="$scriptroot/src/ZeroGC.rs/Cargo.toml"
if [[ "${config,,}" == "release" ]]; then
  cargo build --release --manifest-path "$manifest"
else
  cargo build --manifest-path "$manifest"
fi
