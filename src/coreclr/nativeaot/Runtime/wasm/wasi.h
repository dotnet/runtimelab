// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef	_WASI_H
#define	_WASI_H

#include <sys/resource.h>
#include <sys/mman.h>
#include <pthread.h>

typedef unsigned long long rlim_t;

struct rlimit {
	rlim_t rlim_cur;
	rlim_t rlim_max;
};

#define RLIM_INFINITY (~0ULL)
#define RLIMIT_AS      9

int getrlimit (int resource_id, struct rlimit * ret_rlimit);

#ifndef PALEXPORT
#define PALEXPORT __attribute__ ((__visibility__ ("default")))
#endif // PALEXPORT

PALEXPORT int GlobalizationNative_IndexOf(void* pSortHandle,
                                              void* lpTarget,
                                              int cwTargetLength,
                                              void* lpSource,
                                              int cwSourceLength,
                                              int options,
                                              void* pMatchedLength);

void *mmap_wasi(void *addr, size_t length, int prot, int flags,
                  int fd, off_t offset);
int munmap_wasi(void *addr, size_t length);

int pthread_getattr_np(pthread_t t, pthread_attr_t *a);

#endif // _WASI_H
