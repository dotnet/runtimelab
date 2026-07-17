# ZeroGC

ZeroGC is an experimental, standalone [CoreCLR GC](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/garbage-collection.md)
implementation for .NET that only **allocates** memory and never collects,
compacts, or reclaims it. It is a .NET analog to the JVM's
["Epsilon" no-op GC](https://openjdk.org/jeps/318), and is directly inspired by
the author's earlier prototype, [UpsilonGC](https://github.com/kkokosa/UpsilonGC).

> **Why would you want a GC that never frees memory?**
> It's useful for measuring the pure cost of garbage collection in a workload
> (by comparing against a GC that does none of it), for short-lived batch/CLI
> tools where "leak everything, then exit" is an acceptable trade for
> eliminating GC pause latency entirely, and as a minimal, from-scratch
> reference implementation of the CoreCLR standalone-GC ABI.

ZeroGC is loaded exactly like any other [standalone/out-of-process GC](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/standalone-gc.md):
via `DOTNET_GCName=ZeroGC.dll` (or `COMPlus_GCName`), sitting next to the app's
managed executable. **No changes to the CoreCLR runtime are required or made**
by this project — `C:\github\runtime` was used only as a read-only reference
for the unmodified GC interface headers (`gcinterface.h`, `gcenv.*.h`, etc.)
and to build a local, unmodified `coreclr.dll`/SDK for testing.

## What works

- Implements the full `IGCHeap` + `IGCHandleManager`/`IGCHandleStore` ABI
  surface required by the EE (allocation, write barriers, handle
  creation/destruction, GC.Collect() as a no-op, `GC.GetGCMemoryInfo()`,
  `GC.CollectionCount()`, `GC.GetTotalAllocatedBytes()`, etc.)
- A simple bump-pointer arena allocator: each allocation request reserves a
  chunk from a growing set of large (64 MB) committed segments; individual
  object allocations are carved out with a lock-protected pointer bump.
  Nothing is ever freed, compacted, or generation-tracked.
- Handles (`GCHandle`, weak/strong/pinned/etc.) are implemented as a simple
  fixed-capacity segmented table. Weak handles are **never cleared** (since
  nothing is ever collected, this is consistent with ZeroGC's semantics —
  the referent is always still alive).
- Standard counters (`GC.CollectionCount(n)` always 0,
  `GC.GetTotalAllocatedBytes()`, `GC.GetGCMemoryInfo()` heap/committed sizes,
  `GCSettings.IsServerGC`, etc.) all report real, meaningful values so that
  existing diagnostics/monitoring code keeps working and can be compared
  apples-to-apples against the regular GC.

## Known limitations

- **Memory is never reclaimed.** Long-running or allocation-heavy processes
  will grow without bound until the process is killed or the machine runs
  out of memory. This is the entire point of the GC, not a bug.
- No compaction, no generations, no finalization triggering based on
  memory pressure (finalizers still run at process exit via the normal EE
  shutdown path).
- No heap walking / profiling API support (`ICorProfilerCallback` GC
  callbacks, `dotnet-gcdump`, etc. are not implemented) — the arena is not
  a walkable managed heap structure.
- Card table **and card bundle table** are both implemented (allocated,
  biased, and wired up) purely so the EE's write barrier doesn't fault —
  ZeroGC itself never reads them, since it never needs to know which
  objects were mutated (there's no compaction or generational promotion to
  drive from that information).
- Not thread-scalable in the way Server GC's per-core allocation contexts
  are tuned; it's a straightforward mutex/interlocked-bump design. It's
  sufficient for the benchmarks in this repo but hasn't been tuned for
  heavy multi-threaded allocation.

## Repository layout

```
src/ZeroGC/
  native/                 ZeroGC native GC plugin sources
    ZeroGC.h              Shared declarations
    dllmain.cpp           GC_VersionInfo / GC_Initialize / DllMain entry points
    ZeroGCHeap.cpp        IGCHeap implementation (allocator, write barriers, counters)
    ZeroGCHandles.cpp     IGCHandleManager / IGCHandleStore implementation
    build.ps1             Builds ZeroGC.dll (Release, x64) using VS Build Tools
    tools/symresolve.cpp  Debug-only dbghelp-based crash symbol resolver (see below)
  samples/
    ConsoleApp/           Allocation-heavy console benchmark app
    WebApi/               Self-driving ASP.NET Core (Kestrel) benchmark app
  run-benchmarks.ps1       Orchestrates console+webapi runs under both GCs
  generate-report.ps1      Renders results/results.json -> results/report.html
  results/
    results.json           Raw benchmark output from the last run-benchmarks.ps1 run
    report.html             Self-contained HTML comparison report (the deliverable artifact)
```

## Building ZeroGC.dll

Requires the VC++ build tools (the same toolset used to build CoreCLR) and a
local .NET SDK. From a Developer Command Prompt / PowerShell with `cl.exe`
on PATH (or let `build.ps1` locate `vcvars64.bat` itself):

```powershell
cd src\ZeroGC\native
.\build.ps1
```

This produces `native\ZeroGC.dll` (Release, x64). The script defines
`FEATURE_MANUALLY_MANAGED_CARD_BUNDLES` to match the EE's own build
configuration on AMD64/ARM64 — **this define is required**; without it the
write barrier's card-bundle table handling silently mismatches the EE side
and the process fails fast on the very first write barrier (see "Debugging
notes" below for the full story).

## Running an app with ZeroGC

Copy `ZeroGC.dll` next to the app's published output, then:

```powershell
$env:DOTNET_GCName = "ZeroGC.dll"
dotnet YourApp.dll
```

Unset (`Remove-Item Env:DOTNET_GCName`) or set to nothing to fall back to the
regular (Workstation/Server) GC.

## Sample apps

- **ConsoleApp** — a tight allocation loop (allocates short strings/objects
  into a `List<T>` sized batch, throttled via `Thread.Sleep(1)` per outer
  iteration) that runs for a configurable duration and prints throughput and
  GC counters as a `##RESULT##{json}` line.
- **WebApi** — a minimal ASP.NET Core Kestrel app exposing `/api/work` (an
  allocation + JSON-serialization endpoint) and `/api/ping`; on startup, it
  spawns a background task that self-drives load against `/api/work` via an
  in-process `HttpClient` for a configured duration, then reports the same
  `##RESULT##` JSON shape and exits.

Both apps have their own `global.json` + empty `Directory.Build.props`/
`.targets` overrides so they can be built/published independently of this
repo's root Arcade-SDK-based build files.

To publish a sample manually:

```powershell
cd src\ZeroGC\samples\ConsoleApp   # or WebApi
dotnet publish -c Release -r win-x64 --self-contained -o publish
copy ..\..\native\ZeroGC.dll publish\
```

## Running the benchmark suite

```powershell
cd src\ZeroGC
.\run-benchmarks.ps1 -DurationSeconds 180
```

This runs 4 benchmarks (ConsoleApp x {regular GC, ZeroGC}, WebApi x
{regular GC, ZeroGC}), each for `-DurationSeconds` (default 180s/3 min),
collects results into `results\results.json`, and calls
`generate-report.ps1` automatically to produce `results\report.html`.

Both sample workloads are throttled (`Thread.Sleep(1)` / `Task.Delay(1)`)
so that a multi-minute run under ZeroGC doesn't exhaust machine memory —
measured growth is roughly 24 MB/s (ConsoleApp) and 80 MB/s (WebApi) at the
throttled rate used in this repo; scale `-DurationSeconds` down on
memory-constrained machines.

## Results (3-minute runs, this machine)

See `results/report.html` for the full self-contained comparison report
(tables + bar charts). Summary from the last recorded run:

| Metric (ConsoleApp, 180s) | Regular GC | ZeroGC |
|---|---|---|
| Throughput | 15,786 ops/sec | 15,788 ops/sec |
| Total allocated | 406 MB | 711 MB |
| Gen0/1/2 collections | 25 / 2 / 1 | 0 / 0 / 0 |
| Committed bytes | 20.5 MB | 712.6 MB |
| Working set | 47.2 MB | 445.7 MB |

| Metric (WebApi, 180s) | Regular GC | ZeroGC |
|---|---|---|
| Throughput | 253.0 req/sec | 253.1 req/sec |
| Total allocated | 386.8 MB | 1.44 GB |
| Gen0/1/2 collections | 152 / 1 / 0 | 0 / 0 / 0 |
| Committed bytes | 5.0 MB | 1.44 GB |
| Working set | 65.2 MB | 480.8 MB |

**Takeaway:** throughput is essentially identical (both workloads are
throttled to the same target rate, and neither is GC-bound at this
allocation rate), which is expected — the interesting difference is that
ZeroGC always reports 0 collections and its committed/working-set memory
grows in lockstep with total bytes allocated, while the regular GC's
footprint stays roughly flat thanks to periodic gen0/1/2 collections
reclaiming garbage.

## Debugging notes / lessons learned

While bringing ZeroGC up, allocations worked but the process crashed with a
CoreCLR fail-fast (`EEPOLICY_HANDLE_FATAL_ERROR`) on the very first object
allocated after the first arena refill. Root cause: `FEATURE_MANUALLY_MANAGED_CARD_BUNDLES`
is unconditionally defined for AMD64/ARM64 in the EE's own build
(`clrdefinitions.cmake`), which makes `ErectWriteBarrier` in `gchelpers.cpp`
unconditionally dereference a second "card bundle" byte table (coarser
granularity than the card table, shift=21) on every write barrier that dirties
a new card. ZeroGC was passing `card_bundle_table = nullptr` in
`WriteBarrierParameters`, causing an immediate invalid write. The fix:
allocate and bias a card-bundle table exactly like the card table (same
technique, different shift), and make sure ZeroGC's own build also defines
`FEATURE_MANUALLY_MANAGED_CARD_BUNDLES` so its logic matches the EE's
expectations for this architecture.

**Lesson:** a standalone GC's own preprocessor defines must match the EE's
build configuration for architecture-gated ABI features like this — it's
not enough to reason locally about what a bump-pointer allocator "needs";
the EE-side code paths are compiled assuming specific GC-side behavior for
these define-gated features regardless of what the GC actually needs
internally.

`native/tools/symresolve.cpp` is a small, standalone dbghelp-based tool kept
here as a debugging aid for anyone hitting similar unexplained CoreCLR
fail-fast crashes in a standalone GC. It resolves a raw instruction pointer
(from CoreCLR's `createdump`-based fail-fast crash dump, obtained via
`DOTNET_DbgEnableMiniDump=1` + `DOTNET_DbgMiniDumpName=...` environment
variables) to a symbol + source line/offset using the Microsoft public
symbol server. It requires a modern `dbghelp.dll`/`symsrv.dll` pair (e.g.
from a WinDbg install) alongside its own executable — the OS-shipped
`dbghelp.dll` in `System32` lacks symbol-server support. These DLLs and the
compiled tool binary are not checked into this repository (see
`.gitignore`); rebuild with `cl.exe` if needed.
