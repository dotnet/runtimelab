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

void* _thisPtr; // TODO: workaround for not changing the JIT/EE interface.  As this is static, it will probably fail if multithreaded compilation is attempted
const char* (*_getMangledMethodName)(void*, CORINFO_METHOD_STRUCT_*);
const char* (*_getMangledSymbolName)(void*, void*);
const char* (*_getMangledSymbolNameFromHelperTarget)(void*, void*); // TODO-LLVM: unused, delete.
const char* (*_getTypeName)(void*, CORINFO_CLASS_HANDLE);
const char* (*_addCodeReloc)(void*, void*); // TODO-LLVM: does this really return a string?
uint32_t (*_isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*);
const char* (*_getDocumentFileName)(void*);
uint32_t (*_firstSequencePointLineNumber)(void*);
uint32_t (*_getOffsetLineNumber)(void*, unsigned ilOffset);
uint32_t (*_structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType);
uint32_t (*_padOffset)(void*, CORINFO_CLASS_STRUCT_*, unsigned);
CorInfoTypeWithMod (*_getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*);
CorInfoTypeWithMod (*_getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*);
TypeDescriptor (*_getTypeDescriptor)(void*, CORINFO_CLASS_HANDLE);
CORINFO_METHOD_HANDLE (*_getCompilerHelpersMethodHandle)(void*, const char*, const char*);
uint32_t (*_getInstanceFieldAlignment)(void*, CORINFO_CLASS_HANDLE);

extern "C" DLLEXPORT void registerLlvmCallbacks(void*       thisPtr,
                                                const char* outputFileName,
                                                const char* triple,
                                                const char* dataLayout,
                                                const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_HANDLE),
                                                const char* (*getMangledSymbolNamePtr)(void*, void*),
                                                const char* (*getMangledSymbolNameFromHelperTargetPtr)(void*, void*),
                                                const char* (*getTypeName)(void*, CORINFO_CLASS_HANDLE),
                                                const char* (*addCodeRelocPtr)(void*, void*),
                                                uint32_t (*isRuntimeImport)(void*, CORINFO_METHOD_HANDLE),
                                                const char* (*getDocumentFileName)(void*),
                                                uint32_t (*firstSequencePointLineNumber)(void*),
                                                uint32_t (*getOffsetLineNumber)(void*, unsigned),
                                                uint32_t(*structIsWrappedPrimitive)(void*, CORINFO_CLASS_HANDLE, CorInfoType),
                                                uint32_t(*padOffset)(void*, CORINFO_CLASS_HANDLE, unsigned),
                                                CorInfoTypeWithMod(*getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*),
                                                CorInfoTypeWithMod(*getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*),
                                                TypeDescriptor(*getTypeDescriptor)(void*, CORINFO_CLASS_HANDLE),
                                                CORINFO_METHOD_HANDLE (*getCompilerHelpersMethodHandle)(void*, const char*, const char*),
                                                uint32_t (*getInstanceFieldAlignment)(void*, CORINFO_CLASS_HANDLE))
{
    _thisPtr = thisPtr;
    _getMangledMethodName         = getMangledMethodNamePtr;
    _getMangledSymbolName         = getMangledSymbolNamePtr;
    _getMangledSymbolNameFromHelperTarget = getMangledSymbolNameFromHelperTargetPtr;
    _getTypeName                  = getTypeName;
    _addCodeReloc                 = addCodeRelocPtr;
    _isRuntimeImport              = isRuntimeImport;
    _getDocumentFileName          = getDocumentFileName;
    _firstSequencePointLineNumber = firstSequencePointLineNumber;
    _getOffsetLineNumber          = getOffsetLineNumber;
    _structIsWrappedPrimitive     = structIsWrappedPrimitive;
    _padOffset = padOffset;
    _getArgTypeIncludingParameterized = getArgTypeIncludingParameterized;
    _getParameterType             = getParameterType;
    _getTypeDescriptor            = getTypeDescriptor;
    _getCompilerHelpersMethodHandle       = getCompilerHelpersMethodHandle;
    _getInstanceFieldAlignment     = getInstanceFieldAlignment;

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

Llvm::Llvm(Compiler* compiler)
    : _compiler(compiler),
    _info(compiler->info),
    _function(nullptr),
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
        return helperCallHasShadowStackArg(_compiler->eeGetHelperNum(call->gtCallMethHnd));
    }

    // TODO-LLVM: this is not right for native calls.
    return true;
}

