// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef GREENTHREADS_H
#define GREENTHREADS_H
#include <stdint.h>

struct StackRange
{
    TADDR stackLimit;
    TADDR stackBase;
};

struct GreenThreadStackList;
typedef DPTR(GreenThreadStackList) PTR_GreenThreadStackList;

struct GreenThreadStackList
{
    PTR_GreenThreadStackList prev;
    PTR_GreenThreadStackList next;
    StackRange stackRange;
    int32_t size;
};

class GreenThread;
typedef DPTR(GreenThread) PTR_GreenThread;

struct SuspendedGreenThread;
typedef DPTR(SuspendedGreenThread) PTR_SuspendedGreenThread;

struct SuspendedGreenThread
{
    TADDR currentStackPointer;
    PTR_GreenThreadStackList currentThreadStackSegment;
    PTR_Frame greenThreadFrame;
    PTR_GreenThread pGreenThread;
    PTR_SuspendedGreenThread prev;
    PTR_SuspendedGreenThread next;
};

struct GreenThreadData
{
    StackRange osStackRange;
    TADDR osStackCurrent;
    TADDR greenThreadStackCurrent;
    PTR_Frame pFrameInGreenThread;
    PTR_Frame pFrameInOSThread;
    PTR_GreenThreadStackList pStackListCurrent;
    bool greenThreadOnStack;
    PTR_SuspendedGreenThread suspendedGreenThread;
};

typedef DPTR(GreenThreadData) PTR_GreenThreadData;

typedef uintptr_t (*TakesOneParam)(uintptr_t param);
typedef void (*TakesOneParamNoReturn)(uintptr_t param);
SuspendedGreenThread* GreenThread_StartThread(TakesOneParam functionToExecute, uintptr_t param);
uintptr_t TransitionToOSThread(TakesOneParam functionToExecute, uintptr_t param);
void TransitionToOSThread(TakesOneParamNoReturn functionToExecute, uintptr_t param);

// Must be called from within a green thread.
uintptr_t GreenThread_Yield(); // Attempt to yield out of green thread. If the yield fails, return false, else return true once the thread is resumed.

// Resume execution 
SuspendedGreenThread* GreenThread_ResumeThread(SuspendedGreenThread* pSuspendedThread, uintptr_t yieldReturnValue); // Resume suspended green thread, and destroy SuspendedGreenThread structure, or return a new one if the thread suspends again ... Note this is permitted to return the old one.

// Destroy suspended green thread
void DestroyGreenThread(SuspendedGreenThread* pSuspendedThread); // 

bool GreenThread_IsGreenThread();

void CallOnOSThread(TakesOneParamNoReturn functionToExecute, uintptr_t param);

// TODO: AndrewAu: Better naming
extern SuspendedGreenThread green_head;
extern SuspendedGreenThread green_tail;

void InitGreenThreads();
bool GreenThreadHelpersToSkip(TADDR code);
bool stackPointerLessThan(Thread* thread, TADDR sp1, TADDR sp2);

#endif // GREENTHREADS_H