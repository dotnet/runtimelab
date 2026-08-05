#![allow(dead_code)]

use crate::ffi;
use core::cell::Cell;
use core::ffi::c_void;
use core::ptr;
use core::sync::atomic::{AtomicI64, AtomicPtr, Ordering};

pub static LIVE_HANDLE_COUNT: AtomicI64 = AtomicI64::new(0);
pub static PEAK_HANDLE_COUNT: AtomicI64 = AtomicI64::new(0);

#[repr(C)]
pub struct Slot {
    pub value: AtomicPtr<ffi::Object>,
    pub secondary: *mut ffi::Object,
    pub extra_info: *mut c_void,
    pub r#type: ffi::HandleType,
    pub next_free: *mut Slot,
    pub in_use: bool,
}

impl Slot {
    fn new(value: *mut ffi::Object, r#type: ffi::HandleType) -> Self {
        Self {
            value: AtomicPtr::new(value),
            secondary: ptr::null_mut(),
            extra_info: ptr::null_mut(),
            r#type,
            next_free: ptr::null_mut(),
            in_use: true,
        }
    }
}

// Per-thread free list, intentionally avoiding a shared Treiber stack.
//
// A single global lock would serialize all GCHandle churn process-wide. An earlier
// lock-free shared stack was also incorrect because of a real ABA hazard: thread T1 can
// read `(head = X, next = Y)`, stall, observe another thread pop-then-repush `X`, and
// later succeed with a stale CAS that reinstalls `Y` as the stack head even if `Y` is now
// already in active use on another thread. That can hand out the same slot twice, causing
// silent corruption. A per-thread free list sidesteps the ABA problem entirely because each
// OS thread only ever pushes to and pops from its own list. Slots freed on a different thread
// are simply recycled by that freeing thread instead of the allocating one.
thread_local! {
    static FREE_LIST: Cell<*mut Slot> = const { Cell::new(ptr::null_mut()) };
}

#[repr(C)]
pub struct ZeroGcHandleStore {
    pub vtbl: *const ffi::IGCHandleStoreVtbl,
}

impl ZeroGcHandleStore {
    pub fn new() -> Box<Self> {
        Box::new(Self {
            vtbl: &HANDLE_STORE_VTBL,
        })
    }

    #[inline]
    pub unsafe fn slot_from_handle(handle: ffi::OBJECTHANDLE) -> *mut Slot {
        handle.cast()
    }

