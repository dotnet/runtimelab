#![allow(dead_code, non_snake_case, unused_unsafe)]

use crate::ffi;
use crate::handles::{LIVE_HANDLE_COUNT, PEAK_HANDLE_COUNT};
use crate::pal;
use core::ffi::{c_char, c_void};
use core::ptr;
use core::sync::atomic::{AtomicBool, AtomicI64, AtomicU64, AtomicUsize, Ordering};
use std::cell::RefCell;
use std::sync::Mutex;

pub const ARENA_RESERVE_SIZE: usize = 1usize << 36;
pub const ARENA_COMMIT_CHUNK: usize = 16 * 1024 * 1024;
pub const CONTEXT_ALLOC_QUANTUM: usize = 128 * 1024;
pub const THREAD_ARENA_CHUNK_SIZE: usize = 64 * 1024 * 1024;
const HEADER_PAD: usize = core::mem::size_of::<*const c_void>();
const CARD_BYTE_SHIFT: usize = 11;
const CARD_BUNDLE_BYTE_SHIFT: usize = 21;
const MAX_FROZEN_SEGMENTS: usize = 64;

static CONFIG_NAME: &[u8] = b"ZeroGC.rs\0";
static CONFIG_PUBLIC_KEY: &[u8] = b"System.GC.Name\0";

#[derive(Default)]
pub struct ZeroGcCounters {
    pub TotalAllocatedBytes: AtomicI64,
    pub AllocContextRefills: AtomicI64,
    pub InducedCollectRequests: AtomicI64,
}

pub static COUNTERS: ZeroGcCounters = ZeroGcCounters {
    TotalAllocatedBytes: AtomicI64::new(0),
    AllocContextRefills: AtomicI64::new(0),
    InducedCollectRequests: AtomicI64::new(0),
};

#[derive(Clone, Copy)]
struct ThreadArenaState {
    chunk_base: *mut u8,
    chunk_end: *mut u8,
    next_free: *mut u8,
    commit_end: *mut u8,
}

impl ThreadArenaState {
    const fn new() -> Self {
        Self {
            chunk_base: ptr::null_mut(),
            chunk_end: ptr::null_mut(),
            next_free: ptr::null_mut(),
            commit_end: ptr::null_mut(),
        }
    }
}

thread_local! {
    static THREAD_ARENA: RefCell<ThreadArenaState> = const { RefCell::new(ThreadArenaState::new()) };
}

#[derive(Clone, Copy, Default)]
struct FrozenSegment {
    base: *mut u8,
    allocated: *mut u8,
    committed: *mut u8,
    reserved: *mut u8,
    in_use: bool,
}

#[repr(C)]
pub struct ZeroGcHeap {
    pub vtbl: *const ffi::IGCHeapVtbl,
    arena_base: AtomicUsize,
    arena_next_free: AtomicUsize,
    arena_reserved_end: AtomicUsize,
    start_time_nanos: AtomicU64,
    initialized: AtomicBool,
    frozen_segments: Mutex<[FrozenSegment; MAX_FROZEN_SEGMENTS]>,
}

impl ZeroGcHeap {
    pub fn new() -> Box<Self> {
        Box::new(Self {
            vtbl: &IGC_HEAP_VTBL,
            arena_base: AtomicUsize::new(0),
            arena_next_free: AtomicUsize::new(0),
            arena_reserved_end: AtomicUsize::new(0),
            start_time_nanos: AtomicU64::new(0),
            initialized: AtomicBool::new(false),
            frozen_segments: Mutex::new([FrozenSegment::default(); MAX_FROZEN_SEGMENTS]),
        })
    }

    #[inline]
    fn arena_base(&self) -> *mut u8 {
        self.arena_base.load(Ordering::SeqCst) as *mut u8
    }

    #[inline]
    fn arena_reserved_end(&self) -> *mut u8 {
        self.arena_reserved_end.load(Ordering::SeqCst) as *mut u8
    }

    #[inline]
    fn arena_high_water_mark(&self) -> *mut u8 {
        let next = self.arena_next_free.load(Ordering::SeqCst);
        let end = self.arena_reserved_end.load(Ordering::SeqCst);
        core::cmp::min(next, end) as *mut u8
    }

