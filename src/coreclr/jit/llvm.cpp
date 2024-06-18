// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "jitpch.h"
#include "llvm.h"
#include "CorJitApiId.Shared.cspp"

// TODO-LLVM-Upstream: figure out how to fix these warnings in LLVM headers.
#pragma warning(push)
#pragma warning (disable : 4242)
#pragma warning (disable : 4244)
#pragma warning (disable : 4459)
#pragma warning (disable : 4267)
#include "llvm/Bitcode/BitcodeWriter.h"
#include "llvm/Support/Signals.h"
#pragma warning(pop)

// Must be kept in sync with the managed version in "CorInfoImpl.Llvm.cs".
//
enum class EEApiId
{
    GetMangledMethodName,
    GetSymbolMangledName,
    GetMangledFilterFuncletName,
    GetSignatureForMethodSymbol,
    AddCodeReloc,
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
    GetJitTestInfo,
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
    , m_phiPairs(compiler->getAllocator(CMK_Codegen))
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
        return helperCallHasManagedCallingConvention(call->GetHelperNum());
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
        { FUNC(CORINFO_HELP_LLSH) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_INT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LRSH) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_INT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LRSZ) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_INT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LMUL) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_LMUL_OVF) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ULMUL_OVF) CORINFO_TYPE_ULONG, { CORINFO_TYPE_ULONG, CORINFO_TYPE_ULONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LDIV) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LMOD) CORINFO_TYPE_LONG, { CORINFO_TYPE_LONG, CORINFO_TYPE_LONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ULDIV) CORINFO_TYPE_ULONG, { CORINFO_TYPE_ULONG, CORINFO_TYPE_ULONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ULMOD) CORINFO_TYPE_ULONG, { CORINFO_TYPE_ULONG, CORINFO_TYPE_ULONG }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_LNG2DBL) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_LONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_ULNG2DBL) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_ULONG }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_DBL2INT) CORINFO_TYPE_INT, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2INT_OVF) CORINFO_TYPE_INT, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_DBL2LNG) CORINFO_TYPE_LONG, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2LNG_OVF) CORINFO_TYPE_LONG, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_DBL2UINT) CORINFO_TYPE_UINT, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2UINT_OVF) CORINFO_TYPE_UINT, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "Runtime\MathHelpers.cpp".
        { FUNC(CORINFO_HELP_DBL2ULNG) CORINFO_TYPE_ULONG, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
        { FUNC(CORINFO_HELP_DBL2ULNG_OVF) CORINFO_TYPE_ULONG, { CORINFO_TYPE_DOUBLE }, HFIF_SS_ARG },

        // Implemented as "fmodf"/"fmod".
        { FUNC(CORINFO_HELP_FLTREM) CORINFO_TYPE_FLOAT, { CORINFO_TYPE_FLOAT, CORINFO_TYPE_FLOAT }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_DBLREM) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_DOUBLE, CORINFO_TYPE_DOUBLE }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_FLTROUND) },
        { FUNC(CORINFO_HELP_DBLROUND) },

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
        { FUNC(CORINFO_HELP_NEW_MDARR) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT, CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_NEW_MDARR_RARE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT, CORINFO_TYPE_PTR }, HFIF_SS_ARG },

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
        { FUNC(CORINFO_HELP_LDELEMA_REF) CORINFO_TYPE_BYREF, { CORINFO_TYPE_CLASS, CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // Runtime exports implemented in "Runtime.Base\src\System\Runtime\ExceptionHandling.wasm.cs".
        { FUNC(CORINFO_HELP_THROW) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_RETHROW) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // Implemented in "Runtime\MiscHelpers.cpp".
        { FUNC(CORINFO_HELP_USER_BREAKPOINT) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG},

        // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
        { FUNC(CORINFO_HELP_RNGCHKFAIL) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_OVERFLOW) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWDIVZERO) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWNULLREF) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWMISALIGN) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },

        // Verification is in the process of being deleted from RyuJit.
        { FUNC(CORINFO_HELP_VERIFICATION) },

        // Implemented in "Runtime\EHHelpers.cpp".
        { FUNC(CORINFO_HELP_FAIL_FAST) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG | HFIF_NO_RPI_OR_GC },

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
        { FUNC(CORINFO_HELP_POLL_GC) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG},

        // Debug-only helpers, implemented in "Runtime\wasm\GcStress.cpp".
        { FUNC(CORINFO_HELP_STRESS_GC) CORINFO_TYPE_BYREF, { CORINFO_TYPE_BYREF, CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_CHECK_OBJ) CORINFO_TYPE_CLASS, { CORINFO_TYPE_CLASS }, HFIF_NO_RPI_OR_GC },

        // Write barriers, implemented in "Runtime\portable.cpp".
        { FUNC(CORINFO_HELP_ASSIGN_REF) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_ASSIGN_REF_ENSURE_NONHEAP) }, // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_ASSIGN_BYREF) }, // Not used on WASM.
        { FUNC(CORINFO_HELP_BULK_WRITEBARRIER) },

        // Not used in NativeAOT (or at all in some cases).
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

        // Runtime exports implemented in "src/libraries/System.Private.CoreLib/src/System/SpanHelpers.ByteMemOps.cs".
        { FUNC(CORINFO_HELP_MEMSET) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_BYTE, CORINFO_TYPE_NATIVEUINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MEMZERO) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_NATIVEUINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MEMCPY) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR, CORINFO_TYPE_NATIVEUINT }, HFIF_SS_ARG },

        // Implemented as plain "memset".
        { FUNC(CORINFO_HELP_NATIVE_MEMSET) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT, CORINFO_TYPE_NATIVEUINT }, HFIF_NO_RPI_OR_GC },

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
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

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
        { FUNC(CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR) CORINFO_TYPE_PTR, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE) CORINFO_TYPE_PTR, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR) CORINFO_TYPE_PTR, { CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
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
        { FUNC(CORINFO_HELP_JIT_PINVOKE_BEGIN) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_SS_ARG | HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_JIT_PINVOKE_END) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS) },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS) },

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
        { FUNC(CORINFO_HELP_VALUEPROFILE32) },
        { FUNC(CORINFO_HELP_VALUEPROFILE64) },
        { FUNC(CORINFO_HELP_PARTIAL_COMPILATION_PATCHPOINT) },
        { FUNC(CORINFO_HELP_VALIDATE_INDIRECT_CALL) },
        { FUNC(CORINFO_HELP_DISPATCH_INDIRECT_CALL) },

        { FUNC(CORINFO_HELP_LLVM_GET_OR_INIT_SHADOW_STACK_TOP) CORINFO_TYPE_PTR, { }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_LLVM_EH_CATCH) CORINFO_TYPE_CLASS, { CORINFO_TYPE_NATIVEUINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LLVM_EH_POP_UNWOUND_VIRTUAL_FRAMES) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LLVM_EH_PUSH_VIRTUAL_UNWIND_FRAME) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR, CORINFO_TYPE_NATIVEUINT }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_LLVM_EH_POP_VIRTUAL_UNWIND_FRAME) CORINFO_TYPE_VOID, { }, HFIF_NO_RPI_OR_GC },
        { FUNC(CORINFO_HELP_LLVM_EH_UNHANDLED_EXCEPTION) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LLVM_RESOLVE_INTERFACE_CALL_TARGET) CORINFO_TYPE_PTR, { CORINFO_TYPE_CLASS, CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_LLVM_GET_EXTERNAL_CALL_TARGET) CORINFO_TYPE_PTR, { }, HFIF_NO_RPI_OR_GC },
    };
    // clang-format on

    // Make sure our array is up-to-date.
    static_assert_no_msg(ArrLen(s_infos) == CORINFO_HELP_COUNT);

