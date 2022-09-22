// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Precompiled Header

#include "common.h"
#include "greenthreads.h"

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

struct GreenThreadData
{
    StackRange osStackRange;
    uint8_t* osStackCurrent;
    GreenThreadStackList *pStackListCurrent;
};


thread_local GreenThreadData t_greenThread;

uint8_t* AlignDown(uint8_t* address, size_t alignValue)
{
    uintptr_t addressAsUInt = (uintptr_t)address;
    addressAsUInt &= SIZE_MAX - alignValue + 1;
    return (uint8_t*)addressAsUInt;
}

extern "C" uintptr_t AllocateMoreStackHelper(int argumentStackSize, void* stackPointer)
{
    const int offsetToReturnAddress = 0;
    const int stackSizeOfMoreStackFunction = 0xf0;
    uint8_t *baseAddressOfStackArgs =  ((uint8_t*)stackPointer) + offsetToReturnAddress + sizeof(void*);
    uint8_t *addressOfReturnAddress = ((uint8_t*)stackPointer) + offsetToReturnAddress;
    StackRange* pNewStackRange = (StackRange*)(((uint8_t*)stackPointer) - 0x10);
    StackRange* pOldStackRange = (StackRange*)(((uint8_t*)stackPointer) - 0x20);

    // For the < 0 case, implement swapping to an OS thread context
    void *newArgsLocation;
    if (argumentStackSize < 0)
    {
        argumentStackSize = -(argumentStackSize + 1);
        newArgsLocation = AlignDown(t_greenThread.osStackCurrent - argumentStackSize, 16);
        *pNewStackRange = t_greenThread.osStackRange;
    }
    else
    {
        int stackSizeNeeded = 0x2000; // Hard code to 8KB for now.

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
            uint8_t* stackSegment = (uint8_t*)malloc(stackSizeNeeded+sizeOfRedZone);
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

        newArgsLocation = AlignDown(pNewStackSegment->stackRange.stackBase - argumentStackSize, 16);
        *pNewStackRange = pNewStackSegment->stackRange;
    }
    memcpy(newArgsLocation, baseAddressOfStackArgs, argumentStackSize);
    return (uintptr_t)newArgsLocation;
}

struct TransitionHelperStruct
{
    TakesOneParam function;
    uintptr_t param;
};
typedef void (*TransitionHelperFunction)(uintptr_t dummy, TransitionHelperStruct* param);

extern "C" void TransitionToGreenThreadHelper(uintptr_t functionToExecute, TransitionHelperStruct* param);

void FirstFrameInGreenThread(TransitionHelperFunction functionToExecute, TransitionHelperStruct* param)
{
    param->function(param->param);
}

void TransitionToGreenThread(TakesOneParam functionToExecute, uintptr_t param)
{
    TransitionHelperStruct detailsAboutWhatToCall;
    detailsAboutWhatToCall.function = functionToExecute;
    detailsAboutWhatToCall.param = param;
    TransitionToGreenThreadHelper((uintptr_t)FirstFrameInGreenThread, &detailsAboutWhatToCall);
}
