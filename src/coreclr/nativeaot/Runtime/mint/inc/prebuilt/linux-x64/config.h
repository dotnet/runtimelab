#ifndef __MONO_CONFIG_H__
#define __MONO_CONFIG_H__

#ifdef _MSC_VER

// FIXME This is all questionable but the logs are flooded and nothing else is fixing them.
#pragma warning(disable:4090) // W1: const problem
#pragma warning(disable:4100) // W4: unreferenced formal parameter
#pragma warning(disable:4152) // W4: nonstandard extension, function/data pointer conversion in expression
#pragma warning(disable:4201) // W4: nonstandard extension used: nameless struct/union
#pragma warning(disable:4210) // W4: nonstandard extension used: function given file scope
#pragma warning(disable:4245) // W4: signed/unsigned mismatch
#pragma warning(disable:4389) // W4: signed/unsigned mismatch
#pragma warning(disable:4505) // W4: unreferenced function with internal linkage has been removed
#pragma warning(disable:4702) // W4: unreachable code
#pragma warning(disable:4706) // W4: assignment within conditional expression

#include <SDKDDKVer.h>

#if _WIN32_WINNT < 0x0601
#error "Mono requires Windows 7 or later."
#endif /* _WIN32_WINNT < 0x0601 */

#ifndef HAVE_WINAPI_FAMILY_SUPPORT

#define HAVE_WINAPI_FAMILY_SUPPORT

/* WIN API Family support */
#include <winapifamily.h>

#if WINAPI_FAMILY_PARTITION(WINAPI_PARTITION_DESKTOP)
	#define HAVE_CLASSIC_WINAPI_SUPPORT 1
	#define HAVE_UWP_WINAPI_SUPPORT 0
#elif WINAPI_FAMILY_PARTITION(WINAPI_PARTITION_APP)
	#define HAVE_CLASSIC_WINAPI_SUPPORT 0
	#define HAVE_UWP_WINAPI_SUPPORT 1
#else
	#define HAVE_CLASSIC_WINAPI_SUPPORT 0
	#define HAVE_UWP_WINAPI_SUPPORT 0
#ifndef HAVE_EXTERN_DEFINED_WINAPI_SUPPORT
	#error Unsupported WINAPI family
#endif
#endif

#endif
#endif

/* This platform does not support symlinks */
/* #undef HOST_NO_SYMLINKS */

/* pthread is a pointer */
/* #undef PTHREAD_POINTER_ID */

/* Targeting a Unix-like platform */
#define TARGET_UNIX 1

/* Targeting the Android platform */
/* #undef HOST_ANDROID */

/* ... */
/* #undef TARGET_ANDROID */

/* ... */
/* #undef USE_MACH_SEMA */

/* Targeting the Fuchsia platform */
/* #undef HOST_FUCHSIA */

/* Targeting the AIX and PASE platforms */
/* #undef HOST_AIX */

/* Host Platform is Win32 */
/* #undef HOST_WIN32 */

/* Target Platform is Win32 */
/* #undef TARGET_WIN32 */

/* ... */
/* #undef TARGET_WINDOWS */

/* Host Platform is Darwin */
/* #undef HOST_DARWIN */

/* Host Platform is OSX or Mac Catalyst */
/* #undef HOST_OSX */

/* ... */
/* #undef TARGET_OSX */

/* Host Platform is iOS */
/* #undef HOST_IOS */

/* Host Platform is tvOS */
/* #undef HOST_TVOS */

/* Host Platform is Mac Catalyst */
/* #undef HOST_MACCAT */

/* Target Platform is Linux */
#define TARGET_LINUX 1

/* Target Platform is Linux (bionic libc)*/
/* #undef TARGET_LINUX_BIONIC */

/* Target Platform is Linux (musl libc)*/
/* #undef TARGET_LINUX_MUSL */

/* Use classic Windows API support */
#define HAVE_CLASSIC_WINAPI_SUPPORT 1