    pub unsafe fn initialize(&self) -> ffi::HRESULT {
        if self.initialized.swap(true, Ordering::SeqCst) {
            return ffi::S_OK;
        }

        self.start_time_nanos.store(pal::now_nanos(), Ordering::SeqCst);
        let arena_base = match pal::reserve(ARENA_RESERVE_SIZE) {
            Some(ptr) => ptr,
            None => return ffi::E_OUTOFMEMORY,
        };
        let arena_reserved_end = arena_base.add(ARENA_RESERVE_SIZE);
        self.arena_base.store(arena_base as usize, Ordering::SeqCst);
        self.arena_next_free.store(arena_base as usize, Ordering::SeqCst);
        self.arena_reserved_end.store(arena_reserved_end as usize, Ordering::SeqCst);

        let low_card_index = (arena_base as usize) >> CARD_BYTE_SHIFT;
        let high_card_index = (arena_reserved_end as usize) >> CARD_BYTE_SHIFT;
        let card_table_size = (high_card_index - low_card_index) + 1;
        let card_table_raw = Box::leak(vec![0u8; card_table_size].into_boxed_slice()).as_mut_ptr();
        let card_table_biased = ((card_table_raw as usize).wrapping_sub(low_card_index)) as *mut u8;

        let low_bundle_index = (arena_base as usize) >> CARD_BUNDLE_BYTE_SHIFT;
        let high_bundle_index = (arena_reserved_end as usize) >> CARD_BUNDLE_BYTE_SHIFT;
        let card_bundle_size = (high_bundle_index - low_bundle_index) + 1;
        let card_bundle_raw = Box::leak(vec![0u8; card_bundle_size].into_boxed_slice()).as_mut_ptr();
        let card_bundle_biased = ((card_bundle_raw as usize).wrapping_sub(low_bundle_index)) as *mut u8;

        let mut wb_params = ffi::WriteBarrierParameters {
            operation: ffi::WriteBarrierOp::Initialize,
            is_runtime_suspended: true,
            requires_upper_bounds_check: false,
            card_table: card_table_biased.cast(),
            card_bundle_table: card_bundle_biased.cast(),
            lowest_address: arena_base,
            highest_address: arena_reserved_end,
            ephemeral_low: arena_reserved_end,
            ephemeral_high: arena_reserved_end,
            write_watch_table: ptr::null_mut(),
            region_to_generation_table: ptr::null_mut(),
            region_shr: 0,
            region_use_bitwise_write_barrier: false,
        };

        let clr = crate::G_THE_GC_TO_CLR.load(Ordering::SeqCst);
        ffi::call_stomp_write_barrier(clr, &mut wb_params);

        if let Ok(mut frozen) = self.frozen_segments.lock() {
            for entry in frozen.iter_mut() {
                *entry = FrozenSegment::default();
            }
        }

        ffi::S_OK
    }

    unsafe fn claim_arena_slice(&self, size: usize) -> *mut u8 {
        let old = self.arena_next_free.fetch_add(size, Ordering::SeqCst);
        let end = self.arena_reserved_end.load(Ordering::SeqCst);
        if old.saturating_add(size) > end {
            return ptr::null_mut();
        }
        old as *mut u8
    }

    unsafe fn allocate_from_arena(
        &self,
        acontext: *mut ffi::gc_alloc_context,
        size: usize,
        flags: u32,
    ) -> *mut ffi::Object {
        let aligned_size = (size + 7) & !7usize;
        let is_large = (flags & ffi::GC_ALLOC_LARGE_OBJECT_HEAP) != 0
            || (flags & ffi::GC_ALLOC_PINNED_OBJECT_HEAP) != 0
            || aligned_size >= ffi::LARGE_OBJECT_SIZE;
        let chunk_size = if is_large {
            aligned_size
        } else {
            core::cmp::max(aligned_size, CONTEXT_ALLOC_QUANTUM)
        };
        let reserve_size = chunk_size + HEADER_PAD;

        let mut result = ptr::null_mut();
        THREAD_ARENA.with(|arena_cell| {
            let mut ta = arena_cell.borrow_mut();
            let remaining = if ta.next_free.is_null() || ta.chunk_end.is_null() {
                0
            } else {
                ta.chunk_end as usize - ta.next_free as usize
            };
            if ta.chunk_base.is_null() || remaining < reserve_size {
                let claim_size = core::cmp::max(reserve_size, THREAD_ARENA_CHUNK_SIZE);
                let claim_base = unsafe { self.claim_arena_slice(claim_size) };
                if claim_base.is_null() {
                    return;
                }
                ta.chunk_base = claim_base;
                ta.chunk_end = unsafe { claim_base.add(claim_size) };
                ta.next_free = claim_base;
                ta.commit_end = claim_base;
            }

            let committed_remaining = ta.commit_end as usize - ta.next_free as usize;
            if committed_remaining < reserve_size {
                let needed = reserve_size - committed_remaining;
                let mut commit_size = core::cmp::max(needed, ARENA_COMMIT_CHUNK);
                commit_size = (commit_size + 0xFFFF) & !0xFFFFusize;
                let chunk_remaining = ta.chunk_end as usize - ta.commit_end as usize;
                if commit_size > chunk_remaining {
                    commit_size = chunk_remaining;
                }
                if !pal::commit(ta.commit_end, commit_size) {
                    return;
                }
                ta.commit_end = unsafe { ta.commit_end.add(commit_size) };
            }

            let raw_start = ta.next_free;
            let chunk_start = unsafe { raw_start.add(HEADER_PAD) };
            ta.next_free = unsafe { raw_start.add(reserve_size) };

            unsafe {
                if !is_large {
                    (*acontext).alloc_ptr = chunk_start.add(aligned_size);
                    (*acontext).alloc_limit = chunk_start.add(chunk_size);
                } else {
                    (*acontext).alloc_ptr = chunk_start.add(aligned_size);
                    (*acontext).alloc_limit = chunk_start.add(aligned_size);
                }
                (*acontext).alloc_bytes += aligned_size as i64;
                (*acontext).alloc_count += 1;
            }

            COUNTERS.TotalAllocatedBytes.fetch_add(aligned_size as i64, Ordering::SeqCst);
            COUNTERS.AllocContextRefills.fetch_add(1, Ordering::SeqCst);
            result = chunk_start.cast();
        });
        result
    }

