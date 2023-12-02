// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "jitpch.h"
#include "llvm.h"

#pragma warning (disable: 4459)
#include "llvm/Bitcode/BitcodeWriter.h"
#include "llvm/Support/Signals.h"
#pragma warning (error: 4459)

// Must be kept in sync with the managed version in "CorInfoImpl.Llvm.cs".
//
enum class EEApiId
{
    GetMangledMethodName,
    GetSymbolMangledName,
    GetMangledFilterFuncletName,
    GetSignatureForMethodSymbol,
    AddCodeReloc,
    IsRuntimeImport,
    GetPrimitiveTypeForTrivialWasmStruct,
    GetTypeDescriptor,
    GetAlternativeFunctionName,
    GetExternalMethodAccessor,
    GetDebugTypeForType,
    GetDebugInfoForDebugType,
    GetDebugInfoForCurrentMethod,
    GetSingleThreadedCompilationContext,
    GetExceptionHandlingModel,
    GetExceptionThrownVariable,
    GetExceptionHandlingTable,
    Count
};

enum class JitApiId
{
    StartSingleThreadedCompilation,
    FinishSingleThreadedCompilation,
    Count
};

void* g_callbacks[static_cast<int>(EEApiId::Count)];

CorInfoType HelperFuncInfo::GetSigReturnType() const
{
    return static_cast<CorInfoType>(SigReturnType);
}

CORINFO_CLASS_HANDLE HelperFuncInfo::GetSigReturnClass(Compiler* compiler) const
{
    assert(GetSigReturnType() != CORINFO_TYPE_VALUECLASS);
    return NO_CLASS_HANDLE;
}

CorInfoType HelperFuncInfo::GetSigArgType(size_t index) const
{
    CorInfoType argType = static_cast<CorInfoType>(SigArgTypes[index]);
    assert(argType != CORINFO_TYPE_UNDEF);
    return argType;
}

CORINFO_CLASS_HANDLE HelperFuncInfo::GetSigArgClass(Compiler* compiler, size_t index) const
{
    if (GetSigArgType(index) != CORINFO_TYPE_VALUECLASS)
    {
        return NO_CLASS_HANDLE;
    }

    assert(Func == CORINFO_HELP_GETREFANY);
    return compiler->impGetRefAnyClass();
}

size_t HelperFuncInfo::GetSigArgCount(unsigned* callArgCount) const
{
    if (HasFlag(HFIF_VAR_ARG))
    {
        // TODO-LLVM: it would be nice to get rid of this case once/if we integrate into
        // upstream by using distinct helpers for the two flavors of READYTORUN_DELEGATE_CTOR.
        assert(callArgCount != nullptr);
        return *callArgCount;
    }

    size_t count = 0;
    while (SigArgTypes[count] != CORINFO_TYPE_UNDEF)
    {
        count++;
    }

    assert(count <= MAX_SIG_ARG_COUNT);
    return count;
}

bool Compiler::IsHfa(CORINFO_CLASS_HANDLE hClass) { return false; }
var_types Compiler::GetHfaType(CORINFO_CLASS_HANDLE hClass) { return TYP_UNDEF; }
unsigned Compiler::GetHfaCount(CORINFO_CLASS_HANDLE hClass) { return 0; }

Llvm::Llvm(Compiler* compiler)
    : m_pEECorInfo(*((void**)compiler->info.compCompHnd + 1)) // TODO-LLVM: hack. CorInfoImpl* is the first field of JitInterfaceWrapper.
    , m_context(GetSingleThreadedCompilationContext())
    , _compiler(compiler)
    , m_info(&compiler->info)
    , _builder(m_context->Context)
    , _sdsuMap(compiler->getAllocator(CMK_Codegen))
    , _localsMap(compiler->getAllocator(CMK_Codegen))
    , m_throwHelperBlocksMap(compiler->getAllocator(CMK_Codegen))
    , m_ehModel(GetExceptionHandlingModel())
    , m_debugVariablesMap(compiler->getAllocator(CMK_Codegen))
{
}

/* static */ void Llvm::ConfigureDiagnosticOutput()
{
#ifdef HOST_WINDOWS
    // Disable popups for CRT asserts (which LLVM uses).
    auto dbgBreakHook = [](int reportType, char* message, int* returnValue) -> int {
        if (IsDebuggerPresent())
        {
            DebugBreak();
        }
        return FALSE;
    };

    ::_set_error_mode(_OUT_TO_STDERR);
    _CrtSetReportHook2(_CRT_RPTHOOK_INSTALL, dbgBreakHook);
    _CrtSetReportMode(_CRT_WARN, _CRTDBG_MODE_FILE | _CRTDBG_MODE_DEBUG);
    _CrtSetReportFile(_CRT_WARN, _CRTDBG_FILE_STDERR);
    _CrtSetReportMode(_CRT_ERROR, _CRTDBG_MODE_FILE | _CRTDBG_MODE_DEBUG);
    _CrtSetReportFile(_CRT_ERROR, _CRTDBG_FILE_STDERR);
    _CrtSetReportMode(_CRT_ASSERT, _CRTDBG_MODE_FILE | _CRTDBG_MODE_DEBUG);
    _CrtSetReportFile(_CRT_ASSERT, _CRTDBG_FILE_STDERR);
#endif // HOST_WINDOWS
}

