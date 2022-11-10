// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef TARGET_WASM
#include <string.h>
#include "alloc.h"
#include "compiler.h"
#include "block.h"
#include "gentree.h"
#pragma warning (disable: 4702)
#include "llvm.h"
#include "llvm/ADT/StringRef.h"
#include "llvm/IR/LLVMContext.h"
#include "llvm/IR/Module.h"
#include "llvm/Bitcode/BitcodeWriter.h"
#include "llvm/BinaryFormat/Dwarf.h"
#include "llvm/IR/Verifier.h"
#pragma warning (error: 4702)

LLVMContext      _llvmContext;
Module*          _module            = nullptr;
llvm::DIBuilder* _diBuilder         = nullptr;
Function*        _nullCheckFunction = nullptr;

void* _thisPtr; // TODO: workaround for not changing the JIT/EE interface.  As this is static, it will probably fail if multithreaded compilation is attempted
const char* (*_getMangledMethodName)(void*, CORINFO_METHOD_STRUCT_*);
const char* (*_getMangledSymbolName)(void*, void*);
const char* (*_getMangledSymbolNameFromHelperTarget)(void*, void*); // TODO-LLVM: unused, delete.
const char* (*_getTypeName)(void*, CORINFO_CLASS_HANDLE);
const char* (*_addCodeReloc)(void*, void*);
const uint32_t (*_isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*);
const char* (*_getDocumentFileName)(void*);
const uint32_t (*_firstSequencePointLineNumber)(void*);
const uint32_t (*_getOffsetLineNumber)(void*, unsigned int ilOffset);
const uint32_t (*_structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType);
const uint32_t (*_padOffset)(void*, CORINFO_CLASS_STRUCT_*, unsigned);
const CorInfoTypeWithMod (*_getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*);
const CorInfoTypeWithMod (*_getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*);
const TypeDescriptor (*_getTypeDescriptor)(void*, CORINFO_CLASS_HANDLE);
CORINFO_METHOD_HANDLE (*_getCompilerHelpersMethodHandle)(void*, const char*, const char*);
const uint32_t (*_getInstanceFieldAlignment)(void*, CORINFO_CLASS_HANDLE);

char*                              _outputFileName;
Function*                          _doNothingFunction;

std::unordered_map<CORINFO_CLASS_HANDLE, Type*>* _llvmStructs = new std::unordered_map<CORINFO_CLASS_HANDLE, Type*>();
std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>* _structDescMap = new std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>();

extern "C" DLLEXPORT void registerLlvmCallbacks(void*       thisPtr,
                                                const char* outputFileName,
                                                const char* triple,
                                                const char* dataLayout,
                                                const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*),
                                                const char* (*getMangledSymbolNamePtr)(void*, void*),
                                                const char* (*getMangledSymbolNameFromHelperTargetPtr)(void*, void*),
                                                const char* (*getTypeName)(void*, CORINFO_CLASS_HANDLE),
                                                const char* (*addCodeRelocPtr)(void*, void*),
                                                const uint32_t (*isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*),
                                                const char* (*getDocumentFileName)(void*),
                                                const uint32_t (*firstSequencePointLineNumber)(void*),
                                                const uint32_t (*getOffsetLineNumber)(void*, unsigned int),
                                                const uint32_t(*structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType),
                                                const uint32_t(*padOffset)(void*, CORINFO_CLASS_STRUCT_*, unsigned),
                                                const CorInfoTypeWithMod(*getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*),
                                                const CorInfoTypeWithMod(*getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*),
                                                const TypeDescriptor(*getTypeDescriptor)(void*, CORINFO_CLASS_HANDLE),
                                                CORINFO_METHOD_HANDLE (*getCompilerHelpersMethodHandle)(void*, const char*, const char*),
                                                const uint32_t (*getInstanceFieldAlignment)(void*, CORINFO_CLASS_HANDLE))
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

GCInfo* Llvm::getGCInfo()
{
    if (_gcInfo == nullptr)
    {
        _gcInfo = new (_compiler->getAllocator(CMK_GC)) GCInfo(_compiler);
    }
    return _gcInfo;
}

void emitDebugMetadata(LLVMContext& context)
{
    _module->addModuleFlag(llvm::Module::Warning, "Dwarf Version", 4);
    _module->addModuleFlag(llvm::Module::Warning, "Debug Info Version", 3);
    _diBuilder->finalize();
}

[[noreturn]] void Llvm::failFunctionCompilation()
{
    if (_function != nullptr)
    {
        _function->deleteBody();
    }
    fatal(CORJIT_SKIPPED);
}

// maintains compatiblity with the IL->LLVM generation.  TODO-LLVM, when IL generation is no more, see if we can remove this unwrapping
bool structIsWrappedPrimitive(CORINFO_CLASS_HANDLE classHnd, CorInfoType primitiveType)
{
    return (*_structIsWrappedPrimitive)(_thisPtr, classHnd, primitiveType);
}

void addPaddingFields(unsigned paddingSize, std::vector<Type*>& llvmFields)
{
    unsigned numInts = paddingSize / 4;
    unsigned numBytes = paddingSize - numInts * 4;
    for (unsigned i = 0; i < numInts; i++)
    {
        llvmFields.push_back(Type::getInt32Ty(_llvmContext));
    }
    for (unsigned i = 0; i < numBytes; i++)
    {
        llvmFields.push_back(Type::getInt8Ty(_llvmContext));
    }
}

unsigned getWellKnownTypeSize(CorInfoType corInfoType)
{
    return genTypeSize(JITtype2varType(corInfoType));
}

unsigned Llvm::getElementSize(CORINFO_CLASS_HANDLE classHandle, CorInfoType corInfoType)
{
    if (classHandle != NO_CLASS_HANDLE)
    {
        return _info.compCompHnd->getClassSize(classHandle);
    }
    return getWellKnownTypeSize(corInfoType);
}