    fn get_memory_load(&self) -> u32 {
        pal::memory_status().map(|m| m.mem_load_pct).unwrap_or(0)
    }

    fn total_bytes_in_use(&self) -> usize {
        let base = self.arena_base.load(Ordering::SeqCst);
        let high = self.arena_high_water_mark() as usize;
        high.saturating_sub(base)
    }

    unsafe fn fill_memory_info(
        &self,
        highMemLoadThresholdBytes: *mut u64,
        totalAvailableMemoryBytes: *mut u64,
        lastRecordedMemLoadBytes: *mut u64,
        lastRecordedHeapSizeBytes: *mut u64,
        lastRecordedFragmentationBytes: *mut u64,
        totalCommittedBytes: *mut u64,
        promotedBytes: *mut u64,
        pinnedObjectCount: *mut u64,
        finalizationPendingCount: *mut u64,
        index: *mut u64,
        generation: *mut u32,
        pauseTimePct: *mut u32,
        isCompaction: *mut bool,
        isConcurrent: *mut bool,
        genInfoRaw: *mut u64,
        pauseInfoRaw: *mut u64,
    ) {
        let mem = pal::memory_status().unwrap_or_default();
        let committed = self.total_bytes_in_use() as u64;
        if !highMemLoadThresholdBytes.is_null() { *highMemLoadThresholdBytes = mem.total_phys.saturating_mul(90) / 100; }
        if !totalAvailableMemoryBytes.is_null() { *totalAvailableMemoryBytes = mem.total_phys; }
        if !lastRecordedMemLoadBytes.is_null() { *lastRecordedMemLoadBytes = mem.total_phys.saturating_sub(mem.avail_phys); }
        if !lastRecordedHeapSizeBytes.is_null() { *lastRecordedHeapSizeBytes = committed; }
        if !lastRecordedFragmentationBytes.is_null() { *lastRecordedFragmentationBytes = 0; }
        if !totalCommittedBytes.is_null() { *totalCommittedBytes = committed; }
        if !promotedBytes.is_null() { *promotedBytes = 0; }
        if !pinnedObjectCount.is_null() { *pinnedObjectCount = 0; }
        if !finalizationPendingCount.is_null() { *finalizationPendingCount = 0; }
        if !index.is_null() { *index = 0; }
        if !generation.is_null() { *generation = 0; }
        if !pauseTimePct.is_null() { *pauseTimePct = 0; }
        if !isCompaction.is_null() { *isCompaction = false; }
        if !isConcurrent.is_null() { *isConcurrent = false; }
        if !genInfoRaw.is_null() { ptr::write_bytes(genInfoRaw, 0, 8); }
        if !pauseInfoRaw.is_null() { ptr::write_bytes(pauseInfoRaw, 0, 2); }
    }
}

fn heap_from_this(this: *mut c_void) -> *mut ZeroGcHeap {
    this.cast()
}

macro_rules! heap_call {
    ($($body:tt)*) => {
        crate::abort_on_panic(|| unsafe { $($body)* })
    };
}

