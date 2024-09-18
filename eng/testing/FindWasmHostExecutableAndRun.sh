#!/usr/bin/env bash

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)

source $SCRIPT_DIR/FindWasmHostExecutable.sh "$1"

if [ -n "${WASM_HOST_EXECUTABLE}" ]; then
  shift
  echo $WASM_HOST_EXECUTABLE "$WASM_BINARY_TO_EXECUTE" "$@"
  $WASM_HOST_EXECUTABLE "$WASM_BINARY_TO_EXECUTE" "$@"
else
  echo WASM_HOST_EXECUTABLE not set.
  exit 1
fi