StructDesc* Llvm::getStructDesc(CORINFO_CLASS_HANDLE structHandle)
{
    if (_structDescMap->find(structHandle) == _structDescMap->end())
    {
        TypeDescriptor structTypeDescriptor = _getTypeDescriptor(_thisPtr, structHandle);
        unsigned structSize                 = _info.compCompHnd->getClassSize(structHandle); // TODO-LLVM: add to TypeDescriptor?

        std::vector<CORINFO_FIELD_HANDLE> sparseFields = std::vector<CORINFO_FIELD_HANDLE>(structSize);
        std::vector<unsigned> sparseFieldSizes = std::vector<unsigned>(structSize);

        for (unsigned i = 0; i < structSize; i++)
            sparseFields[i] = nullptr;

        // determine the largest field for unions, and get fields in order of offset
        for (unsigned i = 0; i < structTypeDescriptor.getFieldCount(); i++)
        {
            CORINFO_FIELD_HANDLE fieldHandle = structTypeDescriptor.getField(i);
            unsigned             fldOffset   = _info.compCompHnd->getFieldOffset(fieldHandle);

            assert(fldOffset < structSize);

            CORINFO_CLASS_HANDLE fieldClass;
            CorInfoType corInfoType = _info.compCompHnd->getFieldType(fieldHandle, &fieldClass);

            unsigned fieldSize = getElementSize(fieldClass, corInfoType);

            // store the biggest field at the offset for unions
            if (sparseFields[fldOffset] == nullptr || fieldSize > sparseFieldSizes[fldOffset])
            {
                sparseFields[fldOffset] = fieldHandle;
                sparseFieldSizes[fldOffset] = fieldSize;
            }
        }

        // count the struct fields after replacing fields with equal offsets
        unsigned fieldCount = 0;
        unsigned i          = 0;
        while(i < structSize)
        {
            if (sparseFields[i] != nullptr)
            {
                fieldCount++;
                // clear out any fields that are covered by this field
                for (unsigned j = 1; j < sparseFieldSizes[i]; j++)
                {
                    sparseFields[i + j] = nullptr;
                }
                i += sparseFieldSizes[i];
            }
            else
            {
                i++;
            }
        }

        FieldDesc*  fields     = new FieldDesc[fieldCount];
        StructDesc* structDesc = new StructDesc(fieldCount, fields, structTypeDescriptor.hasSignificantPadding());

        unsigned fieldIx = 0;
        for (unsigned fldOffset = 0; fldOffset < structSize; fldOffset++)
        {
            if (sparseFields[fldOffset] == nullptr)
            {
                continue;
            }

            CORINFO_FIELD_HANDLE fieldHandle = sparseFields[fldOffset];
            CORINFO_CLASS_HANDLE fieldClassHandle = NO_CLASS_HANDLE;

            const CorInfoType corInfoType = _info.compCompHnd->getFieldType(fieldHandle, &fieldClassHandle);
            fields[fieldIx] = FieldDesc(fldOffset, corInfoType, fieldClassHandle);
            fieldIx++;
        }

        _structDescMap->insert({structHandle, structDesc});
    }
    return _structDescMap->at(structHandle);
}

llvm::Type* Llvm::getLlvmTypeForStruct(ClassLayout* classLayout)
{
    return getLlvmTypeForStruct(classLayout->GetClassHandle());
}

llvm::Type* Llvm::getLlvmTypeForStruct(CORINFO_CLASS_HANDLE structHandle)
{
    if (_llvmStructs->find(structHandle) == _llvmStructs->end())
    {
        llvm::Type* llvmType;
        unsigned    fieldAlignment;

        // LLVM thinks certain sizes of struct have a different calling convention than Clang does.
        // Treating them as ints fixes that and is more efficient in general

        unsigned structSize = _info.compCompHnd->getClassSize(structHandle);
        switch (structSize)
        {
            case 1:
                llvmType = Type::getInt8Ty(_llvmContext);
                break;
            case 2:
                fieldAlignment = _getInstanceFieldAlignment(_thisPtr, structHandle);
                if (fieldAlignment == 2)
                {
                    llvmType = Type::getInt16Ty(_llvmContext);
                    break;
                }
            case 4:
                fieldAlignment = _getInstanceFieldAlignment(_thisPtr, structHandle);
                if (fieldAlignment == 4)
                {
                    if (structIsWrappedPrimitive(structHandle, CORINFO_TYPE_FLOAT))
                    {
                        llvmType = Type::getFloatTy(_llvmContext);
                    }
                    else
                    {
                        llvmType = Type::getInt32Ty(_llvmContext);
                    }
                    break;
                }
            case 8:
                fieldAlignment = _getInstanceFieldAlignment(_thisPtr, structHandle);
                if (fieldAlignment == 8)
                {
                    if (structIsWrappedPrimitive(structHandle, CORINFO_TYPE_DOUBLE))
                    {
                        llvmType = Type::getDoubleTy(_llvmContext);
                    }
                    else
                    {
                        llvmType = Type::getInt64Ty(_llvmContext);
                    }
                    break;
                }

            default:
                // Forward-declare the struct in case there's a reference to it in the fields.
                // This must be a named struct or LLVM hits a stack overflow
                const char* name = _getTypeName(_thisPtr, structHandle);
                llvm::StructType* llvmStructType = llvm::StructType::create(_llvmContext, name);
                llvmType = llvmStructType;
                StructDesc* structDesc = getStructDesc(structHandle);
                unsigned    fieldCnt   = structDesc->getFieldCount();


                unsigned lastOffset = 0;
                unsigned totalSize = 0;
                std::vector<Type*> llvmFields = std::vector<Type*>();
                unsigned prevElementSize = 0;


                for (unsigned fieldIx = 0; fieldIx < fieldCnt; fieldIx++)
                {
                    FieldDesc* fieldDesc = structDesc->getFieldDesc(fieldIx);

                    // Pad to this field if necessary
                    unsigned paddingSize = fieldDesc->getFieldOffset() - lastOffset - prevElementSize;
                    if (paddingSize > 0)
                    {
                        addPaddingFields(paddingSize, llvmFields);
                        totalSize += paddingSize;
                    }

                    CorInfoType fieldCorType = fieldDesc->getCorType();
                    
                    unsigned fieldSize = getElementSize(fieldDesc->getClassHandle(), fieldCorType);

                    llvmFields.push_back(getLlvmTypeForCorInfoType(fieldCorType, fieldDesc->getClassHandle()));

                    totalSize += fieldSize;
                    lastOffset = fieldDesc->getFieldOffset();
                    prevElementSize = fieldSize;
                }

                // If explicit layout is greater than the sum of fields, add padding
                if (totalSize < structSize)
                {
                    addPaddingFields(structSize - totalSize, llvmFields);
                }

                llvmStructType->setBody(llvmFields, true);
                break;
        }
        _llvmStructs->insert({ structHandle, llvmType });
    }
    return _llvmStructs->at(structHandle);
}

