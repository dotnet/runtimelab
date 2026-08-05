// Build-time ABI generation (on by default via feature = "bindgen-gen").
//
// By default, this generates Rust bindings for the plain-data GC ABI
// structs/enums by parsing the REAL CoreCLR gc/ headers with clang (via the
// `bindgen` crate). `src/ffi.rs` then `include!()`s the result directly, so
// those ~9 plain structs/enums (gc_alloc_context, WriteBarrierParameters,
// VersionInfo, GcDacVars, oom_history, HandleType, segment_info,
// FinalizerWorkItem, EtwGCSettingsInfo, plus the small status/event enums)
// are no longer hand-transcribed: their layout comes straight from the
// compiler's own AST of the real headers. This is how a mismatch like the
// historical `GcDacVars::oom_info` bug (declared by value instead of as a
// pointer) would now be a straightforward, unavoidable build-time fact
// rather than something that could silently drift from the real headers.
//
// This does NOT and cannot generate the vtable-providing interface structs
// (IGCHeapVtbl / IGCHandleManagerVtbl / IGCHandleStoreVtbl): bindgen/cxx/
// autocxx only support Rust calling into an existing C++ object, not Rust
// supplying a vtable to satisfy a C++ abstract interface (the direction
// ZeroGC.rs needs for those three). Those remain handcrafted in ffi.rs and
// must be reviewed manually against gcinterface.h/.ee.h on ABI-version
// changes.
//
// Usage (default -- requires a local dotnet/runtime checkout and libclang,
// exactly like the C++ ZeroGC build.ps1 already requires -RuntimeRepo):
//   $env:CORECLR_GC_DIR = "C:\path\to\runtime\src\coreclr\gc"
//   $env:LIBCLANG_PATH = "C:\Program Files\LLVM\bin"   # if not auto-detected
//   cargo build
//
// Opt out (e.g. if you don't have a runtime checkout handy) with:
//   cargo build --no-default-features
// -- in which case src/ffi.rs falls back to its own hand-written copies of
// these same types (kept in sync manually; see ffi.rs's `not(feature =
// "bindgen-gen")` fallback module).

#[cfg(feature = "bindgen-gen")]
fn main() {
    use std::env;
    use std::path::PathBuf;

    println!("cargo:rerun-if-env-changed=CORECLR_GC_DIR");
    println!("cargo:rerun-if-changed=build.rs");

    let gc_dir = env::var("CORECLR_GC_DIR").expect(
        "the default `bindgen-gen` feature requires CORECLR_GC_DIR to point at a \
         dotnet/runtime checkout's src\\coreclr\\gc directory, e.g. \
         C:\\github\\runtime\\src\\coreclr\\gc -- or build with --no-default-features to skip \
         this ABI cross-check if you don't have a runtime checkout handy",
    );
    let gc_dir_path = PathBuf::from(&gc_dir);
    if !gc_dir_path.join("gcinterface.h").is_file() {
        panic!(
            "CORECLR_GC_DIR ('{}') does not contain gcinterface.h -- check the path points at \
             <runtime checkout>/src/coreclr/gc",
            gc_dir
        );
    }

    // gcinterface.h/.dac.h pull in a handful of coreclr-internal macros/types
    // from vm/, pal/, inc/ that aren't part of the gc/ directory itself. Stub
    // just enough of them to their "release, non-DAC" meaning so the headers
    // parse as plain C++ without needing the full coreclr build graph.
    let wrapper_h = r#"
#include <cstdint>
#include <cstddef>
#include <cassert>

#define LIMITED_METHOD_CONTRACT
#define DPTR(type) type*

typedef int BOOL;
typedef long HRESULT;
typedef uintptr_t TADDR;
#define CALLBACK

class Object;
class MethodTable;
class Thread;
typedef Object* PTR_Object;
typedef Object** PTR_PTR_Object;
struct _UNCHECKED_OBJECTREF { void* dummy; };
typedef _UNCHECKED_OBJECTREF* UNCHECKED_OBJECTREF;
typedef _UNCHECKED_OBJECTREF* PTR_UNCHECKED_OBJECTREF;

#include "gcinterface.h"
"#;

    let out_dir = PathBuf::from(env::var("OUT_DIR").unwrap());
    let wrapper_path = out_dir.join("bindgen_verify_wrapper.h");
    std::fs::write(&wrapper_path, wrapper_h).expect("failed to write bindgen wrapper header");

    let bindings = bindgen::Builder::default()
        .header(wrapper_path.to_str().unwrap())
        .clang_args(["-x", "c++", "-std=c++17", "-I", &gc_dir])
        // Only the plain-data structs/enums -- NOT the vtable interfaces,
        // which bindgen cannot generate meaningfully for this "Rust provides
        // the vtable" direction.
        .allowlist_type("gc_alloc_context")
        .allowlist_type("WriteBarrierParameters")
        .allowlist_type("WriteBarrierOp")
        .allowlist_type("VersionInfo")
        .allowlist_type("GcDacVars")
        .allowlist_type("oom_history")
        .allowlist_type("oom_reason")
        .allowlist_type("failure_get_memory")
        .allowlist_type("HandleType")
        .allowlist_type("segment_info")
        .allowlist_type("FinalizerWorkItem")
        .allowlist_type("NoGCRegionCallbackFinalizerWorkItem")
        .allowlist_type("EtwGCSettingsInfo")
        .allowlist_type("walk_surv_type")
        .allowlist_type("wait_full_gc_status")
        .allowlist_type("start_no_gc_region_status")
        .allowlist_type("end_no_gc_region_status")
        .allowlist_type("refresh_memory_limit_status")
        .allowlist_type("enable_no_gc_region_callback_status")
        .allowlist_type("GCEventProvider")
        .allowlist_type("GCEventLevel")
        .allowlist_type("GCEventKeyword")
        .allowlist_type("GCConfigurationType")
        // Real Rust enums (not int + consts) for the C++ `enum`/`enum class`
        // types, matching how ffi.rs's hand-written enums are consumed
        // elsewhere in the crate (pattern matching, direct construction).
        .rustified_enum("WriteBarrierOp")
        .rustified_enum("HandleType")
        .rustified_enum("oom_reason")
        .rustified_enum("failure_get_memory")
        .rustified_enum("walk_surv_type")
        .rustified_enum("wait_full_gc_status")
        .rustified_enum("start_no_gc_region_status")
        .rustified_enum("end_no_gc_region_status")
        .rustified_enum("refresh_memory_limit_status")
        .rustified_enum("enable_no_gc_region_callback_status")
        .rustified_enum("GCEventProvider")
        .rustified_enum("GCEventLevel")
        .rustified_enum("GCEventKeyword")
        .rustified_enum("GCConfigurationType")
        .derive_default(true)
        .derive_eq(true)
        .derive_hash(true)
        .layout_tests(true)
        .generate()
        .expect(
            "bindgen failed to parse the real CoreCLR gc/ headers -- this usually means \
             gcinterface.h has changed in a way that needs a new stub in build.rs's wrapper_h",
        );

    let generated_path = out_dir.join("gc_abi_generated.rs");
    bindings
        .write_to_file(&generated_path)
        .expect("failed to write generated bindgen output");

    println!("cargo:rustc-cfg=bindgen_gen_active");
    println!("cargo::rustc-check-cfg=cfg(bindgen_gen_active)");
}

#[cfg(not(feature = "bindgen-gen"))]
fn main() {}