/* Don't use UWP Windows API support */
/* #undef HAVE_UWP_WINAPI_SUPPORT */

/* Define to 1 if you have the <sys/types.h> header file. */
#define HAVE_SYS_TYPES_H 1

/* Define to 1 if you have the <sys/stat.h> header file. */
#define HAVE_SYS_STAT_H 1

/* Define to 1 if you have the <strings.h> header file. */
#define HAVE_STRINGS_H 1

/* Define to 1 if you have the <stdint.h> header file. */
#define HAVE_STDINT_H 1

/* Define to 1 if you have the <unistd.h> header file. */
#define HAVE_UNISTD_H 1

/* Define to 1 if you have the <signal.h> header file. */
#define HAVE_SIGNAL_H 1

/* Define to 1 if you have the <setjmp.h> header file. */
#define HAVE_SETJMP_H 1

/* Define to 1 if you have the <syslog.h> header file. */
#define HAVE_SYSLOG_H 1

/* Define to 1 if you have the <sys/filio.h> header file. */
/* #undef HAVE_SYS_FILIO_H */

/* Define to 1 if you have the <sys/sockio.h> header file. */
/* #undef HAVE_SYS_SOCKIO_H */

/* Define to 1 if you have the <netdb.h> header file. */
#define HAVE_NETDB_H 1

/* Define to 1 if you have the <utime.h> header file. */
#define HAVE_UTIME_H 1

/* Define to 1 if you have the <sys/utime.h> header file. */
/* #undef HAVE_SYS_UTIME_H */

/* Define to 1 if you have the <semaphore.h> header file. */
#define HAVE_SEMAPHORE_H 1

/* Define to 1 if you have the <sys/un.h> header file. */
#define HAVE_SYS_UN_H 1

/* Define to 1 if you have the <sys/syscall.h> header file. */
#define HAVE_SYS_SYSCALL_H 1

/* Define to 1 if you have the <sys/uio.h> header file. */
#define HAVE_SYS_UIO_H 1

/* Define to 1 if you have the <sys/param.h> header file. */
#define HAVE_SYS_PARAM_H 1

/* Define to 1 if you have the <sys/sysctl.h> header file. */
/* #undef HAVE_SYS_SYSCTL_H */

/* Define to 1 if you have the <sys/prctl.h> header file. */
#define HAVE_SYS_PRCTL_H 1

/* Define to 1 if you have the <gnu/lib-names.h> header file. */
#define HAVE_GNU_LIB_NAMES_H 1

/* Define to 1 if you have the <sys/socket.h> header file. */
#define HAVE_SYS_SOCKET_H 1

/* Define to 1 if you have the <sys/utsname.h> header file. */
#define HAVE_SYS_UTSNAME_H 1

/* Define to 1 if you have the <alloca.h> header file. */
#define HAVE_ALLOCA_H 1

/* Define to 1 if you have the <ucontext.h> header file. */
#define HAVE_UCONTEXT_H 1

/* Define to 1 if you have the <pwd.h> header file. */
#define HAVE_PWD_H 1

/* Define to 1 if you have the <grp.h> header file. */
/* #undef HAVE_GRP_H */

/* Define to 1 if you have the <sys/select.h> header file. */
#define HAVE_SYS_SELECT_H 1

/* Define to 1 if you have the <netinet/tcp.h> header file. */
#define HAVE_NETINET_TCP_H 1

/* Define to 1 if you have the <netinet/in.h> header file. */
#define HAVE_NETINET_IN_H 1

/* Define to 1 if you have the <link.h> header file. */
#define HAVE_LINK_H 1

/* Define to 1 if you have the <arpa/inet.h> header file. */
#define HAVE_ARPA_INET_H 1

/* Define to 1 if you have the <unwind.h> header file. */
#define HAVE_UNWIND_H 1

/* Define to 1 if you have the <sys/user.h> header file. */
#define HAVE_SYS_USER_H 1

/* Use static ICU */
/* #undef STATIC_ICU */

/* Use in-tree zlib */
/* #undef INTERNAL_ZLIB */