//------------------------------------------------------------------------
// Returns the VM defined TypeDesc.GetParameterType() for the given type
// Intended for pointers to generate the appropriate LLVM pointer type
// E.g. "[S.P.CoreLib]Internal.Runtime.MethodTable"*
// 
llvm::Type* Llvm::getLlvmTypeForParameterType(CORINFO_CLASS_HANDLE classHnd)
{
    CORINFO_CLASS_HANDLE innerParameterHandle;
    CorInfoType parameterCorInfoType = strip(_getParameterType(_thisPtr, classHnd, &innerParameterHandle));
    if (parameterCorInfoType == CorInfoType::CORINFO_TYPE_VOID)
    {
        return Type::getInt8Ty(_llvmContext); // LLVM doesn't allow void*
    }
    return getLlvmTypeForCorInfoType(parameterCorInfoType, innerParameterHandle);
}

// Copy of logic from ILImporter.GetLLVMTypeForTypeDesc
llvm::Type* Llvm::getLlvmTypeForCorInfoType(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    switch (corInfoType)
    {
        case CorInfoType::CORINFO_TYPE_VOID:
            return Type::getVoidTy(_llvmContext);

        case CorInfoType::CORINFO_TYPE_BOOL:
        case CorInfoType::CORINFO_TYPE_UBYTE:
        case CorInfoType::CORINFO_TYPE_BYTE:
            return Type::getInt8Ty(_llvmContext);

        case CorInfoType::CORINFO_TYPE_SHORT:
        case CorInfoType::CORINFO_TYPE_USHORT:
            return Type::getInt16Ty(_llvmContext);

        case CorInfoType::CORINFO_TYPE_INT:
        case CorInfoType::CORINFO_TYPE_UINT:
        case CorInfoType::CORINFO_TYPE_NATIVEINT:  // TODO: Wasm64 - what does NativeInt mean for Wasm64
            return Type::getInt32Ty(_llvmContext);

        case CorInfoType::CORINFO_TYPE_LONG:
        case CorInfoType::CORINFO_TYPE_ULONG:
            return Type::getInt64Ty(_llvmContext);

        case CorInfoType::CORINFO_TYPE_FLOAT:
            return Type::getFloatTy(_llvmContext);

        case CorInfoType::CORINFO_TYPE_DOUBLE:
            return Type::getDoubleTy(_llvmContext);

        case CorInfoType::CORINFO_TYPE_PTR:
        {
            if (classHnd == NO_CLASS_HANDLE)
            {
                return Type::getInt8Ty(_llvmContext)->getPointerTo();
            }

            return getLlvmTypeForParameterType(classHnd)->getPointerTo();
        }

        case CorInfoType::CORINFO_TYPE_BYREF:
        case CorInfoType::CORINFO_TYPE_CLASS:
            return Type::getInt8PtrTy(_llvmContext);

        case CorInfoType::CORINFO_TYPE_VALUECLASS:
            return getLlvmTypeForStruct(classHnd);

        default:
            failFunctionCompilation();
    }
}

// When looking at a sigInfo from eeGetMethodSig we have CorInfoType(s) but when looking at lclVars we have LclVarDsc or var_type(s), This method exists to allow both to map to LLVM types.
CorInfoType Llvm::toCorInfoType(var_types varType)
{
    switch (varType)
    {
        case TYP_BOOL:
            return CorInfoType::CORINFO_TYPE_BOOL;
        case TYP_BYREF:
            return CorInfoType::CORINFO_TYPE_BYREF;
        case TYP_BYTE:
            return CorInfoType::CORINFO_TYPE_BYTE;
        case TYP_UBYTE:
            return CorInfoType::CORINFO_TYPE_UBYTE;
        case TYP_LCLBLK:
            return CorInfoType::CORINFO_TYPE_VALUECLASS;
        case TYP_DOUBLE:
            return CorInfoType::CORINFO_TYPE_DOUBLE;
        case TYP_FLOAT:
            return CorInfoType::CORINFO_TYPE_FLOAT;
        case TYP_INT:
            return CorInfoType::CORINFO_TYPE_INT;
        case TYP_UINT:
            return CorInfoType::CORINFO_TYPE_UINT;
        case TYP_LONG:
            return CorInfoType::CORINFO_TYPE_LONG;
        case TYP_ULONG:
            return CorInfoType::CORINFO_TYPE_ULONG;
        case TYP_REF:
            return CorInfoType::CORINFO_TYPE_REFANY;
        case TYP_SHORT:
            return CorInfoType::CORINFO_TYPE_SHORT;
        case TYP_USHORT:
            return CorInfoType::CORINFO_TYPE_USHORT;
        case TYP_STRUCT:
            return CorInfoType::CORINFO_TYPE_VALUECLASS;
        case TYP_UNDEF:
            return CorInfoType::CORINFO_TYPE_UNDEF;
        case TYP_VOID:
            return CorInfoType::CORINFO_TYPE_VOID;
        default:
            failFunctionCompilation();
    }
}

CORINFO_CLASS_HANDLE Llvm::tryGetStructClassHandle(LclVarDsc* varDsc)
{
    return varTypeIsStruct(varDsc) ? varDsc->GetStructHnd() : NO_CLASS_HANDLE;;
}

unsigned corInfoTypeAligment(CorInfoType corInfoType)
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
        return _padOffset(_thisPtr, structClassHandle, atOffset);
    }

    alignment = corInfoTypeAligment(corInfoType);
    return roundUp(atOffset, alignment);
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

/// <summary>
/// Returns true if the type can be stored on the LLVM stack
/// instead of the shadow stack in this method. This is the case
/// if it is a non-ref primitive or a struct without GC fields.
/// </summary>
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

/// <summary>
/// Returns true if the method returns a type that must be kept
/// on the shadow stack
/// </summary>
bool Llvm::needsReturnStackSlot(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    return corInfoType != CorInfoType::CORINFO_TYPE_VOID && !canStoreArgOnLlvmStack(compiler, corInfoType, classHnd);
}

bool Llvm::needsReturnStackSlot(Compiler* compiler, GenTreeCall* callee)
{
    CORINFO_SIG_INFO sigInfo;

    compiler->eeGetMethodSig(compiler->info.compMethodHnd, &sigInfo);

    return Llvm::needsReturnStackSlot(compiler, sigInfo.retType, sigInfo.retTypeClass);
}

bool Llvm::needsReturnStackSlot(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    return Llvm::needsReturnStackSlot(_compiler, corInfoType, classHnd);
}

