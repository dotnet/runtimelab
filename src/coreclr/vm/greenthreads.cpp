// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Precompiled Header

#include "common.h"
#include "greenthreads.h"

#ifdef FEATURE_GREENTHREADS
struct GreenThreadData
{
    StackRange osStackRange;
    uint8_t* osStackCurrent;
    uint8_t* greenThreadStackCurrent;
    Frame* pFrameInGreenThread;
    Frame* pFrameInOSThread;
    GreenThreadStackList *pStackListCurrent;
    bool inGreenThread;
    bool transitionedToOSThreadOnGreenThread;
    SuspendedGreenThread* suspendedGreenThread;
};

extern SuspendedGreenThread green_head = {};
extern SuspendedGreenThread green_tail = {};

thread_local GreenThreadData t_greenThread;

void *TransitionToOSThreadAndCallMalloc(size_t memoryToAllocate);

uint8_t* AlignDown(uint8_t* address, size_t alignValue)
{
    uintptr_t addressAsUInt = (uintptr_t)address;
    addressAsUInt &= SIZE_MAX - alignValue + 1;
    return (uint8_t*)addressAsUInt;
}

static const int stackSizeOfMoreStackFunction = 0xe8;
extern "C" uintptr_t AllocateMoreStackHelper(int argumentStackSize, void* stackPointer)
{
    const int offsetToReturnAddress = 8;
    const int sizeOfShadowStore = 0x20; // Windows X64 calling convention has a 32 byte shadow store
    uint8_t *baseAddressOfStackArgs =  ((uint8_t*)stackPointer) + offsetToReturnAddress + 2 * sizeOfShadowStore + sizeof(void*) * 3; // for return address to be used for finding memory to call, and for return address to actual calling function;
    uint8_t *addressOfReturnAddress = ((uint8_t*)stackPointer) + offsetToReturnAddress;
    StackRange* pNewStackRange = (StackRange*)(((uint8_t*)stackPointer) - 0x10);
    StackRange* pOldStackRange = (StackRange*)(((uint8_t*)stackPointer) - 0x20);

    // For the < 0 case, implement swapping to an OS thread context
    uint8_t *newArgsLocation;
    if (argumentStackSize < 0)
    {
        argumentStackSize = -(argumentStackSize + 1);
        newArgsLocation = AlignDown(t_greenThread.osStackCurrent - (stackSizeOfMoreStackFunction + sizeOfShadowStore + sizeof(void*) + argumentStackSize), 16);
        *pNewStackRange = t_greenThread.osStackRange;
    }
    else
    {
        int stackSizeNeeded = 0x200000; // Hard code to 800KB for now. ... avoids dealing with actual segment overflows and GC stack walks and such.

        int sizeOfRedZone = 0x1000;

        GreenThreadStackList* pCurrentStackSegment = t_greenThread.pStackListCurrent;
        GreenThreadStackList* pNewStackSegment = NULL;

        if (pCurrentStackSegment != NULL)
        {
            pNewStackSegment = pCurrentStackSegment->next;
        }

        if (pNewStackSegment == NULL)
        {
            // Allocate a new segment
            uint8_t* stackSegment;
            
            if (pCurrentStackSegment == NULL)
            {
                stackSegment = (uint8_t*)malloc(stackSizeNeeded+sizeOfRedZone);
            }
            else
            {
                stackSegment = (uint8_t*)TransitionToOSThreadAndCallMalloc(stackSizeNeeded+sizeOfRedZone);
            }

            memset(stackSegment, 0, stackSizeNeeded+sizeOfRedZone);
            pNewStackSegment = (GreenThreadStackList*)stackSegment;
            pNewStackSegment->prev = t_greenThread.pStackListCurrent;
            pNewStackSegment->stackRange.stackLimit = stackSegment + sizeOfRedZone;
            pNewStackSegment->stackRange.stackBase = stackSegment + sizeOfRedZone + stackSizeNeeded;
            pNewStackSegment->size = stackSizeNeeded;

            if (pCurrentStackSegment == NULL)
            {
                // This is a new green thread
                t_greenThread.pStackListCurrent = pNewStackSegment;
                t_greenThread.osStackCurrent = ((uint8_t*)stackPointer) + stackSizeOfMoreStackFunction;
                t_greenThread.osStackRange = *pOldStackRange;
            }
            else
            {
                t_greenThread.pStackListCurrent->next = pNewStackSegment;
            }
        }
        t_greenThread.pStackListCurrent = pNewStackSegment;

        newArgsLocation = AlignDown(pNewStackSegment->stackRange.stackBase - (argumentStackSize + sizeOfShadowStore), 16);
        *pNewStackRange = pNewStackSegment->stackRange;
    }
    memcpy(newArgsLocation + sizeOfShadowStore, baseAddressOfStackArgs, argumentStackSize);
    return (uintptr_t)newArgsLocation;
}

