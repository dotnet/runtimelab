// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_memory.h"
#include <string.h>

void * SystemNative_MemAlloc(size_t size)
{
    return malloc(size);
}

void * SystemNative_MemAllocWithZeroInitialize(size_t size)
{
    return calloc(size, 1);
}

void * SystemNative_MemReAlloc(void *ptr, size_t size)
{
    return realloc(ptr, size);
}

void SystemNative_MemFree(void *ptr)
{
    free(ptr);
}

void* SystemNative_MemSet(void *s, int c, uintptr_t n)
{
    return memset(s, c, (size_t)n);
}