/* Define to 1 if you have the <poll.h> header file. */
#define HAVE_POLL_H 1

/* Define to 1 if you have the <sys/poll.h> header file. */
#define HAVE_SYS_POLL_H 1

/* Define to 1 if you have the <sys/wait.h> header file. */
#define HAVE_SYS_WAIT_H 1

/* Define to 1 if you have the <wchar.h> header file. */
#define HAVE_WCHAR_H 1

/* Define to 1 if you have the <linux/magic.h> header file. */
/* #undef HAVE_LINUX_MAGIC_H */

/* Define to 1 if you have the <android/legacy_signal_inlines.h> header file.
   */
/* #undef HAVE_ANDROID_LEGACY_SIGNAL_INLINES_H */

/* The size of `void *', as computed by sizeof. */
#define SIZEOF_VOID_P 8

/* The size of `long', as computed by sizeof. */
#define SIZEOF_LONG 8

/* The size of `int', as computed by sizeof. */
#define SIZEOF_INT 4

/* The size of `long long', as computed by sizeof. */
#define SIZEOF_LONG_LONG 8

/* Reduce runtime requirements (and capabilities) */
/* #undef MONO_SMALL_CONFIG */

/* Disable AOT Compiler */
/* #undef DISABLE_AOT */

/* Disable runtime debugging support */
/* #undef DISABLE_DEBUG */

/* Disable reflection emit support */
/* #undef DISABLE_REFLECTION_EMIT */

/* Disable support debug logging */
/* #undef DISABLE_LOGGING */

/* Disable COM support */
#define DISABLE_COM 1

/* Disable advanced SSA JIT optimizations */
/* #undef DISABLE_SSA */

/* Disable the JIT, only full-aot mode or interpreter will be supported by the
   runtime. */
/* #undef DISABLE_JIT */

/* Disable the interpreter. */
/* #undef DISABLE_INTERPRETER */

/* Disable non-blittable marshalling */
/* #undef DISABLE_NONBLITTABLE */

/* Disable SIMD intrinsics related optimizations. */
/* #undef DISABLE_SIMD */

/* Disable Soft Debugger Agent. */
/* #undef DISABLE_DEBUGGER_AGENT */

/* Disable support code for the LLDB plugin. */
/* #undef DISABLE_LLDB */

/* Disable assertion messages. */
/* #undef DISABLE_ASSERT_MESSAGES */

/* Disable concurrent gc support in SGEN. */
/* #undef DISABLE_SGEN_MAJOR_MARKSWEEP_CONC */

/* Disable minor=split support in SGEN. */
/* #undef DISABLE_SGEN_SPLIT_NURSERY */

/* Disable gc bridge support in SGEN. */
/* #undef DISABLE_SGEN_GC_BRIDGE */

/* Disable debug helpers in SGEN. */
/* #undef DISABLE_SGEN_DEBUG_HELPERS */

/* Disable sockets */
/* #undef DISABLE_SOCKETS */

/* Disables use of DllMaps in MonoVM */
#define DISABLE_DLLMAP 1

/* Disable Threads */
/* #undef DISABLE_THREADS */

/* Disable MONO_LOG_DEST */
/* #undef DISABLE_LOG_DEST */

/* Length of zero length arrays */
#define MONO_ZERO_LEN_ARRAY 0

/* Define to 1 if you have the `sigaction' function. */
#define HAVE_SIGACTION 1

/* Define to 1 if you have the `kill' function. */
#define HAVE_KILL 1

/* CLOCK_MONOTONIC */
#define HAVE_CLOCK_MONOTONIC 1

/* CLOCK_MONOTONIC_COARSE */
#define HAVE_CLOCK_MONOTONIC_COARSE 1

/* clockid_t */
#define HAVE_CLOCKID_T 1

/* mach_absolute_time */
/* #undef HAVE_MACH_ABSOLUTE_TIME */

/* gethrtime */
/* #undef HAVE_GETHRTIME */