var_types Llvm::GetArgTypeForStructWasm(CORINFO_CLASS_HANDLE structHnd, structPassingKind* pPassKind)
{
    // Note the managed and unmanaged ABIs are the same in terms of values, but do differ w.r.t by-ref
    // parameter aliasing guarantees (native assumes no aliasing, we do not).
    bool isPassedByRef;
    CorInfoType argType = getLlvmArgTypeForArg(CORINFO_TYPE_VALUECLASS, structHnd, &isPassedByRef);

    *pPassKind = isPassedByRef ? Compiler::SPK_ByReference : Compiler::SPK_ByValue;
    return JITtype2varType(argType);
}

var_types Llvm::GetReturnTypeForStructWasm(CORINFO_CLASS_HANDLE structHnd, structPassingKind* pPassKind)
{
    bool isReturnByRef;
    CorInfoType retType = getLlvmReturnType(CORINFO_TYPE_VALUECLASS, structHnd, &isReturnByRef);
    if (isReturnByRef)
    {
        *pPassKind = Compiler::SPK_ByReference;
        return TYP_UNKNOWN;
    }

    *pPassKind = Compiler::SPK_PrimitiveType;
    return JITtype2varType(retType);
}

GCInfo* Llvm::getGCInfo()
{
    if (_gcInfo == nullptr)
    {
        _gcInfo = new (_compiler->getAllocator(CMK_GC)) GCInfo(_compiler);
    }

    return _gcInfo;
}

bool Llvm::callRequiresShadowStackSave(const GenTreeCall* call) const
{
    // In general, if the call is itself not managed (does not have a shadow stack argument) **and** may call
    // back into managed code, we need to save the shadow stack pointer, so that the RPI frame can pick it up.
    // Another case where the save/restore is required is when calling into native runtime code that can trigger
    // a GC (canonical example: allocators), to communicate shadow stack bounds to the roots scan.
    // TODO-LLVM-CQ: optimize the GC case by using specialized helpers which would sink the save/restore to the
    // unlikely path of a GC actually happening.
    // TODO-LLVM-CQ: we should skip the managed -> native -> managed transition for runtime imports implemented
    // in managed code as runtime exports.
    //
    if (call->IsHelperCall())
    {
        return helperCallRequiresShadowStackSave(_compiler->eeGetHelperNum(call->gtCallMethHnd));
    }

    // SPGCT calls are assumed to never RPI by contract.
    return !callHasShadowStackArg(call) && !call->IsSuppressGCTransition();
}

bool Llvm::helperCallRequiresShadowStackSave(CorInfoHelpFunc helperFunc) const
{
    // Save/restore is needed if the helper doesn't have a shadow stack argument, unless we know it won't call
    // back into managed code or has special semantics. TODO-LLVM-CQ: mark (make, if required) more helpers
    // "HFIF_NO_RPI_OR_GC".
    unsigned helperFlags = getHelperFuncInfo(helperFunc).Flags;
    return (helperFlags & (HFIF_SS_ARG | HFIF_NO_RPI_OR_GC)) == HFIF_NONE;
}

bool Llvm::callHasShadowStackArg(const GenTreeCall* call) const
{
    return callHasManagedCallingConvention(call);
}

bool Llvm::helperCallHasShadowStackArg(CorInfoHelpFunc helperFunc) const
{
    return helperCallHasManagedCallingConvention(helperFunc);
}

bool Llvm::callHasManagedCallingConvention(const GenTreeCall* call) const
{
    if (call->IsHelperCall())
    {
        return helperCallHasManagedCallingConvention(_compiler->eeGetHelperNum(call->gtCallMethHnd));
    }

    // Runtime imports are effectively unmanaged but are not tracked as such.
    if ((call->gtCallType == CT_USER_FUNC) && IsRuntimeImport(call->gtCallMethHnd))
    {
        return false;
    }

    return !call->IsUnmanaged();
}

bool Llvm::helperCallHasManagedCallingConvention(CorInfoHelpFunc helperFunc) const
{
    return getHelperFuncInfo(helperFunc).HasFlag(HFIF_SS_ARG);
}

bool Llvm::helperCallMayPhysicallyThrow(CorInfoHelpFunc helperFunc) const
{
    // Allocators can throw OOM.
    HelperCallProperties& properties = Compiler::s_helperCallProperties;
    return !properties.NoThrow(helperFunc) || properties.IsAllocator(helperFunc);
}

