// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef GREENTHREADS_H
#define GREENTHREADS_H
#include <stdint.h>

typedef void (*TakesOneParam)(uintptr_t param);
void TransitionToGreenThread(TakesOneParam functionToExecute, uintptr_t param);

#endif // GREENTHREADS_H