/* read_real_time */
/* #undef HAVE_READ_REAL_TIME */

/* Define to 1 if you have the `clock_nanosleep' function. */
#define HAVE_CLOCK_NANOSLEEP 1

/* Define to 1 if you have the <execinfo.h> header file. */
#define HAVE_EXECINFO_H 1

/* Define to 1 if you have the <sys/resource.h> header file. */
#define HAVE_SYS_RESOURCE_H 1

/* Define to 1 if you have the `backtrace_symbols' function. */
#define HAVE_BACKTRACE_SYMBOLS 1

/* Define to 1 if you have the `mkstemp' function. */
#define HAVE_MKSTEMP 1

/* Define to 1 if you have the `mmap' function. */
#define HAVE_MMAP 1

/* Define to 1 if you have the `madvise' function. */
#define HAVE_MADVISE 1

/* Define to 1 if you have the `getrusage' function. */
#define HAVE_GETRUSAGE 1

/* Define to 1 if you have the `dladdr' function. */
#define HAVE_DLADDR 1

/* Define to 1 if you have the `sysconf' function. */
#define HAVE_SYSCONF 1

/* Define to 1 if you have the `getrlimit' function. */
#define HAVE_GETRLIMIT 1

/* Define to 1 if you have the `prctl' function. */
#define HAVE_PRCTL 1

/* Define to 1 if you have the `nl_langinfo' function. */
#define HAVE_NL_LANGINFO 1

/* sched_getaffinity */
#define HAVE_SCHED_GETAFFINITY 1

/* sched_setaffinity */
#define HAVE_SCHED_SETAFFINITY 1

/* Define to 1 if you have the `chmod' function. */
#define HAVE_CHMOD 1

/* Define to 1 if you have the `lstat' function. */
#define HAVE_LSTAT 1

/* Define to 1 if you have the `getdtablesize' function. */
#define HAVE_GETDTABLESIZE 1

/* Define to 1 if you have the `ftruncate' function. */
#define HAVE_FTRUNCATE 1

/* Define to 1 if you have the `msync' function. */
#define HAVE_MSYNC 1

/* Define to 1 if you have the `getpeername' function. */
#define HAVE_GETPEERNAME 1

/* Define to 1 if you have the `utime' function. */
#define HAVE_UTIME 1

/* Define to 1 if you have the `utimes' function. */
#define HAVE_UTIMES 1

/* Define to 1 if you have the `openlog' function. */
#define HAVE_OPENLOG 1

/* Define to 1 if you have the `closelog' function. */
#define HAVE_CLOSELOG 1

/* Define to 1 if you have the `atexit' function. */
#define HAVE_ATEXIT 1

/* Define to 1 if you have the `popen' function. */
#define HAVE_POPEN 1

/* Define to 1 if you have the `strerror_r' function. */
#define HAVE_STRERROR_R 1

/* GLIBC has CPU_COUNT macro in sched.h */
#define HAVE_GNU_CPU_COUNT

/* Have large file support */
/* #undef HAVE_LARGE_FILE_SUPPORT */

/* Have getaddrinfo */
#define HAVE_GETADDRINFO 1

/* Have gethostbyname2 */
#define HAVE_GETHOSTBYNAME2 1

/* Have gethostbyname */
#define HAVE_GETHOSTBYNAME 1

/* Have getprotobyname */
#define HAVE_GETPROTOBYNAME 1

/* Have getprotobyname_r */
#define HAVE_GETPROTOBYNAME_R 1

/* Have getnameinfo */
#define HAVE_GETNAMEINFO 1

/* Have inet_ntop */
#define HAVE_INET_NTOP 1

/* Have inet_pton */
#define HAVE_INET_PTON 1

/* Define to 1 if you have the `inet_aton' function. */
#define HAVE_INET_ATON 1

/* Define to 1 if you have the <pthread.h> header file. */
#define HAVE_PTHREAD_H 1

/* Define to 1 if you have the <pthread_np.h> header file. */
/* #undef HAVE_PTHREAD_NP_H */

