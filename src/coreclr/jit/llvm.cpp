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
Function*        _nullCheckFunction = nullptr;
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
        TypeDescriptor structTypeDescriptor = GetTypeDescriptor(structHandle);
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
                fieldAlignment = GetInstanceFieldAlignment(structHandle);
                if (fieldAlignment == 2)
                {
                    llvmType = Type::getInt16Ty(_llvmContext);
                    break;
                }
            case 4:
                fieldAlignment = GetInstanceFieldAlignment(structHandle);
                if (fieldAlignment == 4)
                {
                    if (StructIsWrappedPrimitive(structHandle, CORINFO_TYPE_FLOAT))
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
                fieldAlignment = GetInstanceFieldAlignment(structHandle);
                if (fieldAlignment == 8)
                {
                    if (StructIsWrappedPrimitive(structHandle, CORINFO_TYPE_DOUBLE))
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
                const char* name = GetTypeName(structHandle);
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
    CorInfoType parameterCorInfoType = strip(GetParameterType(classHnd, &innerParameterHandle));
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
        return PadOffset(structClassHandle, atOffset);
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
    CorInfoTypeWithMod corTypeWithMod = GetArgTypeIncludingParameterized(sigInfo, arg, clsHnd);
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