#ifdef DEBUG
    static bool s_infosVerified = false;
    if (!s_infosVerified)
    {
        for (const HelperFuncInfo& info : s_infos)
        {
            if (info.IsInitialized())
            {
                unsigned flags = info.Flags;

                // Only helpers that will never call managed code are allowed to not have the shadow stack argument.
                assert(((flags & HFIF_SS_ARG) != 0) || ((flags & HFIF_NO_RPI_OR_GC) != 0));
            }
        }

        s_infosVerified = true;
    }
#endif // DEBUG

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
    // In essence, structs are passed by reference except if they are trivial wrappers of a primitive (scalar).
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
    return reinterpret_cast<TReturn (*)(TArgs...)>(g_callbacks[static_cast<int>(Func)])(args...);
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

void Llvm::GetJitTestInfo(CorInfoLlvmJitTestKind kind, CORINFO_LLVM_JIT_TEST_INFO* pInfo)
{
    CallEEApi<EEApiId::GetJitTestInfo, CORINFO_GENERIC_HANDLE>(m_pEECorInfo, kind, pInfo);
}

static void RegisterLlvmInteropExports(void** jitExports);

extern "C" DLLEXPORT int registerLlvmCallbacks(void** jitImports, void** jitExports)
{
    assert((jitImports != nullptr) && (jitImports[static_cast<int>(EEApiId::Count)] == (void*)0x1234));
    assert(jitExports != nullptr);

    memcpy(g_callbacks, jitImports, static_cast<int>(EEApiId::Count) * sizeof(void*));

    RegisterLlvmInteropExports(jitExports);
    jitExports[CJAI_StartSingleThreadedCompilation] = (void*)&Llvm::StartSingleThreadedCompilation;
    jitExports[CJAI_FinishSingleThreadedCompilation] = (void*)&Llvm::FinishSingleThreadedCompilation;
    jitExports[CJAI_Count] = (void*)0x1234;

    for (int i = 0; i < CJAI_Count; i++)
    {
        assert(jitExports[i] != nullptr);
    }

    return 1;
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

#include "LLVMInterop.Shared.cspp"

static LLVMContext* LLVMInterop_LLVMContext_Create()
{
    return new LLVMContext();
}

static Module* LLVMInterop_LLVMModule_Create(
    LLVMContext* context, char* name, size_t nameLength, char* target, size_t targetLength, char* dataLayout, size_t dataLayoutLength)
{
    Module* module = new Module({name, nameLength}, *context);
    module->setTargetTriple({target, targetLength});
    module->setDataLayout({dataLayout, dataLayoutLength});
    return module;
}

static llvm::GlobalAlias* LLVMInterop_LLVMModule_GetNamedAlias(Module* module, char* name, size_t nameLength)
{
    return module->getNamedAlias({name, nameLength});
}

static Function* LLVMInterop_LLVMModule_GetNamedFunction(Module* module, char* name, size_t nameLength)
{
    return module->getFunction({name, nameLength});
}

static llvm::GlobalVariable* LLVMInterop_LLVMModule_GetNamedGlobal(Module* module, char* name, size_t nameLength)
{
    return module->getNamedGlobal({name, nameLength});
}

static llvm::GlobalAlias* LLVMInterop_LLVMModule_AddAlias(Module* module, char* name, size_t nameLength, Type* valueType, Value* aliasee)
{
    return llvm::GlobalAlias::create(
        valueType, 0, llvm::GlobalValue::ExternalLinkage, StringRef(name, nameLength), llvm::cast<llvm::Constant>(aliasee), module);
}

static Function* LLVMInterop_LLVMModule_AddFunction(Module* module, char* name, size_t nameLength, Type* type)
{
    return Function::Create(llvm::cast<FunctionType>(type), llvm::GlobalValue::ExternalLinkage, StringRef(name, nameLength), *module);
}

static llvm::GlobalVariable* LLVMInterop_LLVMModule_AddGlobal(Module* module, char* name, size_t nameLength, Type* type, Value* initializer)
{
    llvm::Constant* init = llvm::cast_or_null<llvm::Constant>(initializer);
    return new llvm::GlobalVariable(*module, type, false, llvm::GlobalValue::ExternalLinkage, init, StringRef(name, nameLength));
}

static void LLVMInterop_LLVMModule_Verify(Module* module)
{
    if (llvm::verifyModule(*module, &llvm::errs()))
    {
        abort();
    }
}

static void LLVMInterop_LLVMModule_PrintToFile(Module* module, char* path, size_t pathLength)
{
    std::error_code code{};
    llvm::raw_fd_ostream output({path, pathLength}, code);
    module->print(output, nullptr);
}

static void LLVMInterop_LLVMModule_WriteBitcodeToFile(Module* module, char* path, size_t pathLength)
{
    std::error_code code{};
    llvm::raw_fd_ostream output({path, pathLength}, code);
    llvm::WriteBitcodeToFile(*module, output);
}

static LLVMContext* LLVMInterop_LLVMType_GetContext(Type* type)
{
    return &type->getContext();
}

static Type* LLVMInterop_LLVMType_GetReturnType(Type* type)
{
    return llvm::cast<FunctionType>(type)->getReturnType();
}

static Type* const* LLVMInterop_LLVMType_GetParamTypes(Type* type, size_t* pCount)
{
    ArrayRef<Type*> types = llvm::cast<FunctionType>(type)->params();
    *pCount = types.size();
    return types.data();
}

static Type* LLVMInterop_LLVMType_GetPointer(LLVMContext* context)
{
    return llvm::PointerType::getUnqual(*context);
}

static Type* LLVMInterop_LLVMType_GetInt(LLVMContext* context, int bitCount)
{
    return Type::getIntNTy(*context, bitCount);
}

static Type* LLVMInterop_LLVMType_GetFloat(LLVMContext* context)
{
    return Type::getFloatTy(*context);
}

static Type* LLVMInterop_LLVMType_GetDouble(LLVMContext* context)
{
    return Type::getDoubleTy(*context);
}

static Type* LLVMInterop_LLVMType_GetVoid(LLVMContext* context)
{
    return Type::getVoidTy(*context);
}

static FunctionType* LLVMInterop_LLVMType_CreateFunction(Type* result, Type** parameters, size_t paramCount)
{
    return FunctionType::get(result, {parameters, paramCount}, /* isVarArg */ false);
}

static llvm::StructType* LLVMInterop_LLVMType_CreateStruct(LLVMContext* context, Type** elements, size_t elementCount, int packed)
{
    return llvm::StructType::get(*context, {elements, elementCount}, packed);
}

static llvm::ArrayType* LLVMInterop_LLVMType_CreateArray(Type* elementType, uint64_t elementCount)
{
    return llvm::ArrayType::get(elementType, elementCount);
}

static Type* LLVMInterop_LLVMValue_TypeOf(Value* value)
{
    return value->getType();
}

static llvm::BasicBlock* LLVMInterop_LLVMValue_AppendBasicBlock(Value* func, char* name, size_t nameLength)
{
    return llvm::BasicBlock::Create(func->getContext(), StringRef{name, nameLength}, llvm::cast<Function>(func));
}

static void LLVMInterop_LLVMValue_AddAttributeAtIndex(Value* value, LLVMAttributeIndex index, void* attribute)
{
    switch (index)
    {
        case LLVMAttributeFunctionIndex:
            llvm::cast<Function>(value)->addFnAttr(llvm::Attribute::fromRawPointer(attribute));
            break;
        default:
            abort();
    }
}

static Value* LLVMInterop_LLVMValue_GetParam(Value* func, unsigned index)
{
    return llvm::cast<Function>(func)->getArg(index);
}

static int LLVMInterop_LLVMValue_GetParamCount(Value* func)
{
    return llvm::cast<Function>(func)->getFunctionType()->getNumParams();
}

static Type* LLVMInterop_LLVMValue_GetValueType(Value* value)
{
    return llvm::cast<llvm::GlobalValue>(value)->getValueType();
}

static void LLVMInterop_LLVMValue_SetAlignment(Value* value, uint64_t alignment)
{
    llvm::cast<llvm::GlobalObject>(value)->setAlignment(llvm::Align(alignment));
}

static void LLVMInterop_LLVMValue_SetSection(Value* value, char* name, size_t nameLength)
{
    llvm::cast<llvm::GlobalObject>(value)->setSection({name, nameLength});
}

static void LLVMInterop_LLVMValue_SetLinkage(Value* value, LLVMLinkage linkage)
{
    llvm::GlobalObject* obj = llvm::cast<llvm::GlobalObject>(value);
    switch (linkage)
    {
        case LLVMAppendingLinkage:
            obj->setLinkage(llvm::GlobalValue::AppendingLinkage);
            break;
        default:
            abort();
    }
}

static void LLVMInterop_LLVMValue_SetAliasee(Value* alias, Value* aliasee)
{
    llvm::cast<llvm::GlobalAlias>(alias)->setAliasee(llvm::cast<llvm::Constant>(aliasee));
}

static Value* LLVMInterop_LLVMValue_CreateConstNull(Type* type)
{
    return llvm::Constant::getNullValue(type);
}

static Value* LLVMInterop_LLVMValue_CreateConstInt(Type* type, uint64_t value)
{
    return llvm::ConstantInt::get(type, value);
}

static Value* LLVMInterop_LLVMValue_CreateConstIntToPtr(Value* value)
{
    Type* type = llvm::PointerType::getUnqual(value->getContext());
    return llvm::ConstantExpr::getIntToPtr(llvm::cast<llvm::Constant>(value), type);
}

static Value* LLVMInterop_LLVMValue_CreateConstGEP(Value* address, int offset)
{
    LLVMContext& context = address->getContext();
    Value* offsetValue = llvm::ConstantInt::get(Type::getInt32Ty(context), offset);
    return llvm::ConstantExpr::getGetElementPtr(Type::getInt8Ty(context), llvm::cast<llvm::Constant>(address), offsetValue);
}

static Value* LLVMInterop_LLVMValue_CreateConstStruct(Type* type, llvm::Constant** elements, size_t elementCount)
{
    return llvm::ConstantStruct::get(llvm::cast<llvm::StructType>(type), {elements, elementCount});
}

static Value* LLVMInterop_LLVMValue_CreateConstArray(Type* type, llvm::Constant** elements, size_t elementCount)
{
    return llvm::ConstantArray::get(llvm::cast<llvm::ArrayType>(type), {elements, elementCount});
}

static Function* LLVMInterop_LLVMBasicBlock_GetParent(llvm::BasicBlock* block)
{
    return block->getParent();
}

static void LLVMInterop_LLVMBasicBlock_MoveAfter(llvm::BasicBlock* block, llvm::BasicBlock* moveAfterBlock)
{
    block->moveAfter(moveAfterBlock);
}

static llvm::IRBuilder<>* LLVMInterop_LLVMBuilder_Create(LLVMContext* context)
{
    return new llvm::IRBuilder<>(*context);
}

static llvm::BasicBlock* LLVMInterop_LLVMBuilder_GetInsertBlock(llvm::IRBuilder<>* builder)
{
    return builder->GetInsertBlock();
}

static Value* LLVMInterop_LLVMBuilder_BuildICmp(llvm::IRBuilder<>* builder, LLVMIntPredicate predicate, Value* left, Value* right, char* name, size_t nameLength)
{
    llvm::ICmpInst::Predicate llvmPredicate;
    switch (predicate)
    {
        case LLVMIntEQ:
            llvmPredicate = llvm::ICmpInst::ICMP_EQ;
            break;
        default:
            abort();
    }
    return builder->CreateICmp(llvmPredicate, left, right, StringRef(name, nameLength));
}

static Value* LLVMInterop_LLVMBuilder_BuildCondBr(llvm::IRBuilder<>* builder, Value* cond, llvm::BasicBlock* trueDest, llvm::BasicBlock* falseDest)
{
    return builder->CreateCondBr(cond, trueDest, falseDest);
}

static Value* LLVMInterop_LLVMBuilder_BuildBr(llvm::IRBuilder<>* builder, llvm::BasicBlock* dest)
{
    return builder->CreateBr(dest);
}

static Value* LLVMInterop_LLVMBuilder_BuildGEP(llvm::IRBuilder<>* builder, Value* address, Value* offset, char* name, size_t nameLength)
{
    return builder->CreateGEP(Type::getInt8Ty(builder->getContext()), address, offset, StringRef(name, nameLength));
}

static Value* LLVMInterop_LLVMBuilder_BuildPtrToInt(llvm::IRBuilder<>* builder, Value* value, Type* type, char* name, size_t nameLength)
{
    return builder->CreatePtrToInt(value, type, StringRef(name, nameLength));
}

static Value* LLVMInterop_LLVMBuilder_BuildIntToPtr(llvm::IRBuilder<>* builder, Value* value, char* name, size_t nameLength)
{
    return builder->CreateIntToPtr(value, llvm::PointerType::get(builder->getContext(), 0), StringRef(name, nameLength));
}

static Value* LLVMInterop_LLVMBuilder_BuildPointerCast(llvm::IRBuilder<>* builder, Value* value, Type* type, char* name, size_t nameLength)
{
    return builder->CreatePointerCast(value, type, StringRef(name, nameLength));
}

static Value* LLVMInterop_LLVMBuilder_BuildCall(llvm::IRBuilder<>* builder, Type* funcType, Value* callee, Value** args, size_t argCount, char* name, size_t nameLength)
{
    return builder->CreateCall(llvm::cast<FunctionType>(funcType), callee, {args, argCount}, StringRef(name, nameLength));
}

static Value* LLVMInterop_LLVMBuilder_BuildLoad(llvm::IRBuilder<>* builder, Type* type, Value* address, char* name, size_t nameLength)
{
    return builder->CreateLoad(type, address, StringRef(name, nameLength));
}

static Value* LLVMInterop_LLVMBuilder_BuildRet(llvm::IRBuilder<>* builder, Value* result)
{
    if (result == nullptr)
    {
        return builder->CreateRetVoid();
    }

    return builder->CreateRet(result);
}

static Value* LLVMInterop_LLVMBuilder_BuildUnreachable(llvm::IRBuilder<>* builder)
{
    return builder->CreateUnreachable();
}

static void LLVMInterop_LLVMBuilder_PositionAtEnd(llvm::IRBuilder<>* builder, llvm::BasicBlock* block)
{
    builder->SetInsertPoint(block);
}

static void LLVMInterop_LLVMBuilder_Dispose(llvm::IRBuilder<>* builder)
{
    delete builder;
}

static void* LLVMInterop_LLVMAttribute_Create(LLVMContext* context, char* name, size_t nameLength, char* value, size_t valueLength)
{
    return llvm::Attribute::get(*context, StringRef(name, nameLength), StringRef(value, valueLength)).getRawPointer();
}

static void RegisterLlvmInteropExports(void** jitExports)
{
    jitExports[CJAI_LLVMInterop_LLVMContext_Create] = (void*)&LLVMInterop_LLVMContext_Create;
    jitExports[CJAI_LLVMInterop_LLVMModule_Create] = (void*)&LLVMInterop_LLVMModule_Create;
    jitExports[CJAI_LLVMInterop_LLVMModule_GetNamedAlias] = (void*)&LLVMInterop_LLVMModule_GetNamedAlias;
    jitExports[CJAI_LLVMInterop_LLVMModule_GetNamedFunction] = (void*)&LLVMInterop_LLVMModule_GetNamedFunction;
    jitExports[CJAI_LLVMInterop_LLVMModule_GetNamedGlobal] = (void*)&LLVMInterop_LLVMModule_GetNamedGlobal;
    jitExports[CJAI_LLVMInterop_LLVMModule_AddAlias] = (void*)&LLVMInterop_LLVMModule_AddAlias;
    jitExports[CJAI_LLVMInterop_LLVMModule_AddFunction] = (void*)&LLVMInterop_LLVMModule_AddFunction;
    jitExports[CJAI_LLVMInterop_LLVMModule_AddGlobal] = (void*)&LLVMInterop_LLVMModule_AddGlobal;
    jitExports[CJAI_LLVMInterop_LLVMModule_Verify] = (void*)&LLVMInterop_LLVMModule_Verify;
    jitExports[CJAI_LLVMInterop_LLVMModule_PrintToFile] = (void*)&LLVMInterop_LLVMModule_PrintToFile;
    jitExports[CJAI_LLVMInterop_LLVMModule_WriteBitcodeToFile] = (void*)&LLVMInterop_LLVMModule_WriteBitcodeToFile;
    jitExports[CJAI_LLVMInterop_LLVMType_GetContext] = (void*)&LLVMInterop_LLVMType_GetContext;
    jitExports[CJAI_LLVMInterop_LLVMType_GetReturnType] = (void*)&LLVMInterop_LLVMType_GetReturnType;
    jitExports[CJAI_LLVMInterop_LLVMType_GetParamTypes] = (void*)&LLVMInterop_LLVMType_GetParamTypes;
    jitExports[CJAI_LLVMInterop_LLVMType_GetPointer] = (void*)&LLVMInterop_LLVMType_GetPointer;
    jitExports[CJAI_LLVMInterop_LLVMType_GetInt] = (void*)&LLVMInterop_LLVMType_GetInt;
    jitExports[CJAI_LLVMInterop_LLVMType_GetFloat] = (void*)&LLVMInterop_LLVMType_GetFloat;
    jitExports[CJAI_LLVMInterop_LLVMType_GetDouble] = (void*)&LLVMInterop_LLVMType_GetDouble;
    jitExports[CJAI_LLVMInterop_LLVMType_GetVoid] = (void*)&LLVMInterop_LLVMType_GetVoid;
    jitExports[CJAI_LLVMInterop_LLVMType_CreateFunction] = (void*)&LLVMInterop_LLVMType_CreateFunction;
    jitExports[CJAI_LLVMInterop_LLVMType_CreateStruct] = (void*)&LLVMInterop_LLVMType_CreateStruct;
    jitExports[CJAI_LLVMInterop_LLVMType_CreateArray] = (void*)&LLVMInterop_LLVMType_CreateArray;
    jitExports[CJAI_LLVMInterop_LLVMValue_TypeOf] = (void*)&LLVMInterop_LLVMValue_TypeOf;
    jitExports[CJAI_LLVMInterop_LLVMValue_AppendBasicBlock] = (void*)&LLVMInterop_LLVMValue_AppendBasicBlock;
    jitExports[CJAI_LLVMInterop_LLVMValue_AddAttributeAtIndex] = (void*)&LLVMInterop_LLVMValue_AddAttributeAtIndex;
    jitExports[CJAI_LLVMInterop_LLVMValue_GetParam] = (void*)&LLVMInterop_LLVMValue_GetParam;
    jitExports[CJAI_LLVMInterop_LLVMValue_GetParamCount] = (void*)&LLVMInterop_LLVMValue_GetParamCount;
    jitExports[CJAI_LLVMInterop_LLVMValue_GetValueType] = (void*)&LLVMInterop_LLVMValue_GetValueType;
    jitExports[CJAI_LLVMInterop_LLVMValue_SetAlignment] = (void*)&LLVMInterop_LLVMValue_SetAlignment;
    jitExports[CJAI_LLVMInterop_LLVMValue_SetSection] = (void*)&LLVMInterop_LLVMValue_SetSection;
    jitExports[CJAI_LLVMInterop_LLVMValue_SetLinkage] = (void*)&LLVMInterop_LLVMValue_SetLinkage;
    jitExports[CJAI_LLVMInterop_LLVMValue_SetAliasee] = (void*)&LLVMInterop_LLVMValue_SetAliasee;
    jitExports[CJAI_LLVMInterop_LLVMValue_CreateConstNull] = (void*)&LLVMInterop_LLVMValue_CreateConstNull;
    jitExports[CJAI_LLVMInterop_LLVMValue_CreateConstInt] = (void*)&LLVMInterop_LLVMValue_CreateConstInt;
    jitExports[CJAI_LLVMInterop_LLVMValue_CreateConstIntToPtr] = (void*)&LLVMInterop_LLVMValue_CreateConstIntToPtr;
    jitExports[CJAI_LLVMInterop_LLVMValue_CreateConstGEP] = (void*)&LLVMInterop_LLVMValue_CreateConstGEP;
    jitExports[CJAI_LLVMInterop_LLVMValue_CreateConstStruct] = (void*)&LLVMInterop_LLVMValue_CreateConstStruct;
    jitExports[CJAI_LLVMInterop_LLVMValue_CreateConstArray] = (void*)&LLVMInterop_LLVMValue_CreateConstArray;
    jitExports[CJAI_LLVMInterop_LLVMBasicBlock_GetParent] = (void*)&LLVMInterop_LLVMBasicBlock_GetParent;
    jitExports[CJAI_LLVMInterop_LLVMBasicBlock_MoveAfter] = (void*)&LLVMInterop_LLVMBasicBlock_MoveAfter;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_Create] = (void*)&LLVMInterop_LLVMBuilder_Create;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_GetInsertBlock] = (void*)&LLVMInterop_LLVMBuilder_GetInsertBlock;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildICmp] = (void*)&LLVMInterop_LLVMBuilder_BuildICmp;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildCondBr] = (void*)&LLVMInterop_LLVMBuilder_BuildCondBr;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildBr] = (void*)&LLVMInterop_LLVMBuilder_BuildBr;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildGEP] = (void*)&LLVMInterop_LLVMBuilder_BuildGEP;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildPtrToInt] = (void*)&LLVMInterop_LLVMBuilder_BuildPtrToInt;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildIntToPtr] = (void*)&LLVMInterop_LLVMBuilder_BuildIntToPtr;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildPointerCast] = (void*)&LLVMInterop_LLVMBuilder_BuildPointerCast;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildCall] = (void*)&LLVMInterop_LLVMBuilder_BuildCall;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildLoad] = (void*)&LLVMInterop_LLVMBuilder_BuildLoad;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildRet] = (void*)&LLVMInterop_LLVMBuilder_BuildRet;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_BuildUnreachable] = (void*)&LLVMInterop_LLVMBuilder_BuildUnreachable;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_PositionAtEnd] = (void*)&LLVMInterop_LLVMBuilder_PositionAtEnd;
    jitExports[CJAI_LLVMInterop_LLVMBuilder_Dispose] = (void*)&LLVMInterop_LLVMBuilder_Dispose;
    jitExports[CJAI_LLVMInterop_LLVMAttribute_Create] = (void*)&LLVMInterop_LLVMAttribute_Create;
}
