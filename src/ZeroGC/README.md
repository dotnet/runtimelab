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
    WebApi/               Self-driving ASP.NET Core (Kestrel) benchmark app (8 concurrent workers)
    GCPerfSim/            Vendored dotnet/performance allocation-pattern simulator (unmodified)
  run-benchmarks.ps1       Orchestrates all 5 scenarios x 3 GC modes via dotnet-counters
  generate-report.ps1      Renders results/results-full.json -> results/report.html
  results/
    results-full.json      Raw benchmark output (summary + percentiles + time series) from the last run
    report.html             Self-contained HTML comparison report (the deliverable artifact)
    raw/                    Per-run dotnet-counters CSV captures (supplementary/debug data)
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

## Sample apps and workloads

Five workloads are benchmarked, each once per GC configuration (Workstation,
Server, ZeroGC):

- **ConsoleApp** — a tight allocation loop (allocates short strings/objects
  into a `List<T>` sized batch, throttled via `Thread.Sleep(1)` per outer
  iteration) that runs for a configurable duration and prints throughput and
  GC counters as a `##RESULT##{json}` line.
- **WebApi** — a minimal ASP.NET Core Kestrel app exposing `/api/work` (an
  allocation + JSON-serialization endpoint) and `/api/ping`; on startup, it
  spawns 8 concurrent in-process `HttpClient` worker loops that self-drive
  load against `/api/work` for a configured duration (a single throttled
  loop tops out around ~60 req/sec due to Windows timer granularity, too
  little to exercise the GC meaningfully — 8 concurrent workers scale
  throughput roughly 8x while keeping each worker's own memory growth rate,
  and therefore the total run's memory growth, bounded and predictable).
  Reports the same `##RESULT##` JSON shape as ConsoleApp and exits.
- **GCPerfSim** (`samples/GCPerfSim/`) — vendored unmodified from
  [dotnet/performance](https://github.com/dotnet/performance/tree/main/src/benchmarks/gc/GCPerfSim)
  (MIT licensed), used for 3 more realistic allocation-pattern scenarios that
  the two hand-written apps above don't exercise well on their own:
  - **gcperfsim-webserver**: moderate-survival, high-throughput SOH churn
    modeling per-request allocation (`-tc 4 -tlgb 0.5 -sohsr 200-3000 -sohsi 15`).
  - **gcperfsim-cache**: LOH-heavy, higher-survival workload modeling a
    cache/large-buffer-heavy service (`-tc 2 -tlgb 0.3 -lohar 100 -lohsr 100000-300000`).
  - **gcperfsim-churn**: many threads, small objects, low survival, modeling
    high-throughput transient object churn (`-tc 8 -tlgb 0.1 -sohsr 200-600 -sohsi 200`).

  All three scenarios pass `-at simple` (GCPerfSim's `SimpleItem` allocation
  type) and add `-c 300000` (extra CPU compute between allocations, which
  dominates wall-clock time far more than GC pause overhead does — this
  keeps run duration roughly comparable across all 3 GC modes even though
  ZeroGC has zero pause time). **`-at simple` is a deliberate, permanent
  choice**: GCPerfSim's default `ReferenceItem` allocation type has a
  pre-existing bug in its survivor-list bookkeeping (`OldArr.NonEmptyLength`
  can underflow in `MemoryAlloc.DoSurvive`, later causing an
  `IndexOutOfRangeException`) that reproduces intermittently at the
  multi-hundred-second scale used here; `SimpleItem` avoids that code path
  entirely and was verified crash-free across all 3 scenarios at full scale.
  Each scenario is bounded by `-tagb` (total allocation across all threads,
  scaled from a `BaseTagbFor600s` constant in `run-benchmarks.ps1`) rather
  than a wall-clock timeout, since GCPerfSim's own `-totalMins` is unreliable
  as a hard stop and `-tagb` is also the memory-safety mechanism that keeps a
  ZeroGC run's worst-case memory bounded regardless of allocation speed.

Both hand-written sample apps have their own `global.json` + empty
`Directory.Build.props`/`.targets` overrides (GCPerfSim too) so they build
independently of this repo's root Arcade-SDK-based build files.

To publish a sample manually:

```powershell
cd src\ZeroGC\samples\ConsoleApp   # or WebApi, or GCPerfSim
dotnet publish -c Release -r win-x64 --self-contained -o publish
copy ..\..\native\ZeroGC.dll publish\
```

## Running the benchmark suite

```powershell
dotnet tool install --global dotnet-counters   # one-time
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
cd src\ZeroGC
.\run-benchmarks.ps1 -DurationSeconds 600
```

This runs all 5 workloads x 3 GC modes (Workstation, Server, ZeroGC) = 15
runs total, each for `-DurationSeconds` (default 600s/10 min for GCPerfSim
scenarios; ConsoleApp/WebApi honor it as a wall-clock duration; GCPerfSim
scenarios honor it by linearly scaling their calibrated `-tagb` allocation
budget, so actual wall time is only approximately `-DurationSeconds`).

For every run, `dotnet-counters collect` attaches to the target process for
the whole duration and captures a genuine second-by-second time series (GC
pause time, working set, committed bytes, heap size, collection counts,
allocation rate, and — for ConsoleApp/WebApi — a custom `ZeroGC.Bench`
`operations` throughput counter) via the standard `System.Runtime`
EventCounters/Meters every .NET process exposes. This works identically for
ZeroGC because its `IGCHeap` counters
(`GC.CollectionCount`/`GetTotalAllocatedBytes`/`GetGCMemoryInfo`) feed the
same counters as the built-in GC — no ZeroGC-specific tooling is required.
GC-mode environment variables (`DOTNET_gcServer`, `DOTNET_GCName`) are
scoped only to each child process via `ProcessStartInfo.EnvironmentVariables`
— never set in the parent shell, since `dotnet-counters` is itself a .NET
process and would otherwise try to load the same GC config for itself.

Results are written incrementally to `results\results-full.json` after each
run (crash-safe: a failure partway through the suite doesn't lose already-
completed runs), then `generate-report.ps1` is invoked automatically to
produce `results\report.html`.

Both hand-written sample workloads are throttled so a multi-minute run under
ZeroGC doesn't exhaust machine memory; each GCPerfSim scenario is bounded by
its calibrated `-tagb` (see above) for the same reason. Scale
`-DurationSeconds` down on memory-constrained machines.

## Results (10-minute runs, this machine)

See `results/report.html` for the full self-contained comparison report:
per-scenario comparison tables (throughput, allocation, collection counts,
heap/committed/working-set memory) for all 3 GC modes, a percentile table
(avg/p50/p90/p99 for GC pause time %, working set, and throughput), and
inline SVG time-series charts (GC pause % / working set / throughput over
the full run) with one line per GC mode.

**Highlights across all 5 workloads:**

- **Throughput/allocation-rate parity:** Workstation, Server, and ZeroGC are
  all within a few percent of each other on ops/sec (ConsoleApp/WebApi) or
  MB/s allocation throughput (GCPerfSim) for every scenario — none of these
  workloads are GC-bound enough at these allocation rates for GC pause time
  to dominate wall-clock time, and the GCPerfSim scenarios' `-c 300000`
  compute cost dominates further.
- **GC pause time %:** ZeroGC is always exactly 0% (there is nothing to
  pause for). Workstation/Server GC pause time stays under ~1% even in the
  most GC-active scenario (gcperfsim-cache), consistent with modern
  background/concurrent GC design.
- **Memory footprint diverges sharply and predictably:** ZeroGC's working
  set, committed bytes, and GC heap size all grow monotonically and track
  total bytes allocated (e.g. in gcperfsim-cache, ZeroGC's working set grows
  from ~1.5 GB to ~6 GB over the 10-minute run), while Workstation/Server GC
  keep their footprint roughly flat via periodic gen0/1/2 collections
  (hundreds of gen0 collections, tens of gen1, single-digit-to-low-teens
  gen2 over the same run).
- **"Total allocated" needs a caveat:** ZeroGC reports a noticeably higher
  "Total allocated" figure than Workstation/Server for the same workload.
  This is expected, not a bug — see the report's inline callout: the real
  GC's `GetTotalAllocatedBytes(precise: false)` is a fast approximation that
  can undercount with many allocating threads, while ZeroGC returns its
  arena's exact bump-pointer position (a true, exact count).

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
