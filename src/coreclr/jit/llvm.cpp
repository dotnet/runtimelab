// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "jitpch.h"
#include "llvm.h"

#pragma warning (disable: 4459)
#include "llvm/Bitcode/BitcodeWriter.h"
#pragma warning (error: 4459)

LLVMContext      _llvmContext;
Module*          _module            = nullptr;
llvm::DIBuilder* _diBuilder         = nullptr;
char*            _outputFileName;
Function*        _doNothingFunction;

std::unordered_map<CORINFO_CLASS_HANDLE, Type*>* _llvmStructs = new std::unordered_map<CORINFO_CLASS_HANDLE, Type*>();
std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>* _structDescMap = new std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>();

// Must be kept in sync with the managed version in "CorInfoImpl.Llvm.cs".
//
enum class EEApiId
{
    GetMangledMethodName,
    GetSymbolMangledName,
    GetTypeName,
    AddCodeReloc,
    IsRuntimeImport,
    GetDocumentFileName,
    FirstSequencePointLineNumber,
    GetOffsetLineNumber,
    StructIsWrappedPrimitive,
    PadOffset,
    GetArgTypeIncludingParameterized,
    GetParameterType,
    GetTypeDescriptor,
    GetInstanceFieldAlignment,
    Count
};

void* _thisPtr; // TODO: workaround for not changing the JIT/EE interface.  As this is static, it will probably fail if multithreaded compilation is attempted
void* g_callbacks[static_cast<int>(EEApiId::Count)];

extern "C" DLLEXPORT void registerLlvmCallbacks(void*       thisPtr,
                                                const char* outputFileName,
                                                const char* triple,
                                                const char* dataLayout,
                                                void**      callbacks)
{
    assert((callbacks != nullptr) && (callbacks[static_cast<int>(EEApiId::Count)] == (void*)0x1234));

    _thisPtr = thisPtr;
    memcpy(g_callbacks, callbacks, static_cast<int>(EEApiId::Count) * sizeof(void*));

    if (_module == nullptr) // registerLlvmCallbacks is called for each method to compile, but must only created the module once.  Better perhaps to split this into 2 calls.
    {
        _module = new Module(llvm::StringRef("netscripten-clrjit"), _llvmContext);
        _module->setTargetTriple(triple);
        _module->setDataLayout(dataLayout);
        _outputFileName = (char*)malloc(strlen(outputFileName) + 7);
        strcpy(_outputFileName, "1.txt"); // ??? without this _outputFileName is corrupted
        strcpy(_outputFileName, outputFileName);
        strcpy(_outputFileName + strlen(_outputFileName) - 3, "clrjit"); // use different module output name for now, TODO: delete if old LLVM gen does not create a module
        strcat(_outputFileName, ".bc");
    }
}