//------------------------------------------------------------------------
// getHelperFuncInfo: Get additional information about a Jit helper.
//
// This is very similar to the "HelperCallProperties" class, but contains
// information relevant to the LLVM target. In particular, we need to know
// whether a given helper is implemented in managed code, and the signature,
// to avoid multiple compilations disagreeing due to the implicit byref<->
// nint conversions.
//
// TODO-LLVM: communicate (at least) the signature through a Jit-EE API.
//
// Arguments:
//    helperFunc - The helper func
//
// Return Value:
//    Reference to the info structure for "helperFunc".
//
/* static */ const HelperFuncInfo& Llvm::getHelperFuncInfo(CorInfoHelpFunc helperFunc)
{
    // Note on Runtime[Type|Method|Field]Handle: it should faithfully be represented as CORINFO_TYPE_VALUECLASS.
    // However, that is currently both not necessary due to the unwrapping performed for LLVM types and not what
    // the Jit expects.
    const int CORINFO_TYPE_RT_HANDLE = CORINFO_TYPE_NATIVEINT;

#define FUNC(helper) INDEBUG_COMMA(helper)

    // clang-format off
    static const HelperFuncInfo s_infos[] =
    {
        { FUNC(CORINFO_HELP_UNDEF) },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DIV) CORINFO_TYPE_INT, { CORINFO_TYPE_INT, CORINFO_TYPE_INT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MOD) CORINFO_TYPE_INT, { CORINFO_TYPE_INT, CORINFO_TYPE_INT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_UDIV) CORINFO_TYPE_UINT, { CORINFO_TYPE_UINT, CORINFO_TYPE_UINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_UMOD) CORINFO_TYPE_UINT, { CORINFO_TYPE_UINT, CORINFO_TYPE_UINT }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_LLSH) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_INT } },
        { FUNC(CORINFO_HELP_LRSH) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_INT } },
        { FUNC(CORINFO_HELP_LRSZ) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_INT } },
        { FUNC(CORINFO_HELP_LMUL) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG } },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_LMUL_OVF) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ULMUL_OVF) CORINFO_TYPE_ULONG, { CORINFO_TYPE_ULONG, CORINFO_TYPE_ULONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LDIV) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LMOD) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ULDIV) CORINFO_TYPE_ULONG, { CORINFO_TYPE_ULONG, CORINFO_TYPE_ULONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ULMOD) CORINFO_TYPE_ULONG, { CORINFO_TYPE_ULONG, CORINFO_TYPE_ULONG }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_LNG2DBL) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_LONG } },
        { FUNC(CORINFO_HELP_ULNG2DBL) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_ULONG } },
        { FUNC(CORINFO_HELP_DBL2INT) CORINFO_TYPE_INT, { CORINFO_TYPE_DOUBLE } },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2INT_OVF) CORINFO_TYPE_INT, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_DBL2LNG) CORINFO_TYPE_LONG, { CORINFO_TYPE_DOUBLE } },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2LNG_OVF) CORINFO_TYPE_LONG, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_DBL2UINT) CORINFO_TYPE_UINT, { CORINFO_TYPE_DOUBLE } },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2UINT_OVF) CORINFO_TYPE_UINT, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_DBL2ULNG) CORINFO_TYPE_ULONG, { CORINFO_TYPE_DOUBLE } },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2ULNG_OVF) CORINFO_TYPE_ULONG, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_FLTREM) CORINFO_TYPE_FLOAT, { CORINFO_TYPE_FLOAT, CORINFO_TYPE_FLOAT } },
        { FUNC(CORINFO_HELP_DBLREM) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_DOUBLE, CORINFO_TYPE_DOUBLE } },
        { FUNC(CORINFO_HELP_FLTROUND) CORINFO_TYPE_FLOAT, { CORINFO_TYPE_FLOAT } },
        { FUNC(CORINFO_HELP_DBLROUND) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_DOUBLE } },

        // Runtime export, implemented in "Runtime.Base\src\System\Runtime\RuntimeExports.cs".
        { FUNC(CORINFO_HELP_NEWFAST) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEWFAST_MAYBEFROZEN) },

        // Implemented in "Runtime\wasm\AllocFast.cpp".
        { FUNC(CORINFO_HELP_NEWSFAST) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEWSFAST_FINALIZE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEWSFAST_ALIGN8) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEWSFAST_ALIGN8_VC) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\ArrayHelpers.cs".
        { FUNC(CORINFO_HELP_NEW_MDARR) CORINFO_TYPE_CLASS, { CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_INT, CORINFO_TYPE_PTR }, HFIF_SS_ARG }, // Oddity: IntPtr used for MethodTable*.
        { FUNC(CORINFO_HELP_NEW_MDARR_RARE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_INT, CORINFO_TYPE_PTR }, HFIF_SS_ARG }, // Oddity: IntPtr used for MethodTable*.

        // Runtime export, implemented in "Runtime.Base\src\System\Runtime\RuntimeExports.cs".
        { FUNC(CORINFO_HELP_NEWARR_1_DIRECT) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEWARR_1_MAYBEFROZEN) },

        // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_NEWARR_1_OBJ) },

        // Implemented in "Runtime\wasm\AllocFast.cpp".
        { FUNC(CORINFO_HELP_NEWARR_1_VC) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEWARR_1_ALIGN8) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT }, HFIF_SS_ARG },

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_STRCNS) },
        { FUNC(CORINFO_HELP_INITCLASS) },
        { FUNC(CORINFO_HELP_INITINSTCLASS) },

        // Runtime exports from "Runtime.Base\src\System\Runtime\TypeCast.cs" and "Runtime.Base\src\System\Runtime\RuntimeExports.cs".
        { FUNC(CORINFO_HELP_ISINSTANCEOFINTERFACE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ISINSTANCEOFARRAY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ISINSTANCEOFCLASS) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ISINSTANCEOFANY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_CHKCASTINTERFACE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_CHKCASTARRAY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_CHKCASTCLASS) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_CHKCASTANY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_CHKCASTCLASS_SPECIAL) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ISINSTANCEOF_EXCEPTION) CORINFO_TYPE_BOOL, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },

        { FUNC(CORINFO_HELP_BOX) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_BOX_NULLABLE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_UNBOX) CORINFO_TYPE_BYREF, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_UNBOX_NULLABLE) CORINFO_TYPE_VOID, { CORINFO_TYPE_BYREF, CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
        { FUNC(CORINFO_HELP_GETREFANY) CORINFO_TYPE_BYREF, { CORINFO_TYPE_RT_HANDLE, CORINFO_TYPE_VALUECLASS}, HFIF_SS_ARG },

        // Implemented in "Runtime.Base\src\System\Runtime\TypeCast.cs".
        { FUNC(CORINFO_HELP_ARRADDR_ST) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS, CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LDELEMA_REF) CORINFO_TYPE_BYREF, { CORINFO_TYPE_CLASS, CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG }, // Oddity: IntPtr used for MethodTable*.

        // Runtime exports implemented in "Runtime.Base\src\System\Runtime\ExcetionHandling.wasm.cs".
        { FUNC(CORINFO_HELP_THROW) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_RETHROW) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // Implemented in "Runtime\MiscHelpers.cpp".
        { FUNC(CORINFO_HELP_USER_BREAKPOINT) CORINFO_TYPE_VOID, { } },

        // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
        { FUNC(CORINFO_HELP_RNGCHKFAIL) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_OVERFLOW) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWDIVZERO) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWNULLREF) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWMISALIGN) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },

        // Verification is in the process of being deleted from RyuJit.
        { FUNC(CORINFO_HELP_VERIFICATION) },

        // Implemented in "Runtime\EHHelpers.cpp".
        { FUNC(CORINFO_HELP_FAIL_FAST) CORINFO_TYPE_VOID, { }, HFIF_NO_RPI_OR_GC },

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_METHOD_ACCESS_EXCEPTION) },
        { FUNC(CORINFO_HELP_FIELD_ACCESS_EXCEPTION) },
        { FUNC(CORINFO_HELP_CLASS_ACCESS_EXCEPTION) },

        // Not used with funclet-based EH.
        { FUNC(CORINFO_HELP_ENDCATCH) },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\SynchronizedMethodHelpers.cs".
        { FUNC(CORINFO_HELP_MON_ENTER) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MON_EXIT) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MON_ENTER_STATIC) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MON_EXIT_STATIC) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_GETCLASSFROMMETHODPARAM) CORINFO_TYPE_PTR, { CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_GETSYNCFROMCLASSHANDLE) CORINFO_TYPE_PTR, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // Apparently NYI in NativeAOT.
        { FUNC(CORINFO_HELP_STOP_FOR_GC) },

        // (Not) implemented in "Runtime\portable.cpp".
        { FUNC(CORINFO_HELP_POLL_GC) CORINFO_TYPE_VOID, { } },

        // Debug-only helpers NYI in NativeAOT.
        { FUNC(CORINFO_HELP_STRESS_GC) },
        { FUNC(CORINFO_HELP_CHECK_OBJ) },

        // Write barriers, implemented in "Runtime\portable.cpp".
        { FUNC(CORINFO_HELP_ASSIGN_REF) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_ASSIGN_REF_ENSURE_NONHEAP) }, // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_ASSIGN_BYREF) }, // Not used on WASM.

        // Not used in NativeAOT (or at all in some cases).
        { FUNC(CORINFO_HELP_ASSIGN_STRUCT) },
        { FUNC(CORINFO_HELP_GETFIELD8) },
        { FUNC(CORINFO_HELP_SETFIELD8) },
        { FUNC(CORINFO_HELP_GETFIELD16) },
        { FUNC(CORINFO_HELP_SETFIELD16) },
        { FUNC(CORINFO_HELP_GETFIELD32) },
        { FUNC(CORINFO_HELP_SETFIELD32) },
        { FUNC(CORINFO_HELP_GETFIELD64) },
        { FUNC(CORINFO_HELP_SETFIELD64) },
        { FUNC(CORINFO_HELP_GETFIELDOBJ) },
        { FUNC(CORINFO_HELP_SETFIELDOBJ) },
        { FUNC(CORINFO_HELP_GETFIELDSTRUCT) },
        { FUNC(CORINFO_HELP_SETFIELDSTRUCT) },
        { FUNC(CORINFO_HELP_GETFIELDFLOAT) },
        { FUNC(CORINFO_HELP_SETFIELDFLOAT) },
        { FUNC(CORINFO_HELP_GETFIELDDOUBLE) },
        { FUNC(CORINFO_HELP_SETFIELDDOUBLE) },
        { FUNC(CORINFO_HELP_GETFIELDADDR) },
        { FUNC(CORINFO_HELP_GETSTATICFIELDADDR) },
        { FUNC(CORINFO_HELP_GETSTATICFIELDADDR_TLS) },
        { FUNC(CORINFO_HELP_GETGENERICS_GCSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETGENERICS_NONGCSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETSHARED_GCSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETSHARED_GCSTATIC_BASE_NOCTOR) },
        { FUNC(CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_NOCTOR) },
        { FUNC(CORINFO_HELP_GETSHARED_GCSTATIC_BASE_DYNAMICCLASS) },
        { FUNC(CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_DYNAMICCLASS) },
        { FUNC(CORINFO_HELP_CLASSINIT_SHARED_DYNAMICCLASS) },
        { FUNC(CORINFO_HELP_GETGENERICS_GCTHREADSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETGENERICS_NONGCTHREADSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE) },
        { FUNC(CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_NOCTOR) },
        { FUNC(CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR) },
        { FUNC(CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_DYNAMICCLASS) },
        { FUNC(CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_DYNAMICCLASS) },
        { FUNC(CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED) },
        { FUNC(CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED) },
            
        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_DBG_IS_JUST_MY_CODE) },
        { FUNC(CORINFO_HELP_PROF_FCN_ENTER) },
        { FUNC(CORINFO_HELP_PROF_FCN_LEAVE) },
        { FUNC(CORINFO_HELP_PROF_FCN_TAILCALL) },
        { FUNC(CORINFO_HELP_BBT_FCN_ENTER) },

        // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_PINVOKE_CALLI) },

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_TAILCALL) },

        // Implemented as "Environment.CurrentManagedThreadId".
        { FUNC(CORINFO_HELP_GETCURRENTMANAGEDTHREADID) CORINFO_TYPE_INT, { }, HFIF_SS_ARG },

        // Part of the inlined PInvoke frame construction feature which is NYI in NativeAOT.
        { FUNC(CORINFO_HELP_INIT_PINVOKE_FRAME) },

        // Implemented as plain "memset"/"memcpy".
        { FUNC(CORINFO_HELP_MEMSET) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT, CORINFO_TYPE_NATIVEUINT } },
        { FUNC(CORINFO_HELP_MEMCPY) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR, CORINFO_TYPE_NATIVEUINT } },

        // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_RUNTIMEHANDLE_METHOD) },
        { FUNC(CORINFO_HELP_RUNTIMEHANDLE_METHOD_LOG) },
        { FUNC(CORINFO_HELP_RUNTIMEHANDLE_CLASS) },
        { FUNC(CORINFO_HELP_RUNTIMEHANDLE_CLASS_LOG) },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\LdTokenHelpers.cs".
        { FUNC(CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG }, // Oddity: IntPtr used for MethodTable*.

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_RT_HANDLE }, HFIF_SS_ARG },

        // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_VIRTUAL_FUNC_PTR) },
        { FUNC(CORINFO_HELP_READYTORUN_NEW) },
        { FUNC(CORINFO_HELP_READYTORUN_NEWARR_1) },

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_READYTORUN_ISINSTANCEOF) },
        { FUNC(CORINFO_HELP_READYTORUN_CHKCAST) },

        // Emitted by the compiler as intrinsics. (see "ILCompiler.LLVM\CodeGen\LLVMObjectWriter.cs", "GetCodeForReadyToRunGenericHelper" and others).
        { FUNC(CORINFO_HELP_READYTORUN_GCSTATIC_BASE) CORINFO_TYPE_PTR, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE) CORINFO_TYPE_PTR, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_THREADSTATIC_BASE) CORINFO_TYPE_PTR, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE) CORINFO_TYPE_PTR, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR) CORINFO_TYPE_PTR, { CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_READYTORUN_GENERIC_HANDLE) CORINFO_TYPE_PTR, { CORINFO_TYPE_PTR }, HFIF_SS_ARG | HFIF_THROW_OR_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_READYTORUN_DELEGATE_CTOR) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS, CORINFO_TYPE_CLASS, CORINFO_TYPE_PTR }, HFIF_SS_ARG | HFIF_VAR_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE) CORINFO_TYPE_PTR, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // NGEN/R2R-specific marker helpers.
        { FUNC(CORINFO_HELP_EE_PERSONALITY_ROUTINE) },
        { FUNC(CORINFO_HELP_EE_PERSONALITY_ROUTINE_FILTER_FUNCLET) },

        // x86-specific write barriers.
        { FUNC(CORINFO_HELP_ASSIGN_REF_EAX) },
        { FUNC(CORINFO_HELP_ASSIGN_REF_EBX) },
        { FUNC(CORINFO_HELP_ASSIGN_REF_ECX) },
        { FUNC(CORINFO_HELP_ASSIGN_REF_ESI) },
        { FUNC(CORINFO_HELP_ASSIGN_REF_EDI) },
        { FUNC(CORINFO_HELP_ASSIGN_REF_EBP) },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF_EAX) },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF_EBX) },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF_ECX) },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF_ESI) },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF_EDI) },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF_EBP) },

        // Debug-only functionality NYI in NativeAOT.
        { FUNC(CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR) },
        { FUNC(CORINFO_HELP_DEBUG_LOG_LOOP_CLONING) },

        // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
        { FUNC(CORINFO_HELP_THROW_ARGUMENTEXCEPTION) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROW_NOT_IMPLEMENTED) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },

        // Dead code.
        { FUNC(CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED) },

        // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_THROW_AMBIGUOUS_RESOLUTION_EXCEPTION) },
        { FUNC(CORINFO_HELP_THROW_ENTRYPOINT_NOT_FOUND_EXCEPTION) },

        // [R]PI helpers, implemented in "Runtime\thread.cpp".
        { FUNC(CORINFO_HELP_JIT_PINVOKE_BEGIN) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_JIT_PINVOKE_END) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },

        // Implemented in "CoreLib\src\System\Runtime\TypeLoaderExports.cs".
        { FUNC(CORINFO_HELP_GVMLOOKUP_FOR_SLOT) CORINFO_TYPE_NATIVEINT, { CORINFO_TYPE_CLASS, CORINFO_TYPE_RT_HANDLE }, HFIF_SS_ARG }, // Oddity: IntPtr used for a pointer.

        // Not used in NativeAOT (stack probing - not used for LLVM).
        { FUNC(CORINFO_HELP_STACK_PROBE) },
        { FUNC(CORINFO_HELP_PATCHPOINT) },
        { FUNC(CORINFO_HELP_CLASSPROFILE32) },
        { FUNC(CORINFO_HELP_CLASSPROFILE64) },
        { FUNC(CORINFO_HELP_DELEGATEPROFILE32) },
        { FUNC(CORINFO_HELP_DELEGATEPROFILE64) },
        { FUNC(CORINFO_HELP_VTABLEPROFILE32) },
        { FUNC(CORINFO_HELP_VTABLEPROFILE64) },
        { FUNC(CORINFO_HELP_COUNTPROFILE32) },
        { FUNC(CORINFO_HELP_COUNTPROFILE64) },
        { FUNC(CORINFO_HELP_PARTIAL_COMPILATION_PATCHPOINT) },
        { FUNC(CORINFO_HELP_VALIDATE_INDIRECT_CALL) },
        { FUNC(CORINFO_HELP_DISPATCH_INDIRECT_CALL) },

        { FUNC(CORINFO_HELP_LLVM_GET_OR_INIT_SHADOW_STACK_TOP) CORINFO_TYPE_PTR, { }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_LLVM_SET_SHADOW_STACK_TOP) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_LLVM_EH_CATCH) CORINFO_TYPE_CLASS, { CORINFO_TYPE_NATIVEUINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LLVM_EH_POP_UNWOUND_VIRTUAL_FRAMES) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LLVM_EH_PUSH_VIRTUAL_UNWIND_FRAME) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR, CORINFO_TYPE_NATIVEUINT }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_LLVM_EH_POP_VIRTUAL_UNWIND_FRAME) CORINFO_TYPE_VOID, { }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_LLVM_EH_UNHANDLED_EXCEPTION) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LLVM_RESOLVE_INTERFACE_CALL_TARGET) CORINFO_TYPE_PTR, { CORINFO_TYPE_CLASS, CORINFO_TYPE_PTR }, HFIF_SS_ARG },
    };
    // clang-format on

    // Make sure our array is up-to-date.
    static_assert_no_msg(ArrLen(s_infos) == CORINFO_HELP_COUNT);

    assert(helperFunc < CORINFO_HELP_COUNT);
    const HelperFuncInfo& info = s_infos[helperFunc];

    // We don't fill out the info for some helpers because we don't expect to encounter them.
    assert(info.IsInitialized() && (info.Func == helperFunc));

    return info;
}

