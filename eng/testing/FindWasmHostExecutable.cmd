@echo off

set __WasmBinaryPathWithoutExtension=%~pdn1

if exist "%__WasmBinaryPathWithoutExtension%.js" (
  set __WasmBinaryExtension=.js
) else if exist "%__WasmBinaryPathWithoutExtension%.mjs" (
  set __WasmBinaryExtension=.mjs
) else if exist "%__WasmBinaryPathWithoutExtension%.wasm" (
  set __WasmBinaryExtension=.wasm
) else (
  exit /b
)

if "%__WasmBinaryExtension%" == ".wasm" (
  if "%WASMER_EXECUTABLE%" == "" (
    :: When running tests locally, assume wasmer is in PATH.
    set WASMER_EXECUTABLE=wasmer
  )

  set WASM_HOST_EXECUTABLE="!WASMER_EXECUTABLE!" --
) else (
  if "%NODEJS_EXECUTABLE%" == "" (
    :: When running tests locally, assume NodeJS is in PATH.
    set NODEJS_EXECUTABLE=node
  )

  set WASM_HOST_EXECUTABLE="!NODEJS_EXECUTABLE!" --stack-trace-limit=100
)

set WASM_BINARY_TO_EXECUTE=%__WasmBinaryPathWithoutExtension%%__WasmBinaryExtension%

if not defined WASM_HOST_EXECUTABLE (
  echo Failed to find the host to execute %WASM_BINARY_TO_EXECUTE%
  exit /b 1
)