    pub unsafe fn alloc_slot(
        &mut self,
        value: *mut ffi::Object,
        r#type: ffi::HandleType,
    ) -> ffi::OBJECTHANDLE {
        let slot = FREE_LIST.with(|free_list| {
            let slot = free_list.get();
            if !slot.is_null() {
                free_list.set((*slot).next_free);
                slot
            } else {
                Box::into_raw(Box::new(Slot::new(value, r#type)))
            }
        });

        if slot.is_null() {
            return ptr::null_mut();
        }

        if !(*slot).in_use {
            (*slot).value.store(value, Ordering::SeqCst);
            (*slot).secondary = ptr::null_mut();
            (*slot).extra_info = ptr::null_mut();
            (*slot).r#type = r#type;
            (*slot).next_free = ptr::null_mut();
            (*slot).in_use = true;
        }

        let live = LIVE_HANDLE_COUNT.fetch_add(1, Ordering::SeqCst) + 1;
        let mut peak = PEAK_HANDLE_COUNT.load(Ordering::SeqCst);
        while live > peak {
            match PEAK_HANDLE_COUNT.compare_exchange(peak, live, Ordering::SeqCst, Ordering::SeqCst) {
                Ok(_) => break,
                Err(observed) => peak = observed,
            }
        }

        slot.cast()
    }

    pub unsafe fn free_slot(&mut self, handle: ffi::OBJECTHANDLE) {
        if handle.is_null() {
            return;
        }

        let slot = Self::slot_from_handle(handle);
        if slot.is_null() || !(*slot).in_use {
            return;
        }

        (*slot).in_use = false;
        (*slot).value.store(ptr::null_mut(), Ordering::SeqCst);
        (*slot).secondary = ptr::null_mut();
        (*slot).extra_info = ptr::null_mut();

        FREE_LIST.with(|free_list| {
            (*slot).next_free = free_list.get();
            free_list.set(slot);
        });

        LIVE_HANDLE_COUNT.fetch_sub(1, Ordering::SeqCst);
    }

    pub unsafe fn create_handle_of_type(
        &mut self,
        object: *mut ffi::Object,
        r#type: ffi::HandleType,
    ) -> ffi::OBJECTHANDLE {
        self.alloc_slot(object, r#type)
    }

    pub unsafe fn create_handle_with_extra_info(
        &mut self,
        object: *mut ffi::Object,
        r#type: ffi::HandleType,
        extra_info: *mut c_void,
    ) -> ffi::OBJECTHANDLE {
        let handle = self.alloc_slot(object, r#type);
        if !handle.is_null() {
            (*Self::slot_from_handle(handle)).extra_info = extra_info;
        }
        handle
    }

    pub unsafe fn create_dependent_handle(
        &mut self,
        primary: *mut ffi::Object,
        secondary: *mut ffi::Object,
    ) -> ffi::OBJECTHANDLE {
        let handle = self.alloc_slot(primary, ffi::HandleType::HNDTYPE_DEPENDENT);
        if !handle.is_null() {
            (*Self::slot_from_handle(handle)).secondary = secondary;
        }
        handle
    }
}

#[repr(C)]
pub struct ZeroGcHandleManager {
    pub vtbl: *const ffi::IGCHandleManagerVtbl,
    pub global_store: *mut ZeroGcHandleStore,
}

impl ZeroGcHandleManager {
    pub fn new() -> Box<Self> {
        Box::new(Self {
            vtbl: &HANDLE_MANAGER_VTBL,
            global_store: ptr::null_mut(),
        })
    }

    pub unsafe fn initialize(&mut self) -> bool {
        let store = ZeroGcHandleStore::new();
        self.global_store = Box::into_raw(store);
        !self.global_store.is_null()
    }

    pub unsafe fn get_global_handle_store(&mut self) -> *mut ffi::IGCHandleStore {
        self.global_store.cast()
    }

    pub unsafe fn create_handle_store(&mut self) -> *mut ffi::IGCHandleStore {
        Box::into_raw(ZeroGcHandleStore::new()).cast()
    }

    pub unsafe fn destroy_handle_store(&mut self, store: *mut ffi::IGCHandleStore) {
        if store.is_null() {
            return;
        }
        drop(Box::from_raw(store.cast::<ZeroGcHandleStore>()));
    }

    pub unsafe fn create_global_handle_of_type(
        &mut self,
        object: *mut ffi::Object,
        r#type: ffi::HandleType,
    ) -> ffi::OBJECTHANDLE {
        (*self.global_store).create_handle_of_type(object, r#type)
    }

    pub unsafe fn create_duplicate_handle(&mut self, handle: ffi::OBJECTHANDLE) -> ffi::OBJECTHANDLE {
        if handle.is_null() {
            return ptr::null_mut();
        }
        let slot = ZeroGcHandleStore::slot_from_handle(handle);
        (*self.global_store).create_handle_of_type((*slot).value.load(Ordering::SeqCst), (*slot).r#type)
    }

    pub unsafe fn destroy_handle_of_type(&mut self, handle: ffi::OBJECTHANDLE) {
        (*self.global_store).free_slot(handle);
    }

    pub unsafe fn set_extra_info_for_handle(&mut self, handle: ffi::OBJECTHANDLE, extra_info: *mut c_void) {
        if handle.is_null() {
            return;
        }
        (*ZeroGcHandleStore::slot_from_handle(handle)).extra_info = extra_info;
    }

    pub unsafe fn get_extra_info_from_handle(&mut self, handle: ffi::OBJECTHANDLE) -> *mut c_void {
        if handle.is_null() {
            return ptr::null_mut();
        }
        (*ZeroGcHandleStore::slot_from_handle(handle)).extra_info
    }

    pub unsafe fn store_object_in_handle(&mut self, handle: ffi::OBJECTHANDLE, object: *mut ffi::Object) {
        if handle.is_null() {
            return;
        }
        (*ZeroGcHandleStore::slot_from_handle(handle)).value.store(object, Ordering::SeqCst);
    }

    pub unsafe fn store_object_in_handle_if_null(
        &mut self,
        handle: ffi::OBJECTHANDLE,
        object: *mut ffi::Object,
    ) -> bool {
        if handle.is_null() {
            return false;
        }
        let slot = ZeroGcHandleStore::slot_from_handle(handle);
        (*slot)
            .value
            .compare_exchange(ptr::null_mut(), object, Ordering::SeqCst, Ordering::SeqCst)
            .is_ok()
    }

    pub unsafe fn set_dependent_handle_secondary(
        &mut self,
        handle: ffi::OBJECTHANDLE,
        object: *mut ffi::Object,
    ) {
        if handle.is_null() {
            return;
        }
        (*ZeroGcHandleStore::slot_from_handle(handle)).secondary = object;
    }

    pub unsafe fn get_dependent_handle_secondary(&mut self, handle: ffi::OBJECTHANDLE) -> *mut ffi::Object {
        if handle.is_null() {
            return ptr::null_mut();
        }
        (*ZeroGcHandleStore::slot_from_handle(handle)).secondary
    }

    pub unsafe fn interlocked_compare_exchange_object_in_handle(
        &mut self,
        handle: ffi::OBJECTHANDLE,
        object: *mut ffi::Object,
        comparand_object: *mut ffi::Object,
    ) -> *mut ffi::Object {
        if handle.is_null() {
            return ptr::null_mut();
        }
        let slot = ZeroGcHandleStore::slot_from_handle(handle);
        match (*slot)
            .value
            .compare_exchange(comparand_object, object, Ordering::SeqCst, Ordering::SeqCst)
        {
            Ok(previous) | Err(previous) => previous,
        }
    }

    pub unsafe fn handle_fetch_type(&mut self, handle: ffi::OBJECTHANDLE) -> ffi::HandleType {
        if handle.is_null() {
            return ffi::HandleType::HNDTYPE_DEFAULT;
        }
        (*ZeroGcHandleStore::slot_from_handle(handle)).r#type
    }
}

fn store_from_this(this: *mut c_void) -> *mut ZeroGcHandleStore {
    this.cast()
}

fn manager_from_this(this: *mut c_void) -> *mut ZeroGcHandleManager {
    this.cast()
}

unsafe extern "C" fn handle_store_destructor(this: *mut c_void) {
    crate::abort_on_panic(|| unsafe {
        if !this.is_null() {
            drop(Box::from_raw(store_from_this(this)));
        }
    })
}

unsafe extern "C" fn handle_store_uproot(_this: *mut c_void) {
    crate::abort_on_panic(|| {})
}

unsafe extern "C" fn handle_store_contains_handle(_this: *mut c_void, handle: ffi::OBJECTHANDLE) -> bool {
    crate::abort_on_panic(|| !handle.is_null())
}

unsafe extern "C" fn handle_store_create_handle_of_type(
    this: *mut c_void,
    object: *mut ffi::Object,
    r#type: ffi::HandleType,
) -> ffi::OBJECTHANDLE {
    crate::abort_on_panic(|| unsafe { (*store_from_this(this)).create_handle_of_type(object, r#type) })
}

unsafe extern "C" fn handle_store_create_handle_of_type_with_affinity(
    this: *mut c_void,
    object: *mut ffi::Object,
    r#type: ffi::HandleType,
    _heap_to_affinitize_to: i32,
) -> ffi::OBJECTHANDLE {
    crate::abort_on_panic(|| unsafe { (*store_from_this(this)).create_handle_of_type(object, r#type) })
}

unsafe extern "C" fn handle_store_create_handle_with_extra_info(
    this: *mut c_void,
    object: *mut ffi::Object,
    r#type: ffi::HandleType,
    extra_info: *mut c_void,
) -> ffi::OBJECTHANDLE {
    crate::abort_on_panic(|| unsafe {
        (*store_from_this(this)).create_handle_with_extra_info(object, r#type, extra_info)
    })
}

unsafe extern "C" fn handle_store_create_dependent_handle(
    this: *mut c_void,
    primary: *mut ffi::Object,
    secondary: *mut ffi::Object,
) -> ffi::OBJECTHANDLE {
    crate::abort_on_panic(|| unsafe { (*store_from_this(this)).create_dependent_handle(primary, secondary) })
}

unsafe extern "C" fn handle_manager_initialize(this: *mut c_void) -> bool {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).initialize() })
}

unsafe extern "C" fn handle_manager_shutdown(_this: *mut c_void) {
    crate::abort_on_panic(|| {})
}

unsafe extern "C" fn handle_manager_get_global_handle_store(this: *mut c_void) -> *mut ffi::IGCHandleStore {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).get_global_handle_store() })
}

unsafe extern "C" fn handle_manager_create_handle_store(this: *mut c_void) -> *mut ffi::IGCHandleStore {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).create_handle_store() })
}

unsafe extern "C" fn handle_manager_destroy_handle_store(this: *mut c_void, store: *mut ffi::IGCHandleStore) {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).destroy_handle_store(store) })
}

unsafe extern "C" fn handle_manager_create_global_handle_of_type(
    this: *mut c_void,
    object: *mut ffi::Object,
    r#type: ffi::HandleType,
) -> ffi::OBJECTHANDLE {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).create_global_handle_of_type(object, r#type) })
}

unsafe extern "C" fn handle_manager_create_duplicate_handle(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
) -> ffi::OBJECTHANDLE {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).create_duplicate_handle(handle) })
}