struct TransitionHelperStruct
{
    TakesOneParam function;
    uintptr_t param;
    uintptr_t result;
};
typedef uintptr_t (*TransitionHelperFunction)(uintptr_t dummy, TransitionHelperStruct* param);

extern "C" uintptr_t GreenThread_StartThreadHelper(uintptr_t functionToExecute, TransitionHelperStruct* param);

extern "C" uintptr_t FirstFrameInGreenThread(TransitionHelperFunction functionToExecute, TransitionHelperStruct* param);

extern "C" uintptr_t FirstFrameInGreenThreadCpp(TransitionHelperFunction functionToExecute, TransitionHelperStruct* param)
{
    if (t_greenThread.inGreenThread)
        __debugbreak();

    t_greenThread.inGreenThread = true;
    GetThread()->SetExecutingOnAltStack();

    uintptr_t result = param->function(param->param);
    t_greenThread.inGreenThread = false;
    t_greenThread.greenThreadStackCurrent = NULL;

    return result;
}

void CleanGreenThreadState()
{
    t_greenThread.inGreenThread = false;
    t_greenThread.osStackCurrent = NULL;
    t_greenThread.greenThreadStackCurrent = NULL;
    memset(&t_greenThread.osStackRange, 0, sizeof(StackRange));
    t_greenThread.pStackListCurrent = NULL;
    t_greenThread.transitionedToOSThreadOnGreenThread = false;
}

void FreeGreenThreadStackList(GreenThreadStackList* pStackList)
{
    // Move to front of stack list
    while (pStackList->prev != NULL)
        pStackList = pStackList->prev;
    
    while (pStackList != NULL)
    {
        GreenThreadStackList* pStackListNext = pStackList->next;
        free(pStackList);
        pStackList = pStackListNext;
    }
}

SuspendedGreenThread* ProduceSuspendedGreenThreadStruct()
{
    if (t_greenThread.inGreenThread)
    {
        // This is a suspension scenario
        SuspendedGreenThread* pNewSuspendedThread = t_greenThread.suspendedGreenThread;
        pNewSuspendedThread->currentStackPointer = t_greenThread.greenThreadStackCurrent;
        pNewSuspendedThread->greenThreadFrame = t_greenThread.pFrameInGreenThread;

        CleanGreenThreadState();
        return pNewSuspendedThread;
    }
    else
    {
        FreeGreenThreadStackList(t_greenThread.pStackListCurrent);
        CleanGreenThreadState();
        return NULL;
    }
}

SuspendedGreenThread* GreenThread_StartThread(TakesOneParam functionToExecute, uintptr_t param)
{
    {
        GCX_COOP();
        t_greenThread.pFrameInOSThread = GetThread()->m_pFrame;
    }
    TransitionHelperStruct detailsAboutWhatToCall;
    detailsAboutWhatToCall.function = functionToExecute;
    detailsAboutWhatToCall.param = param;

    GreenThread_StartThreadHelper((uintptr_t)FirstFrameInGreenThread, &detailsAboutWhatToCall);

    return ProduceSuspendedGreenThreadStruct();
}

uintptr_t FirstFrameInOSThread(TransitionHelperFunction functionToExecute, TransitionHelperStruct* param)
{
    uintptr_t result = param->function(param->param);

    return result;
}

extern "C" uintptr_t TransitionToOSThreadHelper(uintptr_t functionToExecute, TransitionHelperStruct* param);

uintptr_t TransitionToOSThread(TakesOneParam functionToExecute, uintptr_t param)
{
    TransitionHelperStruct detailsAboutWhatToCall;
    detailsAboutWhatToCall.function = functionToExecute;
    detailsAboutWhatToCall.param = param;
    if (!t_greenThread.inGreenThread)
        __debugbreak();

    t_greenThread.inGreenThread = false;
    bool oldtransitionedToOSThreadOnGreenThread = t_greenThread.transitionedToOSThreadOnGreenThread;
    t_greenThread.transitionedToOSThreadOnGreenThread = true;

    uintptr_t result = TransitionToOSThreadHelper((uintptr_t)FirstFrameInOSThread, &detailsAboutWhatToCall);
    t_greenThread.transitionedToOSThreadOnGreenThread = oldtransitionedToOSThreadOnGreenThread;
    t_greenThread.inGreenThread = true;

    return result;
}

void *TransitionToOSThreadAndCallMalloc(size_t memoryToAllocate)
{
    return (void*)TransitionToOSThread((TakesOneParam)malloc, memoryToAllocate);
}

extern "C" void YieldOutOfGreenThreadHelper(StackRange *pOSStackRange, uint8_t* osStackCurrent, uint8_t** greenThreadStackCurrent);

