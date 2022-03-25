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
#include "llvm/IR/DIBuilder.h"
#include "llvm/BinaryFormat/Dwarf.h"
#include "llvm/IR/Verifier.h"
#pragma warning (error: 4702)

using llvm::Function;
using llvm::FunctionType;
using llvm::LLVMContext;
using llvm::ArrayRef;
using llvm::Module;

static Module*          _module    = nullptr;
static llvm::DIBuilder* _diBuilder = nullptr;
static LLVMContext _llvmContext;
static void* _thisPtr; // TODO: workaround for not changing the JIT/EE interface.  As this is static, it will probably fail if multithreaded compilation is attempted
static const char* (*_getMangledMethodName)(void*, CORINFO_METHOD_STRUCT_*);
static const char* (*_getMangledSymbolName)(void*, void*);
static const char* (*_getMangledSymbolNameFromHelperTarget)(void*, void*);
static const char* (*_getTypeName)(void*, CORINFO_CLASS_HANDLE);
static const char* (*_addCodeReloc)(void*, void*);
static const uint32_t (*_isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*);
static const char* (*_getDocumentFileName)(void*);
static const uint32_t (*_firstSequencePointLineNumber)(void*);
static const uint32_t (*_getOffsetLineNumber)(void*, unsigned int ilOffset);
static const uint32_t (*_structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType);
static const uint32_t (*_padOffset)(void*, CORINFO_CLASS_STRUCT_*, unsigned);
static const CorInfoTypeWithMod (*_getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*);
static const CorInfoTypeWithMod (*_getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*);
static const TypeDescriptor (*_getTypeDescriptor)(void*, CORINFO_CLASS_HANDLE);

static char*                              _outputFileName;
static Function*                          _doNothingFunction;

static std::unordered_map<CORINFO_CLASS_HANDLE, Type*>* _llvmStructs = new std::unordered_map<CORINFO_CLASS_HANDLE, Type*>();
static std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>* _structDescMap = new std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>();


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
                                                const TypeDescriptor(*getTypeDescriptor)(void*, CORINFO_CLASS_HANDLE))
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

Value* Llvm::mapGenTreeToValue(GenTree* genTree, Value* valueRef)
{
    if (_sdsuMap->find(genTree) != _sdsuMap->end())
    {
        fatal(CorJitResult::CORJIT_INTERNALERROR);
    }
    _sdsuMap->insert({genTree, valueRef});
    return valueRef;
}