CorInfoType Llvm::getCorInfoTypeForArg(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE& arg, CORINFO_CLASS_HANDLE* clsHnd)
{
    CorInfoTypeWithMod corTypeWithMod = _getArgTypeIncludingParameterized(_thisPtr, sigInfo, arg, clsHnd);
    return strip(corTypeWithMod);
}

Type* Llvm::getLlvmTypeForVarType(var_types type)
{
    // TODO: Fill out with missing type mappings and when all code done via clrjit, default should fail with useful
    // message
    switch (type)
    {
        case TYP_BOOL:
        case TYP_BYTE:
        case TYP_UBYTE:
            return Type::getInt8Ty(_llvmContext);
        case TYP_SHORT:
        case TYP_USHORT:
            return Type::getInt16Ty(_llvmContext);
        case TYP_INT:
        case TYP_UINT:
            return Type::getInt32Ty(_llvmContext);
        case TYP_LONG:
        case TYP_ULONG:
            return Type::getInt64Ty(_llvmContext);
        case var_types::TYP_FLOAT:
            return Type::getFloatTy(_llvmContext);
        case var_types::TYP_DOUBLE:
            return Type::getDoubleTy(_llvmContext);
        case TYP_BYREF:
        case TYP_REF:
            return Type::getInt8PtrTy(_llvmContext);
        case TYP_VOID:
            return Type::getVoidTy(_llvmContext);
        default:
            failFunctionCompilation();
    }
}

Type* Llvm::getLlvmTypeForLclVar(GenTreeLclVar* lclVar)
{
    var_types nodeType = lclVar->TypeGet();

    if (nodeType == TYP_STRUCT)
    {
        return getLlvmTypeForStruct(_compiler->lvaGetDesc(lclVar)->GetLayout());
    }
    return getLlvmTypeForVarType(nodeType);
}

Llvm::Llvm(Compiler* pCompiler)
    : _compiler(pCompiler),
      _info(pCompiler->info),
      _function(nullptr),
      _builder(_llvmContext),
      _prologBuilder(_llvmContext),
      _shadowStackLclNum(BAD_VAR_NUM),
      _retAddressLclNum(BAD_VAR_NUM)
{
    _compiler->eeGetMethodSig(_info.compMethodHnd, &_sigInfo);
}

void Llvm::llvmShutdown()
{
    if (_diBuilder != nullptr)
    {
        emitDebugMetadata(_llvmContext);
    }

    std::error_code ec;

    if (_outputFileName == nullptr) return; // nothing generated

    //TODO-LLVM: when the release build is more stable, reinstate the #ifdef.  For now the text output is useful for debugging
//#ifdef DEBUG
    char* txtFileName = (char*)malloc(strlen(_outputFileName) + 2); // .txt is longer than .bc
    strcpy(txtFileName, _outputFileName);
    strcpy(txtFileName + strlen(_outputFileName) - 2, "txt");
    llvm::raw_fd_ostream textOutputStream(txtFileName, ec);
    _module->print(textOutputStream, (llvm::AssemblyAnnotationWriter*)NULL);
    free(txtFileName);

    // verifyModule returns true when its broken, so invert
    assert(!llvm::verifyModule(*_module, &llvm::errs()));
//#endif //DEBUG

    llvm::raw_fd_ostream OS(_outputFileName, ec);
    llvm::WriteBitcodeToFile(*_module, OS);

    for (const auto &structDesc : *_structDescMap)
    {
        delete structDesc.second;
    }

    delete _module;
}

void Llvm::populateLlvmArgNums()
{
    if (_sigInfo.hasTypeArg())
    {
        failFunctionCompilation();
    }

    _shadowStackLclNum = _compiler->lvaGrabTemp(true DEBUGARG("shadowstack"));
    LclVarDsc* shadowStackVarDsc = _compiler->lvaGetDesc(_shadowStackLclNum);
    unsigned   nextLlvmArgNum    = 0;

    shadowStackVarDsc->lvLlvmArgNum = nextLlvmArgNum++;
    shadowStackVarDsc->lvType    = TYP_I_IMPL;
    shadowStackVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
    shadowStackVarDsc->lvIsParam = true;

    if (needsReturnStackSlot(_sigInfo.retType, _sigInfo.retTypeClass))
    {
        _retAddressLclNum = _compiler->lvaGrabTemp(true DEBUGARG("returnslot"));
        LclVarDsc* retAddressVarDsc  = _compiler->lvaGetDesc(_retAddressLclNum);
        retAddressVarDsc->lvLlvmArgNum = nextLlvmArgNum++;
        retAddressVarDsc->lvType       = TYP_I_IMPL;
        retAddressVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
        retAddressVarDsc->lvIsParam    = true;
    }

    // TODO-LLVM: other non-standard args, generic context, outs

    CORINFO_ARG_LIST_HANDLE sigArgs = _sigInfo.args;
    unsigned firstCorInfoArgLocalNum = 0;
    if (_sigInfo.hasThis())
    {
        firstCorInfoArgLocalNum++;
    }

    if (_info.compRetBuffArg != BAD_VAR_NUM)
    {
        firstCorInfoArgLocalNum++;
    }

    for (unsigned int i = 0; i < _sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CORINFO_CLASS_HANDLE classHnd;
        CorInfoType          corInfoType = getCorInfoTypeForArg(&_sigInfo, sigArgs, &classHnd);
        LclVarDsc*           varDsc      = _compiler->lvaGetDesc(i + firstCorInfoArgLocalNum);
        if (canStoreLocalOnLlvmStack(varDsc))
        {
            varDsc->lvLlvmArgNum  = nextLlvmArgNum++;
            varDsc->lvCorInfoType = corInfoType;
            varDsc->lvClassHnd = classHnd;
        }
    }

    _llvmArgCount = nextLlvmArgNum;
}

