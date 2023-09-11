#ifndef __EGLIB_CONFIG_H
#define __EGLIB_CONFIG_H

#ifdef _MSC_VER

#include <io.h>
#include <stddef.h>

#define MAXPATHLEN 242

#define STDOUT_FILENO (1)
#define STDERR_FILENO (2)

/* FIXME: what should this be ?*/
#define X_OK 4 /* This is really read */
#define WNOHANG 1
#define F_SETFD 1
#define FD_CLOEXEC 1

#ifndef __cplusplus
#undef inline
#define inline __inline
#endif

#define strtok_r strtok_s

#undef G_HAVE_UNISTD_H
#undef G_HAVE_SYS_TIME_H
#undef G_HAVE_SYS_WAIT_H
#undef G_HAVE_PWD_H
#undef G_HAVE_STRNDUP
#define G_HAVE_GETOPT_H 1

#else

#define HAVE_ALLOCA_H 1
#define HAVE_UNISTD_H 1

#ifdef HAVE_ALLOCA_H
#define G_HAVE_ALLOCA_H
#endif

#if HAVE_UNISTD_H
#define G_HAVE_UNISTD_H
#endif

#endif

/*
 * System-dependent settings
 */
#define G_GNUC_PRETTY_FUNCTION   
#define G_GNUC_UNUSED            __attribute__((__unused__))
#define G_BYTE_ORDER             G_LITTLE_ENDIAN
#define G_GNUC_NORETURN          __attribute__((__noreturn__))
#define G_SEARCHPATH_SEPARATOR_S ":"
#define G_SEARCHPATH_SEPARATOR   ':'
#define G_DIR_SEPARATOR          '/'
#define G_DIR_SEPARATOR_S        "/"
#define G_BREAKPOINT()           G_STMT_START { __asm__("int $03"); } G_STMT_END
#define G_OS_UNIX          1
#define G_GSIZE_FORMAT           "zu"

#if defined (HOST_WATCHOS)
#undef G_BREAKPOINT
#define G_BREAKPOINT()
#endif

#if defined (HOST_WASM)
#undef G_BREAKPOINT
#define G_BREAKPOINT() do { printf ("MONO: BREAKPOINT\n"); abort (); } while (0)
#endif

typedef size_t gsize;
typedef ptrdiff_t gssize;
typedef int GPid;

#ifdef _MSC_VER
typedef int pid_t;
#endif

#endif