bool GreenThread_Yield() // Attempt to yield out of green thread. If the yield fails, return false, else return true once the thread is resumed.
{
    if (!t_greenThread.inGreenThread || t_greenThread.transitionedToOSThreadOnGreenThread)
        return false;
    
    {
        GCX_COOP();
        t_greenThread.pFrameInGreenThread = GetThread()->m_pFrame;
        if (t_greenThread.pFrameInGreenThread->PtrNextFrame() != t_greenThread.pFrameInOSThread)
        {
            return false;
        }
        GetThread()->m_pFrame = t_greenThread.pFrameInGreenThread->PtrNextFrame();

        // TODO: AndrewAu: What if we don't have sufficient memory for this?
        SuspendedGreenThread *pNewSuspendedThread = (SuspendedGreenThread*)malloc(sizeof(SuspendedGreenThread));
        pNewSuspendedThread->currentThreadStackSegment = t_greenThread.pStackListCurrent;        
        t_greenThread.suspendedGreenThread = pNewSuspendedThread;

        // TODO: AndrewAu: Synchronization in case multiple threads yield/resume at the same time.
        if (green_head.next == nullptr) { green_head.next = &green_tail; }
        if (green_tail.next == nullptr) { green_tail.prev = &green_head; }

        pNewSuspendedThread->prev = green_tail.prev;
        green_tail.prev->next = pNewSuspendedThread;
        green_tail.prev = pNewSuspendedThread;
        pNewSuspendedThread->next = &green_tail;
    }

    YieldOutOfGreenThreadHelper(&t_greenThread.osStackRange, t_greenThread.osStackCurrent, &t_greenThread.greenThreadStackCurrent);

    {
        GCX_COOP();
        // At this point we've resumed, and the stack is now in the new state way, but the Frame chain is not hooked up.

        SuspendedGreenThread* pNewSuspendedThread = t_greenThread.suspendedGreenThread;
        t_greenThread.suspendedGreenThread = nullptr;

        pNewSuspendedThread->next->prev = pNewSuspendedThread->prev;
        pNewSuspendedThread->prev->next = pNewSuspendedThread->next;
        free(pNewSuspendedThread);

        t_greenThread.pFrameInGreenThread->UNSAFE_SetNextFrame(t_greenThread.pFrameInOSThread);
        GetThread()->m_pFrame = t_greenThread.pFrameInGreenThread;
    }
    return true;
}

bool GreenThread_IsGreenThread()
{
    return t_greenThread.inGreenThread;
}

extern "C" uint8_t* GetResumptionStackPointerAndSaveOSStackPointer(StackRange* pOSStackRange, uint8_t* rbpFromOSThreadBeforeResume)
{
    uint8_t* savedRBPValue = rbpFromOSThreadBeforeResume;
    uint8_t* savedRBXValue = rbpFromOSThreadBeforeResume - (stackSizeOfMoreStackFunction - sizeof(void*)) /*return address */;

    GreenThreadStackList* pStackRange = t_greenThread.pStackListCurrent;
    while (pStackRange->prev != NULL)
        pStackRange = pStackRange->prev;

    // pStackRange is now set to the first stack range used in the green thread

    // Compute the address of the saved RBP/RBX values in the initial FirstFrameInGreenThread frame
    uint8_t**savedRBPValueAddress = (uint8_t**)(pStackRange->stackRange.stackBase - 0x40);
    uint8_t**savedRBXValueAddress = savedRBPValueAddress + 1;
    *savedRBPValueAddress = savedRBPValue;
    *savedRBXValueAddress = savedRBXValue;

    t_greenThread.inGreenThread = true;

    GetThread()->SetExecutingOnAltStack();
    t_greenThread.osStackRange = *pOSStackRange;
    t_greenThread.osStackCurrent = savedRBXValue;

    return t_greenThread.greenThreadStackCurrent;
}

extern "C" void ResumeSuspendedThreadHelper();

SuspendedGreenThread* GreenThread_ResumeThread(SuspendedGreenThread* pSuspendedThread)
{
    if (t_greenThread.inGreenThread)
        __debugbreak();
    
    if (t_greenThread.transitionedToOSThreadOnGreenThread)
        __debugbreak();

    {
        t_greenThread.pFrameInOSThread = GetThread()->m_pFrame;
        t_greenThread.pFrameInGreenThread = pSuspendedThread->greenThreadFrame;
    }

    t_greenThread.pStackListCurrent = pSuspendedThread->currentThreadStackSegment;
    t_greenThread.greenThreadStackCurrent = pSuspendedThread->currentStackPointer;
    t_greenThread.suspendedGreenThread = pSuspendedThread;

    ResumeSuspendedThreadHelper();
    return ProduceSuspendedGreenThreadStruct();
}

extern "C" void End_More_Thread_Bookeeping()
{
    if (t_greenThread.inGreenThread) // This should avoid doing this for the initial jump into the green thread and jumps from green to OS
    {
        if (t_greenThread.pStackListCurrent->prev == NULL)
            __debugbreak();
        t_greenThread.pStackListCurrent = t_greenThread.pStackListCurrent->prev;
    }
}

#endif // FEATURE_GREENTHREADS