void Llvm::ConvertShadowStackLocalNode(GenTreeLclVarCommon* node)
{
    GenTreeLclVarCommon* lclVar = node->AsLclVarCommon();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar->GetLclNum());
    genTreeOps oper = node->OperGet();

    if (!canStoreLocalOnLlvmStack(varDsc))
    {
        // TODO-LLVM: if the offset == 0, just GT_STOREIND at the shadowStack
        unsigned offsetVal = varDsc->GetStackOffset() + node->GetLclOffs();
        GenTreeIntCon* offset = _compiler->gtNewIconNode(offsetVal, TYP_I_IMPL);

        GenTreeLclVar* shadowStackLocal = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
        GenTree* lclAddress = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackLocal, offset);

        genTreeOps indirOper;
        GenTree* storedValue = nullptr;
        switch (node->OperGet())
        {
            case GT_STORE_LCL_VAR:
                indirOper = lclVar->TypeIs(TYP_STRUCT) ? GT_STORE_OBJ : GT_STOREIND;
                storedValue = node->AsOp()->gtGetOp1();
                break;
            case GT_LCL_VAR:
                indirOper = lclVar->TypeIs(TYP_STRUCT) ? GT_OBJ : GT_IND;
                break;
            case GT_LCL_FLD:
                if (lclVar->TypeIs(TYP_STRUCT))
                {
                    //TODO-LLVM: eg. S_P_CoreLib_System_DateTimeParse__Parse
                    // a struct in a struct?
                    //[000026]-- -- -- -- -- --t26 = LCL_FLD struct V03 loc0[+72] Fseq[parsedDate] $83
                    failFunctionCompilation();
                }
                indirOper = GT_IND;
                break;
            case GT_LCL_VAR_ADDR:
            case GT_LCL_FLD_ADDR:
                indirOper = GT_NONE;
                break;
            case GT_STORE_LCL_FLD:
                indirOper   = lclVar->TypeIs(TYP_STRUCT) ? GT_STORE_OBJ : GT_STOREIND;
                storedValue = node->AsOp()->gtGetOp1();
            // TODO-LLVM: example:
                //   / --* t0  ref
                //   *   STORE_LCL_FLD ref V01 tmp0 ud : 1->0 [+0] Fseq[_list]
                break;
            default:
                unreached();
        }
        if (GenTree::OperIsIndir(indirOper))
        {
            node->ChangeOper(indirOper);
            node->AsIndir()->SetAddr(lclAddress);
        }
        if (GenTree::OperIsStore(indirOper))
        {
            node->gtFlags |= GTF_IND_TGT_NOT_HEAP;
            node->AsOp()->gtOp2 = storedValue;
        }
        if (GenTree::OperIsBlk(indirOper))
        {
            node->AsBlk()->SetLayout(varDsc->GetLayout());
            node->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
        }

        CurrentRange().InsertBefore(node, shadowStackLocal, offset);
        if (indirOper == GT_NONE)
        {
            // Local address nodes are directly replaced with the ADD.
            node->ReplaceWith(lclAddress, _compiler);
        }
        else
        {
            CurrentRange().InsertBefore(node, lclAddress);
        }
    }
}

// If the return type must be GC tracked, removes the return type
// and converts to a return slot arg, modifying the call args, and building the necessary IR
GenTreeCall::Use* Llvm::lowerCallReturn(GenTreeCall*      callNode,
                                        GenTreeCall::Use* insertAfterArg)
{
    GenTreeCall::Use* lastArg = insertAfterArg;
    var_types callReturnType = callNode->TypeGet();
    CORINFO_SIG_INFO* calleeSigInfo = callNode->callSig;

    // Some ctors, e.g. strings (and maybe only strings), have a return type in IR so
    // pass the call return type instead of the CORINFO_SIG_INFO return type, which is void in these cases
    if (needsReturnStackSlot(toCorInfoType(callReturnType), calleeSigInfo->retTypeClass))
    {
        // replace the "CALL ref" with a "CALL void" that takes a return address as the first argument
        GenTreeLclVar* shadowStackVar     = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
        GenTreeIntCon* offset             = _compiler->gtNewIconNode(_shadowStackLocalsSize, TYP_I_IMPL);
        GenTree*       returnValueAddress = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackVar, offset);

        // create temp for the return address
        unsigned   returnTempNum    = _compiler->lvaGrabTemp(false DEBUGARG("return value address"));
        LclVarDsc* returnAddrVarDsc = _compiler->lvaGetDesc(returnTempNum);
        returnAddrVarDsc->lvType    = TYP_I_IMPL;

        GenTree*          addrStore     = _compiler->gtNewStoreLclVar(returnTempNum, returnValueAddress);
        GenTree*          returnAddrLcl = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);

        GenTree* returnAddrLclAfterCall = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);
        GenTree* indirNode;
        if (callReturnType == TYP_STRUCT)
        {
            indirNode    = _compiler->gtNewObjNode(calleeSigInfo->retTypeClass, returnAddrLclAfterCall);
        }
        else
        {
            indirNode = _compiler->gtNewOperNode(GT_IND, callReturnType, returnAddrLclAfterCall);
        }
        indirNode->gtFlags |= GTF_IND_TGT_NOT_HEAP; // No RhpAssignRef required
        LIR::Use callUse;
        if (CurrentRange().TryGetUse(callNode, &callUse))
        {
            callUse.ReplaceWith(_compiler, indirNode);
        }
        else
        {
            callNode->ClearUnusedValue();
        }

        GenTreePutArgType* putArg = _compiler->gtNewPutArgType(returnAddrLcl, CORINFO_TYPE_PTR, nullptr);
#if DEBUG
        putArg->SetArgNum(-2);  // -2 will represent the return arg for LLVM
#endif
        lastArg = _compiler->gtInsertNewCallArgAfter(putArg, insertAfterArg);

        callNode->gtReturnType = TYP_VOID;
        callNode->gtCorInfoType = CORINFO_TYPE_VOID;
        callNode->ChangeType(TYP_VOID);

        CurrentRange().InsertBefore(callNode, shadowStackVar, offset, returnValueAddress, addrStore);
        CurrentRange().InsertAfter(addrStore, returnAddrLcl, putArg);
        CurrentRange().InsertAfter(callNode, returnAddrLclAfterCall, indirNode);
    }
    else
    {
        callNode->gtCorInfoType = calleeSigInfo->retType;
    }

    return lastArg;
}

