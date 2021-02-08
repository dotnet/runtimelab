// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XX                                                                           XX
XX                              UnwindInfo                                   XX
XX                                                                           XX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
*/

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#if defined(TARGET_WASM)
typedef union _UNWIND_CODE {
    struct {
        UCHAR CodeOffset;
        UCHAR UnwindOp : 4;
        UCHAR OpInfo : 4;
    };

    struct {
        UCHAR OffsetLow;
        UCHAR UnwindOp : 4;
        UCHAR OffsetHigh : 4;
    } EpilogueCode;

    USHORT FrameOffset;
} UNWIND_CODE, * PUNWIND_CODE;
typedef struct _UNWIND_INFO {
    UCHAR Version : 3;
    UCHAR Flags : 5;
    UCHAR SizeOfProlog;
    UCHAR CountOfUnwindCodes;
    UCHAR FrameRegister : 4;
    UCHAR FrameOffset : 4;
    UNWIND_CODE UnwindCode[1];

    //
    // The unwind codes are followed by an optional DWORD aligned field that
    // contains the exception handler address or the address of chained unwind
    // information. If an exception handler address is specified, then it is
    // followed by the language specified exception handler data.
    //
    //  union {
    //      ULONG ExceptionHandler;
    //      ULONG FunctionEntry;
    //  };
    //
    //  ULONG ExceptionData[];
    //

} UNWIND_INFO, * PUNWIND_INFO;

#ifdef UNIX_AMD64_ABI
short Compiler::mapRegNumToDwarfReg(regNumber reg)
{
    short dwarfReg = DWARF_REG_ILLEGAL;

    switch (reg)
    {
        case REG_RAX:
            dwarfReg = 0;
            break;
        case REG_RCX:
            dwarfReg = 2;
            break;
        case REG_RDX:
            dwarfReg = 1;
            break;
        case REG_RBX:
            dwarfReg = 3;
            break;
        case REG_RSP:
            dwarfReg = 7;
            break;
        case REG_RBP:
            dwarfReg = 6;
            break;
        case REG_RSI:
            dwarfReg = 4;
            break;
        case REG_RDI:
            dwarfReg = 5;
            break;
        case REG_R8:
            dwarfReg = 8;
            break;
        case REG_R9:
            dwarfReg = 9;
            break;
        case REG_R10:
            dwarfReg = 10;
            break;
        case REG_R11:
            dwarfReg = 11;
            break;
        case REG_R12:
            dwarfReg = 12;
            break;
        case REG_R13:
            dwarfReg = 13;
            break;
        case REG_R14:
            dwarfReg = 14;
            break;
        case REG_R15:
            dwarfReg = 15;
            break;
        case REG_XMM0:
            dwarfReg = 17;
            break;
        case REG_XMM1:
            dwarfReg = 18;
            break;
        case REG_XMM2:
            dwarfReg = 19;
            break;
        case REG_XMM3:
            dwarfReg = 20;
            break;
        case REG_XMM4:
            dwarfReg = 21;
            break;
        case REG_XMM5:
            dwarfReg = 22;
            break;
        case REG_XMM6:
            dwarfReg = 23;
            break;
        case REG_XMM7:
            dwarfReg = 24;
            break;
        case REG_XMM8:
            dwarfReg = 25;
            break;
        case REG_XMM9:
            dwarfReg = 26;
            break;
        case REG_XMM10:
            dwarfReg = 27;
            break;
        case REG_XMM11:
            dwarfReg = 28;
            break;
        case REG_XMM12:
            dwarfReg = 29;
            break;
        case REG_XMM13:
            dwarfReg = 30;
            break;
        case REG_XMM14:
            dwarfReg = 31;
            break;
        case REG_XMM15:
            dwarfReg = 32;
            break;
        default:
            noway_assert(!"unexpected REG_NUM");
    }

    return dwarfReg;
}

#endif // UNIX_AMD64_ABI

//------------------------------------------------------------------------
// Compiler::unwindBegProlog: Initialize the unwind info data structures.
// Called at the beginning of main function or funclet prolog generation.
//
void Compiler::unwindBegProlog()
{
#ifdef UNIX_AMD64_ABI
    if (generateCFIUnwindCodes())
    {
        unwindBegPrologCFI();
    }
    else
#endif // UNIX_AMD64_ABI
    {
        unwindBegPrologWindows();
    }
}

void Compiler::unwindBegPrologWindows()
{
    assert(false);
}

//------------------------------------------------------------------------
// Compiler::unwindEndProlog: Called at the end of main function or funclet
// prolog generation to indicate there is no more unwind information for this prolog.
//
void Compiler::unwindEndProlog()
{
    assert(compGeneratingProlog);
}

//------------------------------------------------------------------------
// Compiler::unwindBegEpilog: Called at the beginning of main function or funclet
// epilog generation.
//
void Compiler::unwindBegEpilog()
{
    assert(compGeneratingEpilog);
}

//------------------------------------------------------------------------
// Compiler::unwindEndEpilog: Called at the end of main function or funclet
// epilog generation.
//
void Compiler::unwindEndEpilog()
{
    assert(compGeneratingEpilog);
}

//------------------------------------------------------------------------
// Compiler::unwindPush: Record a push/save of a register.
//
// Arguments:
//    reg - The register being pushed/saved.
//
void Compiler::unwindPush(regNumber reg)
{
#ifdef UNIX_AMD64_ABI
    if (generateCFIUnwindCodes())
    {
        unwindPushPopCFI(reg);
    }
    else
#endif // UNIX_AMD64_ABI
    {
        unwindPushWindows(reg);
    }
}

void Compiler::unwindPushWindows(regNumber reg)
{
    assert(false);
}