CorInfoType HelperFuncInfo::GetSigReturnType() const
{
    return static_cast<CorInfoType>(SigReturnType);
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

size_t HelperFuncInfo::GetSigArgCount() const
{
    size_t count = 0;
    while (SigArgTypes[count] != CORINFO_TYPE_UNDEF)
    {
        count++;
    }

    return count;
}

Llvm::Llvm(Compiler* compiler)
    : _compiler(compiler),
    _info(compiler->info),
    _sigInfo(compiler->info.compMethodInfo->args),
    _builder(_llvmContext),
    _prologBuilder(_llvmContext),
    _blkToLlvmBlksMap(compiler->getAllocator(CMK_Codegen)),
    _sdsuMap(compiler->getAllocator(CMK_Codegen)),
    _localsMap(compiler->getAllocator(CMK_Codegen)),
    _debugMetadataMap(compiler->getAllocator(CMK_Codegen)),
    _shadowStackLclNum(BAD_VAR_NUM),
    _retAddressLclNum(BAD_VAR_NUM)
{
}

void Llvm::llvmShutdown()
{
    if (_diBuilder != nullptr)
    {
        _module->addModuleFlag(llvm::Module::Warning, "Dwarf Version", 4);
        _module->addModuleFlag(llvm::Module::Warning, "Debug Info Version", 3);
        _diBuilder->finalize();
    }

    std::error_code ec;

    if (_outputFileName == nullptr)
    {
        return; // Nothing generated.
    }

    // TODO-LLVM: when the release build is more stable, put under #ifdef DEBUG.
    // For now the text output is useful for debugging
    char* txtFileName = (char*)malloc(strlen(_outputFileName) + 2); // .txt is longer than .bc
    strcpy(txtFileName, _outputFileName);
    strcpy(txtFileName + strlen(_outputFileName) - 2, "txt");
    llvm::raw_fd_ostream textOutputStream(txtFileName, ec);
    _module->print(textOutputStream, (llvm::AssemblyAnnotationWriter*)NULL);
    free(txtFileName);

    // verifyModule returns true when its broken, so invert.
    assert(!llvm::verifyModule(*_module, &llvm::errs()));

    llvm::raw_fd_ostream OS(_outputFileName, ec);
    llvm::WriteBitcodeToFile(*_module, OS);

    for (const auto &structDesc : *_structDescMap)
    {
        delete structDesc.second;
    }

    delete _module;
}

bool Llvm::needsReturnStackSlot(Compiler* compiler, GenTreeCall* callee)
{
    // Currently, we do not place the return value on the shadow stack for helpers
    // (e. g. allocators). This is a potential GC hole. TODO-LLVM: investigate.
    if (callee->IsHelperCall())
    {
        return false;
    }

    return Llvm::needsReturnStackSlot(compiler, toCorInfoType(callee->TypeGet()), callee->gtRetClsHnd);
}

GCInfo* Llvm::getGCInfo()
{
    if (_gcInfo == nullptr)
    {
        _gcInfo = new (_compiler->getAllocator(CMK_GC)) GCInfo(_compiler);
    }
    return _gcInfo;
}

CORINFO_CLASS_HANDLE Llvm::tryGetStructClassHandle(LclVarDsc* varDsc)
{
    return varTypeIsStruct(varDsc) ? varDsc->GetStructHnd() : NO_CLASS_HANDLE;
}

CorInfoType Llvm::getCorInfoTypeForArg(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE& arg, CORINFO_CLASS_HANDLE* clsHnd)
{
    CorInfoTypeWithMod corTypeWithMod = GetArgTypeIncludingParameterized(sigInfo, arg, clsHnd);
    return strip(corTypeWithMod);
}

// When looking at a sigInfo from eeGetMethodSig we have CorInfoType(s) but when looking at lclVars we have LclVarDsc or var_type(s),
// This method exists to allow both to map to LLVM types.
/* static */ CorInfoType Llvm::toCorInfoType(var_types varType)
{
    switch (varType)
    {
        case TYP_BOOL:
            return CORINFO_TYPE_BOOL;
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

// Returns true if the method returns a type that must be kept on the shadow stack
//
bool Llvm::needsReturnStackSlot(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    return corInfoType != CorInfoType::CORINFO_TYPE_VOID && !canStoreArgOnLlvmStack(compiler, corInfoType, classHnd);
}

bool Llvm::callHasShadowStackArg(GenTreeCall* call)
{
    if (call->IsHelperCall())
    {
        return getHelperFuncInfo(_compiler->eeGetHelperNum(call->gtCallMethHnd)).HasFlags(HFIF_SS_ARG);
    }

    // TODO-LLVM: this is not right for native calls.
    return true;
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
const HelperFuncInfo& Llvm::getHelperFuncInfo(CorInfoHelpFunc helperFunc)
{
    // Note on Runtime[Type|Method|Field]Handle: it should faithfully be represented as CORINFO_TYPE_VALUECLASS.
    // However, that is currently both not necessary due to the unwrapping performed for LLVM types and not what
    // the Jit expects. When deleting the unwrapping, fix the runtime signatures to take the underlying pointer instead.
    const int CORINFO_TYPE_RT_HANDLE = CORINFO_TYPE_PTR;
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
        { FUNC(CORINFO_HELP_FLTREM) CORINFO_TYPE_FLOAT, { CORINFO_TYPE_FLOAT } },
        { FUNC(CORINFO_HELP_DBLREM) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_DOUBLE } },
        { FUNC(CORINFO_HELP_FLTROUND) CORINFO_TYPE_FLOAT, { CORINFO_TYPE_FLOAT } },
        { FUNC(CORINFO_HELP_DBLROUND) CORINFO_TYPE_DOUBLE, { CORINFO_TYPE_DOUBLE } },

        // Runtime export, implemented in "Runtime.Base\src\System\Runtime\RuntimeExports.cs".
        { FUNC(CORINFO_HELP_NEWFAST) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR } },

        // Implemented in "Runtime\portable.cpp".
        { FUNC(CORINFO_HELP_NEWSFAST) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_NEWSFAST_FINALIZE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_NEWSFAST_ALIGN8) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_NEWSFAST_ALIGN8_VC) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR } },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\ArrayHelpers.cs".
        { FUNC(CORINFO_HELP_NEW_MDARR) CORINFO_TYPE_CLASS, { CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_INT, CORINFO_TYPE_PTR } }, // Oddity: IntPtr used for MethodTable*.

        // Runtime export, implemented in "Runtime.Base\src\System\Runtime\RuntimeExports.cs".
        { FUNC(CORINFO_HELP_NEWARR_1_DIRECT) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT } },

        // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_NEWARR_1_OBJ) },

        // Implemented in "Runtime\portable.cpp".
        { FUNC(CORINFO_HELP_NEWARR_1_VC) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT } },
        { FUNC(CORINFO_HELP_NEWARR_1_ALIGN8) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_INT } },

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_STRCNS) },
        { FUNC(CORINFO_HELP_STRCNS_CURRENT_MODULE) },
        { FUNC(CORINFO_HELP_INITCLASS) },
        { FUNC(CORINFO_HELP_INITINSTCLASS) },

        // Runtime exports (i. e. implemented in managed code with an unmanaged signature) from
        // "Runtime.Base\src\System\Runtime\TypeCast.cs" and "Runtime.Base\src\System\Runtime\RuntimeExports.cs".
        { FUNC(CORINFO_HELP_ISINSTANCEOFINTERFACE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_ISINSTANCEOFARRAY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_ISINSTANCEOFCLASS) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_ISINSTANCEOFANY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_CHKCASTINTERFACE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_CHKCASTARRAY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_CHKCASTCLASS) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_CHKCASTANY) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_CHKCASTCLASS_SPECIAL) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_BOX) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_BYREF } },
        { FUNC(CORINFO_HELP_BOX_NULLABLE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_PTR, CORINFO_TYPE_BYREF } },
        { FUNC(CORINFO_HELP_UNBOX) CORINFO_TYPE_BYREF, { CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_UNBOX_NULLABLE) CORINFO_TYPE_VOID, { CORINFO_TYPE_BYREF, CORINFO_TYPE_PTR, CORINFO_TYPE_CLASS } },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
        { FUNC(CORINFO_HELP_GETREFANY) CORINFO_TYPE_BYREF, { CORINFO_TYPE_RT_HANDLE, CORINFO_TYPE_VALUECLASS}, HFIF_SS_ARG },

        // Implemented in "Runtime.Base\src\System\Runtime\TypeCast.cs".
        // Note for upstream merging: these helpers will start taking NATIVEINT for the second arg instead of plain INT.
        { FUNC(CORINFO_HELP_ARRADDR_ST) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS, CORINFO_TYPE_INT, CORINFO_TYPE_CLASS } },
        { FUNC(CORINFO_HELP_LDELEMA_REF) CORINFO_TYPE_BYREF, { CORINFO_TYPE_CLASS, CORINFO_TYPE_INT, CORINFO_TYPE_NATIVEINT } }, // Oddity: IntPtr used for MethodTable*.

        // For WASM, currently implemented in the bootstrapper...
        { FUNC(CORINFO_HELP_THROW) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },

        // (Not) implemented in "Runtime\EHHelpers.cpp"
        { FUNC(CORINFO_HELP_RETHROW) CORINFO_TYPE_VOID, { } },

        // Implemented in "Runtime\MiscHelpers.cpp".
        { FUNC(CORINFO_HELP_USER_BREAKPOINT) CORINFO_TYPE_VOID, { } },

        // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
        // Note on "CORINFO_HELP_THROWNULLREF": ***this helpers has been deleted upstream***.
        // We need it. When merging upstream, revert its deletion!
        { FUNC(CORINFO_HELP_RNGCHKFAIL) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_OVERFLOW) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWDIVZERO) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_THROWNULLREF) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },

        // Verification is in the process of being deleted from RyuJit.
        { FUNC(CORINFO_HELP_VERIFICATION) },

        // Implemented in "Runtime\EHHelpers.cpp".
        { FUNC(CORINFO_HELP_FAIL_FAST) CORINFO_TYPE_VOID, { } },

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_METHOD_ACCESS_EXCEPTION) },
        { FUNC(CORINFO_HELP_FIELD_ACCESS_EXCEPTION) },
        { FUNC(CORINFO_HELP_CLASS_ACCESS_EXCEPTION) },

        // Not used with funclet-based EH.
        { FUNC(CORINFO_HELP_ENDCATCH) },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\SynchronizedMethodHelpers.cs".
        { FUNC(CORINFO_HELP_MON_ENTER) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MON_EXIT) CORINFO_TYPE_VOID, { CORINFO_TYPE_CLASS, CORINFO_TYPE_BYREF }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_MON_ENTER_STATIC) CORINFO_TYPE_VOID, { CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_BYREF }, HFIF_SS_ARG }, // Oddity: IntPtr used for MethodTable*.
        { FUNC(CORINFO_HELP_MON_EXIT_STATIC) CORINFO_TYPE_VOID, { CORINFO_TYPE_NATIVEINT, CORINFO_TYPE_BYREF }, HFIF_SS_ARG }, // Oddity: IntPtr used for MethodTable*.

        // Apparently NYI in NativeAOT.
        { FUNC(CORINFO_HELP_GETCLASSFROMMETHODPARAM) },
        { FUNC(CORINFO_HELP_GETSYNCFROMCLASSHANDLE) },
        { FUNC(CORINFO_HELP_STOP_FOR_GC) },

        // (Not) implemented in "Runtime\portable.cpp".
        { FUNC(CORINFO_HELP_POLL_GC) CORINFO_TYPE_VOID, { } },

        // Debug-only helpers NYI in NativeAOT.
        { FUNC(CORINFO_HELP_STRESS_GC) },
        { FUNC(CORINFO_HELP_CHECK_OBJ) },

        // Write barriers, implemented in "Runtime\portable.cpp".
        { FUNC(CORINFO_HELP_ASSIGN_REF) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_CHECKED_ASSIGN_REF) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR } },
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

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_DBG_IS_JUST_MY_CODE) },
        { FUNC(CORINFO_HELP_PROF_FCN_ENTER) },
        { FUNC(CORINFO_HELP_PROF_FCN_LEAVE) },
        { FUNC(CORINFO_HELP_PROF_FCN_TAILCALL) },
        { FUNC(CORINFO_HELP_BBT_FCN_ENTER) },

        // TODO-LLVM: this is not a real "helper"; investigate what needs to be done to enable it.
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
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE) CORINFO_TYPE_CLASS, { CORINFO_TYPE_RT_HANDLE }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL) CORINFO_TYPE_CLASS, { CORINFO_TYPE_RT_HANDLE }, HFIF_SS_ARG },

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\LdTokenHelpers.cs".
        { FUNC(CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_NATIVEINT }, HFIF_SS_ARG }, // Oddity: IntPtr used for MethodTable*.

        // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
        { FUNC(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL) CORINFO_TYPE_VALUECLASS, { CORINFO_TYPE_RT_HANDLE }, HFIF_SS_ARG },

        // Another runtime export from "TypeCast.cs".
        { FUNC(CORINFO_HELP_ARE_TYPES_EQUIVALENT) CORINFO_TYPE_BOOL, { CORINFO_TYPE_PTR, CORINFO_TYPE_PTR } },

        // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_VIRTUAL_FUNC_PTR) },
        { FUNC(CORINFO_HELP_READYTORUN_NEW) },
        { FUNC(CORINFO_HELP_READYTORUN_NEWARR_1) },

        // NYI in NativeAOT.
        { FUNC(CORINFO_HELP_READYTORUN_ISINSTANCEOF) },
        { FUNC(CORINFO_HELP_READYTORUN_CHKCAST) },

        // Emitted by the compiler as intrinsics. (see "ILCompiler.LLVM\CodeGen\LLVMObjectWriter.cs", "GetCodeForReadyToRunGenericHelper").
        { FUNC(CORINFO_HELP_READYTORUN_STATIC_BASE) CORINFO_TYPE_PTR, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR) }, // Not used in NativeAOT.
        { FUNC(CORINFO_HELP_READYTORUN_GENERIC_HANDLE) CORINFO_TYPE_PTR, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_DELEGATE_CTOR) CORINFO_TYPE_VOID, { }, HFIF_SS_ARG },
        { FUNC(CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE) CORINFO_TYPE_PTR, { CORINFO_TYPE_PTR }, HFIF_SS_ARG },

        // NGEN/R2R-specific marker helpers.
        { FUNC(CORINFO_HELP_EE_PRESTUB) },
        { FUNC(CORINFO_HELP_EE_PRECODE_FIXUP) },
        { FUNC(CORINFO_HELP_EE_PINVOKE_FIXUP) },
        { FUNC(CORINFO_HELP_EE_VSD_FIXUP) },
        { FUNC(CORINFO_HELP_EE_EXTERNAL_FIXUP) },
        { FUNC(CORINFO_HELP_EE_VTABLE_FIXUP) },
        { FUNC(CORINFO_HELP_EE_REMOTING_THUNK) },
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

        // [R]PI helpers, implemented in "Runtime\thread.cpp".
        { FUNC(CORINFO_HELP_JIT_PINVOKE_BEGIN) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_JIT_PINVOKE_END) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },
        { FUNC(CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS) CORINFO_TYPE_VOID, { CORINFO_TYPE_PTR } },

        // Implemented in "CoreLib\src\System\Runtime\TypeLoaderExports.cs".
        { FUNC(CORINFO_HELP_GVMLOOKUP_FOR_SLOT) CORINFO_TYPE_NATIVEINT, { CORINFO_TYPE_CLASS, CORINFO_TYPE_RT_HANDLE }, HFIF_SS_ARG }, // Oddity: IntPtr used for a pointer.

        // Not used in NativeAOT (stack probing - not used for LLVM).
        { FUNC(CORINFO_HELP_STACK_PROBE) },
        { FUNC(CORINFO_HELP_PATCHPOINT) },
        { FUNC(CORINFO_HELP_CLASSPROFILE32) },
        { FUNC(CORINFO_HELP_CLASSPROFILE64) },
        { FUNC(CORINFO_HELP_PARTIAL_COMPILATION_PATCHPOINT) }
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

