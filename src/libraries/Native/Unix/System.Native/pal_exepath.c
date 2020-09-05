// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_exepath.h"
#include "pal_utilities.h"

#include <stdlib.h>
#include <string.h>
#include <assert.h>

#if defined(__APPLE__)
#include <mach-o/dyld.h>
#endif

#if defined(__linux__)
#define symlinkEntrypointExecutable "/proc/self/exe"
#endif

// Get full path to the executable for the current process resolving symbolic links.
// On success, the function returns the size of the buffer required to hold the result.
// In case of an error, the function returns -1.
int32_t SystemNative_GetExecutableAbsolutePath(char* buffer, int32_t bufferSize)
{
    char* resolvedPath = NULL;
    int32_t requiredBufferSize;
    int32_t result;

    assert(buffer != NULL);
    assert(bufferSize >= 0);

#if defined(__APPLE__)

    requiredBufferSize = bufferSize;
    result = _NSGetExecutablePath(buffer, (uint32_t*)&requiredBufferSize);
    if (result == -1)
    {
        // The provided buffer is not big enough. Return required size
        return requiredBufferSize;
    }
    else if (result == 0)
    {
        // Resolve symbolic links. Note: realpath will allocate a buffer to hold the result.
        resolvedPath = realpath(buffer, NULL);
    }
    else
    {
        errno = EIO;
    }
#elif __linux__
    // Resolve symbolic links. Note: realpath will allocate a buffer to hold the result.
    resolvedPath = realpath(symlinkEntrypointExecutable, NULL);
#endif

    if (resolvedPath != NULL)
    {
        requiredBufferSize = SizeTToInt32(strlen(resolvedPath) + 1);
        if (requiredBufferSize <= bufferSize)
        {
            strncpy(buffer, resolvedPath, (size_t)requiredBufferSize);
        }

        result = requiredBufferSize;
        free(resolvedPath);
    }
    else
    {
        // Error
        result = -1;
    }

    return result;
}
