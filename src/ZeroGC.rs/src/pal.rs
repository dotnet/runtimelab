#![allow(dead_code, unused_imports)]

#[derive(Clone, Copy, Debug, Default)]
pub struct MemoryStatus {
    pub total_phys: u64,
    pub avail_phys: u64,
    pub mem_load_pct: u32,
}

#[cfg(windows)]
mod imp {
    use super::MemoryStatus;
    use core::mem;
    use core::ptr;
    use windows_sys::Win32::System::Memory::{
        VirtualAlloc, VirtualFree, MEM_COMMIT, MEM_DECOMMIT, MEM_RELEASE, MEM_RESERVE, PAGE_NOACCESS,
        PAGE_READWRITE,
    };
    use windows_sys::Win32::System::SystemInformation::{GlobalMemoryStatusEx, MEMORYSTATUSEX};

    #[link(name = "kernel32")]
    extern "system" {
        fn QueryPerformanceCounter(lpperformancecount: *mut i64) -> i32;
        fn QueryPerformanceFrequency(lpfrequency: *mut i64) -> i32;
    }

    pub fn reserve(size: usize) -> Option<*mut u8> {
        let ptr = unsafe { VirtualAlloc(ptr::null(), size, MEM_RESERVE, PAGE_READWRITE) };
        (!ptr.is_null()).then_some(ptr.cast())
    }

    pub fn commit(addr: *mut u8, size: usize) -> bool {
        !unsafe { VirtualAlloc(addr.cast(), size, MEM_COMMIT, PAGE_READWRITE) }.is_null()
    }

    pub fn decommit(addr: *mut u8, size: usize) -> bool {
        unsafe { VirtualFree(addr.cast(), size, MEM_DECOMMIT) != 0 }
    }

    pub fn release(addr: *mut u8, _size: usize) -> bool {
        unsafe { VirtualFree(addr.cast(), 0, MEM_RELEASE) != 0 }
    }

    pub fn protect_no_access(addr: *mut u8, size: usize) -> bool {
        !unsafe { VirtualAlloc(addr.cast(), size, MEM_COMMIT, PAGE_NOACCESS) }.is_null()
    }

    pub fn now_nanos() -> u64 {
        let mut freq = 0i64;
        let mut counter = 0i64;
        unsafe {
            QueryPerformanceFrequency(&mut freq);
            QueryPerformanceCounter(&mut counter);
        }
        if freq <= 0 {
            return 0;
        }
        ((counter as i128) * 1_000_000_000i128 / (freq as i128)) as u64
    }

    pub fn memory_status() -> Option<MemoryStatus> {
        let mut status = MEMORYSTATUSEX {
            dwLength: mem::size_of::<MEMORYSTATUSEX>() as u32,
            ..unsafe { mem::zeroed() }
        };
        let ok = unsafe { GlobalMemoryStatusEx(&mut status) };
        if ok == 0 {
            return None;
        }
        Some(MemoryStatus {
            total_phys: status.ullTotalPhys,
            avail_phys: status.ullAvailPhys,
            mem_load_pct: status.dwMemoryLoad,
        })
    }
}

#[cfg(unix)]
mod imp {
    use super::MemoryStatus;
    use core::ptr;
    use libc::{
        clock_gettime, mmap, mprotect, munmap, sysinfo, timespec, CLOCK_MONOTONIC, MAP_ANON,
        MAP_FAILED, MAP_NORESERVE, MAP_PRIVATE, PROT_NONE, PROT_READ, PROT_WRITE,
    };

    pub fn reserve(size: usize) -> Option<*mut u8> {
        let ptr = unsafe { mmap(ptr::null_mut(), size, PROT_NONE, MAP_PRIVATE | MAP_ANON | MAP_NORESERVE, -1, 0) };
        (ptr != MAP_FAILED).then_some(ptr.cast())
    }

    pub fn commit(addr: *mut u8, size: usize) -> bool {
        unsafe { mprotect(addr.cast(), size, PROT_READ | PROT_WRITE) == 0 }
    }

    pub fn decommit(addr: *mut u8, size: usize) -> bool {
        unsafe { mprotect(addr.cast(), size, PROT_NONE) == 0 }
    }

    pub fn release(addr: *mut u8, size: usize) -> bool {
        unsafe { munmap(addr.cast(), size) == 0 }
    }

    pub fn protect_no_access(addr: *mut u8, size: usize) -> bool {
        unsafe { mprotect(addr.cast(), size, PROT_NONE) == 0 }
    }

    pub fn now_nanos() -> u64 {
        let mut ts = timespec { tv_sec: 0, tv_nsec: 0 };
        let rc = unsafe { clock_gettime(CLOCK_MONOTONIC, &mut ts) };
        if rc != 0 {
            return 0;
        }
        (ts.tv_sec as u64)
            .saturating_mul(1_000_000_000)
            .saturating_add(ts.tv_nsec as u64)
    }

    pub fn memory_status() -> Option<MemoryStatus> {
        let mut info = unsafe { core::mem::zeroed::<libc::sysinfo>() };
        let rc = unsafe { sysinfo(&mut info) };
        if rc != 0 {
            return None;
        }
        let total = (info.totalram as u64).saturating_mul(info.mem_unit as u64);
        let avail = (info.freeram as u64).saturating_mul(info.mem_unit as u64);
        let mem_load_pct = if total == 0 {
            0
        } else {
            100u32.saturating_sub(((avail.saturating_mul(100)) / total) as u32)
        };
        Some(MemoryStatus {
            total_phys: total,
            avail_phys: avail,
            mem_load_pct,
        })
    }
}

pub use imp::{commit, decommit, memory_status, now_nanos, protect_no_access, release, reserve};