bool Llvm::helperCallHasShadowStackArg(CorInfoHelpFunc helperFunc)
{
    // TODO-LLVM: communicate this through a Jit-EE API.
    switch (helperFunc)
    {
        case CORINFO_HELP_DIV:
        case CORINFO_HELP_MOD:
        case CORINFO_HELP_UDIV:
        case CORINFO_HELP_UMOD:
        case CORINFO_HELP_LMUL_OVF:
        case CORINFO_HELP_ULMUL_OVF:
        case CORINFO_HELP_LDIV:
        case CORINFO_HELP_LMOD:
        case CORINFO_HELP_ULDIV:
        case CORINFO_HELP_ULMOD:
        case CORINFO_HELP_DBL2INT_OVF:
        case CORINFO_HELP_DBL2LNG_OVF:
        case CORINFO_HELP_DBL2UINT_OVF:
        case CORINFO_HELP_DBL2ULNG_OVF:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
            return true;

        case CORINFO_HELP_LLSH:
        case CORINFO_HELP_LRSH:
        case CORINFO_HELP_LRSZ:
        case CORINFO_HELP_LMUL:
        case CORINFO_HELP_LNG2DBL:
        case CORINFO_HELP_ULNG2DBL:
        case CORINFO_HELP_DBL2INT:
        case CORINFO_HELP_DBL2LNG:
        case CORINFO_HELP_DBL2UINT:
        case CORINFO_HELP_DBL2ULNG:
        case CORINFO_HELP_FLTREM:
        case CORINFO_HELP_DBLREM:
        case CORINFO_HELP_FLTROUND:
        case CORINFO_HELP_DBLROUND:
            // Implemented in "Runtime\MathHelpers.cpp".
            return false;

        case CORINFO_HELP_NEWFAST:
        case CORINFO_HELP_NEWSFAST:
        case CORINFO_HELP_NEWSFAST_FINALIZE:
        case CORINFO_HELP_NEWSFAST_ALIGN8:
        case CORINFO_HELP_NEWSFAST_ALIGN8_VC:
        case CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE:
        case CORINFO_HELP_NEW_MDARR:
        case CORINFO_HELP_NEWARR_1_DIRECT:
        case CORINFO_HELP_NEWARR_1_OBJ:
        case CORINFO_HELP_NEWARR_1_VC:
        case CORINFO_HELP_NEWARR_1_ALIGN8:
            // Allocators, implemented in "Runtime\portable.cpp".
            return false;

        case CORINFO_HELP_STRCNS:
        case CORINFO_HELP_STRCNS_CURRENT_MODULE:
        case CORINFO_HELP_INITCLASS:
        case CORINFO_HELP_INITINSTCLASS:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_ISINSTANCEOFINTERFACE:
        case CORINFO_HELP_ISINSTANCEOFARRAY:
        case CORINFO_HELP_ISINSTANCEOFCLASS:
        case CORINFO_HELP_ISINSTANCEOFANY:
        case CORINFO_HELP_CHKCASTINTERFACE:
        case CORINFO_HELP_CHKCASTARRAY:
        case CORINFO_HELP_CHKCASTCLASS:
        case CORINFO_HELP_CHKCASTANY:
        case CORINFO_HELP_CHKCASTCLASS_SPECIAL:
        case CORINFO_HELP_BOX:
        case CORINFO_HELP_BOX_NULLABLE:
        case CORINFO_HELP_UNBOX:
        case CORINFO_HELP_UNBOX_NULLABLE:
        case CORINFO_HELP_ARRADDR_ST:
        case CORINFO_HELP_LDELEMA_REF:
            // Runtime exports, i. e. implemented in managed code with an unmanaged signature.
            // See "Runtime.Base\src\System\Runtime\RuntimeExports.cs", "Runtime.Base\src\System\Runtime\TypeCast.cs",
            return false;

        case CORINFO_HELP_GETREFANY:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
            return true;

        case CORINFO_HELP_THROW:
        case CORINFO_HELP_RETHROW:
            // For WASM, currently implemented in the bootstrapper...
            return false;

        case CORINFO_HELP_USER_BREAKPOINT:
            // Implemented in "Runtime\MiscHelpers.cpp".
            return false;

        case CORINFO_HELP_RNGCHKFAIL:
        case CORINFO_HELP_OVERFLOW:
        case CORINFO_HELP_THROWDIVZERO:
        case CORINFO_HELP_THROWNULLREF:
            // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
            // Note on "CORINFO_HELP_THROWNULLREF": ***this helpers has been deleted upstream***.
            // We need it. When merging upstream, revert its deletion!
            return true;

        case CORINFO_HELP_VERIFICATION:
            // Verification is in the process of being deleted from RyuJit.
            unreached();

        case CORINFO_HELP_FAIL_FAST:
            // Implemented in "Runtime\EHHelpers.cpp".
            return false;

        case CORINFO_HELP_METHOD_ACCESS_EXCEPTION:
        case CORINFO_HELP_FIELD_ACCESS_EXCEPTION:
        case CORINFO_HELP_CLASS_ACCESS_EXCEPTION:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_ENDCATCH:
            // Not used with funclet-based EH.
            unreached();

        case CORINFO_HELP_MON_ENTER:
        case CORINFO_HELP_MON_EXIT:
        case CORINFO_HELP_MON_ENTER_STATIC:
        case CORINFO_HELP_MON_EXIT_STATIC:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\SynchronizedMethodHelpers.cs".
            return true;

        case CORINFO_HELP_GETCLASSFROMMETHODPARAM:
        case CORINFO_HELP_GETSYNCFROMCLASSHANDLE:
        case CORINFO_HELP_STOP_FOR_GC:
            // Apparently NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_POLL_GC:
            // Implemented in "Runtime\portable.cpp".
            return false;

        case CORINFO_HELP_STRESS_GC:
        case CORINFO_HELP_CHECK_OBJ:
            // Debug-only helpers NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_ASSIGN_REF:
        case CORINFO_HELP_CHECKED_ASSIGN_REF:
        case CORINFO_HELP_ASSIGN_REF_ENSURE_NONHEAP:
        case CORINFO_HELP_ASSIGN_BYREF:
            // Write barriers, implemented in "Runtime\portable.cpp".
            return false;

        case CORINFO_HELP_ASSIGN_STRUCT:
        case CORINFO_HELP_GETFIELD8:
        case CORINFO_HELP_SETFIELD8:
        case CORINFO_HELP_GETFIELD16:
        case CORINFO_HELP_SETFIELD16:
        case CORINFO_HELP_GETFIELD32:
        case CORINFO_HELP_SETFIELD32:
        case CORINFO_HELP_GETFIELD64:
        case CORINFO_HELP_SETFIELD64:
        case CORINFO_HELP_GETFIELDOBJ:
        case CORINFO_HELP_SETFIELDOBJ:
        case CORINFO_HELP_GETFIELDSTRUCT:
        case CORINFO_HELP_SETFIELDSTRUCT:
        case CORINFO_HELP_GETFIELDFLOAT:
        case CORINFO_HELP_SETFIELDFLOAT:
        case CORINFO_HELP_GETFIELDDOUBLE:
        case CORINFO_HELP_SETFIELDDOUBLE:
        case CORINFO_HELP_GETFIELDADDR:
        case CORINFO_HELP_GETSTATICFIELDADDR_TLS:
        case CORINFO_HELP_GETGENERICS_GCSTATIC_BASE:
        case CORINFO_HELP_GETGENERICS_NONGCSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_GCSTATIC_BASE_DYNAMICCLASS:
        case CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_DYNAMICCLASS:
        case CORINFO_HELP_CLASSINIT_SHARED_DYNAMICCLASS:
        case CORINFO_HELP_GETGENERICS_GCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETGENERICS_NONGCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_DYNAMICCLASS:
        case CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_DYNAMICCLASS:
            // Not used in NativeAOT (or at all in some cases).
            unreached();

        case CORINFO_HELP_DBG_IS_JUST_MY_CODE:
        case CORINFO_HELP_PROF_FCN_ENTER:
        case CORINFO_HELP_PROF_FCN_LEAVE:
        case CORINFO_HELP_PROF_FCN_TAILCALL:
        case CORINFO_HELP_BBT_FCN_ENTER:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_PINVOKE_CALLI:
            // TODO-LLVM: this is not a real "helper"; investigate what needs to be done to enable it.
            failFunctionCompilation();

        case CORINFO_HELP_TAILCALL:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_GETCURRENTMANAGEDTHREADID:
            // Implemented as "Environment.CurrentManagedThreadId".
            return true;

        case CORINFO_HELP_INIT_PINVOKE_FRAME:
            // Part of the inlined PInvoke frame construction feature which is NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_MEMSET:
        case CORINFO_HELP_MEMCPY:
            // Implemented as plain "memset"/"memcpy".
            return false;

        case CORINFO_HELP_RUNTIMEHANDLE_METHOD:
        case CORINFO_HELP_RUNTIMEHANDLE_METHOD_LOG:
        case CORINFO_HELP_RUNTIMEHANDLE_CLASS:
        case CORINFO_HELP_RUNTIMEHANDLE_CLASS_LOG:
            // Not used in NativeAOT.
            unreached();

        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE:
        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
            return true;

        case CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD:
        case CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD:
        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\LdTokenHelpers.cs".
            return true;

        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
            return true;

        case CORINFO_HELP_ARE_TYPES_EQUIVALENT:
            // Another runtime export from "TypeCast.cs".
            return false;

        case CORINFO_HELP_VIRTUAL_FUNC_PTR:
        case CORINFO_HELP_READYTORUN_NEW:
        case CORINFO_HELP_READYTORUN_NEWARR_1:
            // Not used in NativeAOT.
            unreached();

        case CORINFO_HELP_READYTORUN_ISINSTANCEOF:
        case CORINFO_HELP_READYTORUN_CHKCAST:
        case CORINFO_HELP_READYTORUN_STATIC_BASE:
        case CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR:
        case CORINFO_HELP_READYTORUN_GENERIC_HANDLE:
        case CORINFO_HELP_READYTORUN_DELEGATE_CTOR:
        case CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE:
            // Not static methods; currently we "inline" them for LLVM.
            // Should be "unreached" once all are handled.
            failFunctionCompilation();

        case CORINFO_HELP_EE_PRESTUB:
        case CORINFO_HELP_EE_PRECODE_FIXUP:
        case CORINFO_HELP_EE_PINVOKE_FIXUP:
        case CORINFO_HELP_EE_VSD_FIXUP:
        case CORINFO_HELP_EE_EXTERNAL_FIXUP:
        case CORINFO_HELP_EE_VTABLE_FIXUP:
        case CORINFO_HELP_EE_REMOTING_THUNK:
        case CORINFO_HELP_EE_PERSONALITY_ROUTINE:
        case CORINFO_HELP_EE_PERSONALITY_ROUTINE_FILTER_FUNCLET:
            // NGEN/R2R-specific marker helpers.
            unreached();

        case CORINFO_HELP_ASSIGN_REF_EAX:
        case CORINFO_HELP_ASSIGN_REF_EBX:
        case CORINFO_HELP_ASSIGN_REF_ECX:
        case CORINFO_HELP_ASSIGN_REF_ESI:
        case CORINFO_HELP_ASSIGN_REF_EDI:
        case CORINFO_HELP_ASSIGN_REF_EBP:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EAX:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EBX:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_ECX:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_ESI:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EDI:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EBP:
            // x86-specific write barriers.
            unreached();

        case CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR:
        case CORINFO_HELP_DEBUG_LOG_LOOP_CLONING:
            // Debug-only functionality NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_THROW_ARGUMENTEXCEPTION:
        case CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION:
        case CORINFO_HELP_THROW_NOT_IMPLEMENTED:
        case CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED:
            // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
            return true;

        case CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED:
            // Dead code.
            unreached();

        case CORINFO_HELP_JIT_PINVOKE_BEGIN:
        case CORINFO_HELP_JIT_PINVOKE_END:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS:
            // [R]PI helpers, implemented in "Runtime\thread.cpp".
            return false;

        case CORINFO_HELP_GVMLOOKUP_FOR_SLOT:
            // TODO-LLVM: fix.
            failFunctionCompilation();

        case CORINFO_HELP_STACK_PROBE:
        case CORINFO_HELP_PATCHPOINT:
        case CORINFO_HELP_CLASSPROFILE32:
        case CORINFO_HELP_CLASSPROFILE64:
        case CORINFO_HELP_PARTIAL_COMPILATION_PATCHPOINT:
            unreached();

        default:
            // Add new helpers to the above as necessary.
            unreached();
    }
}