CorInfoType Llvm::getLlvmArgTypeForArg(CorInfoType argSigType, CORINFO_CLASS_HANDLE argSigClass, bool* pIsByRef)
{
    assert(argSigType != CORINFO_TYPE_UNDEF);
    if (argSigType == CORINFO_TYPE_REFANY)
    {
        argSigType = CORINFO_TYPE_VALUECLASS;
    }
    //
    // WASM C ABI is documented here: https://github.com/WebAssembly/tool-conventions/blob/main/BasicCABI.md.
    // In essense, structs are passed by reference except if they are trivial wrappers of a primitive (scalar).
    // We follow this rule for the native calling convention as well as the managed one.
    //
    bool isByRef = false;
    CorInfoType argType = argSigType;
    if (argSigType == CORINFO_TYPE_VALUECLASS)
    {
        argType = GetPrimitiveTypeForTrivialWasmStruct(argSigClass);
        if (argType == CORINFO_TYPE_UNDEF)
        {
            argType = CORINFO_TYPE_PTR;
            isByRef = true;
        }
    }

    if (pIsByRef != nullptr)
    {
        *pIsByRef = isByRef;
    }
    return argType;
}

CorInfoType Llvm::getLlvmReturnType(CorInfoType sigRetType, CORINFO_CLASS_HANDLE sigRetClass, bool* pIsByRef)
{
    assert(sigRetType != CORINFO_TYPE_UNDEF);
    if (sigRetType == CORINFO_TYPE_REFANY)
    {
        sigRetType = CORINFO_TYPE_VALUECLASS;
    }

    CorInfoType returnType = sigRetType;
    if (sigRetType == CORINFO_TYPE_VALUECLASS)
    {
        returnType = GetPrimitiveTypeForTrivialWasmStruct(sigRetClass);
    }

    bool isByRef = returnType == CORINFO_TYPE_UNDEF;
    if (pIsByRef != nullptr)
    {
        *pIsByRef = isByRef;
    }
    return isByRef ? CORINFO_TYPE_VOID : returnType;
}

