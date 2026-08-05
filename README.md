# ZeroGC.rs

ZeroGC.rs is an experimental, from-scratch **Rust** reimplementation of
[ZeroGC](https://github.com/dotnet/runtimelab/tree/feature/ZeroGC) — a
standalone [CoreCLR GC](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/garbage-collection.md)
that only ever **allocates** memory and never collects, compacts, or
reclaims it (a .NET analog of the JVM's ["Epsilon" no-op GC](https://openjdk.org/jeps/318)).

This is a **separate, independent experiment** from `feature/ZeroGC`: it does
not share history or code with the C++ implementation. The C++ project is
used purely as a design/behavioral reference — the bump-pointer arena
allocator, per-thread allocation-context design, write-barrier/card-table
setup, and handle-table semantics are all carried over, rewritten from
scratch in Rust against the same CoreCLR standalone-GC ABI
(`IGCHeap`/`IGCHandleManager`/`IGCHandleStore`, see
[`gcinterface.h`](https://github.com/dotnet/runtime/blob/main/src/coreclr/gc/gcinterface.h)).

Like the C++ original, ZeroGC.rs is loaded exactly like any other
[standalone/out-of-process GC](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/standalone-gc.md):
via `DOTNET_GCName=zerogc_rs.dll` (or `.so`), sitting next to the app's
managed executable. **No changes to the CoreCLR runtime are required or
made** by this project.

## Why Rust?

CoreCLR's GC-EE ABI is C++-vtable based. This experiment exists to answer:
can a memory-safer systems language build an ABI-compatible standalone GC
plugin without relying on C++, by hand-constructing `repr(C)` vtables that
match the C++ interface layout exactly? See `src/ZeroGC.rs/src/ffi.rs` for
the technique and its caveats (it is inherently `unsafe`-heavy at the ABI
boundary, same as any FFI shim).

## Repository layout

```
src/ZeroGC.rs/
  Cargo.toml           The crate manifest (cdylib)
  src/
    lib.rs              GC_VersionInfo / GC_Initialize exports, DllMain
    ffi.rs               repr(C) ABI types + hand-built IGCHeap/IGCHandleManager/
                         IGCHandleStore vtables, pinned to a specific
                         GC_INTERFACE_MAJOR/MINOR_VERSION (see file header)
    heap.rs              IGCHeap implementation: per-thread arena bump
                         allocator, write barrier/card table setup, counters
    handles.rs           IGCHandleStore / IGCHandleManager implementation
    pal.rs               Minimal OS shim (VirtualAlloc/mmap, QPC/clock_gettime,
                         memory-load queries) for Windows + Unix
```

## Building

```
build.cmd   (Windows)
build.sh    (Linux/macOS)
```

These scripts drive `cargo build [--release]` directly against
`src/ZeroGC.rs/Cargo.toml` — there is no managed (dotnet/runtime) code in
this experiment today, so the usual Arcade/msbuild orchestration is
bypassed. You can also build directly with Cargo:

```
cargo build --release --manifest-path src/ZeroGC.rs/Cargo.toml
```

## Status

This is an early bootstrap: the crate mirrors the C++ ZeroGC's behavior
(bump-pointer arena, per-thread allocation contexts, trivial/no-op handling
of collection-related APIs, handle table) but has not yet been validated
end-to-end against a live CoreCLR host.

## Known limitations

Same as the C++ ZeroGC this is modeled on:

- **Memory is never reclaimed.** This is the entire point of the GC, not a
  bug — long-running or allocation-heavy processes will grow without bound.
- No compaction, no generations, no finalization triggered by memory
  pressure.
- No heap walking / profiling API support (`ICorProfilerCallback` GC
  callbacks, `dotnet-gcdump`, etc.).

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