// Returns true if the type can be stored on the LLVM stack
// instead of the shadow stack in this method. This is the case
// if it is a non-ref primitive or a struct without GC fields.
//
bool Llvm::canStoreLocalOnLlvmStack(LclVarDsc* varDsc)
{
    return !varDsc->HasGCPtr();
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
    if (_function != nullptr)
    {
        _function->deleteBody();
    }
    fatal(CORJIT_SKIPPED);
}

const char* Llvm::GetMangledMethodName(CORINFO_METHOD_HANDLE methodHandle)
{
    return _getMangledMethodName(_thisPtr, methodHandle);
}

const char* Llvm::GetMangledSymbolName(void* symbol)
{
    return _getMangledSymbolName(_thisPtr, symbol);
}

const char* Llvm::GetTypeName(CORINFO_CLASS_HANDLE typeHandle)
{
    return _getTypeName(_thisPtr, typeHandle);
}

const char* Llvm::AddCodeReloc(void* handle)
{
    return _addCodeReloc(_thisPtr, handle);
}

bool Llvm::IsRuntimeImport(CORINFO_METHOD_HANDLE methodHandle)
{
    return _isRuntimeImport(_thisPtr, methodHandle) != 0;
}

const char* Llvm::GetDocumentFileName()
{
    return _getDocumentFileName(_thisPtr);
}