unsafe extern "C" fn heap_is_valid_segment_size(this: *mut c_void, size: usize) -> bool {
    let _ = this;
    heap_call! { (size & (size - 1)) == 0 }
}
unsafe extern "C" fn heap_is_valid_gen0_max_size(_this: *mut c_void, _size: usize) -> bool { heap_call! { true } }
unsafe extern "C" fn heap_get_valid_segment_size(_this: *mut c_void, _large_seg: bool) -> usize { heap_call! { ARENA_COMMIT_CHUNK } }
unsafe extern "C" fn heap_set_reserved_vm_limit(_this: *mut c_void, _vmlimit: usize) { heap_call! { () } }
unsafe extern "C" fn heap_wait_until_concurrent_gc_complete(_this: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_is_concurrent_gc_in_progress(_this: *mut c_void) -> bool { heap_call! { false } }
unsafe extern "C" fn heap_temporary_enable_concurrent_gc(_this: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_temporary_disable_concurrent_gc(_this: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_is_concurrent_gc_enabled(_this: *mut c_void) -> bool { heap_call! { false } }
unsafe extern "C" fn heap_wait_until_concurrent_gc_complete_async(_this: *mut c_void, _timeout: i32) -> ffi::HRESULT { heap_call! { ffi::S_OK } }
unsafe extern "C" fn heap_get_number_of_finalizable(_this: *mut c_void) -> usize { heap_call! { 0 } }
unsafe extern "C" fn heap_get_next_finalizable(_this: *mut c_void) -> *mut ffi::Object { heap_call! { ptr::null_mut() } }
unsafe extern "C" fn heap_get_memory_info(
    this: *mut c_void,
    highMemLoadThresholdBytes: *mut u64,
    totalAvailableMemoryBytes: *mut u64,
    lastRecordedMemLoadBytes: *mut u64,
    lastRecordedHeapSizeBytes: *mut u64,
    lastRecordedFragmentationBytes: *mut u64,
    totalCommittedBytes: *mut u64,
    promotedBytes: *mut u64,
    pinnedObjectCount: *mut u64,
    finalizationPendingCount: *mut u64,
    index: *mut u64,
    generation: *mut u32,
    pauseTimePct: *mut u32,
    isCompaction: *mut bool,
    isConcurrent: *mut bool,
    genInfoRaw: *mut u64,
    pauseInfoRaw: *mut u64,
    _kind: i32,
) {
    heap_call! {
        (*heap_from_this(this)).fill_memory_info(
            highMemLoadThresholdBytes,
            totalAvailableMemoryBytes,
            lastRecordedMemLoadBytes,
            lastRecordedHeapSizeBytes,
            lastRecordedFragmentationBytes,
            totalCommittedBytes,
            promotedBytes,
            pinnedObjectCount,
            finalizationPendingCount,
            index,
            generation,
            pauseTimePct,
            isCompaction,
            isConcurrent,
            genInfoRaw,
            pauseInfoRaw,
        )
    }
}
unsafe extern "C" fn heap_get_memory_load(this: *mut c_void) -> u32 { heap_call! { (*heap_from_this(this)).get_memory_load() } }
unsafe extern "C" fn heap_get_gc_latency_mode(_this: *mut c_void) -> i32 { heap_call! { 2 } }
unsafe extern "C" fn heap_set_gc_latency_mode(_this: *mut c_void, _mode: i32) -> i32 { heap_call! { 0 } }
unsafe extern "C" fn heap_get_loh_compaction_mode(_this: *mut c_void) -> i32 { heap_call! { 0 } }
unsafe extern "C" fn heap_set_loh_compaction_mode(_this: *mut c_void, _mode: i32) { heap_call! { () } }
unsafe extern "C" fn heap_register_for_full_gc_notification(_this: *mut c_void, _g2: u32, _loh: u32) -> bool { heap_call! { false } }
unsafe extern "C" fn heap_cancel_full_gc_notification(_this: *mut c_void) -> bool { heap_call! { false } }
unsafe extern "C" fn heap_wait_for_full_gc_approach(_this: *mut c_void, _timeout: i32) -> i32 { heap_call! { ffi::wait_full_gc_status::wait_full_gc_na as i32 } }
unsafe extern "C" fn heap_wait_for_full_gc_complete(_this: *mut c_void, _timeout: i32) -> i32 { heap_call! { ffi::wait_full_gc_status::wait_full_gc_na as i32 } }
unsafe extern "C" fn heap_which_generation(_this: *mut c_void, _obj: *mut ffi::Object) -> u32 { heap_call! { 0 } }
unsafe extern "C" fn heap_collection_count(_this: *mut c_void, _generation: i32, _mode: i32) -> i32 { heap_call! { 0 } }
unsafe extern "C" fn heap_start_no_gc_region(_this: *mut c_void, _total: u64, _loh_known: bool, _loh: u64, _disallow: bool) -> i32 { heap_call! { ffi::start_no_gc_region_status::start_no_gc_success as i32 } }
unsafe extern "C" fn heap_end_no_gc_region(_this: *mut c_void) -> i32 { heap_call! { ffi::end_no_gc_region_status::end_no_gc_success as i32 } }
unsafe extern "C" fn heap_get_total_bytes_in_use(this: *mut c_void) -> usize { heap_call! { (*heap_from_this(this)).total_bytes_in_use() } }
unsafe extern "C" fn heap_get_total_allocated_bytes(this: *mut c_void) -> u64 { heap_call! { (*heap_from_this(this)).total_bytes_in_use() as u64 } }
unsafe extern "C" fn heap_garbage_collect(_this: *mut c_void, _generation: i32, _low_memory: bool, _mode: i32) -> ffi::HRESULT { heap_call! { COUNTERS.InducedCollectRequests.fetch_add(1, Ordering::SeqCst); ffi::S_OK } }
unsafe extern "C" fn heap_get_max_generation(_this: *mut c_void) -> u32 { heap_call! { 2 } }
unsafe extern "C" fn heap_set_finalization_run(_this: *mut c_void, _obj: *mut ffi::Object) { heap_call! { () } }
unsafe extern "C" fn heap_register_for_finalization(_this: *mut c_void, _gen: i32, _obj: *mut ffi::Object) -> bool { heap_call! { true } }
unsafe extern "C" fn heap_get_last_gc_percent_time_in_gc(_this: *mut c_void) -> i32 { heap_call! { 0 } }
unsafe extern "C" fn heap_get_last_gc_generation_size(_this: *mut c_void, _gen: i32) -> usize { heap_call! { 0 } }
unsafe extern "C" fn heap_initialize(this: *mut c_void) -> ffi::HRESULT { heap_call! { (*heap_from_this(this)).initialize() } }
unsafe extern "C" fn heap_is_promoted(_this: *mut c_void, _object: *mut ffi::Object) -> bool { heap_call! { true } }
unsafe extern "C" fn heap_is_heap_pointer(this: *mut c_void, object: *mut c_void, _small_heap_only: bool) -> bool {
    heap_call! {
        let heap = &*heap_from_this(this);
        let p = object as usize;
        let low = heap.arena_base.load(Ordering::SeqCst);
        let high = heap.arena_high_water_mark() as usize;
        p >= low && p < high
    }
}
unsafe extern "C" fn heap_get_condemned_generation(_this: *mut c_void) -> u32 { heap_call! { 0 } }
unsafe extern "C" fn heap_is_gc_in_progress_helper(_this: *mut c_void, _consider: bool) -> bool { heap_call! { false } }
unsafe extern "C" fn heap_get_gc_count(_this: *mut c_void) -> u32 { heap_call! { 0 } }
unsafe extern "C" fn heap_is_thread_using_allocation_context_heap(_this: *mut c_void, _acontext: *mut ffi::gc_alloc_context, _thread_number: i32) -> bool { heap_call! { true } }
unsafe extern "C" fn heap_is_ephemeral(_this: *mut c_void, _object: *mut ffi::Object) -> bool { heap_call! { true } }
unsafe extern "C" fn heap_wait_until_gc_complete(_this: *mut c_void, _consider: bool) -> u32 { heap_call! { 0 } }
unsafe extern "C" fn heap_fix_alloc_context(_this: *mut c_void, _acontext: *mut ffi::gc_alloc_context, _arg: *mut c_void, _heap: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_get_current_obj_size(_this: *mut c_void) -> usize { heap_call! { COUNTERS.TotalAllocatedBytes.load(Ordering::SeqCst) as usize } }
unsafe extern "C" fn heap_set_gc_in_progress(_this: *mut c_void, _in_progress: bool) { heap_call! { () } }
unsafe extern "C" fn heap_runtime_structures_valid(_this: *mut c_void) -> bool { heap_call! { true } }
unsafe extern "C" fn heap_set_suspension_pending(_this: *mut c_void, _pending: bool) { heap_call! { () } }
unsafe extern "C" fn heap_set_yield_processor_scaling_factor(_this: *mut c_void, _factor: f32) { heap_call! { () } }
unsafe extern "C" fn heap_shutdown(_this: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_get_last_gc_start_time(_this: *mut c_void, _generation: i32) -> usize { heap_call! { 0 } }
unsafe extern "C" fn heap_get_last_gc_duration(_this: *mut c_void, _generation: i32) -> usize { heap_call! { 0 } }
unsafe extern "C" fn heap_get_now(this: *mut c_void) -> usize {
    heap_call! {
        let heap = &*heap_from_this(this);
        let start = heap.start_time_nanos.load(Ordering::SeqCst);
        let now = pal::now_nanos();
        now.saturating_sub(start).saturating_div(1_000_000) as usize
    }
}
unsafe extern "C" fn heap_alloc(this: *mut c_void, acontext: *mut ffi::gc_alloc_context, size: usize, flags: u32) -> *mut ffi::Object {
    heap_call! { (*heap_from_this(this)).allocate_from_arena(acontext, size, flags) }
}
unsafe extern "C" fn heap_publish_object(_this: *mut c_void, _obj: *mut u8) { heap_call! { () } }
unsafe extern "C" fn heap_set_wait_for_gc_event(_this: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_reset_wait_for_gc_event(_this: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_is_large_object(_this: *mut c_void, object: *mut ffi::Object) -> bool {
    heap_call! {
        if object.is_null() {
            false
        } else {
            let mt = (*object).GetGCSafeMethodTable();
            !mt.is_null() && (*mt).GetBaseSize() as usize >= ffi::LARGE_OBJECT_SIZE
        }
    }
}
unsafe extern "C" fn heap_validate_object_member(_this: *mut c_void, _obj: *mut ffi::Object) { heap_call! { () } }
unsafe extern "C" fn heap_next_obj(_this: *mut c_void, _object: *mut ffi::Object) -> *mut ffi::Object { heap_call! { ptr::null_mut() } }
unsafe extern "C" fn heap_get_containing_object(_this: *mut c_void, _ptr: *mut c_void, _collected_only: bool) -> *mut ffi::Object { heap_call! { ptr::null_mut() } }
unsafe extern "C" fn heap_diag_walk_object(_this: *mut c_void, _obj: *mut ffi::Object, _fn: ffi::walk_fn, _context: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_diag_walk_object2(_this: *mut c_void, _obj: *mut ffi::Object, _fn: ffi::walk_fn2, _context: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_diag_walk_heap(_this: *mut c_void, _fn: ffi::walk_fn, _context: *mut c_void, _gen: i32, _walk_loh: bool) { heap_call! { () } }
unsafe extern "C" fn heap_diag_walk_survivors_with_type(_this: *mut c_void, _gc_context: *mut c_void, _fn: ffi::record_surv_fn, _diag_context: *mut c_void, _ty: ffi::walk_surv_type, _gen: i32) { heap_call! { () } }
unsafe extern "C" fn heap_diag_walk_finalize_queue(_this: *mut c_void, _gc_context: *mut c_void, _fn: ffi::fq_walk_fn) { heap_call! { () } }
unsafe extern "C" fn heap_diag_scan_finalize_queue(_this: *mut c_void, _fn: ffi::fq_scan_fn, _context: *mut ffi::ScanContext) { heap_call! { () } }
unsafe extern "C" fn heap_diag_scan_handles(_this: *mut c_void, _fn: ffi::handle_scan_fn, _gen: i32, _context: *mut ffi::ScanContext) { heap_call! { () } }
unsafe extern "C" fn heap_diag_scan_dependent_handles(_this: *mut c_void, _fn: ffi::handle_scan_fn, _gen: i32, _context: *mut ffi::ScanContext) { heap_call! { () } }
unsafe extern "C" fn heap_diag_descr_generations(this: *mut c_void, callback: ffi::gen_walk_fn, context: *mut c_void) {
    heap_call! {
        if let Some(callback) = callback {
            let heap = &*heap_from_this(this);
            callback(context, 0, heap.arena_base(), heap.arena_high_water_mark(), heap.arena_reserved_end());
        }
    }
}
unsafe extern "C" fn heap_diag_trace_gc_segments(_this: *mut c_void) { heap_call! { () } }
unsafe extern "C" fn heap_diag_get_gc_settings(_this: *mut c_void, settings: *mut ffi::EtwGCSettingsInfo) {
    heap_call! {
        if !settings.is_null() {
            ptr::write_bytes(settings, 0, 1);
        }
    }
}
unsafe extern "C" fn heap_stress_heap(_this: *mut c_void, _acontext: *mut ffi::gc_alloc_context) -> bool { heap_call! { false } }
unsafe extern "C" fn heap_register_frozen_segment(this: *mut c_void, pseginfo: *mut ffi::segment_info) -> ffi::segment_handle {
    heap_call! {
        if pseginfo.is_null() {
            return ptr::null_mut();
        }
        let heap = &*heap_from_this(this);
        let mut frozen = heap.frozen_segments.lock().unwrap();
        for (idx, entry) in frozen.iter_mut().enumerate() {
            if !entry.in_use {
                entry.in_use = true;
                entry.base = (*pseginfo).pvMem.cast();
                entry.allocated = entry.base.add((*pseginfo).ibAllocated);
                entry.committed = entry.base.add((*pseginfo).ibCommit);
                entry.reserved = entry.base.add((*pseginfo).ibReserved);
                return (idx + 1) as *mut c_void;
            }
        }
        ptr::null_mut()
    }
}
unsafe extern "C" fn heap_unregister_frozen_segment(this: *mut c_void, seg: ffi::segment_handle) {
    heap_call! {
        let idx = (seg as usize).wrapping_sub(1);
        if idx >= MAX_FROZEN_SEGMENTS {
            return;
        }
        let heap = &*heap_from_this(this);
        let mut frozen = heap.frozen_segments.lock().unwrap();
        frozen[idx] = FrozenSegment::default();
    }
}
unsafe extern "C" fn heap_is_in_frozen_segment(this: *mut c_void, object: *mut ffi::Object) -> bool {
    heap_call! {
        let p = object.cast::<u8>();
        let heap = &*heap_from_this(this);
        let frozen = heap.frozen_segments.lock().unwrap();
        frozen.iter().any(|entry| entry.in_use && p >= entry.base && p < entry.committed)
    }
}
unsafe extern "C" fn heap_control_events(_this: *mut c_void, _keyword: ffi::GCEventKeyword, _level: ffi::GCEventLevel) { heap_call! { () } }
unsafe extern "C" fn heap_control_private_events(_this: *mut c_void, _keyword: ffi::GCEventKeyword, _level: ffi::GCEventLevel) { heap_call! { () } }
unsafe extern "C" fn heap_get_generation_with_range(this: *mut c_void, _object: *mut ffi::Object, ppStart: *mut *mut u8, ppAllocated: *mut *mut u8, ppReserved: *mut *mut u8) -> u32 {
    heap_call! {
        let heap = &*heap_from_this(this);
        if !ppStart.is_null() { *ppStart = heap.arena_base(); }
        if !ppAllocated.is_null() { *ppAllocated = heap.arena_high_water_mark(); }
        if !ppReserved.is_null() { *ppReserved = heap.arena_reserved_end(); }
        0
    }
}
unsafe extern "C" fn heap_get_total_pause_duration(_this: *mut c_void) -> i64 { heap_call! { 0 } }
unsafe extern "C" fn heap_enumerate_configuration_values(_this: *mut c_void, context: *mut c_void, configuration_value_func: ffi::ConfigurationValueFunc) {
    heap_call! {
        if let Some(callback) = configuration_value_func {
            callback(
                context,
                CONFIG_NAME.as_ptr().cast::<c_char>(),
                CONFIG_PUBLIC_KEY.as_ptr().cast::<c_char>(),
                ffi::GCConfigurationType::StringUtf8,
                CONFIG_NAME.as_ptr() as usize as i64,
            );
        }
    }
}
unsafe extern "C" fn heap_update_frozen_segment(this: *mut c_void, seg: ffi::segment_handle, allocated: *mut u8, committed: *mut u8) {
    heap_call! {
        let idx = (seg as usize).wrapping_sub(1);
        if idx >= MAX_FROZEN_SEGMENTS {
            return;
        }
        let heap = &*heap_from_this(this);
        let mut frozen = heap.frozen_segments.lock().unwrap();
        frozen[idx].allocated = allocated;
        frozen[idx].committed = committed;
    }
}
unsafe extern "C" fn heap_refresh_memory_limit(_this: *mut c_void) -> i32 { heap_call! { ffi::refresh_memory_limit_status::refresh_success as i32 } }
unsafe extern "C" fn heap_enable_no_gc_region_callback(_this: *mut c_void, _callback: *mut ffi::NoGCRegionCallbackFinalizerWorkItem, _threshold: u64) -> ffi::enable_no_gc_region_callback_status { heap_call! { ffi::enable_no_gc_region_callback_status::not_started } }
unsafe extern "C" fn heap_get_extra_work_for_finalization(_this: *mut c_void) -> *mut ffi::FinalizerWorkItem { heap_call! { ptr::null_mut() } }
unsafe extern "C" fn heap_get_generation_budget(_this: *mut c_void, _generation: i32) -> u64 { heap_call! { ARENA_COMMIT_CHUNK as u64 } }
unsafe extern "C" fn heap_get_loh_threshold(_this: *mut c_void) -> usize { heap_call! { ffi::LARGE_OBJECT_SIZE } }
unsafe extern "C" fn heap_diag_walk_heap_with_ac_handling(_this: *mut c_void, _fn: ffi::walk_fn, _context: *mut c_void, _gen: i32, _walk_loh: bool) { heap_call! { () } }
unsafe extern "C" fn heap_null_bridge_objects_weak_refs(_this: *mut c_void, _length: usize, _unreachable: *mut c_void) { heap_call! { () } }

pub static IGC_HEAP_VTBL: ffi::IGCHeapVtbl = ffi::IGCHeapVtbl {
    is_valid_segment_size: heap_is_valid_segment_size,
    is_valid_gen0_max_size: heap_is_valid_gen0_max_size,
    get_valid_segment_size: heap_get_valid_segment_size,
    set_reserved_vm_limit: heap_set_reserved_vm_limit,
    wait_until_concurrent_gc_complete: heap_wait_until_concurrent_gc_complete,
    is_concurrent_gc_in_progress: heap_is_concurrent_gc_in_progress,
    temporary_enable_concurrent_gc: heap_temporary_enable_concurrent_gc,
    temporary_disable_concurrent_gc: heap_temporary_disable_concurrent_gc,
    is_concurrent_gc_enabled: heap_is_concurrent_gc_enabled,
    wait_until_concurrent_gc_complete_async: heap_wait_until_concurrent_gc_complete_async,
    get_number_of_finalizable: heap_get_number_of_finalizable,
    get_next_finalizable: heap_get_next_finalizable,
    get_memory_info: heap_get_memory_info,
    get_memory_load: heap_get_memory_load,
    get_gc_latency_mode: heap_get_gc_latency_mode,
    set_gc_latency_mode: heap_set_gc_latency_mode,
    get_loh_compaction_mode: heap_get_loh_compaction_mode,
    set_loh_compaction_mode: heap_set_loh_compaction_mode,
    register_for_full_gc_notification: heap_register_for_full_gc_notification,
    cancel_full_gc_notification: heap_cancel_full_gc_notification,
    wait_for_full_gc_approach: heap_wait_for_full_gc_approach,
    wait_for_full_gc_complete: heap_wait_for_full_gc_complete,
    which_generation: heap_which_generation,
    collection_count: heap_collection_count,
    start_no_gc_region: heap_start_no_gc_region,
    end_no_gc_region: heap_end_no_gc_region,
    get_total_bytes_in_use: heap_get_total_bytes_in_use,
    get_total_allocated_bytes: heap_get_total_allocated_bytes,
    garbage_collect: heap_garbage_collect,
    get_max_generation: heap_get_max_generation,
    set_finalization_run: heap_set_finalization_run,
    register_for_finalization: heap_register_for_finalization,
    get_last_gc_percent_time_in_gc: heap_get_last_gc_percent_time_in_gc,
    get_last_gc_generation_size: heap_get_last_gc_generation_size,
    initialize: heap_initialize,
    is_promoted: heap_is_promoted,
    is_heap_pointer: heap_is_heap_pointer,
    get_condemned_generation: heap_get_condemned_generation,
    is_gc_in_progress_helper: heap_is_gc_in_progress_helper,
    get_gc_count: heap_get_gc_count,
    is_thread_using_allocation_context_heap: heap_is_thread_using_allocation_context_heap,
    is_ephemeral: heap_is_ephemeral,
    wait_until_gc_complete: heap_wait_until_gc_complete,
    fix_alloc_context: heap_fix_alloc_context,
    get_current_obj_size: heap_get_current_obj_size,
    set_gc_in_progress: heap_set_gc_in_progress,
    runtime_structures_valid: heap_runtime_structures_valid,
    set_suspension_pending: heap_set_suspension_pending,
    set_yield_processor_scaling_factor: heap_set_yield_processor_scaling_factor,
    shutdown: heap_shutdown,
    get_last_gc_start_time: heap_get_last_gc_start_time,
    get_last_gc_duration: heap_get_last_gc_duration,
    get_now: heap_get_now,
    alloc: heap_alloc,
    publish_object: heap_publish_object,
    set_wait_for_gc_event: heap_set_wait_for_gc_event,
    reset_wait_for_gc_event: heap_reset_wait_for_gc_event,
    is_large_object: heap_is_large_object,
    validate_object_member: heap_validate_object_member,
    next_obj: heap_next_obj,
    get_containing_object: heap_get_containing_object,
    diag_walk_object: heap_diag_walk_object,
    diag_walk_object2: heap_diag_walk_object2,
    diag_walk_heap: heap_diag_walk_heap,
    diag_walk_survivors_with_type: heap_diag_walk_survivors_with_type,
    diag_walk_finalize_queue: heap_diag_walk_finalize_queue,
    diag_scan_finalize_queue: heap_diag_scan_finalize_queue,
    diag_scan_handles: heap_diag_scan_handles,
    diag_scan_dependent_handles: heap_diag_scan_dependent_handles,
    diag_descr_generations: heap_diag_descr_generations,
    diag_trace_gc_segments: heap_diag_trace_gc_segments,
    diag_get_gc_settings: heap_diag_get_gc_settings,
    stress_heap: heap_stress_heap,
    register_frozen_segment: heap_register_frozen_segment,
    unregister_frozen_segment: heap_unregister_frozen_segment,
    is_in_frozen_segment: heap_is_in_frozen_segment,
    control_events: heap_control_events,
    control_private_events: heap_control_private_events,
    get_generation_with_range: heap_get_generation_with_range,
    get_total_pause_duration: heap_get_total_pause_duration,
    enumerate_configuration_values: heap_enumerate_configuration_values,
    update_frozen_segment: heap_update_frozen_segment,
    refresh_memory_limit: heap_refresh_memory_limit,
    enable_no_gc_region_callback: heap_enable_no_gc_region_callback,
    get_extra_work_for_finalization: heap_get_extra_work_for_finalization,
    get_generation_budget: heap_get_generation_budget,
    get_loh_threshold: heap_get_loh_threshold,
    diag_walk_heap_with_ac_handling: heap_diag_walk_heap_with_ac_handling,
    null_bridge_objects_weak_refs: heap_null_bridge_objects_weak_refs,
};

pub fn handle_counts() -> (i64, i64) {
    (
        LIVE_HANDLE_COUNT.load(Ordering::SeqCst),
        PEAK_HANDLE_COUNT.load(Ordering::SeqCst),
    )
}