// When looking at a sigInfo from eeGetMethodSig we have CorInfoType(s) but when looking at lclVars we have LclVarDsc or var_type(s),
// This method exists to allow both to map to LLVM types.
/* static */ CorInfoType Llvm::toCorInfoType(var_types type)
{
    switch (type)
    {
        case TYP_BYREF:
            return CORINFO_TYPE_BYREF;
        case TYP_BYTE:
            return CORINFO_TYPE_BYTE;
        case TYP_UBYTE:
            return CORINFO_TYPE_UBYTE;
        case TYP_DOUBLE:
            return CORINFO_TYPE_DOUBLE;
        case TYP_FLOAT:
            return CORINFO_TYPE_FLOAT;
        case TYP_INT:
            return CORINFO_TYPE_INT;
        case TYP_UINT:
            return CORINFO_TYPE_UINT;
        case TYP_LONG:
            return CORINFO_TYPE_LONG;
        case TYP_ULONG:
            return CORINFO_TYPE_ULONG;
        case TYP_REF:
            return CORINFO_TYPE_CLASS;
        case TYP_SHORT:
            return CORINFO_TYPE_SHORT;
        case TYP_USHORT:
            return CORINFO_TYPE_USHORT;
        case TYP_STRUCT:
            return CORINFO_TYPE_VALUECLASS;
        case TYP_UNDEF:
            return CORINFO_TYPE_UNDEF;
        case TYP_VOID:
            return CORINFO_TYPE_VOID;
        default:
            unreached();
    }
}

