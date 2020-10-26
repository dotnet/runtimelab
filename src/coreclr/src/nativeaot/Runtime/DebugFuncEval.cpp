// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "common.h"
#include "CommonTypes.h"
#include "DebugFuncEval.h"
#include "rhassert.h"
#include "RWLock.h"
#include "slist.h"
#include "RuntimeInstance.h"

GVAL_IMPL_INIT(uint32_t, g_FuncEvalMode, 0);
GVAL_IMPL_INIT(uint32_t, g_FuncEvalParameterBufferSize, 0);
GVAL_IMPL_INIT(uint64_t, g_MostRecentFuncEvalHijackInstructionPointer, 0);

#ifndef DACCESS_COMPILE

/* static */ uint32_t DebugFuncEval::GetFuncEvalParameterBufferSize()
{
    return g_FuncEvalParameterBufferSize;
}

/* static */ uint32_t DebugFuncEval::GetFuncEvalMode()
{
    return g_FuncEvalMode;
}

/* static */ uint64_t DebugFuncEval::GetMostRecentFuncEvalHijackInstructionPointer()
{
    return g_MostRecentFuncEvalHijackInstructionPointer;
}

/// <summary>
/// Retrieve the global FuncEval parameter buffer size.
/// </summary>
/// <remarks>
/// During debugging, if a FuncEval is requested,
/// the func eval infrastructure needs to know how much buffer to allocate for the debugger to
/// write the parameter information in. The C# supporting code will call this API to obtain the
/// buffer size. By that time, the value should have been set through the UpdateFuncEvalParameterBufferSize()
/// method on the ISosRedhawk7 interface.
/// </remarks>
EXTERN_C REDHAWK_API uint32_t __cdecl RhpGetFuncEvalParameterBufferSize()
{
    return DebugFuncEval::GetFuncEvalParameterBufferSize();
}

/// <summary>
/// Retrieve the global FuncEval mode.
/// </summary>
/// <remarks>
/// During debugging, if a FuncEval is requested,
/// the func eval infrastructure needs to know what mode to execute the FuncEval request
/// The C# supporting code will call this API to obtain the mode. By that time, the value
/// should have been set through the UpdateFuncEvalMode() method on the ISosRedhawk7 interface.
/// </remarks>
EXTERN_C REDHAWK_API uint32_t __cdecl RhpGetFuncEvalMode()
{
    return DebugFuncEval::GetFuncEvalMode();
}

/// <summary>
/// Initiate the func eval abort
/// </summary>
/// <remarks>
/// This is the entry point of FuncEval abort
/// When the debugger decides to abort the FuncEval, it will create a remote thread calling this function.
/// This function will call back into the DebugFuncEvalAbortHelper to perform the abort.
EXTERN_C REDHAWK_API void __cdecl RhpInitiateFuncEvalAbort(void* pointerFromDebugger)
{
    DebugFuncEvalAbortHelperFunctionType debugFuncEvalAbortHelperFunction = (DebugFuncEvalAbortHelperFunctionType)GetRuntimeInstance()->GetClasslibFunctionFromCodeAddress((void*)g_MostRecentFuncEvalHijackInstructionPointer, ClasslibFunctionId::DebugFuncEvalAbortHelper);
    ASSERT(debugFuncEvalAbortHelperFunction != nullptr);
    debugFuncEvalAbortHelperFunction((int64_t)pointerFromDebugger);
}

#else

uint64_t DebugFuncEval::GetMostRecentFuncEvalHijackInstructionPointer()
{
    return g_MostRecentFuncEvalHijackInstructionPointer;
}

#endif //!DACCESS_COMPILE

EXTERN_C void RhpDebugFuncEvalHelper(void*, void*);
GPTR_IMPL_INIT(PTR_VOID, g_RhpDebugFuncEvalHelperAddr, (void **)(&RhpDebugFuncEvalHelper));

GPTR_IMPL_INIT(PTR_VOID, g_RhpInitiateFuncEvalAbortAddr, (void**)&RhpInitiateFuncEvalAbort);