Value* Llvm::getGenTreeValue(GenTree* op)
{
    Value* mapValue = _sdsuMap->at(op);

    assert(mapValue != nullptr);

    return mapValue;
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

        // LLVM thinks certain sizes of struct have a different calling convention than Clang does.
        // Treating them as ints fixes that and is more efficient in general

        unsigned structSize = _info.compCompHnd->getClassSize(structHandle);
        unsigned structAlignment = _info.compCompHnd->getClassAlignmentRequirement(structHandle);
        switch (structSize)
        {
            case 1:
                llvmType = Type::getInt8Ty(_llvmContext);
                break;
            case 2:
                if (structAlignment == 2)
                {
                    llvmType = Type::getInt16Ty(_llvmContext);
                    break;
                }
            case 4:
                if (structAlignment == 4)
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
                if (structAlignment == 8)
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
bool canStoreLocalOnLlvmStack(LclVarDsc* varDsc)
{
    return !varDsc->HasGCPtr();
}

bool Llvm::canStoreArgOnLlvmStack(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    // structs with no GC pointers can go on LLVM stack.
    if (corInfoType == CorInfoType::CORINFO_TYPE_VALUECLASS)
    {
        // Use getClassAtribs over typGetObjLayout because EETypePtr has CORINFO_FLG_GENERIC_TYPE_VARIABLE? which fails with typGetObjLayout
        uint32_t classAttribs = _info.compCompHnd->getClassAttribs(classHnd);

        return (classAttribs & (CORINFO_FLG_CONTAINS_GC_PTR | CORINFO_FLG_CONTAINS_STACK_PTR)) == 0;
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
bool Llvm::needsReturnStackSlot(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    return corInfoType != CorInfoType::CORINFO_TYPE_VOID && !canStoreArgOnLlvmStack(corInfoType, classHnd);
}

CorInfoType Llvm::getCorInfoTypeForArg(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE& arg, CORINFO_CLASS_HANDLE* clsHnd)
{
    CorInfoTypeWithMod corTypeWithMod = _getArgTypeIncludingParameterized(_thisPtr, sigInfo, arg, clsHnd);
    return strip(corTypeWithMod);
}

FunctionType* Llvm::getFunctionType()
{
    // TODO-LLVM: delete this when these signatures implemented
    if (_sigInfo.hasExplicitThis() || _sigInfo.hasTypeArg())
        failFunctionCompilation();

    std::vector<llvm::Type*> argVec(_llvmArgCount);
    llvm::Type*              retLlvmType;

    for (unsigned i = 0; i < _compiler->lvaCount; i++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(i);
        if (varDsc->lvIsParam)
        {
            assert(varDsc->lvLlvmArgNum != BAD_LLVM_ARG_NUM);

            argVec[varDsc->lvLlvmArgNum] = getLlvmTypeForCorInfoType(varDsc->lvCorInfoType, varDsc->lvClassHnd);
        }
    }

    retLlvmType = _retAddressLclNum == BAD_VAR_NUM
        ? getLlvmTypeForCorInfoType(_sigInfo.retType, _sigInfo.retTypeClass)
        : Type::getVoidTy(_llvmContext);

    return FunctionType::get(retLlvmType, ArrayRef<Type*>(argVec), false);
}

FunctionType* Llvm::createFunctionTypeForCall(GenTreeCall* call)
{
    llvm::Type* retLlvmType = getLlvmTypeForCorInfoType(call->gtCorInfoType, call->gtRetClsHnd);

    std::vector<llvm::Type*> argVec = std::vector<llvm::Type*>();

    for (GenTreeCall::Use& use : call->Args())
    {
        GenTreePutArgType* putArg = use.GetNode()->AsPutArgType();
        argVec.push_back(getLlvmTypeForCorInfoType(putArg->GetCorInfoType(), putArg->GetClsHnd()));
    }

    return FunctionType::get(retLlvmType, ArrayRef<Type*>(argVec), false);
}

Value* getOrCreateExternalSymbol(const char* symbolName, Type* symbolType = nullptr)
{
    if (symbolType == nullptr)
    {
        symbolType = Type::getInt32PtrTy(_llvmContext);
    }

    Value* symbol = _module->getGlobalVariable(symbolName);
    if (symbol == nullptr)
    {
        symbol = new llvm::GlobalVariable(*_module, symbolType, true, llvm::GlobalValue::LinkageTypes::ExternalLinkage, (llvm::Constant*)nullptr, symbolName);
    }
    return symbol;
}

Function* getOrCreateRhpAssignRef()
{
    Function* llvmFunc = _module->getFunction("RhpAssignRef");
    if (llvmFunc == nullptr)
    {
        llvmFunc = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>{Type::getInt8PtrTy(_llvmContext), Type::getInt8PtrTy(_llvmContext)}, false), Function::ExternalLinkage, 0U, "RhpAssignRef", _module); // TODO: ExternalLinkage forced as linked from old module
    }
    return llvmFunc;
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

llvm::Instruction* Llvm::getCast(llvm::Value* source, Type* targetType)
{
    Type* sourceType = source->getType();
    if (sourceType == targetType)
        return nullptr;

    Type::TypeID sourceTypeID = sourceType->getTypeID();
    Type::TypeID targetTypeId = targetType->getTypeID();

    if (targetTypeId == Type::TypeID::PointerTyID)
    {
        switch (sourceTypeID)
        {
            case Type::TypeID::PointerTyID:
                return new llvm::BitCastInst(source, targetType, "CastPtrToPtr");
            case Type::TypeID::IntegerTyID:
                return new llvm::IntToPtrInst(source, targetType, "CastPtrToInt");
            default:
                failFunctionCompilation();
        }
    }
    if (targetTypeId == Type::TypeID::IntegerTyID)
    {
        switch (sourceTypeID)
        {
            case Type::TypeID::PointerTyID:
                return new llvm::PtrToIntInst(source, targetType, "CastPtrToInt");
            case Type::TypeID::IntegerTyID:
                if (sourceType->getPrimitiveSizeInBits() > targetType->getPrimitiveSizeInBits())
                {
                    return new llvm::TruncInst(source, targetType, "TruncInt");
                }
            default:
                failFunctionCompilation();
        }
    }

    failFunctionCompilation();
}

Value* Llvm::castIfNecessary(Value* source, Type* targetType, llvm::IRBuilder<>* builder)
{
    if (builder == nullptr)
    {
        builder = &_builder;
    }

    llvm::Instruction* castInst = getCast(source, targetType);
    if (castInst == nullptr)
        return source;

    return builder->Insert(castInst);
}

Value* Llvm::castToPointerToLlvmType(Value* address, llvm::Type* llvmType)
{
    return castIfNecessary(address, llvmType->getPointerTo());
}

void Llvm::castingStore(Value* toStore, Value* address, llvm::Type* llvmType)
{
    _builder.CreateStore(castIfNecessary(toStore, llvmType),
        castToPointerToLlvmType(address, llvmType));
}

void Llvm::castingStore(Value* toStore, Value* address, var_types type)
{
    castingStore(toStore, address, getLlvmTypeForVarType(type));
}

/// <summary>
/// Returns the llvm arg number or shadow stack offset for the corresponding local which must be loaded from an argument
/// </summary>
LlvmArgInfo Llvm::getLlvmArgInfoForArgIx(unsigned int lclNum)
{
    if (_sigInfo.hasExplicitThis() || _sigInfo.hasTypeArg())
        failFunctionCompilation();

    unsigned int llvmArgNum    = 1; // skip shadow stack arg
    bool         returnOnStack = false;

    LlvmArgInfo             llvmArgInfo = {
        -1 /* default to not an LLVM arg*/, _sigInfo.hasThis() ? TARGET_POINTER_SIZE : 0U /* this is the first pointer on
                                                                                       the shadow stack */
    };

    if (_compiler->info.compRetBuffArg != BAD_VAR_NUM)
    {
        if (lclNum == 0)
        {
            // the first IR arg is the return address, but its not in sigInfo so handle here
            llvmArgInfo.m_argIx = llvmArgNum;
            return llvmArgInfo;
        }
        lclNum--; // line up with sigArgs
    }

    if (needsReturnStackSlot(_sigInfo.retType, _sigInfo.retTypeClass))
    {
        llvmArgNum++;
    }

    // adjust lclNum for this which is not in _sigInfo.args
    unsigned thisAdjustedLclNum = _sigInfo.hasThis() ? lclNum - 1 : lclNum;

    CORINFO_ARG_LIST_HANDLE sigArgs = _sigInfo.args;

    unsigned int shadowStackOffset = llvmArgInfo.m_shadowStackOffset;

    unsigned int i = 0;
    for (; i < _sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CORINFO_CLASS_HANDLE clsHnd;
        CorInfoType corInfoType = getCorInfoTypeForArg(&_sigInfo, sigArgs, &clsHnd);
        if (canStoreArgOnLlvmStack(corInfoType, clsHnd))
        {
            if (thisAdjustedLclNum == i)
            {
                llvmArgInfo.m_argIx = llvmArgNum;
                break;
            }

            llvmArgNum++;
        }
        else
        {
            if (thisAdjustedLclNum == i)
            {
                llvmArgInfo.m_shadowStackOffset = shadowStackOffset;
                break;
            }

            shadowStackOffset += TARGET_POINTER_SIZE; // TODO size of arg, for now only handles byrefs and class types
        }
    }
    return llvmArgInfo;
}

void Llvm::emitDoNothingCall()
{
    if (_doNothingFunction == nullptr)
    {
        _doNothingFunction = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>(), false), Function::ExternalLinkage, 0U, "llvm.donothing", _module);
    }
    _builder.CreateCall(_doNothingFunction);
}

void Llvm::buildAdd(GenTree* node, Value* op1, Value* op2)
{
    Type* op1Type = op1->getType();
    if (op1Type->isPointerTy() && op2->getType()->isIntegerTy())
    {
        // GEPs scale indices, bitcasting to i8* makes them equivalent to the raw offsets we have in IR
        mapGenTreeToValue(node, _builder.CreateGEP(castIfNecessary(op1, Type::getInt8PtrTy(_llvmContext)), op2));
    }
    else if (op1Type->isIntegerTy() && op2->getType() == op1Type)
    {
        mapGenTreeToValue(node, _builder.CreateAdd(op1, op2));
    }
    else
    {
        // unsupported add type combination
        failFunctionCompilation();
    }
}

void Llvm::buildDiv(GenTree* node)
{
    Type* targetType = getLlvmTypeForVarType(node->TypeGet());
    Value* dividendValue = consumeValue(node->gtGetOp1(), targetType);
    Value* divisorValue  = consumeValue(node->gtGetOp2(), targetType);

    Value* resultValue;
    // TODO-LLVM: exception handling.  Div by 0 and INT32/64_MIN / -1
    switch (node->TypeGet())
    {
        case TYP_FLOAT:
        case TYP_DOUBLE:
            resultValue = _builder.CreateFDiv(dividendValue, divisorValue);
            break;

        default:
            resultValue = _builder.CreateSDiv(dividendValue, divisorValue);
            break;
    }

    mapGenTreeToValue(node, resultValue);
}

unsigned int Llvm::getTotalRealLocalOffset()
{
    return _shadowStackLocalsSize;
}

unsigned int Llvm::getTotalLocalOffset()
{
    unsigned int offset = getTotalRealLocalOffset();
    return AlignUp(offset, TARGET_POINTER_SIZE);
}

llvm::Value* Llvm::getShadowStackOffest(Value* shadowStack, unsigned int offset)
{
    if (offset == 0)
    {
        return shadowStack;
    }

    return _builder.CreateGEP(shadowStack, _builder.getInt32(offset));
}

llvm::BasicBlock* Llvm::getLLVMBasicBlockForBlock(BasicBlock* block)
{
    llvm::BasicBlock* llvmBlock;
    if (_blkToLlvmBlkVectorMap->Lookup(block, &llvmBlock))
        return llvmBlock;

    llvmBlock = llvm::BasicBlock::Create(_llvmContext, "", _function);
    _blkToLlvmBlkVectorMap->Set(block, llvmBlock);
    return llvmBlock;
}

void Llvm::storeOnShadowStack(GenTree* operand, Value* shadowStackForCallee, unsigned int offset)
{
    castingStore(consumeValue(operand, Type::getInt8PtrTy(_llvmContext)),
                 getShadowStackOffest(shadowStackForCallee, offset), Type::getInt8PtrTy(_llvmContext));
}

// shadow stack moved up to avoid overwriting anything on the stack in the compiling method
llvm::Value* Llvm::getShadowStackForCallee()
{
    unsigned int offset = getTotalLocalOffset();

    return offset == 0 ? _function->getArg(0) : _builder.CreateGEP(_function->getArg(0), _builder.getInt32(offset));
}

//------------------------------------------------------------------------
// consumeValue: Get the Value* "node" produces when consumed as "targetLlvmType".
//
// During codegen, we follow the "normalize on demand" convention, i. e.
// the IR nodes produce "raw" values that have exactly the types of nodes,
// preserving small types, pointers, etc. However, the user in the IR
// consumes "actual" types, and this is the method where we normalize
// to those types. We could have followed the reverse convention and
// normalized on production of "Value*"s, but we presume the "on demand"
// convention is more efficient LLVM-IR-size-wise. It allows us to avoid
// situations where we'd be upcasting only to immediately truncate, which
// would be the case for small typed arguments and relops feeding jumps,
// to name a few examples.
//
// Arguments:
//    node           - the node for which to obtain the normalized value of
//    targetLlvmType - the LLVM type through which the user uses "node"
//
// Return Value:
//    The normalized value, of "targetLlvmType" type.
//
Value* Llvm::consumeValue(GenTree* node, Type* targetLlvmType)
{
    Value* nodeValue = getGenTreeValue(node);
    Value* finalValue = nodeValue;

    if (nodeValue->getType() != targetLlvmType)
    {
        // int to pointer type (TODO-LLVM: WASM64: use POINTER_BITs when set correctly, also below for getInt32Ty)
        if (nodeValue->getType() == Type::getInt32Ty(_llvmContext) && targetLlvmType->isPointerTy())
        {
            return _builder.CreateIntToPtr(nodeValue, targetLlvmType);
        }

        // pointer to ints
        if (nodeValue->getType()->isPointerTy() && targetLlvmType == Type::getInt32Ty(_llvmContext))
        {
            return _builder.CreatePtrToInt(nodeValue, Type::getInt32Ty(_llvmContext));
        }

        // i32* e.g symbols, to i8*
        if (nodeValue->getType()->isPointerTy() && targetLlvmType->isPointerTy())
        {
            return _builder.CreateBitCast(nodeValue, targetLlvmType);
        }

        // int and smaller int conversions
        assert(targetLlvmType->isIntegerTy() && nodeValue->getType()->isIntegerTy() &&
               nodeValue->getType()->getPrimitiveSizeInBits() <= 32 && targetLlvmType->getPrimitiveSizeInBits() <= 32);
        if (nodeValue->getType()->getPrimitiveSizeInBits() < targetLlvmType->getPrimitiveSizeInBits())
        {
            var_types trueNodeType = TYP_UNDEF;

            switch (node->OperGet())
            {
                case GT_CALL:
                    trueNodeType = static_cast<var_types>(node->AsCall()->gtReturnType);
                    break;

                case GT_LCL_VAR:
                    trueNodeType = _compiler->lvaGetDesc(node->AsLclVarCommon())->TypeGet();
                    break;

                case GT_EQ:
                case GT_NE:
                case GT_LT:
                case GT_LE:
                case GT_GE:
                case GT_GT:
                    // This is the special case for relops. Ordinary codegen "just knows" they need zero-extension.
                    assert(nodeValue->getType() == Type::getInt1Ty(_llvmContext));
                    trueNodeType = TYP_UBYTE;
                    break;

                default:
                    trueNodeType = node->TypeGet();
                    break;
            }

            assert(varTypeIsSmall(trueNodeType));

            finalValue = varTypeIsSigned(trueNodeType) ? _builder.CreateSExt(nodeValue, targetLlvmType)
                                                       : _builder.CreateZExt(nodeValue, targetLlvmType);
        }
        else
        {
            // Truncate.
            finalValue = _builder.CreateTrunc(nodeValue, targetLlvmType);
        }
    }

    return finalValue;
}

Function* Llvm::getOrCreateLlvmFunction(const char* symbolName, GenTreeCall* call)
{
    Function* llvmFunc = _module->getFunction(symbolName);

    if (llvmFunc == nullptr)
    {
        // assume ExternalLinkage, if the function is defined in the clrjit module, then it is replaced and an
        // extern added to the Ilc module
        llvmFunc =
            Function::Create(createFunctionTypeForCall(call), Function::ExternalLinkage, 0U, symbolName, _module);
    }
    return llvmFunc;
}

llvm::Value* Llvm::buildUserFuncCall(GenTreeCall* call)
{
    llvm::FunctionCallee llvmFuncCallee;

    if (call->gtCallType == CT_USER_FUNC || call->gtCallType == CT_INDIRECT)
    {
        if (call->IsVirtualVtable() || call->gtCallType == CT_INDIRECT)
        {
            FunctionType* functionType = createFunctionTypeForCall(call);
            GenTree* calleeNode = call->IsVirtualVtable() ? call->gtControlExpr : call->gtCallAddr;

            Value* funcPtr = castIfNecessary(getGenTreeValue(calleeNode), functionType->getPointerTo());

            llvmFuncCallee = {functionType, funcPtr};
        }
        else
        {
            const char* symbolName = (*_getMangledSymbolName)(_thisPtr, call->gtEntryPoint.handle);

            (*_addCodeReloc)(_thisPtr, call->gtEntryPoint.handle);
            Function* llvmFunc = getOrCreateLlvmFunction(symbolName, call);

            llvmFuncCallee = llvmFunc;
        }
    }

    std::vector<llvm::Value*> argVec = std::vector<llvm::Value*>();

    GenTreePutArgType* lastArg = nullptr;
    for (GenTreeCall::Use& use : call->Args())
    {
        lastArg = use.GetNode()->AsPutArgType();
        argVec.push_back(consumeValue(lastArg->gtGetOp1(), getLlvmTypeForCorInfoType(lastArg->GetCorInfoType(), lastArg->GetClsHnd())));
    }

    Value* llvmCall = _builder.CreateCall(llvmFuncCallee, ArrayRef<Value*>(argVec));
    return mapGenTreeToValue(call, llvmCall);
}

FunctionType* Llvm::buildHelperLlvmFunctionType(GenTreeCall* call, bool withShadowStack)
{
    Type* retLlvmType = getLlvmTypeForVarType(call->TypeGet());
    std::vector<llvm::Type*> argVec;

    if (withShadowStack)
    {
        argVec.push_back(Type::getInt8PtrTy(_llvmContext));
    }

    for (GenTreeCall::Use& use : call->Args())
    {
        Type* argLlvmType = getLlvmTypeForVarType(use.GetNode()->TypeGet());
        argVec.push_back(argLlvmType);
    }

    return FunctionType::get(retLlvmType, ArrayRef<llvm::Type*>(argVec), false);
}

bool Llvm::helperRequiresShadowStack(CORINFO_METHOD_HANDLE corinfoMethodHnd)
{
    //TODO-LLVM: is there a better way to identify managed helpers?
    //Probably want to lower the math helpers to ordinary GT_CASTs and
    //handle in the LLVM (as does ILToLLVMImporter) to avoid this overhead
    return corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_GVMLOOKUP_FOR_SLOT) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_DBL2INT_OVF) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_DBL2LNG_OVF) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_DBL2UINT_OVF) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_DBL2ULNG_OVF) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_LDIV) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_LMUL_OVF) ||
           corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_ULMUL_OVF);
}

