// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "wasi.h"
#include <signal.h>
#include <sys/mman.h>
#include <malloc.h>
#include "CommonTypes.h"
#include "../CommonMacros.h"
#include <string.h>

int getrlimit (int resource_id, struct rlimit * ret_rlimit)
{
  switch(resource_id)
  {
    case RLIMIT_AS :
      // TODO-LLVM: how to implement rlimits for Wasi
      // The gc wants to know this in GetRestrictedPhysicalMemoryLimit.  Will try with an arbritrary value of 1GB
      ret_rlimit->rlim_cur = 1024 * 1024 * 1024;
      ret_rlimit->rlim_max = 1024 * 1024 * 1024;
      break;
    default :
      return -1;
  }

  return 0;
}

// TODO-LLVM: replace with ICU

static int GlobalizationNative_IndexOf(void* pSortHandle,
                                              void* lpTarget,
                                              int cwTargetLength,
                                              void* lpSource,
                                              int cwSourceLength,
                                              int options,
                                              void* pMatchedLength)
{
  return 0;
}

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

