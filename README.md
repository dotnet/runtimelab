# CoreCLR Interpreter

See official dotnet/runtime [README.md](https://github.com/dotnet/runtime/blob/main/README.md).

## Build

Pass the following flags to the `build.cmd` or `build.sh` file depending on the target platform:

> `-s clr+libs+clr.hosts -lc Release -rc Debug`

Construct the `CORE_ROOT` environment, including the `corerun` testing binary:

Windows:

> `src\tests\build.cmd x64 Debug CoreCLR GenerateLayoutOnly /p:LibrariesConfiguration=Release`

Linux/macOS:

> `./src/tests/build.sh arm64 Debug CoreCLR GenerateLayoutOnly -p:LibrariesConfiguration=Release`

## Running

The `corerun` binary accepts a `dotenv` file format. The following environment variables are recommended to run the interpreter.

The following environment variables can be pasted into a file and passed to the `corerun` binary using the `-e` flag.

```
DOTNET_TieredCompilation=0
DOTNET_ReadyToRun=0
DOTNET_InterpreterDoLoopMethods=1
DOTNET_InterpreterFallback=0
DOTNET_ForceInterpreter=1
DOTNET_InterpreterUseCaching=0
DOTNET_InterpreterHWIntrinsicsIsSupportedFalse=1
```

The following are helpful in debugging hangs and the "current state" of the interpreter.

```
DOTNET_TraceInterpreterEntries=1
DOTNET_TraceInterpreterIL=1
```

Many verification steps assume the following is disabled.
`DOTNET_InterpreterLooseRules=0`

## Running IL scenarios

After building CoreCLR, the following can be executed:

> `.\artifacts\bin\coreclr\windows.x64.Debug\ilasm.exe /EXE /OUTPUT=.\artifacts\tmp\App.dll App.il`