/* Define to 1 if you have the `pthread_mutex_timedlock' function. */
/* #undef HAVE_PTHREAD_MUTEX_TIMEDLOCK */

/* Define to 1 if you have the `pthread_getattr_np' function. */
/* #undef HAVE_PTHREAD_GETATTR_NP */

/* Define to 1 if you have the `pthread_attr_get_np' function. */
/* #undef HAVE_PTHREAD_ATTR_GET_NP */

/* Define to 1 if you have the `pthread_getname_np' function. */
#define HAVE_PTHREAD_GETNAME_NP 1

/* Define to 1 if you have the `pthread_setname_np' function. */
#define HAVE_PTHREAD_SETNAME_NP 1

/* Define to 1 if you have the `pthread_cond_timedwait_relative_np' function.
   */
/* #undef HAVE_PTHREAD_COND_TIMEDWAIT_RELATIVE_NP */

/* Define to 1 if you have the `pthread_kill' function. */
#define HAVE_PTHREAD_KILL 1

/* Define to 1 if you have the `pthread_attr_setstacksize' function. */
#define HAVE_PTHREAD_ATTR_SETSTACKSIZE 1

/* Define to 1 if you have the `pthread_get_stackaddr_np' function. */
/* #undef HAVE_PTHREAD_GET_STACKADDR_NP */

/* Define to 1 if you have the `pthread_jit_write_protect_np' function. */
/* #undef HAVE_PTHREAD_JIT_WRITE_PROTECT_NP */

/* Have getauxval */
#define HAVE_GETAUXVAL 1

/* Define to 1 if you have the declaration of `pthread_mutexattr_setprotocol',
   and to 0 if you don't. */
#define HAVE_DECL_PTHREAD_MUTEXATTR_SETPROTOCOL 1

/* Enable support for using sigaltstack for SIGSEGV and stack overflow handling, this doesn't work on some platforms */
/* #undef ENABLE_SIGALTSTACK */

/* Define to 1 if you have the `poll' function. */
#define HAVE_POLL 1

/* Define to 1 if you have the <sys/ioctl.h> header file. */
#define HAVE_SYS_IOCTL_H 1

/* Define to 1 if you have the <net/if.h> header file. */
#define HAVE_NET_IF_H 1

/* sockaddr_in has sin_len */
/* #undef HAVE_SOCKADDR_IN_SIN_LEN */

/* sockaddr_in6 has sin6_len */
/* #undef HAVE_SOCKADDR_IN6_SIN_LEN */

/* Have getifaddrs */
#define HAVE_GETIFADDRS 1

/* Have access */
#define HAVE_ACCESS 1

/* Have getpid */
#define HAVE_GETPID 1

/* Have mktemp */
#define HAVE_MKTEMP 1

/* Define to 1 if you have the <sys/statvfs.h> header file. */
#define HAVE_SYS_STATVFS_H 1

/* Define to 1 if you have the <sys/statfs.h> header file. */
#define HAVE_SYS_STATFS_H 1

/* Define to 1 if you have the <sys/mman.h> header file. */
#define HAVE_SYS_MMAN_H 1

/* Define to 1 if you have the <sys/mount.h> header file. */
#define HAVE_SYS_MOUNT_H 1

/* Define to 1 if you have the `getfsstat' function. */
/* #undef HAVE_GETFSSTAT */

/* Define to 1 if you have the `mremap' function. */
#define HAVE_MREMAP 1

/* Define to 1 if you have the `posix_fadvise' function. */
#define HAVE_POSIX_FADVISE 1

/* Define to 1 if you have the `vsnprintf' function. */
#define HAVE_VSNPRINTF 1

/* struct statfs */
#define HAVE_STATFS 1

/* Define to 1 if you have the `statvfs' function. */
#define HAVE_STATVFS 1

/* Define to 1 if you have the `setpgid' function. */
#define HAVE_SETPGID 1

