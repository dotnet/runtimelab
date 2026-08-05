mod ffi;
mod pal;
mod heap;
mod handles;

use core::ffi::{c_char, c_void};
use core::ptr;
use core::sync::atomic::{AtomicBool, AtomicPtr, Ordering};

pub static G_THE_GC_TO_CLR: AtomicPtr<ffi::IGCToCLR> = AtomicPtr::new(ptr::null_mut());
pub static G_ZEROGC_HEAP: AtomicPtr<heap::ZeroGcHeap> = AtomicPtr::new(ptr::null_mut());
pub static G_OLD_METHOD_TABLE_FLAGS: AtomicBool = AtomicBool::new(false);

static NAME: &[u8] = b"ZeroGC.rs\0";

pub(crate) fn abort_on_panic<R, F>(f: F) -> R
where
    F: FnOnce() -> R + std::panic::UnwindSafe,
{
    match std::panic::catch_unwind(f) {
        Ok(value) => value,
        Err(_) => std::process::abort(),
    }
}

#[no_mangle]
pub extern "C" fn GC_VersionInfo(info: *mut ffi::VersionInfo) {
    abort_on_panic(|| unsafe {
        if info.is_null() {
            std::process::abort();
        }
        let runtime_major = (*info).MajorVersion;
        G_OLD_METHOD_TABLE_FLAGS.store(runtime_major < 2, Ordering::SeqCst);
        (*info).MajorVersion = ffi::GC_INTERFACE_MAJOR_VERSION;
        (*info).MinorVersion = ffi::GC_INTERFACE_MINOR_VERSION;
        (*info).BuildVersion = 0;
        (*info).Name = NAME.as_ptr().cast::<c_char>();
    })
}

#[no_mangle]
pub extern "C" fn GC_Initialize(
    clr_to_gc: *mut ffi::IGCToCLR,
    gc_heap: *mut *mut c_void,
    gc_handle_manager: *mut *mut c_void,
    gc_dac_vars: *mut ffi::GcDacVars,
) -> i32 {
    abort_on_panic(|| unsafe {
        if gc_heap.is_null() || gc_handle_manager.is_null() || gc_dac_vars.is_null() {
            return ffi::E_INVALIDARG;
        }

        G_THE_GC_TO_CLR.store(clr_to_gc, Ordering::SeqCst);
        ptr::write_bytes(gc_dac_vars, 0, 1);

        let mut handle_manager = handles::ZeroGcHandleManager::new();
        if !handle_manager.initialize() {
            return ffi::E_OUTOFMEMORY;
        }
        let handle_manager_ptr = Box::into_raw(handle_manager);

        let heap = heap::ZeroGcHeap::new();
        let heap_ptr = Box::into_raw(heap);
        if heap_ptr.is_null() {
            return ffi::E_OUTOFMEMORY;
        }

        G_ZEROGC_HEAP.store(heap_ptr, Ordering::SeqCst);
        *gc_heap = heap_ptr.cast();
        *gc_handle_manager = handle_manager_ptr.cast();
        ffi::S_OK
    })
}

#[cfg(windows)]
mod dllmain {
    use core::ffi::c_void;

    const DLL_PROCESS_ATTACH: u32 = 1;

    #[link(name = "kernel32")]
    extern "system" {
        fn DisableThreadLibraryCalls(hlibmodule: isize) -> i32;
    }

    #[no_mangle]
    pub extern "system" fn DllMain(hinst_dll: isize, reason: u32, _reserved: *mut c_void) -> i32 {
        crate::abort_on_panic(|| unsafe {
            if reason == DLL_PROCESS_ATTACH {
                DisableThreadLibraryCalls(hinst_dll);
            }
            1
        })
    }
}