bool Llvm::canStoreArgOnLlvmStack(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    // structs with no GC pointers can go on LLVM stack.
    if (corInfoType == CORINFO_TYPE_VALUECLASS)
    {
        ClassLayout* classLayout = compiler->typGetObjLayout(classHnd);
        return !classLayout->HasGCPtr();
    }

    if (corInfoType == CORINFO_TYPE_BYREF || corInfoType == CORINFO_TYPE_CLASS || corInfoType == CORINFO_TYPE_REFANY)
    {
        return false;
    }

    return true;
}

static unsigned corInfoTypeAligment(CorInfoType corInfoType)
{
    unsigned size = TARGET_POINTER_SIZE; // TODO Wasm64 aligns pointers at 4 or 8?
    switch (corInfoType)
    {
        case CORINFO_TYPE_LONG:
        case CORINFO_TYPE_ULONG:
        case CORINFO_TYPE_DOUBLE:
            size = 8;
    }
    return size;
}

unsigned int Llvm::padOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE structClassHandle, unsigned int atOffset)
{
    unsigned int alignment;
    if (corInfoType == CORINFO_TYPE_VALUECLASS)
    {
        return PadOffset(structClassHandle, atOffset);
    }

    alignment = corInfoTypeAligment(corInfoType);
    return roundUp(atOffset, alignment);
}

