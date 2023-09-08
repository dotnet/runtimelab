# Mint 🍃

## Project structure

There is a new managed assembly [`System.Private.Mint`](../System.Private.Mint/src/) and a native library `libmint.a` (in [Runtme/mint](../Runtime/mint/))

The managed build defines `FEATURE_MINT` (for example to build CoreLib with the right bits of reflection included) in all the nativeaot managed projects, and also `MINT_IMPLEMENTATION` in `System.Private.Mint`.

The native build defines `NATIVEAOT_MINT` which can be used to conditionally compile Mono/NativeAOT code.  There are Mono shims
in `Runtime/mint/inc/monoshim` to make the C compiler happy.

**FIXME** `config.h` - The Mono build auto-generates a `config.h` file based on some `autoconf`-style probing of the system.  In the interest of expediency, we will have a pre-generated `monoshim/config-osx-arm64.h` with the relevant defines, and as a consequence
the project only builds on Apple M1 machines.

## Building

```console
./build.sh clr.aot+libs+packs -rc Debug /p:BuildNativeAOTRuntimePack=true
```

For fast iteration you can do

```
./build.sh clr.aot -rc Debug -b
```

but this will not update the runtime packs for trying it out.

## Running

Set `/p:UseInterpreter=true` in the sample .csproj

