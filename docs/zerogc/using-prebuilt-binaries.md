# Using a ZeroGC binary

This page is for people who want to try ZeroGC against their own app.

See [`../../src/ZeroGC/README.md`](../../src/ZeroGC/README.md) for what
ZeroGC is, how it's implemented, and its measured performance
characteristics. This page only covers *consuming* a built binary.

## 1. Build the right binary

**No prebuilt binaries are published for this experiment.** Any binary
officially distributed from `dotnet/runtimelab` needs to be built and
hosted on Microsoft's own infrastructure rather than a personal
account/fork - ZeroGC doesn't have that build plumbing (Arcade/official
Azure Pipelines integration producing a signed NuGet package) set up yet.

Build `ZeroGC.dll` (Windows) or `libZeroGC.so` (Linux) yourself following
the "Building ZeroGC.dll / libZeroGC.so" section of
[`../../src/ZeroGC/README.md`](../../src/ZeroGC/README.md#building-zerogcdll--libzerogcso),
picking the `dotnet/runtime` tag/branch that matches your app's target
runtime version (net10.0 GA vs. net11.0 preview - see
[Why per-version, and why it matters](#why-per-version-and-why-it-matters)
below for why this choice is a hard requirement, not just a
recommendation).

## 2. Place the binary next to your app

Copy the native binary you just built (see `native/build.ps1`'s or
`native/build-linux.sh`'s output path) into your app's published output
folder, next to `YourApp.dll`:

```
YourApp/
  YourApp.dll
  YourApp.runtimeconfig.json
  ZeroGC.dll          <-- copied here (Windows)
  ... (or libZeroGC.so on Linux)
```

## 3. Tell the runtime to use it

Set the `DOTNET_GCName` environment variable to the binary's file name
before launching your app:

```powershell
# Windows (PowerShell)
$env:DOTNET_GCName = "ZeroGC.dll"
dotnet YourApp.dll
```

```bash
# Linux (bash)
DOTNET_GCName=libZeroGC.so dotnet YourApp.dll
```

To go back to the regular (Workstation/Server) GC, unset the variable
(`Remove-Item Env:DOTNET_GCName` / `unset DOTNET_GCName`) or don't set it
at all.

`DOTNET_GCName` also works as the equivalent `runtimeconfig.json` knob
(`configProperties."System.GC.Name"`) or `AppContext` switch, if you'd
rather bake the setting into the app's publish output than set an
environment variable - see the
[runtime's own standalone-GC docs](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/garbage-collection.md)
for the full list of equivalent knobs.

## 4. Verify it's actually active (optional but recommended)

ZeroGC reports its own name back through `GC.GetConfigurationValues()`
(used internally by `dotnet-counters`/diagnostics tooling) as
`"ZeroGC"`/`libZeroGC.so`/`ZeroGC.dll` depending on platform. The
simplest sanity check is watching memory: with ZeroGC active, working-set
memory should grow monotonically and `GC.CollectionCount(0/1/2)` should
stay at `0` for the whole process lifetime, since ZeroGC never collects.

## Why per-version, and why it matters

CoreCLR's standalone-GC loader only rejects a GC whose reported major ABI
version is *older* than what the runtime expects - it does **not** verify
that the GC's assumptions about object layout (MethodTable header flags,
card-table encoding, etc.) still match the runtime it's loaded into. Those
assumptions **can and do change** between runtime versions without any ABI
version bump at all (ZeroGC's own code has hit this at least once already -
see `g_oldMethodTableFlags` in `src/ZeroGC/native/dllmain.cpp`).

That means **a `ZeroGC.dll`/`libZeroGC.so` built for net10.0 must only be
run with a net10.0 app - never with net11.0 or any other major version**,
even though the runtime's own loader won't stop you from trying. Using the
wrong version is very likely to crash or, worse, silently corrupt object
layout without an immediate crash. Always build against the `dotnet/runtime`
tag/branch matching your app's actual target runtime.