unsafe extern "C" fn handle_manager_destroy_handle_of_type(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
    _type: ffi::HandleType,
) {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).destroy_handle_of_type(handle) })
}

unsafe extern "C" fn handle_manager_destroy_handle_of_unknown_type(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
) {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).destroy_handle_of_type(handle) })
}

unsafe extern "C" fn handle_manager_set_extra_info_for_handle(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
    _type: ffi::HandleType,
    extra_info: *mut c_void,
) {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).set_extra_info_for_handle(handle, extra_info) })
}

unsafe extern "C" fn handle_manager_get_extra_info_from_handle(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
) -> *mut c_void {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).get_extra_info_from_handle(handle) })
}

unsafe extern "C" fn handle_manager_store_object_in_handle(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
    object: *mut ffi::Object,
) {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).store_object_in_handle(handle, object) })
}

unsafe extern "C" fn handle_manager_store_object_in_handle_if_null(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
    object: *mut ffi::Object,
) -> bool {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).store_object_in_handle_if_null(handle, object) })
}

unsafe extern "C" fn handle_manager_set_dependent_handle_secondary(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
    object: *mut ffi::Object,
) {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).set_dependent_handle_secondary(handle, object) })
}

unsafe extern "C" fn handle_manager_get_dependent_handle_secondary(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
) -> *mut ffi::Object {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).get_dependent_handle_secondary(handle) })
}