/* Define to 1 if you have the `system' function. */
#ifdef _MSC_VER
#if HAVE_WINAPI_FAMILY_SUPPORT(HAVE_CLASSIC_WINAPI_SUPPORT)
#define HAVE_SYSTEM 1
#endif
#else
#define HAVE_SYSTEM 1
#endif

/* Define to 1 if you have the `fork' function. */
#define HAVE_FORK 1

/* Define to 1 if you have the `execv' function. */
#define HAVE_EXECV 1

/* Define to 1 if you have the `execve' function. */
#define HAVE_EXECVE 1

/* Define to 1 if you have the `waitpid' function. */
#define HAVE_WAITPID 1

/* Define to 1 if you have the `localtime_r' function. */
#define HAVE_LOCALTIME_R 1

/* Define to 1 if you have the `mkdtemp' function. */
#define HAVE_MKDTEMP 1

/* The size of `size_t', as computed by sizeof. */
#define SIZEOF_SIZE_T 8

#define HAVE_GNU_STRERROR_R 1

/* Define to 1 if the system has the type `struct sockaddr'. */
/* #undef HAVE_STRUCT_SOCKADDR */

/* Define to 1 if the system has the type `struct sockaddr_in'. */
/* #undef HAVE_STRUCT_SOCKADDR_IN */

/* Define to 1 if the system has the type `struct sockaddr_in6'. */
#define HAVE_STRUCT_SOCKADDR_IN6 1

/* Define to 1 if the system has the type `struct stat'. */
/* #undef HAVE_STRUCT_STAT */

/* Define to 1 if the system has the type `struct timeval'. */
#define HAVE_STRUCT_TIMEVAL 1

/* Define to 1 if `st_atim' is a member of `struct stat'. */
#define HAVE_STRUCT_STAT_ST_ATIM 1

/* Define to 1 if `st_atimespec' is a member of `struct stat'. */
/* #undef HAVE_STRUCT_STAT_ST_ATIMESPEC */

/* Define to 1 if `super_class' is a member of `struct objc_super'. */
/* #undef HAVE_OBJC_SUPER_SUPER_CLASS */

/* Define to 1 if you have the <sys/time.h> header file. */
#define HAVE_SYS_TIME_H 1

/* Define to 1 if you have the <dirent.h> header file. */
#define HAVE_DIRENT_H 1

/* Define to 1 if you have the <CommonCrypto/CommonDigest.h> header file. */
/* #undef HAVE_COMMONCRYPTO_COMMONDIGEST_H */

/* Define to 1 if you have the <sys/random.h> header file. */
#define HAVE_SYS_RANDOM_H 1

/* Define to 1 if you have the `getrandom' function. */
#define HAVE_GETRANDOM 1

/* Define to 1 if you have the `getentropy' function. */
#define HAVE_GETENTROPY 1

/* Define to 1 if you have the `strlcpy' function. */
/* #undef HAVE_STRLCPY */

/* Define to 1 if you have the <winternl.h> header file. */
/* #undef HAVE_WINTERNL_H */

/* Have socklen_t */
#define HAVE_SOCKLEN_T 1

/* Define to 1 if you have the `execvp' function. */
#define HAVE_EXECVP 1

/* Name of /dev/random */
#define NAME_DEV_RANDOM "/dev/random"

/* Enable DTrace probes */
/* #undef ENABLE_DTRACE */

/* AOT cross offsets file */
/* #undef MONO_OFFSETS_FILE */

/* Enable the LLVM back end */
/* #undef ENABLE_LLVM */

/* Runtime support code for llvm enabled */
/* #undef ENABLE_LLVM_RUNTIME */

/* 64 bit mode with 4 byte longs and pointers */
/* #undef MONO_ARCH_ILP32 */

/* The runtime is compiled for cross-compiling mode */
/* #undef MONO_CROSS_COMPILE */

/* ... */
/* #undef TARGET_BROWSER */

/* ... */
/* #undef TARGET_WASM */

/* ... */
/* #undef TARGET_WASI */

