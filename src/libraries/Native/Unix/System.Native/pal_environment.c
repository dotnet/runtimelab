// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_config.h"
#include "pal_environment.h"

#include <stdlib.h>
#include <string.h>
#if HAVE_NSGETENVIRON
#include <crt_externs.h>
#endif
#if HAVE_SCHED_GETCPU
#include <sched.h>
#endif

char* SystemNative_GetEnv(const char* variable)
{
    return getenv(variable);
}

char** SystemNative_GetEnviron()
{
#if HAVE_NSGETENVIRON
    return *(_NSGetEnviron());
#else
    extern char **environ;
    return environ;
#endif
}

void SystemNative_FreeEnviron(char** environ)
{
    // no op
    (void)environ;
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