void Llvm::failUnsupportedCalls(GenTreeCall* callNode)
{
    // we can't do these yet
    if ((callNode->gtCallType != CT_INDIRECT && _isRuntimeImport(_thisPtr, callNode->gtCallMethHnd)) || callNode->IsTailCall())
    {
        failFunctionCompilation();
    }

    CORINFO_SIG_INFO* calleeSigInfo = callNode->callSig;
    // TODO-LLVM: not attempting to compile generic signatures with context arg via clrjit yet
    // Investigate which methods do not get callSig set - happens currently with the Generics test
    if (calleeSigInfo == nullptr || calleeSigInfo->hasTypeArg())
    {
        failFunctionCompilation();
    }

    if (callNode->gtCallArgs != nullptr)
    {
        for (GenTree* operand : callNode->Operands())
        {
            if (operand->IsArgPlaceHolderNode() || !operand->IsValue())
            {
                // Either of these situations may happen with calls.
                continue;
            }
            if (operand == callNode->gtControlExpr || operand == callNode->gtCallAddr)
            {
                // vtable target or indirect target
                continue;
            }

            fgArgTabEntry* curArgTabEntry = _compiler->gtArgEntryByNode(callNode, operand);
            if (curArgTabEntry->nonStandardArgKind == NonStandardArgKind::VirtualStubCell)
            {
                failFunctionCompilation();
            }
        }
    }
}

GenTree* Llvm::createStoreNode(var_types nodeType, GenTree* addr, GenTree* data, ClassLayout* structClassLayout)
{
    GenTree* storeNode;
    if (nodeType == TYP_STRUCT)
    {
        storeNode = new (_compiler, GT_STORE_OBJ) GenTreeObj(nodeType, addr, data, structClassLayout);
    }
    else
    {
        storeNode = new (_compiler, GT_STOREIND) GenTreeStoreInd(nodeType, addr, data);
    }
    return storeNode;
}

GenTree* Llvm::createShadowStackStoreNode(var_types nodeType, GenTree* addr, GenTree* data, ClassLayout* structClassLayout)
{
    GenTree* storeNode = createStoreNode(nodeType, addr, data, structClassLayout);
    storeNode->gtFlags |= GTF_IND_TGT_NOT_HEAP;

    return storeNode;
}

//------------------------------------------------------------------------
// lowerCallToShadowStack: Lower the call, rewriting its arguments.
//
// This method has two primary objectives:
//  1) Transfer the information about the arguments from arg info to explicit
//     PutArgType nodes, to make it easy for codegen to consume it. Also, get
//     rid of the late/non-late argument distinction, by sorting the inserted nodes
//     in the original evaluation order, matching that of them in the signature.
//  2) Rewrite arguments and the return to be stored on the shadow stack. We take
//     the arguments which need to be on the shadow stack, remove them from the call
//     arguments list, store their values on the shadow stack, at offsets calculated
//     in a simple increasing order, matching the signature. We also rewrite returns
//     that must be on the shadow stack, see "lowerCallReturn".
//
void Llvm::lowerCallToShadowStack(GenTreeCall* callNode)
{
    // rewrite the args, adding shadow stack, and moving gc tracked args to the shadow stack
    unsigned shadowStackUseOffest = 0;

    fgArgInfo*                 argInfo    = callNode->fgArgInfo;
    unsigned int               argCount   = argInfo->ArgCount();
    fgArgTabEntry**            argTable   = argInfo->ArgTable();
    std::vector<OperandArgNum> sortedArgs = std::vector<OperandArgNum>(argCount);
    OperandArgNum*             sortedData = sortedArgs.data();

    GenTreeCall::Use* lastArg;
    GenTreeCall::Use* insertReturnAfter;
    GenTreeCall::Use* callThisArg = callNode->gtCallThisArg;

    callNode->ResetArgInfo();
    callNode->gtCallThisArg = nullptr;

    // set up the callee shadowstack, creating a temp and the PUTARG
    GenTreeLclVar* shadowStackVar = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
    GenTreeIntCon* offset         = _compiler->gtNewIconNode(_shadowStackLocalsSize, TYP_I_IMPL);
    // TODO-LLVM: possible performance benefit: when _shadowStackLocalsSize == 0, then omit the GT_ADD.
    GenTree* calleeShadowStack = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackVar, offset);

    GenTreePutArgType* calleeShadowStackPutArg =
        _compiler->gtNewPutArgType(calleeShadowStack, CORINFO_TYPE_PTR, NO_CLASS_HANDLE);
#ifdef DEBUG
    calleeShadowStackPutArg->SetArgNum(-1); // -1 will represent the shadowstack  arg for LLVM
#endif

    callNode->gtCallArgs     = _compiler->gtNewCallArgs(calleeShadowStackPutArg);
    lastArg                  = callNode->gtCallArgs;
    insertReturnAfter        = lastArg; // add the return slot after the shadow stack arg
    callNode->gtCallLateArgs = nullptr;

    CurrentRange().InsertBefore(callNode, shadowStackVar, offset, calleeShadowStack, calleeShadowStackPutArg);

    lastArg = lowerCallReturn(callNode, insertReturnAfter);

    for (unsigned i = 0; i < argCount; i++)
    {
        fgArgTabEntry* curArgTabEntry = argTable[i];
        unsigned int   argNum         = curArgTabEntry->argNum;
        OperandArgNum  opAndArg       = {argNum, curArgTabEntry->GetNode()};
        sortedData[argNum]            = opAndArg;
    }

    CORINFO_SIG_INFO* calleeSigInfo = callNode->callSig;
    // Relies on the fact all arguments not in the signature come before those that are.
    unsigned firstSigArgIx = argCount - calleeSigInfo->numArgs;

    CORINFO_ARG_LIST_HANDLE sigArgs = calleeSigInfo->args;
    unsigned                argIx   = 0;

    for (OperandArgNum opAndArg : sortedArgs)
    {
        CORINFO_CLASS_HANDLE clsHnd      = NO_CLASS_HANDLE;
        CorInfoType          corInfoType = CORINFO_TYPE_UNDEF;

        // "this" not in sigInfo arg list
        bool isThis = callThisArg != nullptr && opAndArg.argNum == 0 && calleeSigInfo->hasThis();
        bool isSigArg = argIx >= firstSigArgIx;
        if (isSigArg)
        {
            corInfoType = getCorInfoTypeForArg(calleeSigInfo, sigArgs, &clsHnd);
        }
        else if (!isThis)
        {
            corInfoType = toCorInfoType(opAndArg.operand->TypeGet());
        }

        bool argOnShadowStack = isThis || (isSigArg && !canStoreArgOnLlvmStack(_compiler, corInfoType, clsHnd));
        if (argOnShadowStack)
        {
            if (corInfoType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffest = padOffset(corInfoType, clsHnd, shadowStackUseOffest);
            }

            if (opAndArg.operand->OperIs(GT_FIELD_LIST))
            {
                for (GenTreeFieldList::Use& use : opAndArg.operand->AsFieldList()->Uses())
                {
                    assert(use.GetType() != TYP_STRUCT);

                    GenTree*       lclShadowStack = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
                    GenTreeIntCon* fieldOffset =
                        _compiler->gtNewIconNode(_shadowStackLocalsSize + shadowStackUseOffest + use.GetOffset(),
                                                 TYP_I_IMPL);
                    GenTree* fieldSlotAddr =
                        _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, lclShadowStack, fieldOffset);
                    GenTree* fieldStoreNode = createShadowStackStoreNode(use.GetType(), fieldSlotAddr, use.GetNode(), nullptr);

                    CurrentRange().InsertBefore(callNode, lclShadowStack, fieldOffset, fieldSlotAddr,
                                                fieldStoreNode);
                }

                CurrentRange().Remove(opAndArg.operand);
            }
            else
            {
                GenTree*       lclShadowStack = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
                GenTreeIntCon* offset =
                    _compiler->gtNewIconNode(_shadowStackLocalsSize + shadowStackUseOffest, TYP_I_IMPL);
                GenTree* slotAddr  = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, lclShadowStack, offset);

                GenTree* storeNode = createShadowStackStoreNode(opAndArg.operand->TypeGet(), slotAddr, opAndArg.operand,
                                                                corInfoType == CORINFO_TYPE_VALUECLASS
                                                                    ? _compiler->typGetObjLayout(clsHnd)
                                                                    : nullptr);
                CurrentRange().InsertBefore(callNode, lclShadowStack, offset, slotAddr, storeNode);
            }

            if (corInfoType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffest = padNextOffset(corInfoType, clsHnd, shadowStackUseOffest);
            }
            else
            {
                shadowStackUseOffest += TARGET_POINTER_SIZE;
            }
        }
        else
        {
            // arg on LLVM stack
            GenTreePutArgType* putArg = _compiler->gtNewPutArgType(opAndArg.operand, corInfoType, clsHnd);
#if DEBUG
            putArg->SetArgNum(opAndArg.argNum);
#endif
            lastArg = _compiler->gtInsertNewCallArgAfter(putArg, lastArg);

            CurrentRange().InsertBefore(callNode, putArg);
        }
        if (isSigArg)
        {
            sigArgs = _info.compCompHnd->getArgNext(sigArgs);
        }

        argIx++;
    }
}