//------------------------------------------------------------------------
// getLlvmArgTypeForCallArg: Get the ABI type for the given call argument.
//
// Assumes that the ABI info has already been initialized.
//
// Arguments:
//    arg - The call argument
//
// Return Value:
//    The type to use for "arg" when constructing LLVM signatures.
//
/* static */ CorInfoType Llvm::getLlvmArgTypeForCallArg(CallArg* arg)
{
    assert(arg->AbiInfo.ArgType != TYP_UNDEF);
    if (arg->AbiInfo.IsPointer)
    {
        return CORINFO_TYPE_PTR;
    }

    assert(!arg->AbiInfo.PassedByRef);
    return toCorInfoType(arg->AbiInfo.ArgType);
}

TargetAbiType Llvm::getAbiTypeForType(var_types type)
{
    switch (genActualType(type))
    {
        case TYP_VOID:
            return TargetAbiType::Void;
        case TYP_INT:
            return TargetAbiType::Int32;
        case TYP_LONG:
            return TargetAbiType::Int64;
        case TYP_REF:
        case TYP_BYREF:
            return (TARGET_POINTER_SIZE == 4) ? TargetAbiType::Int32 : TargetAbiType::Int64;
        case TYP_FLOAT:
            return TargetAbiType::Float;
        case TYP_DOUBLE:
            return TargetAbiType::Double;
        default:
            unreached();
    }
}