void Llvm::buildHelperFuncCall(GenTreeCall* call)
{
    if (call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_GENERIC_HANDLE) ||
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE) ||
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_GVMLOOKUP_FOR_SLOT) || /* generates an extra parameter in the signature */
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_DELEGATE_CTOR))
    {
        // TODO-LLVM
        failFunctionCompilation();
    }

    if (call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_STATIC_BASE))
    {
        const char* symbolName = (*_getMangledSymbolName)(_thisPtr, call->gtEntryPoint.handle);
        Function* llvmFunc = _module->getFunction(symbolName);
        if (llvmFunc == nullptr)
        {
            llvmFunc = Function::Create(buildHelperLlvmFunctionType(call, true), Function::ExternalLinkage, 0U, symbolName, _module); // TODO: ExternalLinkage forced as defined in ILC module
        }

        // replacement for _info.compCompHnd->recordRelocation(nullptr, gtCall->gtEntryPoint.handle, IMAGE_REL_BASED_REL32);
        (*_addCodeReloc)(_thisPtr, call->gtEntryPoint.handle);

        mapGenTreeToValue(call, _builder.CreateCall(llvmFunc, getShadowStackForCallee()));
        return;
    }
    else
    {
        fgArgInfo* argInfo = call->fgArgInfo;
        unsigned int argCount = argInfo->ArgCount();
        fgArgTabEntry** argTable = argInfo->ArgTable();
        std::vector<OperandArgNum> sortedArgs = std::vector<OperandArgNum>(argCount);
        OperandArgNum* sortedData = sortedArgs.data();
        bool requiresShadowStack = helperRequiresShadowStack(call->gtCallMethHnd);

        //TODO-LLVM: refactor calling code with user calls.
        for (unsigned i = 0; i < argCount; i++)
        {
            fgArgTabEntry* curArgTabEntry = argTable[i];
            unsigned int   argNum = curArgTabEntry->argNum;
            OperandArgNum  opAndArg = { argNum, curArgTabEntry->GetNode() };
            sortedData[argNum] = opAndArg;
        }

        void* pAddr = nullptr;

        CorInfoHelpFunc helperNum = _compiler->eeGetHelperNum(call->gtCallMethHnd);
        void* addr = _compiler->compGetHelperFtn(helperNum, &pAddr);
        const char* symbolName = (*_getMangledSymbolName)(_thisPtr, addr);
        Function* llvmFunc = _module->getFunction(symbolName);
        if (llvmFunc == nullptr)
        {
            llvmFunc = Function::Create(buildHelperLlvmFunctionType(call, requiresShadowStack), Function::ExternalLinkage, 0U, symbolName, _module);
        }

        (*_addCodeReloc)(_thisPtr, addr);

        std::vector<llvm::Value*> argVec;
        unsigned argIx = 0;

        Value* shadowStackForCallee = getShadowStackForCallee();
        if (requiresShadowStack)
        {
            argVec.push_back(shadowStackForCallee);
            argIx++;
        }
        else
        {
            // we may come back into managed from the unmanaged call so store the shadowstack
            _builder.CreateStore(shadowStackForCallee, getOrCreateExternalSymbol("t_pShadowStackTop", Type::getInt8PtrTy(_llvmContext)));
        }

        for (OperandArgNum opAndArg : sortedArgs)
        {
            if ((opAndArg.operand->gtOper == GT_CNS_INT) && opAndArg.operand->IsIconHandle())
            {
                void* iconValue = (void*)(opAndArg.operand->AsIntCon()->IconValue());
                const char* methodTableName = (*_getMangledSymbolName)(_thisPtr, iconValue);
                (*_addCodeReloc)(_thisPtr, iconValue);
                argVec.push_back(castIfNecessary(_builder.CreateLoad(castIfNecessary(getOrCreateExternalSymbol(methodTableName), Type::getInt32PtrTy(_llvmContext)->getPointerTo())), llvmFunc->getArg(argIx)->getType()));
            }
            else
            {
                argVec.push_back(consumeValue(opAndArg.operand, llvmFunc->getArg(argIx)->getType()));
            }
            argIx++;
        }
        // TODO-LLVM: If the block has a handler, this will need to be an invoke.  E.g. create a CallOrInvoke as per ILToLLVMImporter
        mapGenTreeToValue(call, _builder.CreateCall(llvmFunc, llvm::ArrayRef<Value*>(argVec)));
        if (call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_THROW))
        {
            _builder.CreateUnreachable();
        }
    }
}

