#!/usr/bin/env bash

exename=$(basename "$1" .wasm)
exename=$(basename "$exename" .dll)
exename=$(basename "$exename" .js)
dirname=$(dirname "$1")

node="node --stack_trace_limit=100"

if [ -e "${dirname}/${exename}.js" ]; then
  WASM_HOST_EXECUTABLE=$node
  WASM_BINARY_TO_EXECUTE="${dirname}/${exename}.js"
elif [ -e "${dirname}/main.js" ]; then
  WASM_HOST_EXECUTABLE=$node
  WASM_BINARY_TO_EXECUTE="${dirname}/main.js"
elif [ -e "${dirname}/${exename}.mjs" ]; then
  WASM_HOST_EXECUTABLE=$node
  WASM_BINARY_TO_EXECUTE="${dirname}/${exename}.mjs"
elif [ -e "${dirname}/main.mjs" ]; then
  WASM_HOST_EXECUTABLE=$node
  WASM_BINARY_TO_EXECUTE="${dirname}/main.mjs"
elif [ -e "${dirname}/${exename}.wasm" ]; then
  if [ -z "$WASMTIME_EXECUTABLE" ]; then
    WASMTIME_EXECUTABLE=wasmtime
  fi
  WASM_HOST_EXECUTABLE="$WASMTIME_EXECUTABLE run -S http"
  WASM_BINARY_TO_EXECUTE="${dirname}/${exename}.wasm"
fi