unsafe extern "C" fn handle_manager_interlocked_compare_exchange_object_in_handle(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
    object: *mut ffi::Object,
    comparand_object: *mut ffi::Object,
) -> *mut ffi::Object {
    crate::abort_on_panic(|| unsafe {
        (*manager_from_this(this)).interlocked_compare_exchange_object_in_handle(handle, object, comparand_object)
    })
}

unsafe extern "C" fn handle_manager_handle_fetch_type(
    this: *mut c_void,
    handle: ffi::OBJECTHANDLE,
) -> ffi::HandleType {
    crate::abort_on_panic(|| unsafe { (*manager_from_this(this)).handle_fetch_type(handle) })
}

unsafe extern "C" fn handle_manager_trace_ref_counted_handles(
    _this: *mut c_void,
    _callback: ffi::HANDLESCANPROC,
    _param1: usize,
    _param2: usize,
) {
    crate::abort_on_panic(|| {})
}

pub static HANDLE_STORE_VTBL: ffi::IGCHandleStoreVtbl = ffi::IGCHandleStoreVtbl {
    uproot: handle_store_uproot,
    contains_handle: handle_store_contains_handle,
    create_handle_of_type: handle_store_create_handle_of_type,
    create_handle_of_type_with_affinity: handle_store_create_handle_of_type_with_affinity,
    create_handle_with_extra_info: handle_store_create_handle_with_extra_info,
    create_dependent_handle: handle_store_create_dependent_handle,
    destructor: handle_store_destructor,
};

pub static HANDLE_MANAGER_VTBL: ffi::IGCHandleManagerVtbl = ffi::IGCHandleManagerVtbl {
    initialize: handle_manager_initialize,
    shutdown: handle_manager_shutdown,
    get_global_handle_store: handle_manager_get_global_handle_store,
    create_handle_store: handle_manager_create_handle_store,
    destroy_handle_store: handle_manager_destroy_handle_store,
    create_global_handle_of_type: handle_manager_create_global_handle_of_type,
    create_duplicate_handle: handle_manager_create_duplicate_handle,
    destroy_handle_of_type: handle_manager_destroy_handle_of_type,
    destroy_handle_of_unknown_type: handle_manager_destroy_handle_of_unknown_type,
    set_extra_info_for_handle: handle_manager_set_extra_info_for_handle,
    get_extra_info_from_handle: handle_manager_get_extra_info_from_handle,
    store_object_in_handle: handle_manager_store_object_in_handle,
    store_object_in_handle_if_null: handle_manager_store_object_in_handle_if_null,
    set_dependent_handle_secondary: handle_manager_set_dependent_handle_secondary,
    get_dependent_handle_secondary: handle_manager_get_dependent_handle_secondary,
    interlocked_compare_exchange_object_in_handle: handle_manager_interlocked_compare_exchange_object_in_handle,
    handle_fetch_type: handle_manager_handle_fetch_type,
    trace_ref_counted_handles: handle_manager_trace_ref_counted_handles,
};