void Llvm::buildCall(GenTree* node)
{
    GenTreeCall* call = node->AsCall();
    if (call->gtCallType == CT_HELPER)
    {
        buildHelperFuncCall(call);
    }
    else if ((call->gtCallType == CT_USER_FUNC || call->gtCallType == CT_INDIRECT) &&
             !call->IsVirtualStub() /* TODO: Virtual stub not implemented */)
    {
        buildUserFuncCall(call);
    }
    else
    {
        failFunctionCompilation();
    }
}

void Llvm::buildCast(GenTreeCast* cast)
{
    var_types castFromType  = genActualType(cast->CastOp());
    var_types castToType    = cast->CastToType();
    Value*    castFromValue = consumeValue(cast->CastOp(), getLlvmTypeForVarType(castFromType));
    Value*    castValue     = nullptr;

    switch (castFromType)
    {
        case TYP_INT:
        case TYP_LONG:
            switch (castToType)
            {
                case TYP_BOOL:
                case TYP_BYTE:
                case TYP_UBYTE:
                case TYP_SHORT:
                case TYP_USHORT:
                case TYP_INT:
                case TYP_UINT:
                    // "Cast(integer -> small type)" is "s/zext<int>(truncate<small type>)".
                    // Here we will truncate and leave the extension for the user to consume.
                    castValue = _builder.CreateTrunc(castFromValue, getLlvmTypeForVarType(castToType));
                    break;

                case TYP_LONG:
                    castValue = cast->IsUnsigned()
                                    ? _builder.CreateZExt(castFromValue, getLlvmTypeForVarType(castToType))
                                    : _builder.CreateSExt(castFromValue, getLlvmTypeForVarType(castToType));
                    break;

                case TYP_DOUBLE:
                    castValue = cast->IsUnsigned()
                                    ? _builder.CreateUIToFP(castFromValue, getLlvmTypeForVarType(castToType))
                                    : _builder.CreateSIToFP(castFromValue, getLlvmTypeForVarType(castToType));
                    break;

                default:
                    failFunctionCompilation(); // NYI
            }
            break;

        case TYP_FLOAT:
        case TYP_DOUBLE:
            switch (castToType)
            {
                case TYP_FLOAT:
                case TYP_DOUBLE:
                    castValue = _builder.CreateFPCast(castFromValue, getLlvmTypeForVarType(TYP_DOUBLE));
                    break;
                case TYP_BYTE:
                case TYP_SHORT:
                case TYP_INT:
                case TYP_LONG:
                    castValue = _builder.CreateFPToSI(castFromValue, getLlvmTypeForVarType(castToType));
                    break;

                case TYP_BOOL:
                case TYP_UBYTE:
                case TYP_USHORT:
                case TYP_UINT:
                case TYP_ULONG:
                    castValue = _builder.CreateFPToUI(castFromValue, getLlvmTypeForVarType(castToType));
                    break;

                default:
                    unreached();
            }
            break;

        default:
            failFunctionCompilation(); // NYI
    }

    mapGenTreeToValue(cast, castValue);
}

void Llvm::buildCnsDouble(GenTreeDblCon* node)
{
    if (node->TypeIs(TYP_DOUBLE))
    {
        mapGenTreeToValue(node, llvm::ConstantFP::get(Type::getDoubleTy(_llvmContext), node->gtDconVal));
    }
    else
    {
        assert(node->TypeIs(TYP_FLOAT));
        mapGenTreeToValue(node, llvm::ConstantFP::get(Type::getFloatTy(_llvmContext), node->gtDconVal));
    }
}

void Llvm::buildCnsInt(GenTree* node)
{
    if (node->gtType == TYP_INT)
    {
        if (node->IsIconHandle())
        {
            // TODO-LLVM : consider lowering these to "IND(CLS_VAR_ADDR)"
            if (node->IsIconHandle(GTF_ICON_TOKEN_HDL) || node->IsIconHandle(GTF_ICON_CLASS_HDL) ||
                node->IsIconHandle(GTF_ICON_METHOD_HDL) || node->IsIconHandle(GTF_ICON_FIELD_HDL))
            {
                const char* symbolName = (*_getMangledSymbolName)(_thisPtr, (void*)(node->AsIntCon()->IconValue()));
                (*_addCodeReloc)(_thisPtr, (void*)node->AsIntCon()->IconValue());
                mapGenTreeToValue(node, _builder.CreateLoad(getOrCreateExternalSymbol(symbolName)));
            }
            else
            {
                //TODO-LLVML: other ICON handle types
                failFunctionCompilation();
            }
        }
        else
        {
            mapGenTreeToValue(node, _builder.getInt32(node->AsIntCon()->IconValue()));
        }
        return;
    }
    if (node->gtType == TYP_REF)
    {
        ssize_t intCon = node->AsIntCon()->gtIconVal;
        if (node->IsIconHandle(GTF_ICON_STR_HDL))
        {
            const char* symbolName = (*_getMangledSymbolName)(_thisPtr, (void *)(node->AsIntCon()->IconValue()));
            (*_addCodeReloc)(_thisPtr, (void*)node->AsIntCon()->IconValue());
            mapGenTreeToValue(node, _builder.CreateLoad(getOrCreateExternalSymbol(symbolName)));
            return;
        }
        // TODO: delete this check, just handling string constants and null ptr stores for now, other TYP_REFs not implemented yet
        if (intCon != 0)
        {
            failFunctionCompilation();
        }

        mapGenTreeToValue(node, _builder.CreateIntToPtr(_builder.getInt32(intCon), Type::getInt8PtrTy(_llvmContext))); // TODO: wasm64
        return;
    }
    failFunctionCompilation();
}

void Llvm::buildInd(GenTree* node, Value* ptr)
{
    // cast the pointer to create the correct load instructions
    mapGenTreeToValue(node, _builder.CreateLoad(
                                 castIfNecessary(ptr,
                                     getLlvmTypeForVarType(node->TypeGet())->getPointerTo())));
}

void Llvm::buildObj(GenTreeObj* node)
{
    // cast the pointer to create the correct load instructions
    mapGenTreeToValue(node, _builder.CreateLoad(
                          castIfNecessary(getGenTreeValue(node->AsOp()->gtOp1),
                                          getLlvmTypeForStruct(node->GetLayout())->getPointerTo())));
}

Value* Llvm::buildJTrue(GenTree* node, Value* opValue)
{
    return _builder.CreateCondBr(opValue, getLLVMBasicBlockForBlock(_currentBlock->bbJumpDest), getLLVMBasicBlockForBlock(_currentBlock->bbNext));
}