unsigned int Llvm::padNextOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE structClassHandle, unsigned int atOffset)
{
    unsigned int size;
    if (corInfoType == CORINFO_TYPE_VALUECLASS)
    {
        size = getElementSize(structClassHandle, corInfoType);
    }
    else
    {
        size = corInfoTypeAligment(corInfoType);
    }

    return padOffset(corInfoType, structClassHandle, atOffset) + size;
}

[[noreturn]] void Llvm::failFunctionCompilation()
{
    for (Function* llvmFunc : m_functions)
    {
        if (llvmFunc != nullptr)
        {
            llvmFunc->deleteBody();
        }
    }

    fatal(CORJIT_SKIPPED);
}

template <EEApiId Func, typename TReturn, typename... TArgs>
TReturn CallEEApi(TArgs... args)
{
    return static_cast<TReturn (*)(void*, TArgs...)>(g_callbacks[static_cast<int>(Func)])(_thisPtr, args...);
}

const char* Llvm::GetMangledMethodName(CORINFO_METHOD_HANDLE methodHandle)
{
    return CallEEApi<EEApiId::GetMangledMethodName, const char*>(methodHandle);
}

const char* Llvm::GetMangledSymbolName(void* symbol)
{
    return CallEEApi<EEApiId::GetSymbolMangledName, const char*>(symbol);
}