CORINFO_GENERIC_HANDLE Llvm::getSymbolHandleForHelperFunc(CorInfoHelpFunc helperFunc)
{
    void* pIndirection = nullptr;
    void* handle = _compiler->compGetHelperFtn(static_cast<CorInfoHelpFunc>(helperFunc), &pIndirection);
    assert(pIndirection == nullptr);

    return CORINFO_GENERIC_HANDLE(handle);
}

CORINFO_GENERIC_HANDLE Llvm::getSymbolHandleForClassToken(mdToken token)
{
    // The importer call here relies on RyuJit not inlining EH (which it currently does not).
    CORINFO_RESOLVED_TOKEN resolvedToken;
    _compiler->impResolveToken((BYTE*)&token, &resolvedToken, CORINFO_TOKENKIND_Class);

    void* pIndirection = nullptr;
    CORINFO_CLASS_HANDLE typeSymbolHandle = m_info->compCompHnd->embedClassHandle(resolvedToken.hClass, &pIndirection);
    assert(pIndirection == nullptr);

    return CORINFO_GENERIC_HANDLE(typeSymbolHandle);
}

template <EEApiId Func, typename TReturn, typename... TArgs>
TReturn CallEEApi(TArgs... args)
{
    return static_cast<TReturn (*)(TArgs...)>(g_callbacks[static_cast<int>(Func)])(args...);
}

const char* Llvm::GetMangledMethodName(CORINFO_METHOD_HANDLE methodHandle)
{
    return CallEEApi<EEApiId::GetMangledMethodName, const char*>(m_pEECorInfo, methodHandle);
}

const char* Llvm::GetMangledSymbolName(void* symbol)
{
    return CallEEApi<EEApiId::GetSymbolMangledName, const char*>(m_pEECorInfo, symbol);
}

const char* Llvm::GetMangledFilterFuncletName(unsigned index)
{
    return CallEEApi<EEApiId::GetMangledFilterFuncletName, const char*>(m_pEECorInfo, index);
}

bool Llvm::GetSignatureForMethodSymbol(CORINFO_GENERIC_HANDLE symbolHandle, CORINFO_SIG_INFO* pSig)
{
    return CallEEApi<EEApiId::GetSignatureForMethodSymbol, int>(m_pEECorInfo, symbolHandle, pSig) != 0;
}

void Llvm::AddCodeReloc(void* handle)
{
    CallEEApi<EEApiId::AddCodeReloc, void>(m_pEECorInfo, handle);
}

bool Llvm::IsRuntimeImport(CORINFO_METHOD_HANDLE methodHandle) const
{
    return CallEEApi<EEApiId::IsRuntimeImport, uint32_t>(m_pEECorInfo, methodHandle) != 0;
}