void Llvm::buildCmp(GenTree* node, Value* op1, Value* op2)
{
    llvm::CmpInst::Predicate llvmPredicate;

    bool isIntOrPtr = op1->getType()->isIntOrPtrTy();
    switch (node->OperGet())
    {
        case GT_EQ:
            llvmPredicate = isIntOrPtr ? llvm::CmpInst::Predicate::ICMP_EQ : llvm::CmpInst::Predicate::FCMP_OEQ;
            break;
        case GT_NE:
            llvmPredicate = isIntOrPtr ? llvm::CmpInst::Predicate::ICMP_NE : llvm::CmpInst::Predicate::FCMP_ONE;
            break;
        case GT_LE:
            llvmPredicate = isIntOrPtr ? (node->IsUnsigned() ? llvm::CmpInst::Predicate::ICMP_ULE : llvm::CmpInst::Predicate::ICMP_SLE)
                : llvm::CmpInst::Predicate::FCMP_OLE;
            break;
        case GT_LT:
            llvmPredicate = isIntOrPtr ? (node->IsUnsigned() ? llvm::CmpInst::Predicate::ICMP_ULT : llvm::CmpInst::Predicate::ICMP_SLT)
                : llvm::CmpInst::Predicate::FCMP_OLT;
            break;
        case GT_GE:
            llvmPredicate = isIntOrPtr ? (node->IsUnsigned() ? llvm::CmpInst::Predicate::ICMP_UGE : llvm::CmpInst::Predicate::ICMP_SGE)
                : llvm::CmpInst::Predicate::FCMP_OGE;
            break;
        case GT_GT:
            llvmPredicate = isIntOrPtr ? (node->IsUnsigned() ? llvm::CmpInst::Predicate::ICMP_UGT : llvm::CmpInst::Predicate::ICMP_SGT)
                : llvm::CmpInst::Predicate::FCMP_OGT;
            break;
        default:
            failFunctionCompilation(); // TODO all genTreeOps values

    }
    // comparing refs and ints is valid LIR, but not LLVM so handle that case by converting the int to a ref
    if (op1->getType() != op2->getType())
    {
        if (op1->getType()->isPointerTy() && op2->getType()->isIntegerTy())
        {
            op2 = _builder.CreateIntToPtr(op2, op1->getType());
        }
        else if (op2->getType()->isPointerTy() && op1->getType()->isIntegerTy())
        {
            op1 = _builder.CreateIntToPtr(op1, op2->getType());
        }
        else
        {
            // TODO-LLVM: other valid LIR comparisons
            failFunctionCompilation();
        }
    }
    mapGenTreeToValue(node, _builder.CreateCmp(llvmPredicate, op1, op2));
}

// in case we haven't seen the phi args yet, create just the phi nodes and fill in the args at the end
void Llvm::buildEmptyPhi(GenTreePhi* phi)
{
    llvm::PHINode* llvmPhiNode = _builder.CreatePHI(getLlvmTypeForVarType(phi->TypeGet()), phi->NumChildren());
    _phiPairs.push_back({ phi, llvmPhiNode });
    mapGenTreeToValue(phi, llvmPhiNode);
}

void Llvm::fillPhis()
{
    for (PhiPair phiPair : _phiPairs)
    {
        llvm::PHINode* llvmPhiNode = phiPair.llvmPhiNode;

        for (GenTreePhi::Use& use : phiPair.irPhiNode->Uses())
        {
            GenTreePhiArg* phiArg = use.GetNode()->AsPhiArg();
            unsigned       lclNum = phiArg->GetLclNum();
            unsigned       ssaNum = phiArg->GetSsaNum();

            Value* localPhiArg = nullptr;
            auto iter = _localsMap->find({ lclNum, ssaNum });
            if (iter == _localsMap->end())
            {
                //TODO-LLVM: Uninitialised locals are caught here as they would fail on the equivalent assert below.
                // See https://github.com/dotnet/runtimelab/pull/1744#discussion_r757791229
                if (!(_compiler->lvaIsParameter(lclNum) && ssaNum == SsaConfig::FIRST_SSA_NUM))
                {
                    failFunctionCompilation();
                }
                // Arguments are implicitly defined on entry to the method.
                assert(_compiler->lvaIsParameter(lclNum) && ssaNum == SsaConfig::FIRST_SSA_NUM);
                LlvmArgInfo  llvmArgInfo = getLlvmArgInfoForArgIx(lclNum);
                localPhiArg = _function->getArg(llvmArgInfo.m_argIx);
            }
            else
            {
                localPhiArg = iter->second;
            }

            Value* phiRealArgValue;
            llvm::Instruction* castRequired = getCast(localPhiArg, llvmPhiNode->getType());
            if (castRequired != nullptr)
            {
                // This cast is needed when
                // 1) The phi arg real type is short and the definition is the actual longer type, e.g. for bool/int
                // 2) There is a pointer difference, e.g. i8* v i32* and perhaps different levels of indirection: i8** and i8*
                llvm::BasicBlock::iterator phiInsertPoint = _builder.GetInsertPoint();
                llvm::BasicBlock* phiBlock = _builder.GetInsertBlock();
                llvm::Instruction* predBlockTerminator = getLLVMBasicBlockForBlock(phiArg->gtPredBB)->getTerminator();

                _builder.SetInsertPoint(predBlockTerminator);
                phiRealArgValue = _builder.Insert(castRequired);
                _builder.SetInsertPoint(phiBlock, phiInsertPoint);
            }
            else
            {
                phiRealArgValue = localPhiArg;
            }
            llvmPhiNode->addIncoming(phiRealArgValue, getLLVMBasicBlockForBlock(phiArg->gtPredBB));
        }
    }
}

void Llvm::buildUnaryOperation(GenTree* node)
{
    Value* result;
    Value* op1Value = consumeValue(node->gtGetOp1(), getLlvmTypeForVarType(node->TypeGet()));

    switch (node->OperGet())
    {
        case GT_NEG:
            if (op1Value->getType()->isFloatingPointTy())
            {
                result = _builder.CreateFNeg(op1Value, "fneg");
            }
            else
            {
                result = _builder.CreateNeg(op1Value, "neg");
            }
            break;
        case GT_NOT:
            result = _builder.CreateNot(op1Value, "not");
            break;
        default:
            failFunctionCompilation();  // TODO-LLVM: other binary operators
    }
    mapGenTreeToValue(node, result);
}

void Llvm::buildBinaryOperation(GenTree* node)
{
    Value* result;
    Type*  targetType = getLlvmTypeForVarType(node->TypeGet());
    Value* op1 = consumeValue(node->gtGetOp1(), targetType);
    Value* op2 = consumeValue(node->gtGetOp2(), targetType);

    switch (node->OperGet())
    {
        case GT_AND:
            result = _builder.CreateAnd(op1, op2, "and");
            break;
        case GT_OR:
            result = _builder.CreateOr(op1, op2, "or");
            break;
        case GT_XOR:
            result = _builder.CreateXor(op1, op2, "xor");
            break;
        default:
            failFunctionCompilation();  // TODO-LLVM: other binary operaions
    }
    mapGenTreeToValue(node, result);
}

void Llvm::buildShift(GenTreeOp* node)
{
    Type*  llvmTargetType = getLlvmTypeForVarType(node->TypeGet());
    Value* numBitsToShift = consumeValue(node->gtOp2, getLlvmTypeForVarType(node->gtOp2->TypeGet()));

    // LLVM requires the operands be the same type as the shift itself.
    // Shift counts are assumed to never be negative, so we zero extend.
    if (numBitsToShift->getType()->getPrimitiveSizeInBits() < llvmTargetType->getPrimitiveSizeInBits())
    {
        numBitsToShift = _builder.CreateZExt(numBitsToShift, llvmTargetType);
    }

    Value* op1Value = consumeValue(node->gtOp1, llvmTargetType);
    Value* result;

    switch (node->OperGet())
    {
        case GT_LSH:
            result = _builder.CreateShl(op1Value, numBitsToShift, "lsh");
            break;
        case GT_RSH:
            result = _builder.CreateAShr(op1Value, numBitsToShift, "rsh");
            break;
        case GT_RSZ:
            result = _builder.CreateLShr(op1Value, numBitsToShift, "rsz");
            break;
        default:
            failFunctionCompilation();  // TODO-LLVM: other shift types
    }
    mapGenTreeToValue(node, result);
}

void Llvm::buildReturn(GenTree* node)
{
    switch (node->TypeGet())
    {
        case TYP_BOOL:
        case TYP_BYTE:
        case TYP_UBYTE:
        case TYP_SHORT:
        case TYP_USHORT:
        case TYP_INT:
        case TYP_UINT:
        case TYP_LONG:
        case TYP_ULONG:
            _builder.CreateRet(castIfNecessary(getGenTreeValue(node->gtGetOp1()), getLlvmTypeForCorInfoType(_sigInfo.retType, _sigInfo.retTypeClass)));
            return;
        case TYP_VOID:
            _builder.CreateRetVoid();
            return;
        default:
            failFunctionCompilation();
    }
}