const char* Llvm::GetTypeName(CORINFO_CLASS_HANDLE typeHandle)
{
    return CallEEApi<EEApiId::GetTypeName, const char*>(typeHandle);
}

void Llvm::AddCodeReloc(void* handle)
{
    CallEEApi<EEApiId::AddCodeReloc, void>(handle);
}

bool Llvm::IsRuntimeImport(CORINFO_METHOD_HANDLE methodHandle)
{
    return CallEEApi<EEApiId::IsRuntimeImport, uint32_t>(methodHandle) != 0;
}

const char* Llvm::GetDocumentFileName()
{
    return CallEEApi<EEApiId::GetDocumentFileName, const char*>();
}

uint32_t Llvm::FirstSequencePointLineNumber()
{
    return CallEEApi<EEApiId::FirstSequencePointLineNumber, uint32_t>();
}

uint32_t Llvm::GetOffsetLineNumber(unsigned ilOffset)
{
    return CallEEApi<EEApiId::GetOffsetLineNumber, uint32_t>(ilOffset);
}

bool Llvm::StructIsWrappedPrimitive(CORINFO_CLASS_HANDLE typeHandle, CorInfoType corInfoType)
{
    // Maintains compatiblity with the IL->LLVM generation.
    // TODO-LLVM, when IL generation is no more, see if we can remove this unwrapping.
    return CallEEApi<EEApiId::StructIsWrappedPrimitive, uint32_t>(typeHandle, corInfoType) != 0;
}