CorInfoType Llvm::GetPrimitiveTypeForTrivialWasmStruct(CORINFO_CLASS_HANDLE structHandle)
{
    return CallEEApi<EEApiId::GetPrimitiveTypeForTrivialWasmStruct, CorInfoType>(m_pEECorInfo, structHandle);
}

void Llvm::GetTypeDescriptor(CORINFO_CLASS_HANDLE typeHandle, TypeDescriptor* pTypeDescriptor)
{
    CallEEApi<EEApiId::GetTypeDescriptor, void>(m_pEECorInfo, typeHandle, pTypeDescriptor);
}

const char* Llvm::GetAlternativeFunctionName()
{
    return CallEEApi<EEApiId::GetAlternativeFunctionName, const char*>(m_pEECorInfo);
}

CORINFO_GENERIC_HANDLE Llvm::GetExternalMethodAccessor(
    CORINFO_METHOD_HANDLE methodHandle, const TargetAbiType* sig, int sigLength)
{
    return CallEEApi<EEApiId::GetExternalMethodAccessor, CORINFO_GENERIC_HANDLE>(m_pEECorInfo, methodHandle, sig,
                                                                               sigLength);
}

CORINFO_LLVM_DEBUG_TYPE_HANDLE Llvm::GetDebugTypeForType(CORINFO_CLASS_HANDLE typeHandle)
{
    return CallEEApi<EEApiId::GetDebugTypeForType, CORINFO_LLVM_DEBUG_TYPE_HANDLE>(m_pEECorInfo, typeHandle);
}

void Llvm::GetDebugInfoForDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle, CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo)
{
    CallEEApi<EEApiId::GetDebugInfoForDebugType, void>(m_pEECorInfo, debugTypeHandle, pInfo);
}

void Llvm::GetDebugInfoForCurrentMethod(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
{
    CallEEApi<EEApiId::GetDebugInfoForCurrentMethod, void>(m_pEECorInfo, pInfo);
}

SingleThreadedCompilationContext* Llvm::GetSingleThreadedCompilationContext()
{
    return CallEEApi<EEApiId::GetSingleThreadedCompilationContext, SingleThreadedCompilationContext*>(m_pEECorInfo);
}

CorInfoLlvmEHModel Llvm::GetExceptionHandlingModel()
{
    return CallEEApi<EEApiId::GetExceptionHandlingModel, CorInfoLlvmEHModel>(m_pEECorInfo);
}

CORINFO_GENERIC_HANDLE Llvm::GetExceptionThrownVariable()
{
    return CallEEApi<EEApiId::GetExceptionThrownVariable, CORINFO_GENERIC_HANDLE>(m_pEECorInfo);
}

CORINFO_GENERIC_HANDLE Llvm::GetExceptionHandlingTable(CORINFO_LLVM_EH_CLAUSE* pClauses, int count)
{
    return CallEEApi<EEApiId::GetExceptionHandlingTable, CORINFO_GENERIC_HANDLE>(m_pEECorInfo, pClauses, count);
}

extern "C" DLLEXPORT void registerLlvmCallbacks(void** jitImports, void** jitExports)
{
    assert((jitImports != nullptr) && (jitImports[static_cast<int>(EEApiId::Count)] == (void*)0x1234));
    assert(jitExports != nullptr);

    memcpy(g_callbacks, jitImports, static_cast<int>(EEApiId::Count) * sizeof(void*));
    jitExports[static_cast<int>(JitApiId::StartSingleThreadedCompilation)] = &Llvm::StartSingleThreadedCompilation;
    jitExports[static_cast<int>(JitApiId::FinishSingleThreadedCompilation)] = &Llvm::FinishSingleThreadedCompilation;
    jitExports[static_cast<int>(JitApiId::Count)] = (void*)0x1234;
}

/* static */ SingleThreadedCompilationContext* Llvm::StartSingleThreadedCompilation(
    const char* path, const char* triple, const char* dataLayout)
{
    SingleThreadedCompilationContext* context = new SingleThreadedCompilationContext(path);
    context->Module.setTargetTriple(triple);
    context->Module.setDataLayout(dataLayout);

    return context;
}

/* static */ void Llvm::FinishSingleThreadedCompilation(SingleThreadedCompilationContext* context)
{
    assert(context != nullptr);

    Module& module = context->Module;
    if (context->DebugCompileUnitsMap.GetCount() != 0)
    {
        module.addModuleFlag(llvm::Module::Warning, "Dwarf Version", 4);
        module.addModuleFlag(llvm::Module::Warning, "Debug Info Version", 3);
    }

    std::error_code code;
    StringRef outputFilePath = module.getName();
    if (JitConfig.JitCheckLlvmIR())
    {
        StringRef outputFilePathWithoutExtension = outputFilePath.take_front(outputFilePath.find_last_of('.'));
        llvm::raw_fd_ostream textOutputStream(Twine(outputFilePathWithoutExtension + ".txt").str(), code);
        module.print(textOutputStream, nullptr);
    }

    llvm::raw_fd_ostream bitCodeFileStream(outputFilePath, code);
    llvm::WriteBitcodeToFile(module, bitCodeFileStream);

    delete context;
}