void Llvm::lowerStoreLcl(GenTreeLclVarCommon* storeLclNode)
{
    LclVarDsc* addrVarDsc = _compiler->lvaGetDesc(storeLclNode->GetLclNum());

    if (addrVarDsc->CanBeReplacedWithItsField(_compiler))
    {
        ClassLayout* layout      = addrVarDsc->GetLayout();
        GenTree*     data        = storeLclNode->gtGetOp1();
        var_types    addrVarType = addrVarDsc->TypeGet();

        storeLclNode->SetOper(GT_LCL_VAR_ADDR);
        storeLclNode->ChangeType(TYP_I_IMPL);
        storeLclNode->SetLclNum(addrVarDsc->lvFieldLclStart);

        GenTree* storeObjNode = new (_compiler, GT_STORE_OBJ) GenTreeObj(addrVarType, storeLclNode, data, layout);
        storeObjNode->gtFlags |= GTF_ASG;

        CurrentRange().InsertAfter(storeLclNode, storeObjNode);
    }

    if (storeLclNode->TypeIs(TYP_STRUCT))
    {
        GenTree* dataOp = storeLclNode->gtGetOp1();
        CORINFO_CLASS_HANDLE dataHandle = _compiler->gtGetStructHandleIfPresent(dataOp);
        if (dataOp->OperIs(GT_IND))
        {
            // Special case: "gtGetStructHandleIfPresent" sometimes guesses the handle from
            // field sequences, but we will always need to transform TYP_STRUCT INDs into OBJs.
            dataHandle = NO_CLASS_HANDLE;
        }

        if (addrVarDsc->GetStructHnd() != dataHandle)
        {
            if (dataOp->OperIsIndir())
            {
                dataOp->SetOper(GT_OBJ);
                dataOp->AsObj()->SetLayout(addrVarDsc->GetLayout());
            }
            else if (dataOp->OperIs(GT_LCL_VAR)) // can get icon 0 here
            {
                GenTreeLclVarCommon* dataLcl = dataOp->AsLclVarCommon();
                LclVarDsc* dataVarDsc = _compiler->lvaGetDesc(dataLcl->GetLclNum());

                dataVarDsc->lvHasLocalAddr = 1;

                GenTree* dataAddrNode = _compiler->gtNewLclVarAddrNode(dataLcl->GetLclNum());

                dataLcl->ChangeOper(GT_OBJ);
                dataLcl->AsObj()->SetAddr(dataAddrNode);
                dataLcl->AsObj()->SetLayout(addrVarDsc->GetLayout());

                CurrentRange().InsertBefore(dataLcl, dataAddrNode);
            }
        }
    }
}

void Llvm::lowerFieldOfDependentlyPromotedStruct(GenTree* node)
{
    if (node->OperIsLocal() || node->OperIsLocalAddr())
    {
        GenTreeLclVarCommon* lclVar = node->AsLclVarCommon();
        uint16_t             offset = lclVar->GetLclOffs();
        LclVarDsc*           varDsc = _compiler->lvaGetDesc(lclVar->GetLclNum());        

        if (_compiler->lvaIsFieldOfDependentlyPromotedStruct(varDsc))
        {
            switch (node->OperGet())
            {
                case GT_LCL_VAR:
                    lclVar->SetOper(GT_LCL_FLD);
                    break;

                case GT_STORE_LCL_VAR:
                    lclVar->SetOper(GT_STORE_LCL_FLD);
                    break;

                case GT_LCL_VAR_ADDR:
                    lclVar->SetOper(GT_LCL_FLD_ADDR);
                    break;
            }

            lclVar->SetLclNum(varDsc->lvParentLcl);
            lclVar->AsLclFld()->SetLclOffs(varDsc->lvFldOffset + offset);

            if ((node->gtFlags & GTF_VAR_DEF) != 0)
            {
                // Conservatively assume these become partial.
                // TODO-ADDR: only apply to stores be precise.
                node->gtFlags |= GTF_VAR_USEASG;
            }
        }
    }
}

