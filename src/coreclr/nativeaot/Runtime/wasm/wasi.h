// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef	_WASI_H
#define	_WASI_H

#include <sys/resource.h>
#include <sys/mman.h>

typedef unsigned long long rlim_t;

struct rlimit {
	rlim_t rlim_cur;
	rlim_t rlim_max;
};

#define RLIM_INFINITY (~0ULL)
#define RLIMIT_AS      9

int getrlimit (int resource_id, struct rlimit * ret_rlimit);

void *mmap_wasi(void *addr, size_t length, int prot, int flags,
                  int fd, off_t offset);
int munmap_wasi(void *addr, size_t length);

#endif // _WASI_H
