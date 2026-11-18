# Laxori GC (experimental)

This folder contains Laxori, an experimental standalone GC for CoreCLR. Laxori is the LXR collector of
`src/coreclr/gc_lxr` (reference counting with an Immix heap and a concurrent backup trace) plus a thread-local tier:

- Each thread allocates new objects in its own private regions. A region is a run of Immix blocks that one thread owns.
- The write barrier and the handle table track when an object escapes from its thread (for example, a reference is
  stored in a shared object, or the object gets a handle). The region of an escaped object is published to the shared
  LXR heap.
- A thread collects its own private regions with a thread-local mark and compaction (the algorithms of Satori's
  `SatoriRegion`, on Immix blocks).

The collector is a prototype. Use it only for experiments and measurements.

The files are a separate copy of the LXR sources with a `laxori_` prefix, so the two collectors can change
independently. The parts that are new in Laxori:

- `laxori_private.cpp`, `laxori_private.h`: the private regions and the escape tracking.
- `laxori_gchandletable.cpp`: a copy of `gc/gchandletable.cpp` that publishes the region of an object before the
  object gets a handle.
- `laxori_write_barrier.cpp`, `amd64/`: the LXR barrier plus the check of the region owner.

## Build

Laxori is supported only on Windows x64 and Linux x64, and only as a standalone GC. It is off by default. To build it,
give the CMake option to the CoreCLR build:

```
build.cmd clr -c Release -cmakeargs "-DCLR_CMAKE_ENABLE_LAXORI_GC=1"
./build.sh clr -c Release -cmakeargs "-DCLR_CMAKE_ENABLE_LAXORI_GC=1"
```

The build makes `clrlaxorigc.dll` (`libclrlaxorigc.so` on Linux). By default it also makes `clrlaxorigc-bitmap.dll`
(`libclrlaxorigc-bitmap.so`), a variant that finds object starts with a separate bit plane
(`CLR_CMAKE_LAXORI_BUILD_BITMAP_VARIANT`).

## Use

Put the GC library next to the runtime and select it with `DOTNET_GCName`:

```
set DOTNET_GCName=clrlaxorigc.dll
export DOTNET_GCName=libclrlaxorigc.so
```

The settings are read from `DOTNET_LAXORI*` environment variables. Most of them are the LXR settings with the
`LAXORI` prefix (for example `DOTNET_LAXORIImmixReserveMB`). Size settings are decimal MB. The settings of the
private tier:

- `DOTNET_LAXORIPrivate=0`: turns off the private tier. It is on by default.
- `DOTNET_LAXORIRegionKB`: the size of a private region in KB (default 256).

See `LaxoriGcHeap::Initialize` in `laxori_gc.cpp` for the full list.
