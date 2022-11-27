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
    CORINFO_SIG_INFO sigInfo;

    // TODO-LLVM: this is expensive. Why not just check call->TypeGet() / call->gtRetClsHnd?
    compiler->eeGetMethodSig(compiler->info.compMethodHnd, &sigInfo);

    return Llvm::needsReturnStackSlot(compiler, sigInfo.retType, sigInfo.retTypeClass);
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
CorInfoType Llvm::toCorInfoType(var_types varType)
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
        case TYP_LCLBLK:
            return CORINFO_TYPE_VALUECLASS;
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
            return CORINFO_TYPE_REFANY;
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
            failFunctionCompilation();
    }
}

// Returns true if the method returns a type that must be kept on the shadow stack
//
bool Llvm::needsReturnStackSlot(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    return corInfoType != CorInfoType::CORINFO_TYPE_VOID && !canStoreArgOnLlvmStack(compiler, corInfoType, classHnd);
}

bool Llvm::needsReturnStackSlot(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    return Llvm::needsReturnStackSlot(_compiler, corInfoType, classHnd);
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
    if (corInfoType == CorInfoType::CORINFO_TYPE_VALUECLASS)
    {
        ClassLayout* classLayout = compiler->typGetObjLayout(classHnd);
        return !classLayout->HasGCPtr();
    }

    if (corInfoType == CorInfoType::CORINFO_TYPE_BYREF || corInfoType == CorInfoType::CORINFO_TYPE_CLASS ||
        corInfoType == CorInfoType::CORINFO_TYPE_REFANY)
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