uint32_t Llvm::PadOffset(CORINFO_CLASS_HANDLE typeHandle, unsigned atOffset)
{
    return CallEEApi<EEApiId::PadOffset, uint32_t>(typeHandle, atOffset);
}

CorInfoTypeWithMod Llvm::GetArgTypeIncludingParameterized(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE arg, CORINFO_CLASS_HANDLE* pTypeHandle)
{
    return CallEEApi<EEApiId::GetArgTypeIncludingParameterized, CorInfoTypeWithMod>(sigInfo, arg, pTypeHandle);
}

CorInfoTypeWithMod Llvm::GetParameterType(CORINFO_CLASS_HANDLE typeHandle, CORINFO_CLASS_HANDLE* pInnerParameterTypeHandle)
{
    return CallEEApi<EEApiId::GetParameterType, CorInfoTypeWithMod>(typeHandle, pInnerParameterTypeHandle);
}

TypeDescriptor Llvm::GetTypeDescriptor(CORINFO_CLASS_HANDLE typeHandle)
{
    return CallEEApi<EEApiId::GetTypeDescriptor, TypeDescriptor>(typeHandle);
}

uint32_t Llvm::GetInstanceFieldAlignment(CORINFO_CLASS_HANDLE fieldTypeHandle)
{
    return CallEEApi<EEApiId::GetInstanceFieldAlignment, uint32_t>(fieldTypeHandle);
}
