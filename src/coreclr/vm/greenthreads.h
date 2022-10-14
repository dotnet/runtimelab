// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef GREENTHREADS_H
#define GREENTHREADS_H
#include <stdint.h>

struct StackRange
{
    uint8_t* stackLimit;
    uint8_t* stackBase;
};

struct GreenThreadStackList
{
    GreenThreadStackList *prev;
    GreenThreadStackList *next;
    StackRange stackRange;
    int size;
};

struct SuspendedGreenThread
{
    uint8_t* currentStackPointer;
    GreenThreadStackList* currentThreadStackSegment;
    Frame* greenThreadFrame;
    SuspendedGreenThread* prev;
    SuspendedGreenThread* next;
};

typedef uintptr_t (*TakesOneParam)(uintptr_t param);
SuspendedGreenThread* GreenThread_StartThread(TakesOneParam functionToExecute, uintptr_t param);
uintptr_t TransitionToOSThread(TakesOneParam functionToExecute, uintptr_t param);

// Must be called from within a green thread.
bool GreenThread_Yield(); // Attempt to yield out of green thread. If the yield fails, return false, else return true once the thread is resumed.

// Resume execution 
SuspendedGreenThread* GreenThread_ResumeThread(SuspendedGreenThread* pSuspendedThread); // Resume suspended green thread, and destroy SuspendedGreenThread structure, or return a new one if the thread suspends again ... Note this is permitted to return the old one.

// Destroy suspended green thread
void DestroyGreenThread(SuspendedGreenThread* pSuspendedThread); // 

bool GreenThread_IsGreenThread();

// TODO: AndrewAu: Better naming
extern SuspendedGreenThread green_head;
extern SuspendedGreenThread green_tail;

#endif // GREENTHREADS_H