uint32_t Llvm::FirstSequencePointLineNumber()
{
    return _firstSequencePointLineNumber(_thisPtr);
}

uint32_t Llvm::GetOffsetLineNumber(unsigned ilOffset)
{
    return _getOffsetLineNumber(_thisPtr, ilOffset);
}

bool Llvm::StructIsWrappedPrimitive(CORINFO_CLASS_HANDLE typeHandle, CorInfoType corInfoType)
{
    // Maintains compatiblity with the IL->LLVM generation.
    // TODO-LLVM, when IL generation is no more, see if we can remove this unwrapping.
    return _structIsWrappedPrimitive(_thisPtr, typeHandle, corInfoType) != 0;
}

uint32_t Llvm::PadOffset(CORINFO_CLASS_HANDLE typeHandle, unsigned atOffset)
{
    return _padOffset(_thisPtr, typeHandle, atOffset);
}

CorInfoTypeWithMod Llvm::GetArgTypeIncludingParameterized(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE arg, CORINFO_CLASS_HANDLE* pTypeHandle)
{
    return _getArgTypeIncludingParameterized(_thisPtr, sigInfo, arg, pTypeHandle);
}

CorInfoTypeWithMod Llvm::GetParameterType(CORINFO_CLASS_HANDLE typeHandle, CORINFO_CLASS_HANDLE* pInnerParameterTypeHandle)
{
    return _getParameterType(_thisPtr, typeHandle, pInnerParameterTypeHandle);
}

TypeDescriptor Llvm::GetTypeDescriptor(CORINFO_CLASS_HANDLE typeHandle)
{
    return _getTypeDescriptor(_thisPtr, typeHandle);
}

CORINFO_METHOD_HANDLE Llvm::GetCompilerHelpersMethodHandle(const char* helperClassTypeName, const char* helperMethodName)
{
    return _getCompilerHelpersMethodHandle(_thisPtr, helperClassTypeName, helperMethodName);
}

uint32_t Llvm::GetInstanceFieldAlignment(CORINFO_CLASS_HANDLE fieldTypeHandle)
{
    return _getInstanceFieldAlignment(_thisPtr, fieldTypeHandle);
}
