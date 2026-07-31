# Using a ZeroGC binary

This page is for people who want to try ZeroGC against their own app.

See [`../../src/ZeroGC/README.md`](../../src/ZeroGC/README.md) for what
ZeroGC is, how it's implemented, and its measured performance
characteristics. This page only covers *consuming* a built binary.

## 1. Get the binary

Signed binaries are published as NuGet packages, built by Microsoft's own
official Arcade/Azure Pipelines infrastructure and pushed to the
`dotnet-experimental` feed - one package per targeted runtime major version
(see [Why per-version, and why it matters](#why-per-version-and-why-it-matters)
below for why you must pick the package matching your app's TFM):

| Your app's TFM | Package |
|---|---|
| `net10.0` | `Microsoft.DotNet.RuntimeLab.ZeroGC.Net10` |
| `net11.0` | `Microsoft.DotNet.RuntimeLab.ZeroGC.Net11` |

Add the feed to a `nuget.config` in your project (or solution) directory:

```xml
<configuration>
  <packageSources>
    <add key="dotnet-experimental" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

Then add the package (use the exact prerelease version currently published -
`--version` and `--prerelease` cannot be combined in the same command):

```powershell
dotnet add package Microsoft.DotNet.RuntimeLab.ZeroGC.Net10 --version 1.0.0-zerogc.26381.4
```

Alternatively, build `ZeroGC.dll`/`libZeroGC.so` yourself from source - see
"Building ZeroGC.dll / libZeroGC.so" in
[`../../src/ZeroGC/README.md`](../../src/ZeroGC/README.md#building-zerogcdll--libzerogcso).

## 2. Place the binary next to your app

The package only carries the native `ZeroGC.dll`/`libZeroGC.so` as a
RID-specific native asset - it is **not** automatically flattened next to
your app's executable by a plain `dotnet build`. You have two options,
both verified end-to-end:

### Option A - copy the file manually (lightweight, no publish required)

Best if you just want to try ZeroGC against an existing `dotnet build`
output without changing your deployment model. After `dotnet build`, copy
the single native file from the NuGet package cache into your build
output, next to `YourApp.dll`:

```powershell
# Windows, framework-dependent dotnet build output
Copy-Item "$env:USERPROFILE\.nuget\packages\microsoft.dotnet.runtimelab.zerogc.net10\1.0.0-zerogc.26381.4\runtimes\win-x64\native\ZeroGC.dll" `
          "bin\Release\net10.0\ZeroGC.dll"
```

```bash
# Linux
cp ~/.nuget/packages/microsoft.dotnet.runtimelab.zerogc.net10/1.0.0-zerogc.26381.4/runtimes/linux-x64/native/libZeroGC.so \
   bin/Release/net10.0/libZeroGC.so
```

For repeat use, automate this with a post-build MSBuild `<Copy>` target or
a CI script step, rather than copying by hand every time.

### Option B - `dotnet publish -r` (RID-specific publish)

If you're already publishing RID-specific output (self-contained or
framework-dependent), the package's native asset is picked up and
flattened automatically - no manual copy needed:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

```
YourApp/bin/Release/net10.0/win-x64/publish/
  YourApp.dll
  YourApp.runtimeconfig.json
  ZeroGC.dll          <-- placed here automatically (Windows)
  ... (or libZeroGC.so on Linux, with -r linux-x64)
```

Both options were validated to produce identical behavior (0 gen0
collections, monotonically growing working set) - pick whichever fits
your existing build/deploy process; Option A avoids the overhead of a
RID-specific publish if you only want a quick experiment.

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