#ifdef UNIX_AMD64_ABI
#endif // UNIX_AMD64_ABI

//------------------------------------------------------------------------
// Compiler::unwindAllocStack: Record a stack frame allocation (sub sp, X).
//
// Arguments:
//    size - The size of the stack frame allocation (the amount subtracted from the stack pointer).
//
void Compiler::unwindAllocStack(unsigned size)
{
#ifdef UNIX_AMD64_ABI
    if (generateCFIUnwindCodes())
    {
        unwindAllocStackCFI(size);
    }
    else
#endif // UNIX_AMD64_ABI
    {
        unwindAllocStackWindows(size);
    }
}

void Compiler::unwindAllocStackWindows(unsigned size)
{
    assert(false);
}

//------------------------------------------------------------------------
// Compiler::unwindSetFrameReg: Record a frame register.
//
// Arguments:
//    reg    - The register being set as the frame register.
//    offset - The offset from the current stack pointer that the frame pointer will point at.
//
void Compiler::unwindSetFrameReg(regNumber reg, unsigned offset)
{
#ifdef UNIX_AMD64_ABI
    if (generateCFIUnwindCodes())
    {
        unwindSetFrameRegCFI(reg, offset);
    }
    else
#endif // UNIX_AMD64_ABI
    {
        unwindSetFrameRegWindows(reg, offset);
    }
}

void Compiler::unwindSetFrameRegWindows(regNumber reg, unsigned offset)
{
    assert(false);
}

//------------------------------------------------------------------------
// Compiler::unwindSaveReg: Record a register save.
//
// Arguments:
//    reg    - The register being saved.
//    offset - The offset from the current stack pointer where the register is being saved.
//
void Compiler::unwindSaveReg(regNumber reg, unsigned offset)
{
#ifdef UNIX_AMD64_ABI
    if (generateCFIUnwindCodes())
    {
        unwindSaveRegCFI(reg, offset);
    }
    else
#endif // UNIX_AMD64_ABI
    {
        unwindSaveRegWindows(reg, offset);
    }
}

void Compiler::unwindSaveRegWindows(regNumber reg, unsigned offset)
{
    assert(false);
}

#ifdef UNIX_AMD64_ABI
void Compiler::unwindSaveRegCFI(regNumber reg, unsigned offset)
{
    assert(compGeneratingProlog);

    if (RBM_CALLEE_SAVED & genRegMask(reg))
    {
        FuncInfoDsc* func = funCurrentFunc();

        unsigned int cbProlog = unwindGetCurrentOffset(func);
        createCfiCode(func, cbProlog, CFI_REL_OFFSET, mapRegNumToDwarfReg(reg), offset);
    }
}
#endif // UNIX_AMD64_ABI

#ifdef DEBUG

//------------------------------------------------------------------------
// DumpUnwindInfo: Dump the unwind data.
//
// Arguments:
//    isHotCode   - true if this unwind data is for the hot section, false otherwise.
//    startOffset - byte offset of the code start that this unwind data represents.
//    endOffset   - byte offset of the code end   that this unwind data represents.
//    pHeader     - pointer to the unwind data blob.
//
void DumpUnwindInfo(bool                     isHotCode,
                    UNATIVE_OFFSET           startOffset,
                    UNATIVE_OFFSET           endOffset,
                    const UNWIND_INFO* const pHeader)
{
    assert(false);
}

#endif // DEBUG

//------------------------------------------------------------------------
// Compiler::unwindReserve: Ask the VM to reserve space for the unwind information
// for the function and all its funclets. Called once, just before asking the VM
// for memory and emitting the generated code. Calls unwindReserveFunc() to handle
// the main function and each of the funclets, in turn.
//
void Compiler::unwindReserve()
{
    assert(!compGeneratingProlog);
    assert(!compGeneratingEpilog);

    assert(compFuncInfoCount > 0);
    for (unsigned funcIdx = 0; funcIdx < compFuncInfoCount; funcIdx++)
    {
        unwindReserveFunc(funGetFunc(funcIdx));
    }
}

//------------------------------------------------------------------------
// Compiler::unwindReserveFunc: Reserve the unwind information from the VM for a
// given main function or funclet.
//
// Arguments:
//    func - The main function or funclet to reserve unwind info for.
//
void Compiler::unwindReserveFunc(FuncInfoDsc* func)
{
    assert(false);
}

//------------------------------------------------------------------------
// Compiler::unwindEmit: Report all the unwind information to the VM.
//
// Arguments:
//    pHotCode  - Pointer to the beginning of the memory with the function and funclet hot  code.
//    pColdCode - Pointer to the beginning of the memory with the function and funclet cold code.
//
void Compiler::unwindEmit(void* pHotCode, void* pColdCode)
{
    assert(!compGeneratingProlog);
    assert(!compGeneratingEpilog);

    assert(compFuncInfoCount > 0);
    for (unsigned funcIdx = 0; funcIdx < compFuncInfoCount; funcIdx++)
    {
        unwindEmitFunc(funGetFunc(funcIdx), pHotCode, pColdCode);
    }
}

//------------------------------------------------------------------------
// Compiler::unwindEmitFunc: Report the unwind information to the VM for a
// given main function or funclet. Reports the hot section, then the cold
// section if necessary.
//
// Arguments:
//    func      - The main function or funclet to reserve unwind info for.
//    pHotCode  - Pointer to the beginning of the memory with the function and funclet hot  code.
//    pColdCode - Pointer to the beginning of the memory with the function and funclet cold code.
//
void Compiler::unwindEmitFunc(FuncInfoDsc* func, void* pHotCode, void* pColdCode)
{
    assert(false);
}

#endif // defined(TARGET_WASM)
