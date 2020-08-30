// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_config.h"
#include "pal_environment.h"

#include <stdlib.h>
#include <string.h>
#if HAVE_SCHED_GETCPU
#include <sched.h>
#endif
#if HAVE_NSGETENVIRON
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

__attribute__((noreturn))
void SystemNative_Exit(int32_t exitCode)
{
    exit(exitCode);
}

__attribute__((noreturn))
void SystemNative_Abort()
{
    abort();
}

char** SystemNative_GetEnviron()
{
    char** sysEnviron;

#if HAVE_NSGETENVIRON
    sysEnviron = *(_NSGetEnviron());
#else   // HAVE_NSGETENVIRON
    extern char **environ;
    sysEnviron = environ;
#endif  // HAVE_NSGETENVIRON

    return sysEnviron;
}