void Llvm::lowerToShadowStack()
{
    for (BasicBlock* _currentBlock : _compiler->Blocks())
    {
        _currentRange = &LIR::AsRange(_currentBlock);
        for (GenTree* node : CurrentRange())
        {
            lowerFieldOfDependentlyPromotedStruct(node);

            if (node->OperIs(GT_STORE_LCL_VAR))
            {
                lowerStoreLcl(node->AsLclVarCommon());
            }

            if (node->OperIsLocal() || node->OperIsLocalAddr())
            {
                ConvertShadowStackLocalNode(node->AsLclVarCommon());
            }
            else if (node->IsCall())
            {
                GenTreeCall* callNode = node->AsCall();

                if (callNode->IsHelperCall())
                {
                    // helper calls are built differently
                    continue;
                }

                failUnsupportedCalls(callNode);

                lowerCallToShadowStack(callNode);

                if ((_compiler->fgIsThrow(callNode) || callNode->IsNoReturn()) && (callNode->gtNext != nullptr))
                {
                    // If there is a no return, or always throw call, delete the dead code so we can add the unreachable statment immediately, and not after any dead RET
                    CurrentRange().Remove(callNode->gtNext, _currentBlock->lastNode());
                }
            }
            else if (node->OperIs(GT_RETURN) && _retAddressLclNum != BAD_VAR_NUM)
            {
                var_types originalReturnType = node->TypeGet();
                if(node->TypeIs(TYP_VOID))
                {
                    /* TODO-LLVM: retbuf .   compHasRetBuffArg doesn't seem to have an implementation */
                    failFunctionCompilation();
                }

                LclVarDsc* retAddressVarDsc = _compiler->lvaGetDesc(_retAddressLclNum);
                retAddressVarDsc->lvIsParam = 1;
                retAddressVarDsc->lvType = TYP_I_IMPL;

                GenTreeLclVar* retAddressLocal = _compiler->gtNewLclvNode(_retAddressLclNum, TYP_I_IMPL);
                GenTree* storeNode = createShadowStackStoreNode(originalReturnType, retAddressLocal, node->AsOp()->gtGetOp1(),
                                    originalReturnType == TYP_STRUCT ? _compiler->typGetObjLayout(_sigInfo.retTypeClass) : nullptr);

                GenTreeOp* retNode = node->AsOp();
                retNode->gtOp1 = nullptr;
                node->ChangeType(TYP_VOID);

                CurrentRange().InsertBefore(node, retAddressLocal, storeNode);
            }

            if (node->OperIsLocalAddr() || node->OperIsLocalField())
            {
                // Indicates that this local is to live on the LLVM frame, and will not participate in SSA.
                _compiler->lvaGetDesc(node->AsLclVarCommon())->lvHasLocalAddr = 1;
            }
        }
    }
}


//------------------------------------------------------------------------
// Convert GT_STORE_LCL_VAR and GT_LCL_VAR to use the shadow stack when the local needs to be GC tracked,
// rewrite calls that returns GC types to do so via a store to a passed in address on the shadow stack.
// Likewise, store the returned value there if required.
//
void Llvm::Lower()
{
    populateLlvmArgNums();

    _shadowStackLocalsSize = 0;

    std::vector<LclVarDsc*> locals;
    unsigned localsParamCount = 0;

    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        if (varDsc->lvIsParam)
        {
            if (_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_INDEPENDENT)
            {
                for (unsigned index = 0; index < varDsc->lvFieldCnt; index++)
                {
                    unsigned   fieldLclNum = varDsc->lvFieldLclStart + index;
                    LclVarDsc* fieldVarDsc = _compiler->lvaGetDesc(fieldLclNum);
                    if (fieldVarDsc->lvRefCnt(RCS_NORMAL) != 0)
                    {
                        _compiler->fgEnsureFirstBBisScratch();
                        LIR::Range& firstBlockRange = LIR::AsRange(_compiler->fgFirstBB);

                        GenTree* fieldValue =
                            _compiler->gtNewLclFldNode(lclNum, fieldVarDsc->TypeGet(), fieldVarDsc->lvFldOffset);

                        GenTree* fieldStore = _compiler->gtNewStoreLclVar(fieldLclNum, fieldValue);
                        firstBlockRange.InsertAtBeginning(fieldStore);
                        firstBlockRange.InsertAtBeginning(fieldValue);
                    }

                    fieldVarDsc->lvIsStructField = false;
                    fieldVarDsc->lvParentLcl     = BAD_VAR_NUM;
                    fieldVarDsc->lvIsParam       = false;
                }

                varDsc->lvPromoted      = false;
                varDsc->lvFieldLclStart = BAD_VAR_NUM;
                varDsc->lvFieldCnt      = 0;
            }
            else if (_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_DEPENDENT)
            {
                /* dependent promotion, just mark fields as not lvIsParam */
                for (unsigned index = 0; index < varDsc->lvFieldCnt; index++)
                {
                    unsigned   fieldLclNum = varDsc->lvFieldLclStart + index;
                    LclVarDsc* fieldVarDsc = _compiler->lvaGetDesc(fieldLclNum);
                    fieldVarDsc->lvIsParam = false;
                }
            }
        }

        if (!canStoreLocalOnLlvmStack(varDsc))
        {
            if (_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_INDEPENDENT)
            {
                // The individual fields will placed on the shadow stack.
                continue;
            }
            if (_compiler->lvaIsFieldOfDependentlyPromotedStruct(varDsc))
            {
                // The fields will be referenced through the parent.
                continue;
            }

            locals.push_back(varDsc);
            if (varDsc->lvIsParam)
            {
                localsParamCount++;
                varDsc->lvIsParam = false;
            }
        }
    }

    if (_compiler->opts.OptimizationEnabled())
    {
        std::sort(locals.begin() + localsParamCount, locals.end(), [](const LclVarDsc* lhs, const LclVarDsc* rhs) { return lhs->lvRefCntWtd() > rhs->lvRefCntWtd(); });
    }

    unsigned int offset = 0;
    for (unsigned i = 0; i < locals.size(); i++)
    {
        LclVarDsc* varDsc = locals.at(i);
        CorInfoType corInfoType = toCorInfoType(varDsc->TypeGet());
        CORINFO_CLASS_HANDLE classHandle = tryGetStructClassHandle(varDsc);

        offset = padOffset(corInfoType, classHandle, offset);
        varDsc->SetStackOffset(offset);
        offset = padNextOffset(corInfoType, classHandle, offset);
    }
    _shadowStackLocalsSize = offset;

    lowerToShadowStack();
}
#endif