/* The JIT/AOT targets WatchOS */
/* #undef TARGET_WATCHOS */

/* ... */
/* #undef TARGET_PS3 */

/* ... */
/* #undef TARGET_XBOX360 */

/* ... */
/* #undef TARGET_PS4 */

/* Target is RISC-V */
/* #undef TARGET_RISCV */

/* Target is 32-bit RISC-V */
/* #undef TARGET_RISCV32 */

/* Target is 64-bit RISC-V */
/* #undef TARGET_RISCV64 */

/* ... */
/* #undef TARGET_X86 */

/* ... */
#define TARGET_AMD64 1

/* ... */
/* #undef TARGET_ARM */

/* ... */
/* #undef TARGET_ARM64 */

/* ... */
/* #undef TARGET_POWERPC */

/* ... */
/* #undef TARGET_POWERPC64 */

/* ... */
/* #undef TARGET_S390X */

/* ... */
/* #undef HOST_WASM */

/* ... */
/* #undef HOST_BROWSER */

/* ... */
/* #undef HOST_WASI */

/* ... */
/* #undef HOST_X86 */

/* ... */
#define HOST_AMD64 1

/* ... */
/* #undef HOST_ARM */

/* ... */
/* #undef HOST_ARM64 */

/* ... */
/* #undef HOST_POWERPC */

/* ... */
/* #undef HOST_POWERPC64 */

/* ... */
/* #undef HOST_S390X */

/* Host is RISC-V */
/* #undef HOST_RISCV */

/* Host is 32-bit RISC-V */
/* #undef HOST_RISCV32 */

/* Host is 64-bit RISC-V */
/* #undef HOST_RISCV64 */

/* ... */
#define USE_GCC_ATOMIC_OPS 1

/* The JIT/AOT targets iOS */
/* #undef TARGET_IOS */

/* The JIT/AOT targets tvOS */
/* #undef TARGET_TVOS */

/* The JIT/AOT targets Mac Catalyst */
/* #undef TARGET_MACCAT */

/* The JIT/AOT targets OSX or Mac Catalyst */
/* #undef TARGET_OSX */

/* The JIT/AOT targets Apple platforms */
/* #undef TARGET_MACH */

/* The JIT/AOT targets Apple mobile platforms */
/* #undef TARGET_APPLE_MOBILE */

/* byte order of target */
#define TARGET_BYTE_ORDER G_LITTLE_ENDIAN

/* wordsize of target */
#define TARGET_SIZEOF_VOID_P 8

/* size of target machine integer registers */
#define SIZEOF_REGISTER 8

/* host or target doesn't allow unaligned memory access */
/* #undef NO_UNALIGNED_ACCESS */

/* Support for the deprecated attribute */
/* #undef HAVE_DEPRECATED */

/* Moving collector */
#define HAVE_MOVING_COLLECTOR 1

/* Defaults to concurrent GC */
#define HAVE_CONC_GC_AS_DEFAULT 1

/* Define to 1 if you have the `stpcpy' function. */
#define HAVE_STPCPY 1

/* Define to 1 if you have the `strtok_r' function. */
#define HAVE_STRTOK_R 1

/* Define to 1 if you have the `rewinddir' function. */
#define HAVE_REWINDDIR 1

/* Define to 1 if you have the `vasprintf' function. */
#define HAVE_VASPRINTF 1

/* Overridable allocator support enabled */
/* #undef ENABLE_OVERRIDABLE_ALLOCATORS */

/* Define to 1 if you have the `strndup' function. */
#define HAVE_STRNDUP 1

/* Define to 1 if you have the <getopt.h> header file. */
#define HAVE_GETOPT_H 1

/* Icall symbol map enabled */
/* #undef ENABLE_ICALL_SYMBOL_MAP */

/* Icall export enabled */
/* #undef ENABLE_ICALL_EXPORT */

/* Icall tables disabled */
/* #undef DISABLE_ICALL_TABLES */

