// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <malloc.h>
#include <string.h>

#include "CommonTypes.h"
#include "CommonMacros.h"

// WASI SDK currently has an implementation of mmap that does not observe the alignment requirement
// so we have an incomplete implementation here which cannot handle partial unmaps.
void *mmap_wasi(void *addr, size_t length, int prot, int flags,
                  int fd, off_t offset)
{
    void * pRetVal;
    int result = posix_memalign(&pRetVal, OS_PAGE_SIZE, length);
    if(result != 0)
    {
        return NULL; // failed
    }
    memset(pRetVal, 0, length);
    return pRetVal;
}

int munmap_wasi(void *addr, size_t length) {

    // Release the memory. - if it is a partial munmap we are doomed
    free(addr);

    // Success!
    return 0;
}
