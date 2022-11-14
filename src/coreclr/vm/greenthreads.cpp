// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Precompiled Header

#include "common.h"
#include "greenthreads.h"

#ifndef FEATURE_GREENTHREADS
void CallOnOSThread(TakesOneParamNoReturn functionToExecute, uintptr_t param)
{
    functionToExecute(param);
}

FCIMPL0(void, JIT_GreenThreadMoreStack)
{
}
FCIMPLEND
#else // FEATURE_GREENTHREADS
struct GreenThreadData
{
    StackRange osStackRange;
    uint8_t* osStackCurrent;
    uint8_t* greenThreadStackCurrent;
    Frame* pFrameInGreenThread;
    Frame* pFrameInOSThread;
    GreenThreadStackList *pStackListCurrent;
    bool inGreenThread;
    bool greenThreadOnStack;
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
static const int frameOffsetMoreStackFunction = 0xe0;
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
        assert(t_greenThread.inGreenThread);
        t_greenThread.inGreenThread = false;

        argumentStackSize = -(argumentStackSize + 1);
        newArgsLocation = AlignDown(t_greenThread.osStackCurrent - (stackSizeOfMoreStackFunction + frameOffsetMoreStackFunction + sizeOfShadowStore + sizeof(void*) + argumentStackSize), 16);
        *pNewStackRange = t_greenThread.osStackRange;
    }
    else
    {
        // Top 6 bits of argumentStackSize is used to compute the stackSizeNeeded
        int stackSizeNeededSelector = argumentStackSize >> 26;

        argumentStackSize = argumentStackSize & 0x3FFFFFF; // Lower 26 bits are the argument stack size
                                                           // NOTE: This is putting a 64MB limit on argument size. I think this is OK.
        int stackSizeNeeded = 1 << stackSizeNeededSelector;

        stackSizeNeeded = max(stackSizeNeeded, 0x200000);  // Hard code to 800KB for now. ... avoids dealing with actual segment overflows and GC stack walks and such.

        int sizeOfRedZone = 0x1000;

        GreenThreadStackList* pCurrentStackSegment = t_greenThread.pStackListCurrent;
        GreenThreadStackList* pNewStackSegment = NULL;

        if (pCurrentStackSegment != NULL)
        {
            pNewStackSegment = pCurrentStackSegment->next;
            assert(t_greenThread.inGreenThread);
        }
        else
        {
            t_greenThread.inGreenThread = true;
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

#pragma optimize( "", off ) // Disable optimizations on this function as it is involved in thread transitions, and the use of thread statics across
                            // across a potential OS transition does not work correctly. (The TLS variable access implicit in GetThread() stored 
                            // across the OS thread transition. Naturally this causes interesting failures.)
extern "C" uintptr_t FirstFrameInGreenThreadCpp(TransitionHelperFunction functionToExecute, TransitionHelperStruct* param)
{
    GetThread()->SetExecutingOnAltStack();
    assert(t_greenThread.inGreenThread);
    FrameWithCookie<GreenThreadFrame> f;

    {
        GCX_COOP();
        f.Push(GetThread());
    }

    uintptr_t result = param->function(param->param);

    {
        GCX_COOP();
        f.Pop(GetThread());
    }

    t_greenThread.greenThreadStackCurrent = NULL;

    return result;
}
#pragma optimize( "", on )

void CleanGreenThreadState()
{
    t_greenThread.osStackCurrent = NULL;
    t_greenThread.greenThreadStackCurrent = NULL;
    memset(&t_greenThread.osStackRange, 0, sizeof(StackRange));
    t_greenThread.pStackListCurrent = NULL;
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

SuspendedGreenThread* ProduceSuspendedGreenThreadStruct(GreenThread* pGreenThread)
{
    if (t_greenThread.inGreenThread)
    {
        // This is a suspension scenario
        SuspendedGreenThread* pNewSuspendedThread = t_greenThread.suspendedGreenThread;
        pNewSuspendedThread->currentStackPointer = t_greenThread.greenThreadStackCurrent;
        pNewSuspendedThread->greenThreadFrame = t_greenThread.pFrameInGreenThread;
        pNewSuspendedThread->pGreenThread = pGreenThread;
        pGreenThread->m_currentThreadObj = NULL;
        t_greenThread.inGreenThread = false;

        CleanGreenThreadState();
        return pNewSuspendedThread;
    }
    else
    {
        FreeGreenThreadStackList(t_greenThread.pStackListCurrent);
        delete pGreenThread;
        CleanGreenThreadState();
        return NULL;
    }
}

SuspendedGreenThread* GreenThread_StartThread(TakesOneParam functionToExecute, uintptr_t param)
{
    OBJECTHANDLE threadObjectHandle;

    if (t_greenThread.greenThreadOnStack)
        __debugbreak();

    if (t_greenThread.inGreenThread)
        __debugbreak();

    GreenThread* pGreenThread = new GreenThread();
    {
        GCX_COOP();
        t_greenThread.pFrameInOSThread = GetThread()->m_pFrame;

        THREADBASEREF attempt = (THREADBASEREF) AllocateObject(g_pThreadClass);
        GCPROTECT_BEGIN(attempt);
        attempt->SetIsGreenThread();
        threadObjectHandle = GetAppDomain()->CreateStrongHandle(attempt);
        
        pGreenThread->m_ExposedObject = threadObjectHandle;
        attempt->SetManagedThreadId(pGreenThread->m_ThreadId);
        GCPROTECT_END();
    }

    TransitionHelperStruct detailsAboutWhatToCall;
    detailsAboutWhatToCall.function = functionToExecute;
    detailsAboutWhatToCall.param = param;

    ThreadBase* pOldThreadBase = GetThread()->GetActiveThreadBase();
    GetThread()->SetActiveThreadBase(pGreenThread);
    pGreenThread->m_currentThreadObj = GetThread();

    assert(t_greenThread.inGreenThread == false);
    t_greenThread.greenThreadOnStack = true;
    GreenThread_StartThreadHelper((uintptr_t)FirstFrameInGreenThread, &detailsAboutWhatToCall);
    t_greenThread.greenThreadOnStack = false;

    GetThread()->SetActiveThreadBase(pOldThreadBase);
    return ProduceSuspendedGreenThreadStruct(pGreenThread);
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

    uintptr_t result = TransitionToOSThreadHelper((uintptr_t)FirstFrameInOSThread, &detailsAboutWhatToCall);

    return result;
}

void TransitionToOSThread(TakesOneParamNoReturn functionToExecute, uintptr_t param)
{
    TransitionHelperStruct detailsAboutWhatToCall;
    detailsAboutWhatToCall.function = (TakesOneParam)functionToExecute;
    detailsAboutWhatToCall.param = param;
    if (!t_greenThread.inGreenThread)
        __debugbreak();


    TransitionToOSThreadHelper((uintptr_t)FirstFrameInOSThread, &detailsAboutWhatToCall);
}

void CallOnOSThread(TakesOneParamNoReturn functionToExecute, uintptr_t param)
{
    if (!t_greenThread.inGreenThread)
        functionToExecute(param);
    else
        TransitionToOSThread(functionToExecute, param);
}

void *TransitionToOSThreadAndCallMalloc(size_t memoryToAllocate)
{
    return (void*)TransitionToOSThread((TakesOneParam)malloc, memoryToAllocate);
}

extern "C" void YieldOutOfGreenThreadHelper(StackRange *pOSStackRange, uint8_t* osStackCurrent, uint8_t** greenThreadStackCurrent);

thread_local uintptr_t green_thread_yield_return_value;

#pragma optimize( "", off ) // Disable optimizations on this function as it is involved in thread transitions, and the use of thread statics across
                            // across a potential OS transition does not work correctly. (The TLS variable access implicit in GetThread() stored 
                            // across the OS thread transition. Naturally this causes interesting failures.)
uintptr_t GreenThread_Yield() // Attempt to yield out of green thread. If the yield fails, return 0, else return true once the thread is resumed.
{
    if (!t_greenThread.greenThreadOnStack)
        __debugbreak();

    if (!t_greenThread.inGreenThread)
        return 0;
    
    {
        GCX_COOP();
        t_greenThread.pFrameInGreenThread = GetThread()->m_pFrame;
        if (t_greenThread.pFrameInGreenThread->PtrNextFrame()->PtrNextFrame() != t_greenThread.pFrameInOSThread)
        {
            return false;
        }
        GetThread()->m_pFrame = t_greenThread.pFrameInGreenThread->PtrNextFrame()->PtrNextFrame();

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

        t_greenThread.pFrameInGreenThread->PtrNextFrame()->UNSAFE_SetNextFrame(t_greenThread.pFrameInOSThread);
        ((InlinedCallFrame*)t_greenThread.pFrameInGreenThread)->UNSAFE_UpdateThreadPointer(GetThread());
        GetThread()->m_pFrame = t_greenThread.pFrameInGreenThread;
    }
    return green_thread_yield_return_value;
}

#pragma optimize( "", on ) // Re-enable optimizations

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

    assert(t_greenThread.inGreenThread == false);
    t_greenThread.inGreenThread = true;

    GetThread()->SetExecutingOnAltStack();
    t_greenThread.osStackRange = *pOSStackRange;
    t_greenThread.osStackCurrent = savedRBXValue;

    *(StackRange*)(rbpFromOSThreadBeforeResume - 0x30) = t_greenThread.pStackListCurrent->stackRange;

    return t_greenThread.greenThreadStackCurrent;
}

extern "C" void ResumeSuspendedThreadHelper();

SuspendedGreenThread* GreenThread_ResumeThread(SuspendedGreenThread* pSuspendedThread, uintptr_t yieldReturnValue)
{
    green_thread_yield_return_value = yieldReturnValue;

    if (t_greenThread.inGreenThread)
        __debugbreak();
    
    if (t_greenThread.greenThreadOnStack)
        __debugbreak();

    {
        t_greenThread.pFrameInOSThread = GetThread()->m_pFrame;
        t_greenThread.pFrameInGreenThread = pSuspendedThread->greenThreadFrame;
    }

    t_greenThread.pStackListCurrent = pSuspendedThread->currentThreadStackSegment;
    t_greenThread.greenThreadStackCurrent = pSuspendedThread->currentStackPointer;
    GreenThread* pGreenThread = pSuspendedThread->pGreenThread;
    pGreenThread->m_currentThreadObj = GetThread();

    t_greenThread.suspendedGreenThread = pSuspendedThread;

    ThreadBase* pOldThreadBase = GetThread()->GetActiveThreadBase();
    GetThread()->SetActiveThreadBase(pGreenThread);

    t_greenThread.greenThreadOnStack = true;

    ResumeSuspendedThreadHelper();
    t_greenThread.greenThreadOnStack = false;

    GetThread()->SetActiveThreadBase(pOldThreadBase);
    return ProduceSuspendedGreenThreadStruct(pGreenThread);
}

extern "C" void End_More_Thread_Bookeeping(uint8_t* pStackLimitTransitioningFrom)
{
    if (t_greenThread.inGreenThread)
    {
        if (t_greenThread.pStackListCurrent->prev == NULL)
        {
            // We should only hit this path when a green thread is finishing
            t_greenThread.inGreenThread = false;

            // The OS stack range may be inaccurate when handled as a normally restored value, as the OS can change it during chkstk or normal execution. Restore it to the last value found on return from an OS thread.
            // This is a bit of a hack, as a few assembly instructions before this code is executed, we restore it to something that was set up on entrance to the morestack function.
            _ASSERTE(pStackLimitTransitioningFrom >= t_greenThread.osStackRange.stackLimit);
            ((uint8_t**)NtCurrentTeb())[2] = t_greenThread.osStackRange.stackLimit;
        }
        else
        {
            // Cases where we are returning across a stack boundary
            t_greenThread.pStackListCurrent = t_greenThread.pStackListCurrent->prev;
        }
    }
    else
    {
        // This is the return from a transition to an OS thread.
        t_greenThread.inGreenThread = true;

        // The saved stack limit may be inaccurate due to OS stack changes(during chkstck and such). Replace the saved value
        t_greenThread.osStackRange.stackLimit = pStackLimitTransitioningFrom;
    }
}

struct ThreadTransitionData
{
    void* fptr;
    uintptr_t stacksize;
};

extern "C"
{
    thread_local ThreadTransitionData t_greenThreadTransitionData;
}

extern "C" void TransitionToOSThreadHelper2();

#endif // FEATURE_GREENTHREADS

// This helper uses a _RAW helper as it is called after the GC transition part of a P/Invoke
HCIMPL2_RAW(void*, JIT_GreenThreadTransition, void* fptr, uintptr_t stackSize)
{
#ifdef FEATURE_GREENTHREADS
    if (t_greenThread.inGreenThread)
    {
        t_greenThreadTransitionData.fptr = fptr;
        t_greenThreadTransitionData.stacksize = (uintptr_t)((-(intptr_t)stackSize) - 1);
        return TransitionToOSThreadHelper2;
    }
    else
    {
        return fptr;
    }
#else
    // TODO: Actually implement this thing
    return fptr;
#endif
}
HCIMPLEND_RAW
