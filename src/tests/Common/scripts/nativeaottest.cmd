@rem This script is a bridge that allows running NativeAOT compiled executables instead of using corerun.
@rem
@rem To use this script, set the CLRCustomTestLauncher environment variable to the full path of this script.
@rem
@rem The .cmd files of the individual tests will call this script to launch the test.
@rem This script gets the following arguments
@rem 1. Full path to the directory of the test binaries (the test .cmd file is in there)
@rem 2. Filename of the test executable
@rem 3. - n. Additional arguments that were passed to the test .cmd

@rem File name of the test executable is the original assembly, so swap .dll for .exe
set __ExeFileName=%2
set __ExeFileName=%__ExeFileName:~0,-4%.exe

set __JsFileName=%2
set __JsFileName=%__JsFileName:~0,-4%.js
set __JsFilePath=%1\native\%__JsFileName%

set __WasmFileName=%2
set __WasmFileName=%__WasmFileName:~0,-4%.wasm
set __WasmFilePath=%1native\%__WasmFileName%

if exist %__JsFilePath% (
  if "%NODEJS_EXECUTABLE%" == "" (
    REM when running tests locally, assume NodeJS is in PATH
    set NODEJS_EXECUTABLE=node
  )

  %_DebuggerFullPath% !NODEJS_EXECUTABLE! --stack-trace-limit=100 %__JsFilePath% %3 %4 %5 %6 %7 %8 %9
) else if exist %__WasmFilePath% (
  if "%WASMER_EXECUTABLE%" == "" (
    REM when running tests locally, assume wasmer is in PATH
    set WASMER_EXECUTABLE=wasmer
  )

  %_DebuggerFullPath% !WASMER_EXECUTABLE! %__WasmFilePath% %3 %4 %5 %6 %7 %8 %9
) else (
  %_DebuggerFullPath% %1\native\%__ExeFileName% %3 %4 %5 %6 %7 %8 %9
)

