@echo off
setlocal enabledelayedexpansion

call %~dp0FindWasmHostExecutable.cmd %1 || exit /b
if not defined WASM_BINARY_TO_EXECUTE (
  exit /b 1
)

echo !WASM_HOST_EXECUTABLE! %* && !WASM_HOST_EXECUTABLE! %*