void Llvm::buildStoreInd(GenTreeStoreInd* storeIndOp)
{
    Value* address = getGenTreeValue(storeIndOp->Addr());
    Value* toStore = getGenTreeValue(storeIndOp->Data());
    if (toStore->getType()->isPointerTy() && (storeIndOp->gtFlags & GTF_IND_TGT_NOT_HEAP) == 0)
    {
        // RhpAssignRef will never reverse PInvoke, so do not need to store the shadow stack here
        _builder.CreateCall(getOrCreateRhpAssignRef(), ArrayRef<Value*>{address, castIfNecessary(toStore, Type::getInt8PtrTy(_llvmContext))});
    }
    else
    {
        castingStore(toStore, address, storeIndOp->gtType);
    }
}

// Copies endOffset - startOffset bytes, endOffset is exclusive.
unsigned Llvm::buildMemCpy(Value* baseAddress, unsigned startOffset, unsigned endOffset, Value* srcAddress)
{
    Value* destAddress = _builder.CreateGEP(baseAddress, _builder.getInt32(startOffset));
    unsigned size = endOffset - startOffset;

    _builder.CreateMemCpy(destAddress, llvm::Align(), srcAddress, llvm::Align(), size);

    return size;
}

void Llvm::storeObjAtAddress(Value* baseAddress, Value* data, StructDesc* structDesc)
{
    unsigned fieldCount = structDesc->getFieldCount();
    unsigned bytesStored = 0;

    for (unsigned i = 0; i < fieldCount; i++)
    {
        FieldDesc* fieldDesc = structDesc->getFieldDesc(i);
        unsigned   fieldOffset = fieldDesc->getFieldOffset();
        Value*     address     = _builder.CreateGEP(baseAddress, _builder.getInt32(fieldOffset));

        if (structDesc->hasSignificantPadding() && fieldOffset > bytesStored)
        {
            bytesStored += buildMemCpy(baseAddress, bytesStored, fieldOffset, address);
        }

        Value* fieldData = nullptr;
        if (data->getType()->isStructTy())
        {
            const llvm::StructLayout* structLayout = _module->getDataLayout().getStructLayout(static_cast<llvm::StructType*>(data->getType()));

            unsigned llvmFieldIndex = structLayout->getElementContainingOffset(fieldOffset);
            fieldData               = _builder.CreateExtractValue(data, llvmFieldIndex);
        }
        else
        {
            // single field IL structs are not LLVM structs
            fieldData = data;
        }

        if (fieldData->getType()->isStructTy())
        {
            assert(fieldDesc->getClassHandle() != NO_CLASS_HANDLE);

            // recurse into struct
            storeObjAtAddress(address, fieldData, getStructDesc(fieldDesc->getClassHandle()));

            bytesStored += fieldData->getType()->getPrimitiveSizeInBits() / BITS_PER_BYTE;
        }
        else
        {
            if (fieldDesc->isGcPointer())
            {
                _builder.CreateCall(getOrCreateRhpAssignRef(),
                                    ArrayRef<Value*>{address,
                                                     castIfNecessary(fieldData, Type::getInt8PtrTy(_llvmContext))});
                bytesStored += TARGET_POINTER_SIZE;
            }
            else
            {
                _builder.CreateStore(fieldData, castIfNecessary(address, fieldData->getType()->getPointerTo()));

                bytesStored += fieldData->getType()->getPrimitiveSizeInBits() / BITS_PER_BYTE;
            }
        }
    }

    unsigned llvmStructSize = data->getType()->getPrimitiveSizeInBits() / BITS_PER_BYTE;
    if (structDesc->hasSignificantPadding() && llvmStructSize > bytesStored)
    {
        Value* srcAddress = _builder.CreateGEP(baseAddress, _builder.getInt32(bytesStored));

        buildMemCpy(baseAddress, bytesStored, llvmStructSize, srcAddress);
    }
}

void Llvm::buildStoreObj(GenTreeObj* storeOp)
{
    if (!storeOp->OperIsBlk())
    {
        failFunctionCompilation(); // cant get struct handle. TODO-LLVM: try as an assert
    }

    ClassLayout* structLayout = storeOp->GetLayout();

    Value* baseAddressValue = getGenTreeValue(storeOp->Addr());

    // zero initialization  check
    GenTree* dataOp = storeOp->Data();
    if (dataOp->IsIntegralConst(0))
    {
        _builder.CreateMemSet(baseAddressValue, _builder.getInt8(0), _builder.getInt32(structLayout->GetSize()), {});
        return;
    }

    CORINFO_CLASS_HANDLE structClsHnd  = structLayout->GetClassHandle();
    StructDesc*          structDesc    = getStructDesc(structClsHnd);
    bool targetNotHeap = ((storeOp->gtFlags & GTF_IND_TGT_NOT_HEAP) != 0) || storeOp->Addr()->OperIsLocalAddr();

    Value* dataValue = getGenTreeValue(storeOp->Data());
    if (targetNotHeap)
    {
        _builder.CreateStore(dataValue, castIfNecessary(baseAddressValue, dataValue->getType()->getPointerTo())); 
    }
    else
    {
        storeObjAtAddress(baseAddressValue, dataValue, structDesc);
    }
}

Value* Llvm::localVar(GenTreeLclVar* lclVar)
{
    Value*       llvmRef;
    unsigned int lclNum = lclVar->GetLclNum();
    unsigned int ssaNum = lclVar->GetSsaNum();
    LclVarDsc*   varDsc = _compiler->lvaGetDesc(lclVar);

    if (isLlvmFrameLocal(varDsc))
    {
        llvmRef = _builder.CreateLoad(m_allocas[lclNum]);
    }
    else if (_localsMap->find({lclNum, ssaNum}) == _localsMap->end())
    {
        if (varDsc->lvLlvmArgNum != BAD_LLVM_ARG_NUM)
        {
            // TODO-LLVM: we dont handle this yet
            if (_info.compRetBuffArg != BAD_VAR_NUM)
            {
                failFunctionCompilation();
            }

            llvmRef = _function->getArg(varDsc->lvLlvmArgNum);
            _localsMap->insert({{lclNum, ssaNum}, llvmRef});
        }
        else
        {
            // unhandled scenario, local is not defined already, and is not a parameter
            failFunctionCompilation();
        }
    }
    else
    {
        llvmRef = _localsMap->at({lclNum, ssaNum});
    }

    // implicit truncating from long to int
    if (llvmRef->getType() == Type::getInt64Ty(_llvmContext) && lclVar->TypeIs(TYP_INT))
    {
        llvmRef = _builder.CreateTrunc(llvmRef, Type::getInt32Ty(_llvmContext));
    }

    mapGenTreeToValue(lclVar, llvmRef);
    return llvmRef;
}

void Llvm::buildLocalField(GenTreeLclFld* lclFld)
{
    assert(!lclFld->TypeIs(TYP_STRUCT));

    unsigned   lclNum = lclFld->GetLclNum();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    assert(isLlvmFrameLocal(varDsc));

    // TODO-LLVM: if this is an only value type field, or at offset 0, we can optimize.
    Value* structAddrValue = m_allocas[lclNum];
    Value* structAddrInt8Ptr = castIfNecessary(structAddrValue, Type::getInt8PtrTy(_llvmContext));
    Value* fieldAddressValue = _builder.CreateGEP(structAddrInt8Ptr, _builder.getInt16(lclFld->GetLclOffs()));
    Value* fieldAddressTypedValue =
        castIfNecessary(fieldAddressValue, getLlvmTypeForVarType(lclFld->TypeGet())->getPointerTo());

    mapGenTreeToValue(lclFld, _builder.CreateLoad(fieldAddressTypedValue));
}

void Llvm::buildLocalVarAddr(GenTreeLclVarCommon* lclAddr)
{
    unsigned int lclNum = lclAddr->GetLclNum();
    if (lclAddr->isLclField())
    {
        Value* bytePtr = castIfNecessary(m_allocas[lclNum], Type::getInt8PtrTy(_llvmContext));
        mapGenTreeToValue(lclAddr, _builder.CreateGEP(bytePtr, _builder.getInt16(lclAddr->GetLclOffs())));
    }
    else
    {
        mapGenTreeToValue(lclAddr, m_allocas[lclNum]);
    }
}