/* QCalls disabled */
/* #undef DISABLE_QCALLS */

/* Embedded PDB support disabled */
/* #undef DISABLE_EMBEDDED_PDB */

/* log profiler compressed output disabled */
/* #undef DISABLE_LOG_PROFILER_GZ */

/* Have __thread keyword */
/* #undef MONO_KEYWORD_THREAD */

/* tls_model available */
#define HAVE_TLS_MODEL_ATTR 1

/* ARM v5 */
/* #undef HAVE_ARMV5 */

/* ARM v6 */
/* #undef HAVE_ARMV6 */

/* ARM v7 */
/* #undef HAVE_ARMV7 */

/* RISC-V FPABI is double-precision */
/* #undef RISCV_FPABI_DOUBLE */

/* RISC-V FPABI is single-precision */
/* #undef RISCV_FPABI_SINGLE */

/* RISC-V FPABI is soft float */
/* #undef RISCV_FPABI_SOFT */

/* Use malloc for each single mempool allocation */
/* #undef USE_MALLOC_FOR_MEMPOOLS */

/* Enable lazy gc thread creation by the embedding host. */
/* #undef LAZY_GC_THREAD_CREATION */

/* Enable cooperative stop-the-world garbage collection. */
/* #undef ENABLE_COOP_SUSPEND */

/* Enable hybrid suspend for GC stop-the-world */
#define ENABLE_HYBRID_SUSPEND 1

/* Enable experiment 'Tiered Compilation' */
/* #undef ENABLE_EXPERIMENT_TIERED */

/* Enable EventPipe library support */
#define ENABLE_PERFTRACING 1

/* Define to 1 if you have /usr/include/malloc.h. */
/* #undef HAVE_USR_INCLUDE_MALLOC_H */

/* Define to 1 if you have linux cgroups */
#define HAVE_CGROUP_SUPPORT 1

/* The architecture this is running on */
#define MONO_ARCHITECTURE "amd64"

/* Disable banned functions from being used by the runtime */
#define MONO_INSIDE_RUNTIME 1

/* Version number of package */
#define VERSION "9.0.0.0"

/* Full version number of package */
#define FULL_VERSION "42.42.42.42424"

/* Define to 1 if you have the <dlfcn.h> header file. */
#define HAVE_DLFCN_H 1

/* Enable lazy gc thread creation by the embedding host */
/* #undef LAZY_GC_THREAD_CREATION */

/* Enable additional checks */
#define ENABLE_CHECKED_BUILD 1

/* Enable compile time checking that getter functions are used */
#define ENABLE_CHECKED_BUILD_PRIVATE_TYPES 1

/* Enable runtime GC Safe / Unsafe mode assertion checks (must set env var MONO_CHECK_MODE=gc) */
/* #undef ENABLE_CHECKED_BUILD_GC */

/* Enable runtime history of per-thread coop state transitions (must set env var MONO_CHECK_MODE=thread) */
/* #undef ENABLE_CHECKED_BUILD_THREAD */

/* Enable runtime checks of mempool references between metadata images (must set env var MONO_CHECK_MODE=metadata) */
/* #undef ENABLE_CHECKED_BUILD_METADATA */

/* Enable runtime checks of casts between types */
/* #undef ENABLE_CHECKED_BUILD_CASTS */

/* Enable static linking of mono runtime components */
#define STATIC_COMPONENTS

/* Enable perf jit dump support */
#define ENABLE_JIT_DUMP 1

/* Enable System.WeakAttribute support */
/* #undef ENABLE_WEAK_ATTR */

/* Enable WebCIL image loader */
/* #undef ENABLE_WEBCIL */

/* define if clockgettime exists */
#define HAVE_CLOCK_GETTIME 1

#if defined(ENABLE_LLVM) && defined(HOST_WIN32) && defined(TARGET_WIN32) && (!defined(TARGET_AMD64) || !defined(_MSC_VER))
#error LLVM for host=Windows and target=Windows is only supported on x64 MSVC build.
#endif

#endif
