# Using a prebuilt ZeroGC binary

This page is for people who just want to try ZeroGC against their own app -
no C++ toolchain, no local `dotnet/runtime` checkout, no building anything.

See [`../../src/ZeroGC/README.md`](../../src/ZeroGC/README.md) for what
ZeroGC is, how it's implemented, and its measured performance
characteristics. This page only covers *consuming* a prebuilt binary.

## 1. Download the right zip

Prebuilt binaries are published as GitHub Release assets, built by
[`.github/workflows/zerogc-release.yml`](../../.github/workflows/zerogc-release.yml)
whenever a `zerogc-v*` tag is pushed:
[dotnet/runtimelab releases](https://github.com/dotnet/runtimelab/releases) -
look for releases tagged `zerogc-v*`.

Each release has **4 zip assets** - pick the one matching your platform
*and* your app's target runtime version:

| Zip | Platform | Target runtime |
|---|---|---|
| `ZeroGC-win-x64-net10.0.zip`   | Windows x64 | .NET 10.0 (GA) |
| `ZeroGC-win-x64-net11.0.zip`   | Windows x64 | .NET 11.0 (preview) |
| `ZeroGC-linux-x64-net10.0.zip` | Linux x64   | .NET 10.0 (GA) |
| `ZeroGC-linux-x64-net11.0.zip` | Linux x64   | .NET 11.0 (preview) |

**Match the target-runtime column to the runtime your app actually runs
on** (check your app's `<TargetFramework>` / the `dotnet --version` of the
runtime it's deployed with) - see [Why per-version, and why it matters](#why-per-version-and-why-it-matters)
below for why this is a hard requirement, not just a recommendation.

Each zip contains:

- `ZeroGC.dll` (Windows) or `libZeroGC.so` (Linux) - the binary itself.
- `manifest.json` - records the exact `dotnet/runtime` git tag it was built
  against, the ZeroGC commit SHA, the platform/target-runtime pair, and the
  build date, so you can always trace a binary back to exactly what
  produced it.

## 2. Place the binary next to your app

Unzip and copy just the native binary (you can ignore `manifest.json` at
runtime; keep it around for your own records) into your app's published
output folder, next to `YourApp.dll`:

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
layout without an immediate crash. Always match the zip's target-runtime
column to your app's actual target runtime.

## Building it yourself instead

If you'd rather build from source (e.g. to target a runtime version this
repo doesn't yet publish binaries for), see the "Building ZeroGC.dll /
libZeroGC.so" section of
[`../../src/ZeroGC/README.md`](../../src/ZeroGC/README.md#building-zerogcdll--libzerogcso).