// LLVM operations like ICmpNE return an i1, but in the IR it is expected to be an Int (i32).
/* E.g.
*                                                 /--*  t10    int
                                                  +--*  t11    int
N009 ( 30, 14) [000012] ---XG-------        t12 = *  NE        int
                                                  /--*  t12    int
N011 ( 34, 17) [000016] DA-XG-------              *  STORE_LCL_VAR int    V03 loc1
*/
Value* Llvm::zextIntIfNecessary(Value* intValue)
{
    llvm::TypeSize intSize = intValue->getType()->getPrimitiveSizeInBits();
    if (intSize < TARGET_POINTER_SIZE * 8)
    {
        return _builder.CreateIntCast(intValue, Type::getInt32Ty(_llvmContext), false);
    }
    return intValue;
}

bool Llvm::isLlvmFrameLocal(LclVarDsc* varDsc)
{
    assert(canStoreLocalOnLlvmStack(varDsc) && (_compiler->fgSsaPassesCompleted >= 1));
    return !varDsc->lvInSsa;
}

void Llvm::storeLocalVar(GenTreeLclVar* lclVar)
{
    Type*  destLlvmType = getLlvmTypeForLclVar(lclVar);
    Value* localValue   = nullptr;

    // zero initialization check
    if (lclVar->TypeIs(TYP_STRUCT) && lclVar->gtGetOp1()->IsIntegralConst(0))
    {
        localValue = llvm::Constant::getNullValue(destLlvmType);
    }
    else
    {
        localValue = consumeValue(lclVar->gtGetOp1(), destLlvmType);
    }

    unsigned lclNum = lclVar->GetLclNum();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar);

    if (isLlvmFrameLocal(varDsc))
    {
        Value* lclAddressValue = m_allocas[lclNum];
        _builder.CreateStore(localValue, castIfNecessary(lclAddressValue, destLlvmType->getPointerTo()));
    }
    else
    {
        if (varDsc->lvIsParam) // TODO-LLVM: not doing params yet
        {
            failFunctionCompilation();
        }

        SsaPair ssaPair = {lclNum, lclVar->GetSsaNum()};
        _localsMap->insert({ssaPair, localValue});
    }
}

void Llvm::visitNode(GenTree* node)
{
    genTreeOps oper = node->OperGet();
    switch (oper)
    {
        case GT_ADD:
            buildAdd(node, getGenTreeValue(node->AsOp()->gtOp1), getGenTreeValue(node->AsOp()->gtOp2));
            break;
        case GT_DIV:
            buildDiv(node);
            break;
        case GT_CALL:
            buildCall(node);
            break;
        case GT_CAST:
            buildCast(node->AsCast());
            break;
        case GT_CNS_DBL:
            buildCnsDouble(node->AsDblCon());
            break;
        case GT_CNS_INT:
            buildCnsInt(node);
            break;
        case GT_IL_OFFSET:
            _currentOffset = node->AsILOffset()->gtStmtILoffsx;
            _currentOffsetDiLocation = nullptr;
            break;
        case GT_IND:
            buildInd(node, getGenTreeValue(node->AsOp()->gtOp1));
            break;
        case GT_JTRUE:
            buildJTrue(node, getGenTreeValue(node->AsOp()->gtOp1));
            break;
        case GT_LCL_FLD:
            buildLocalField(node->AsLclFld());
            break;
        case GT_LCL_VAR:
            localVar(node->AsLclVar());
            break;
        case GT_LCL_VAR_ADDR:
        case GT_LCL_FLD_ADDR:
            buildLocalVarAddr(node->AsLclVarCommon());
            break;
        case GT_LSH:
        case GT_RSH:
        case GT_RSZ:
            buildShift(node->AsOp());
            break;
        case GT_EQ:
        case GT_NE:
        case GT_LE:
        case GT_LT:
        case GT_GE:
        case GT_GT:
            buildCmp(node, getGenTreeValue(node->AsOp()->gtOp1), getGenTreeValue(node->AsOp()->gtOp2));
            break;
        case GT_NEG:
        case GT_NOT:
            buildUnaryOperation(node);
            break;
        case GT_NO_OP:
            emitDoNothingCall();
            break;
        case GT_OBJ:
            buildObj(node->AsObj());
            break;
        case GT_PHI:
            buildEmptyPhi(node->AsPhi());
            break;
        case GT_PHI_ARG:
        case GT_PUTARG_TYPE:
            break;
        case GT_RETURN:
            buildReturn(node);
            break;
        case GT_STORE_LCL_VAR:
            storeLocalVar(node->AsLclVar());
            break;
        case GT_STOREIND:
            buildStoreInd(node->AsStoreInd());
            break;
        case GT_STORE_OBJ:
            buildStoreObj(node->AsObj());
            break;
        case GT_AND:
        case GT_OR:
        case GT_XOR:
            buildBinaryOperation(node);
            break;
        default:
            failFunctionCompilation();
    }
}

void Llvm::startImportingBasicBlock(BasicBlock* block)
{
    _currentBlock = block;
}

void Llvm::endImportingBasicBlock(BasicBlock* block)
{
    if ((block->bbJumpKind == BBjumpKinds::BBJ_NONE) && block->bbNext != nullptr)
    {
        _builder.CreateBr(getLLVMBasicBlockForBlock(block->bbNext));
        return;
    }
    if ((block->bbJumpKind == BBjumpKinds::BBJ_ALWAYS) && block->bbJumpDest != nullptr)
    {
        _builder.CreateBr(getLLVMBasicBlockForBlock(block->bbJumpDest));
        return;
    }
    //TODO: other jump kinds
}

void Llvm::generateProlog()
{
    // create a prolog block to store arguments passed on shadow stack, TODO: other things from ILToLLVMImporter to come
    llvm::BasicBlock* prologBlock = llvm::BasicBlock::Create(_llvmContext, "Prolog", _function);
    _prologBuilder.SetInsertPoint(prologBlock);

    createAllocasForLocalsWithAddrOp();

    llvm::BasicBlock* block0 = getLLVMBasicBlockForBlock(_compiler->fgFirstBB);
    _prologBuilder.SetInsertPoint(_prologBuilder.CreateBr(block0)); // position _prologBuilder to add locals and arguments
    _builder.SetInsertPoint(block0);
}

struct DebugMetadata Llvm::getOrCreateDebugMetadata(const char* documentFileName)
{
    std::string fullPath = documentFileName;

    struct DebugMetadata debugMetadata;
    auto findResult = _debugMetadataMap.find(fullPath);
    if (findResult == _debugMetadataMap.end())
    {
        // check Unix and Windows path styles
        std::size_t botDirPos = fullPath.find_last_of("/");
        if (botDirPos == std::string::npos)
        {
            botDirPos = fullPath.find_last_of("\\");
        }
        std::string directory = ""; // is it possible there is never a directory?
        std::string fileName;
        if (botDirPos != std::string::npos)
        {
            directory = fullPath.substr(0, botDirPos);
            fileName = fullPath.substr(botDirPos + 1, fullPath.length());
        }
        else
        {
            fileName = fullPath;
        }

        _diBuilder                 = new llvm::DIBuilder(*_module);
        llvm::DIFile* fileMetadata = _diBuilder->createFile(fileName, directory);

        // TODO: get the right value for isOptimized
        llvm::DICompileUnit* compileUnit =
            _diBuilder->createCompileUnit(llvm::dwarf::DW_LANG_C /* no dotnet choices in the enum */, fileMetadata,
                                          "ILC",
                                       0 /* Optimized */, "", 1, "", llvm::DICompileUnit::DebugEmissionKind::FullDebug,
                                       0, 0, 0, llvm::DICompileUnit::DebugNameTableKind::Default, false, "");

        debugMetadata = {fileMetadata, compileUnit};
        _debugMetadataMap.insert({fullPath, debugMetadata});
    }
    else debugMetadata = findResult->second;

    return debugMetadata;
}

llvm::DILocation* Llvm::createDebugFunctionAndDiLocation(struct DebugMetadata debugMetadata, unsigned int lineNo)
{
    if (_debugFunction == nullptr)
    {
        llvm::DISubroutineType* functionMetaType = _diBuilder->createSubroutineType({} /* TODO - function parameter types*/, llvm::DINode::DIFlags::FlagZero);
        uint32_t lineNumber = _firstSequencePointLineNumber(_thisPtr);

        const char* methodName = _info.compCompHnd->getMethodName(_info.compMethodHnd, nullptr);
        _debugFunction = _diBuilder->createFunction(debugMetadata.fileMetadata, methodName,
                                                    methodName, debugMetadata.fileMetadata, lineNumber,
                                                    functionMetaType, lineNumber, llvm::DINode::DIFlags::FlagZero,
                                                    llvm::DISubprogram::DISPFlags::SPFlagDefinition |
                                                        llvm::DISubprogram::DISPFlags::SPFlagLocalToUnit);
        _function->setSubprogram(_debugFunction);
    }
    return llvm::DILocation::get(_llvmContext, lineNo, 0, _debugFunction);
}

