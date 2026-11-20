# LXR GC (experimental)

This folder contains an experimental standalone GC for CoreCLR. It is a port of LXR, a garbage collector that uses
reference counting together with an Immix heap and a concurrent backup trace:

> Wenyu Zhao, Stephen M. Blackburn, and Kathryn S. McKinley.
> "Low-Latency, High-Throughput Garbage Collection." PLDI 2022.

The collector is a prototype. Use it only for experiments and measurements.

## Main parts

- `lxr_gc.cpp`, `lxr_gc.h`: the `IGCHeap` implementation, the settings and the allocation path.
- `lxr_reference_counting.cpp`, `reference_count.cpp`: the reference-count engine (increments, lazy decrements,
  mature-block selection, evacuation and the backup trace).
- `immix_*.cpp`: the Immix block and line allocator for small objects.
- `large_object_space.cpp`: the large object space (LOS).
- `side_metadata.cpp`: the side metadata planes (counts, marks, unlog bits).
- `lxr_work_scheduler.cpp`, `lxr_work_packet.cpp`, `lxr_pause_coordinator.cpp`: the parallel workers and the pauses.
- `lxr_write_barrier.cpp`, `amd64/`: the field-logging write barrier. The runtime and the JIT use it through the
  shared write-barrier changes in `src/coreclr/vm`, `src/coreclr/jit` and `src/coreclr/nativeaot`.
- `tests/`: native unit tests of the parts. They are built only when a test option `CLR_CMAKE_ENABLE_*_TEST` of
  `src/coreclr/CMakeLists.txt` is on.

## Build

LXR is supported only on Windows x64 and Linux x64, and only as a standalone GC. It is off by default. To build it,
give the CMake option to the CoreCLR build:

```
build.cmd clr -c Release -cmakeargs "-DCLR_CMAKE_ENABLE_LXR_GC=1"
./build.sh clr -c Release -cmakeargs "-DCLR_CMAKE_ENABLE_LXR_GC=1"
```

The build makes `clrlxrgc.dll` (`libclrlxrgc.so` on Linux). By default it also makes `clrlxrgc-bitmap.dll`
(`libclrlxrgc-bitmap.so`), a variant that finds object starts with a separate bit plane
(`CLR_CMAKE_LXR_BUILD_BITMAP_VARIANT`).

## Use

Put the GC library next to the runtime and select it with `DOTNET_GCName`:

```
set DOTNET_GCName=clrlxrgc.dll
export DOTNET_GCName=libclrlxrgc.so
```

The settings are read from `DOTNET_LXR*` environment variables. Size settings are decimal MB. Examples:

- `DOTNET_LXRImmixReserveMB`, `DOTNET_LXRLOSReserveMB`: the address-space reserve of the Immix space and the LOS. The
  default of each is the memory limit of the process.
- `DOTNET_LXRNativePlaneLimitMB`: the limit of the side metadata memory.
- `DOTNET_LXRConcGCThreads`, `DOTNET_LXRGCThreads`: the number of GC worker threads.
- `DOTNET_LXRPolicyTrace=1`: prints the collection policy decisions.
- `DOTNET_LXRLookupStats=1`: collects interior-lookup timing and counters and prints cumulative `LXRLOOKUP` statistics
  at each pause. Collection is off by default, independently of `DOTNET_LXRPhaseTimers`.
- `DOTNET_LXRTraceStats=1`: counts the diagnostic trace and remembered-set statistics (remembered and replayed entries,
  deletion marks, forced scans, concurrent slices). No collection decision uses them. Counting is off by default, and the
  metrics read `UINT64_MAX` while it is off. `DOTNET_LXRWindowStats=1` turns it on.

See `LxrGcHeap::Initialize` in `lxr_gc.cpp` for the full list.
