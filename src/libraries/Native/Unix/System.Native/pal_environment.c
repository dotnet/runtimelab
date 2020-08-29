// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_common.h"
#include "pal_time.h"

#include <stdlib.h>
#include <string.h>
#if HAVE_SCHED_GETCPU
#include <sched.h>
#endif
#if HAVE__NSGETENVIRON
#include <crt_externs.h>
#endif


char* SystemNative_GetEnv(const char* variable)
{
    return getenv(variable);
}

int32_t SystemNative_SchedGetCpu()
{
#if HAVE_SCHED_GETCPU
    return sched_getcpu();
#else
    return -1;
#endif
}

void SystemNative_Exit(int32_t exitCode)
{
    exit(exitCode);
}

void SystemNative_Abort()
{
    abort();
}

char** SystemNative_GetEnviron()
{
    char** sysEnviron;

#if HAVE__NSGETENVIRON
    sysEnviron = *(_NSGetEnviron());
#else   // HAVE__NSGETENVIRON
    extern char **environ;
    sysEnviron = environ;
#endif  // HAVE__NSGETENVIRON

    return sysEnviron;
}