void Llvm::startImportingNode()
{
    if (_debugMetadata.diCompileUnit != nullptr && _currentOffsetDiLocation == nullptr)
    {
        unsigned int lineNo = _getOffsetLineNumber(_thisPtr, _currentOffset);

        _currentOffsetDiLocation = createDebugFunctionAndDiLocation(_debugMetadata, lineNo);
        _builder.SetCurrentDebugLocation(_currentOffsetDiLocation);
    }
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

#ifdef DEBUG
    char* txtFileName = (char*)malloc(strlen(_outputFileName) + 2); // .txt is longer than .bc
    strcpy(txtFileName, _outputFileName);
    strcpy(txtFileName + strlen(_outputFileName) - 2, "txt");
    llvm::raw_fd_ostream textOutputStream(txtFileName, ec);
    _module->print(textOutputStream, (llvm::AssemblyAnnotationWriter*)NULL);
    free(txtFileName);

    // verifyModule returns true when its broken, so invert
    assert(!llvm::verifyModule(*_module, &llvm::errs()));
#endif //DEBUG

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
                assert(!lclVar->TypeIs(TYP_STRUCT));
                indirOper = GT_IND;
                break;
            case GT_LCL_VAR_ADDR:
            case GT_LCL_FLD_ADDR:
                indirOper = GT_NONE;
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
    if (callNode->gtCallType != CT_INDIRECT && _isRuntimeImport(_thisPtr, callNode->gtCallMethHnd))
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
            regNumber      argReg         = curArgTabEntry->GetRegNum();
            if (argReg == REG_STK || curArgTabEntry->argType == TYP_BYREF) // TODO-LLVM: out and ref args
            {
                failFunctionCompilation();
            }
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

        bool argOnShadowStack = isThis || (isSigArg && !canStoreArgOnLlvmStack(corInfoType, clsHnd));
        if (argOnShadowStack)
        {
            GenTree* lclShadowStack = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);

            if (corInfoType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffest = padOffset(corInfoType, clsHnd, shadowStackUseOffest);
            }

            GenTreeIntCon* offset    = _compiler->gtNewIconNode(_shadowStackLocalsSize + shadowStackUseOffest, TYP_I_IMPL);
            GenTree*       slotAddr  = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, lclShadowStack, offset);
            GenTree*       storeNode =
                createShadowStackStoreNode(opAndArg.operand->TypeGet(), slotAddr, opAndArg.operand,
                                corInfoType == CORINFO_TYPE_VALUECLASS ? _compiler->typGetObjLayout(clsHnd) : nullptr);

            if (corInfoType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffest = padNextOffset(corInfoType, clsHnd, shadowStackUseOffest);
            }
            else
            {
                shadowStackUseOffest += TARGET_POINTER_SIZE;
            }

            CurrentRange().InsertBefore(callNode, lclShadowStack, offset, slotAddr, storeNode);
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

void Llvm::lowerToShadowStack()
{
    for (BasicBlock* _currentBlock : _compiler->Blocks())
    {
        _currentRange = &LIR::AsRange(_currentBlock);
        for (GenTree* node : CurrentRange())
        {
            if (node->OperIs(GT_STORE_LCL_VAR, GT_LCL_VAR, GT_LCL_FLD, GT_LCL_VAR_ADDR, GT_LCL_FLD_ADDR))
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
            else if (node->OperIs(GT_STORE_LCL_VAR))
            {
                lowerStoreLcl(node->AsLclVarCommon());
            }
        }
    }
}

//------------------------------------------------------------------------
// Convert GT_STORE_LCL_VAR and GT_LCL_VAR to use the shadow stack when the local needs to be GC tracked,
// rewrite calls that returns GC types to do so via a store to a passed in address on the shadow stack.
// Likewise, store the returned value there if required.
//
void Llvm::PlaceAndConvertShadowStackLocals()
{
    populateLlvmArgNums();

    _shadowStackLocalsSize = 0;

    std::vector<LclVarDsc*> locals;
    unsigned localsParamCount = 0;

    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
        if (!canStoreLocalOnLlvmStack(varDsc))
        {
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

void Llvm::createAllocasForLocalsWithAddrOp()
{
    m_allocas = std::vector<Value*>(_compiler->lvaCount, nullptr);

    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        // TODO-LLVM: Consider turning off FEATURE_FIXED_OUT_ARGS.
        if(_compiler->lvaOutgoingArgSpaceVar == lclNum)
        {
            continue;
        }

        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        if (canStoreLocalOnLlvmStack(varDsc) && isLlvmFrameLocal(varDsc))
        {
            CORINFO_CLASS_HANDLE classHandle = tryGetStructClassHandle(varDsc);
            Type* llvmType = getLlvmTypeForCorInfoType(toCorInfoType(varDsc->TypeGet()), classHandle);
            Value* allocaValue = _prologBuilder.CreateAlloca(llvmType);
            m_allocas[lclNum] = allocaValue;

            if (varDsc->lvIsParam)
            {
                LlvmArgInfo argInfo = getLlvmArgInfoForArgIx(lclNum);
                assert(argInfo.IsLlvmArg());
                Value* dataValue = _function->getArg(argInfo.m_argIx);
                _prologBuilder.CreateStore(dataValue, castIfNecessary(allocaValue, dataValue->getType()->getPointerTo(),
                                                                      &_prologBuilder));
            }
        }
    }
}

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
// TODO-LLVM: A legal optimization here is to not GC-report things if we know they're not on the heap (say this is actually a local). 
void Llvm::Compile()
{
    CompAllocator allocator = _compiler->getAllocator();
    BlkToLlvmBlkVectorMap blkToLlvmBlkVectorMap(allocator);
    _blkToLlvmBlkVectorMap = &blkToLlvmBlkVectorMap;
    std::unordered_map<GenTree*, Value*> sdsuMap;
    _sdsuMap = &sdsuMap;
    _localsMap = new std::unordered_map<SsaPair, Value*, SsaPairHash>();
    const char* mangledName = (*_getMangledMethodName)(_thisPtr, _info.compMethodHnd);
    _function = _module->getFunction(mangledName);
    _debugFunction = nullptr;
    _debugMetadata.diCompileUnit = nullptr;

    if (_function == nullptr)
    {
        _function = Function::Create(getFunctionType(), Function::ExternalLinkage, 0U, mangledName,
            _module); // TODO: ExternalLinkage forced as linked from old module
    }

    // mono does this via Javascript (pal_random.js), but prefer not to introduce that dependency as it limits the ability to run out of the browser.
    // Copy the temporary workaround from the IL->LLVM generator for now.
    if (!strcmp(mangledName, "S_P_CoreLib_Interop__GetRandomBytes"))
    {
        // this would normally fill the buffer parameter, but we'll just leave the buffer as is and that will be our "random" data for now
        llvm::BasicBlock* llvmBlock = llvm::BasicBlock::Create(_llvmContext, "", _function);
        _builder.SetInsertPoint(llvmBlock);
        _builder.CreateRetVoid();
        return;
    }

    if (_compiler->opts.compDbgInfo)
    {
        const char* documentFileName = _getDocumentFileName(_thisPtr);
        if (documentFileName && *documentFileName != '\0')
        {
            _debugMetadata = getOrCreateDebugMetadata(documentFileName);
        }
    }

    generateProlog();

    for (BasicBlock* block = _compiler->fgFirstBB; block; block = block->bbNext)
    {
        // TODO-LLVM: finret basic blocks
        if (block->bbJumpKind == BBJ_EHFINALLYRET)
        {
            failFunctionCompilation();
        }

        startImportingBasicBlock(block);

        llvm::BasicBlock* entry = getLLVMBasicBlockForBlock(block);
        _builder.SetInsertPoint(entry);
        for (GenTree* node : LIR::AsRange(block))
        {
            startImportingNode();
            visitNode(node);
        }
        endImportingBasicBlock(block);
    }

    fillPhis();

    if (_debugFunction != nullptr)
    {
        _diBuilder->finalizeSubprogram(_debugFunction);
    }
}
#endif
