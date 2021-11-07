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
static const char* (*_addCodeReloc)(void*, void*);
static const uint32_t (*_isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*);
static const char* (*_getDocumentFileName)(void*);
static const uint32_t (*_firstSequencePointLineNumber)(void*);
static const uint32_t (*_getOffsetLineNumber)(void*, unsigned int ilOffset);
static const uint32_t(*_structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType);
static const uint32_t(*_padOffset)(void*, CORINFO_CLASS_STRUCT_*, unsigned);
static const CorInfoTypeWithMod(*_getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*);
static const CorInfoTypeWithMod(*_getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*);

static char*                              _outputFileName;
static Function*                          _doNothingFunction;

static std::unordered_map<CORINFO_CLASS_HANDLE, Type*>* _llvmStructs = new std::unordered_map<CORINFO_CLASS_HANDLE, Type*>();


extern "C" DLLEXPORT void registerLlvmCallbacks(void*       thisPtr,
                                                const char* outputFileName,
                                                const char* triple,
                                                const char* dataLayout,
                                                const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*),
                                                const char* (*getMangledSymbolNamePtr)(void*, void*),
                                                const char* (*addCodeRelocPtr)(void*, void*),
                                                const uint32_t (*isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*),
                                                const char* (*getDocumentFileName)(void*),
                                                const uint32_t (*firstSequencePointLineNumber)(void*),
                                                const uint32_t (*getOffsetLineNumber)(void*, unsigned int),
                                                const uint32_t(*structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType),
                                                const uint32_t(*padOffset)(void*, CORINFO_CLASS_STRUCT_*, unsigned),
                                                const CorInfoTypeWithMod(*getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*),
                                                const CorInfoTypeWithMod(*getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*))
{
    _thisPtr = thisPtr;
    _getMangledMethodName         = getMangledMethodNamePtr;
    _getMangledSymbolName         = getMangledSymbolNamePtr;
    _addCodeReloc                 = addCodeRelocPtr;
    _isRuntimeImport              = isRuntimeImport;
    _getDocumentFileName          = getDocumentFileName;
    _firstSequencePointLineNumber = firstSequencePointLineNumber;
    _getOffsetLineNumber          = getOffsetLineNumber;
    _structIsWrappedPrimitive     = structIsWrappedPrimitive;
    _padOffset = padOffset;
    _getArgTypeIncludingParameterized = getArgTypeIncludingParameterized;
    _getParameterType = getParameterType;

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
    if (op->IsLocal())
    {
        unsigned lclNum = op->AsLclVar()->GetLclNum();
        if (lclNum == _shadowStackLclNum)
        {
            return _function->getArg(0);
        }
        else if (lclNum == _retAddressLclNum)
        {
            return _function->getArg(1);
        }
    }
    return _sdsuMap->at(op);
}

// maintains compatiblity with the IL->LLVM generation.  TODO-LLVM, when IL generation is no more, see if we can remove this unwrapping
bool structIsWrappedPrimitive(CORINFO_CLASS_HANDLE classHnd, CorInfoType primitiveType)
{
    return (*_structIsWrappedPrimitive)(_thisPtr, classHnd, primitiveType);
}

void addPaddingFields(unsigned paddingSize, std::vector<Type*> llvmFields)
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
                const char* name = _info.compCompHnd->getClassName(structHandle);
                llvm::StructType* llvmStructType = llvm::StructType::create(_llvmContext, _info.compCompHnd->getClassName(structHandle));
                llvmType = llvmStructType;
                unsigned fieldCnt = _info.compCompHnd->getClassNumInstanceFields(structHandle);

                std::vector<CORINFO_FIELD_HANDLE> sparseFields = std::vector<CORINFO_FIELD_HANDLE>(structSize);
                std::vector<Type*> llvmFields = std::vector<Type*>();

                for (unsigned i = 0; i < structSize; i++) sparseFields[i] = nullptr;

                for (unsigned i = 0; i < fieldCnt; i++)
                {
                    CORINFO_FIELD_HANDLE fieldHandle = _info.compCompHnd->getFieldInClass(structHandle, i);
                    unsigned fldOffset = _info.compCompHnd->getFieldOffset(fieldHandle);

                    assert(fldOffset < structSize);

                    // store the biggest field at the offset for unions
                    if (sparseFields[fldOffset] == nullptr ||
                        _info.compCompHnd->getClassSize(_info.compCompHnd->getFieldClass(fieldHandle)) > _info.compCompHnd->getClassSize(_info.compCompHnd->getFieldClass(sparseFields[fldOffset])))
                    {
                        sparseFields[fldOffset] = fieldHandle;
                    }
                }
                unsigned lastOffset = -1;
                CORINFO_CLASS_HANDLE prevClass = nullptr;
                CorInfoType prevCorInfoType = CorInfoType::CORINFO_TYPE_UNDEF;
                unsigned totalSize = 0;

                for (unsigned curOffset = 0; curOffset < structSize;)
                {
                    CORINFO_FIELD_HANDLE fieldHandle = sparseFields[curOffset];
                    if (fieldHandle == nullptr)
                    {
                        curOffset++;
                        continue;
                    }

                    int prevElementSize;
                    if (prevCorInfoType == CorInfoType::CORINFO_TYPE_UNDEF)
                    {
                        lastOffset = 0;
                        prevElementSize = 0;
                    }
                    else
                    {
                        prevElementSize = getElementSize(prevClass, prevCorInfoType);
                    }

                    // Pad to this field if necessary
                    unsigned paddingSize = curOffset - lastOffset - prevElementSize;
                    if (paddingSize > 0)
                    {
                        addPaddingFields(paddingSize, llvmFields);
                        totalSize += paddingSize;
                    }

                    CORINFO_CLASS_HANDLE fieldClassHandle = NO_CLASS_HANDLE;
                    CorInfoType fieldCorType = _info.compCompHnd->getFieldType(fieldHandle, &fieldClassHandle);
                    
                    int fieldSize = getElementSize(fieldClassHandle, fieldCorType);

                    llvmFields.push_back(getLlvmTypeForCorInfoType(fieldCorType, fieldClassHandle));

                    totalSize += fieldSize;
                    lastOffset = curOffset;
                    prevClass = fieldClassHandle;
                    prevCorInfoType = fieldCorType;

                    curOffset += fieldSize;
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
        case TYP_LCLBLK: // TODO: outgoing args space - need to get an example compiling, e.g. https://github.com/dotnet/runtimelab/blob/40f9ff64ae80596bcddcec16a7e1a8f57a0b2cff/src/tests/nativeaot/SmokeTests/HelloWasm/HelloWasm.cs#L3492 to see what's
            // going on.  CORINFO_TYPE_VALUECLASS is a better mapping but if that is mapped as of now, then canStoreTypeOnLlvmStack will fail compilation for most methods.
            failFunctionCompilation();
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
        default:
            failFunctionCompilation();
    }
}

CORINFO_CLASS_HANDLE Llvm::tryGetStructClassHandle(LclVarDsc* varDsc)
{
    return varTypeIsStruct(varDsc) ? varDsc->GetStructHnd() : NO_CLASS_HANDLE;;
}

unsigned int Llvm::padOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE structClassHandle, unsigned int atOffset)
{
    unsigned int alignment;
    if (corInfoType == CorInfoType::CORINFO_TYPE_BYREF || corInfoType == CorInfoType::CORINFO_TYPE_CLASS ||
        corInfoType == CorInfoType::CORINFO_TYPE_REFANY)
    {
        // simplified for just pointers
        alignment = TARGET_POINTER_SIZE; // TODO Wasm64 aligns pointers at 4 or 8?
    }
    else
    {
        assert(corInfoType == CorInfoType::CORINFO_TYPE_VALUECLASS);
        return _padOffset(_thisPtr, structClassHandle, atOffset);
    }
    return roundUp(atOffset, alignment);
}

unsigned int Llvm::padNextOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE structClassHandle, unsigned int atOffset)
{
    unsigned int size;
    if (corInfoType == CorInfoType::CORINFO_TYPE_BYREF || corInfoType == CorInfoType::CORINFO_TYPE_CLASS ||
        corInfoType == CorInfoType::CORINFO_TYPE_REFANY)
    {
        size = TARGET_POINTER_SIZE;
    }
    else
    {
        assert(corInfoType == CorInfoType::CORINFO_TYPE_VALUECLASS);
        size = getElementSize(structClassHandle, corInfoType);
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

        return (classAttribs & CORINFO_FLG_CONTAINS_GC_PTR) == 0;
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

CorInfoType Llvm::getCorInfoTypeForArg(CORINFO_SIG_INFO& sigInfo, CORINFO_ARG_LIST_HANDLE& arg, CORINFO_CLASS_HANDLE* clsHnd)
{
    CorInfoTypeWithMod corTypeWithMod = _getArgTypeIncludingParameterized(_thisPtr, &sigInfo, arg, clsHnd);
    return strip(corTypeWithMod);
}

FunctionType* Llvm::getFunctionTypeForSigInfo(CORINFO_SIG_INFO& sigInfo)
{
    if (sigInfo.hasExplicitThis() || sigInfo.hasTypeArg())
        failFunctionCompilation();

    // start vector with shadow stack arg, this might reduce the number of bitcasts as a i8**, TODO: try it and check LLVM bitcode size
    std::vector<llvm::Type*> argVec{Type::getInt8PtrTy(_llvmContext)};
    llvm::Type*              retLlvmType;

    if (needsReturnStackSlot(sigInfo.retType, sigInfo.retTypeClass))
    {
        argVec.push_back(Type::getInt8PtrTy(_llvmContext));
        retLlvmType = Type::getVoidTy(_llvmContext);
    }
    else
    {
        retLlvmType = getLlvmTypeForCorInfoType(sigInfo.retType, sigInfo.retTypeClass);
    }

    CORINFO_ARG_LIST_HANDLE  sigArgs = sigInfo.args;

    //TODO: not attempting to compile generic signatures with context arg via clrjit yet
    if (sigInfo.hasTypeArg())
    {
        failFunctionCompilation();
        //signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // *MethodTable
    }

    for (unsigned int i = 0; i < sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CORINFO_CLASS_HANDLE classHnd;
        CorInfoType corInfoType = getCorInfoTypeForArg(sigInfo, sigArgs, &classHnd);
        if (canStoreArgOnLlvmStack(corInfoType, classHnd))
        {
            argVec.push_back(getLlvmTypeForCorInfoType(corInfoType, classHnd));
        }
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
        case TYP_REF:
            return Type::getInt8PtrTy(_llvmContext);
        default:
            failFunctionCompilation();
    }
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

Value* Llvm::castIfNecessary(Value* source, Type* targetType)
{
    llvm::Instruction* castInst = getCast(source, targetType);
    if (castInst == nullptr)
        return source;

    return _builder.Insert(castInst);
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
LlvmArgInfo Llvm::getLlvmArgInfoForArgIx(CORINFO_SIG_INFO& sigInfo, unsigned int lclNum)
{
    if (sigInfo.hasExplicitThis() || sigInfo.hasTypeArg())
        failFunctionCompilation();

    unsigned int llvmArgNum    = 1; // skip shadow stack arg
    bool         returnOnStack = false;

    LlvmArgInfo             llvmArgInfo = {
        -1 /* default to not an LLVM arg*/, sigInfo.hasThis() ? TARGET_POINTER_SIZE : 0 /* this is the first pointer on
                                                                                       the shadow stack */
    };
    if (needsReturnStackSlot(sigInfo.retType, sigInfo.retTypeClass))
    {
        if (lclNum == 0)
        {
            // the first IR arg is the return address, but its not in sigInfo so handle here
            llvmArgInfo.m_argIx = llvmArgNum;
            return llvmArgInfo;
        }
        lclNum--;  // line up with sigArgs
        llvmArgNum++;
    }

    CORINFO_ARG_LIST_HANDLE sigArgs     = sigInfo.args;

    unsigned int shadowStackOffset = llvmArgInfo.m_shadowStackOffset;

    unsigned int i = 0;
    for (; i < sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CORINFO_CLASS_HANDLE clsHnd;
        CorInfoType corInfoType = getCorInfoTypeForArg(sigInfo, sigArgs, &clsHnd);
        if (canStoreArgOnLlvmStack(corInfoType, clsHnd))
        {
            if (lclNum == i)
            {
                llvmArgInfo.m_argIx = llvmArgNum;
                break;
            }

            llvmArgNum++;
        }
        else
        {
            if (lclNum == i)
            {
                llvmArgInfo.m_shadowStackOffset = shadowStackOffset;
                break;
            }

            shadowStackOffset += TARGET_POINTER_SIZE; // TODO size of arg, for now only handles byrefs and class types
        }
    }
    assert(lclNum == i); // lclNum not an argument
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
    if (op1->getType()->isPointerTy() && op2->getType()->isIntegerTy())
    {
        mapGenTreeToValue(node, _builder.CreateGEP(op1, op2));
    }
    else if (op1->getType()->isIntegerTy() && op2->getType() == op1->getType())
    {
        mapGenTreeToValue(node, _builder.CreateAdd(op1, op2));
    }
    else
    {
        // unsupported add type combination
        failFunctionCompilation();
    }
}

Value* Llvm::genTreeAsLlvmType(GenTree* tree, Type* type)
{
    Value* v = getGenTreeValue(tree);
    if (v->getType() == type)
        return v;

    if (tree->IsIntegralConst() && tree->TypeIs(TYP_INT))
    {
        if (type->isPointerTy())
        {
            return _builder.CreateIntToPtr(v, type);
        }
        return _builder.getInt({(unsigned int)type->getPrimitiveSizeInBits().getFixedSize(), (uint64_t)tree->AsIntCon()->IconValue(), true});
    }
    failFunctionCompilation();
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

bool Llvm::isThisArg(GenTreeCall* call, GenTree* operand)
{
    if (call->gtCallThisArg == nullptr)
    {
        return false;
    }

    return _compiler->gtGetThisArg(call) == operand;
}

void Llvm::storeOnShadowStack(GenTree* operand, Value* shadowStackForCallee, unsigned int offset)
{
    castingStore(genTreeAsLlvmType(operand, Type::getInt8PtrTy(_llvmContext)),
                 getShadowStackOffest(shadowStackForCallee, offset), Type::getInt8PtrTy(_llvmContext));
}

// shadow stack moved up to avoid overwriting anything on the stack in the compiling method
llvm::Value* Llvm::getShadowStackForCallee()
{
    unsigned int offset = getTotalLocalOffset();

    return offset == 0 ? _function->getArg(0) : _builder.CreateGEP(_function->getArg(0), _builder.getInt32(offset));
}

llvm::Value* Llvm::buildUserFuncCall(GenTreeCall* call)
{
    const char* symbolName = (*_getMangledSymbolName)(_thisPtr, call->gtEntryPoint.handle);
    if (_isRuntimeImport(_thisPtr, call->gtCallMethHnd))
    {
        failFunctionCompilation();
    }

    (*_addCodeReloc)(_thisPtr, call->gtEntryPoint.handle);
    Function* llvmFunc = _module->getFunction(symbolName);
    CORINFO_SIG_INFO sigInfo;
    _compiler->eeGetMethodSig(call->gtCallMethHnd, &sigInfo);

    if (llvmFunc == nullptr)
    {
        CORINFO_ARG_LIST_HANDLE sigArgs = sigInfo.args;

        // assume ExternalLinkage, if the function is defined in the clrjit module, then it is replaced and an extern
        // added to the Ilc module
        llvmFunc = Function::Create(getFunctionTypeForSigInfo(sigInfo), Function::ExternalLinkage,
                                    0U, symbolName, _module);
    }
    std::vector<llvm::Value*> argVec;

    // shadowstack arg first
    Value* shadowStackForCallee = getShadowStackForCallee(); // TODO: store this in the prolog and calculate once.
    argVec.push_back(shadowStackForCallee); // TODO-LLVM: we are not moving this past any return address args, so the return address is clobbering the first slot in the shadow stack.
                                            // As the return is the last thing executed this seems ok, but then the question becomes why do we need the return address arg at all
                                            // apart from compatibility with IL->LLVM?

    unsigned int                      shadowStackUseOffest = 0;
    int                               argIx                = 0;
    GenTree*                          thisArg              = nullptr;
    fgArgInfo*                        argInfo              = call->fgArgInfo;
    unsigned int                      argCount             = argInfo->ArgCount();
    fgArgTabEntry**                   argTable             = argInfo->ArgTable();
    std::vector<OperandArgNum>        sortedArgs           = std::vector<OperandArgNum>(argCount);
    OperandArgNum*                    sortedData           = sortedArgs.data();

    for (unsigned i = 0; i < argCount; i++)
    {
        fgArgTabEntry* curArgTabEntry = argTable[i];
        unsigned int   argNum         = curArgTabEntry->argNum;
        OperandArgNum  opAndArg       = {argNum, curArgTabEntry->GetNode()};
        sortedData[argNum]            = opAndArg;
    }

    for (OperandArgNum opAndArg : sortedArgs)
    {
        if (opAndArg.operand->IsArgPlaceHolderNode())
        {
            continue;
        }
        LlvmArgInfo llvmArgInfo = getLlvmArgInfoForArgIx(sigInfo, argIx);
        if (llvmArgInfo.m_argIx >= 0)
        {
            // pass the parameter on the LLVM stack
            argVec.push_back(genTreeAsLlvmType(opAndArg.operand, llvmFunc->getArg(llvmArgInfo.m_argIx)->getType()));
        }
        else
        {
            // pass on shadow stack
            storeOnShadowStack(opAndArg.operand, shadowStackForCallee, shadowStackUseOffest);
            shadowStackUseOffest += TARGET_POINTER_SIZE;
        }
        argIx++;
    }
    Value* llvmCall = _builder.CreateCall(llvmFunc, ArrayRef<Value*>(argVec));
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
    return corinfoMethodHnd == _compiler->eeFindHelper(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE);
}

void Llvm::buildHelperFuncCall(GenTreeCall* call)
{
    if (call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_GENERIC_HANDLE) ||
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE) ||
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
                argVec.push_back(genTreeAsLlvmType(opAndArg.operand, llvmFunc->getArg(argIx)->getType()));
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
    else if (call->gtCallType == CT_USER_FUNC && !call->IsVirtualStub() /* TODO: Virtual stub not implemented */)
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
    var_types castToType = cast->CastToType();
    if (castToType == TYP_BOOL && cast->TypeIs(TYP_INT) && cast->CastOp()->TypeIs(TYP_INT))
    {
        Value* intValue = _builder.CreateZExt(getGenTreeValue(cast->CastOp()), getLlvmTypeForVarType(TYP_INT));
        mapGenTreeToValue(cast, intValue); // nothing to do except map the source value to the destination GenTree
    }
    else if (castToType == TYP_DOUBLE && cast->CastOp()->TypeIs(TYP_FLOAT))
    {
        mapGenTreeToValue(cast, _builder.CreateFPCast(getGenTreeValue(cast->CastOp()), getLlvmTypeForVarType(TYP_DOUBLE)));
    }
    else if (cast->TypeIs(TYP_LONG) && genActualTypeIsInt(cast->CastOp()))
    {
        // Cast pointer to int if necessary.  TODO-LLVM: candidate for lowering?
        Value* sourceValue = castIfNecessary(getGenTreeValue(cast->CastOp()), getLlvmTypeForVarType(cast->CastOp()->TypeGet()));

        mapGenTreeToValue(cast,
            cast->IsUnsigned()
            ? _builder.CreateZExt(sourceValue, getLlvmTypeForVarType(cast->CastToType()))
            : _builder.CreateSExt(sourceValue, getLlvmTypeForVarType(cast->CastToType())));
    }
    else if (cast->TypeIs(TYP_INT, TYP_LONG) && cast->CastOp()->TypeIs(TYP_FLOAT, TYP_DOUBLE))
    {
        mapGenTreeToValue(cast,
            cast->IsUnsigned()
                ? _builder.CreateFPToUI(getGenTreeValue(cast->CastOp()), getLlvmTypeForVarType(cast->CastToType()))
                : _builder.CreateFPToSI(getGenTreeValue(cast->CastOp()), getLlvmTypeForVarType(cast->CastToType())));
    }
    else
    {
        // TODO: other casts
        failFunctionCompilation();
    }
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
        mapGenTreeToValue(node, _builder.getInt32(node->AsIntCon()->IconValue()));
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
    // first cast the pointer to create the correct load instructions, then cast the result incase we are loading a small int into an int32
    mapGenTreeToValue(node, castIfNecessary(_builder.CreateLoad(
                                 castIfNecessary(ptr,
                                     getLlvmTypeForVarType(node->TypeGet())->getPointerTo())), getLlvmTypeForVarType(genActualType(node))));
}

Value* Llvm::buildJTrue(GenTree* node, Value* opValue)
{
    return _builder.CreateCondBr(opValue, getLLVMBasicBlockForBlock(_currentBlock->bbJumpDest), getLLVMBasicBlockForBlock(_currentBlock->bbNext));
}

void Llvm::buildCmp(genTreeOps op, GenTree* node, Value* op1, Value* op2)
{
    llvm::CmpInst::Predicate llvmPredicate;

    bool isIntOrPtr = op1->getType()->isIntOrPtrTy();
    switch (op)
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
                // Arguments are implicitly defined on entry to the method.
                assert(_compiler->lvaIsParameter(lclNum) && ssaNum == SsaConfig::FIRST_SSA_NUM);
                LlvmArgInfo  llvmArgInfo = getLlvmArgInfoForArgIx(_sigInfo, lclNum);
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

void Llvm::importStoreInd(GenTreeStoreInd* storeIndOp)
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

Value* Llvm::localVar(GenTreeLclVar* lclVar)
{
    Value*       llvmRef;
    unsigned int lclNum = lclVar->GetLclNum();
    unsigned int ssaNum = lclVar->GetSsaNum();
    if (_localsMap->find({lclNum, ssaNum}) == _localsMap->end())
    {
        if (_compiler->lvaIsParameter(lclNum))
        {
            if (lclNum == _shadowStackLclNum)
            {
                llvmRef = _function->getArg(0);
            }
            else if (lclNum == _retAddressLclNum)
            {
                llvmRef = _function->getArg(1);
            }
            else
            {
                // adjust for "this" (on shadowstack, not an arg)
                unsigned int argIx = _info.compIsStatic ? lclNum : lclNum - 1;

                // adjust for return address arg, not in siginfo
                if (_functionSigHasReturnAddress)
                {
                    argIx++;
                }

                if (_info.compRetBuffArg != BAD_VAR_NUM)
                {
                    failFunctionCompilation();
                }
                LlvmArgInfo  llvmArgInfo = getLlvmArgInfoForArgIx(_sigInfo, argIx);
                llvmRef = _function->getArg(llvmArgInfo.m_argIx);
            }

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

int Llvm::getLocalOffsetAtIndex(GenTreeLclVar* lclVar)
{
    int        offset;

    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar);
    if (canStoreLocalOnLlvmStack(varDsc))
    {
        offset = -1;
    }
    else
    {
        offset = 0;

        for (unsigned lclNum = 0; lclNum < lclVar->GetLclNum(); lclNum++)
        {
            varDsc = _compiler->lvaGetDesc(lclNum);
            if (!varDsc->lvIsParam)
            {
                CorInfoType corInfoType = toCorInfoType(varDsc->TypeGet());
                if (!canStoreLocalOnLlvmStack(varDsc))
                {
                    offset = padNextOffset(corInfoType, tryGetStructClassHandle(varDsc), offset);
                }
            }
        }
        offset = padOffset(toCorInfoType(lclVar->TypeGet()), tryGetStructClassHandle(varDsc), offset);
    }

    return offset;
}

Value* Llvm::getLocalVarAddress(GenTreeLclVar* lclVar) {
    //// TODO: 1 - need the address context logic from ILToLLVMImporter when exception blocks are implemented
    ////       2 - ILToLLVMImporter caches the gep in the prolog, this creates the gep each time which is wasteful - look to copy more of the logic from ILToLLVMImporter.LoadVarAddress
    unsigned int varOffset = getLocalOffsetAtIndex(lclVar);
    if (varOffset == -1)
    {
        // if these are used in exception handlers, then they need to be stored, for now just fail
        failFunctionCompilation();
    }
    return _builder.CreateGEP(_function->getArg(0), _builder.getInt32(varOffset), "lclVar");
}

void Llvm::storeLocalVar(GenTreeLclVar* lclVar)
{
    if (lclVar->gtFlags & GTF_VAR_DEF)
    {
        Value* valueRef = getGenTreeValue(lclVar->gtGetOp1());
        assert(valueRef != nullptr);
        // This could be done in the NE operator, but sometimes that would be needless, e.g. when followed by JTRUE
        // TODO-LLVM: As this is a zero extend widening operation, this is only valid if the small int is unsigned.  We don't know that here, so likely it would be better to
        // delete this and do the cast in the operator.  It seems likely that the cast will be a nop anyway, at least in Wasm, as Wasm does not have any number types smaller than i32
        if (valueRef->getType()->isIntegerTy())
        {
            valueRef = zextIntIfNecessary(valueRef);
        }

        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar);
        SsaPair ssaPair = {lclVar->GetLclNum(), lclVar->GetSsaNum()};
        _localsMap->insert({ssaPair, valueRef });
    }
    else
    {
        failFunctionCompilation();
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
        case GT_LCL_VAR:
            localVar(node->AsLclVar());
            break;
        case GT_EQ:
        case GT_NE:
        case GT_LE:
        case GT_LT:
        case GT_GE:
        case GT_GT:
            buildCmp(oper, node, getGenTreeValue(node->AsOp()->gtOp1), getGenTreeValue(node->AsOp()->gtOp2));
            break;
        case GT_NO_OP:
            emitDoNothingCall();
            break;
        case GT_PHI:
            buildEmptyPhi(node->AsPhi());
            break;
        case GT_PHI_ARG:
            break;
        case GT_RETURN:
            buildReturn(node);
            break;
        case GT_STORE_LCL_VAR:
            storeLocalVar(node->AsLclVar());
            break;
        case GT_STOREIND:
            importStoreInd((GenTreeStoreInd*)node);
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

        _debugFunction = _diBuilder->createFunction(debugMetadata.fileMetadata, _info.compMethodName,
                                                    _info.compMethodName, debugMetadata.fileMetadata, lineNumber,
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
    _functionSigHasReturnAddress = needsReturnStackSlot(_sigInfo.retType, _sigInfo.retTypeClass);
}

void Llvm::llvmShutdown()
{
    if (_diBuilder != nullptr)
    {
        emitDebugMetadata(_llvmContext);
    }
#ifdef DEBUG
    if (_outputFileName == nullptr) return; // nothing generated
    std::error_code ec;
    char* txtFileName = (char*)malloc(strlen(_outputFileName) + 2); // .txt is longer than .bc
    strcpy(txtFileName, _outputFileName);
    strcpy(txtFileName + strlen(_outputFileName) - 2, "txt");
    llvm::raw_fd_ostream textOutputStream(txtFileName, ec);
    _module->print(textOutputStream, (llvm::AssemblyAnnotationWriter*)NULL);
    free(txtFileName);
#endif //DEBUG
    llvm::raw_fd_ostream OS(_outputFileName, ec);
    llvm::WriteBitcodeToFile(*_module, OS);
    delete _module;
    //    Module.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);
}

void Llvm::ConvertShadowStackLocalNode(GenTreeLclVarCommon* node)
{
    GenTreeLclVarCommon* lclVar = node->AsLclVarCommon();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar->GetLclNum());
    genTreeOps oper = node->OperGet();
    if (!canStoreLocalOnLlvmStack(varDsc))
    {
        // TODO-LLVM: if the offset == 0, just GT_STOREIND at the shadowStack
        GenTreeIntCon* offset = _compiler->gtNewIconNode(varDsc->GetStackOffset(), TYP_I_IMPL);
        GenTreeLclVar* shadowStackVar = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
        GenTree* lclAddress = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackVar, offset);

        genTreeOps indirOper = GT_NONE;
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
            default:
                unreached();
        }
        node->ChangeOper(indirOper);
        node->AsIndir()->SetAddr(lclAddress);
        if (GenTree::OperIsStore(indirOper))
        {
            node->gtFlags |= GTF_IND_TGT_NOT_HEAP;
            node->AsOp()->gtOp2 = storedValue;
        }
        if (GenTree::OperIsBlk(indirOper))
        {
            GenTreeBlk* blk = node->AsBlk();
            CORINFO_CLASS_HANDLE handle = varDsc->GetStructHnd();
            blk->SetLayout(_compiler->typGetObjLayout(handle));
            blk->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
        }
        CurrentRange().InsertBefore(node, offset, shadowStackVar, lclAddress);
    }
}

void Llvm::ConvertShadowStackLocals()
{
    _shadowStackLclNum = _compiler->lvaGrabTemp(true DEBUGARG("shadowstack"));
    LclVarDsc* shadowStackVarDsc = _compiler->lvaGetDesc(_shadowStackLclNum);
    shadowStackVarDsc->lvIsParam = 1;
    shadowStackVarDsc->lvType = TYP_I_IMPL;

    for (BasicBlock* _currentBlock : _compiler->Blocks())
    {
        _currentRange = &LIR::AsRange(_currentBlock);
        for (GenTree* node : CurrentRange())
        {
            if (node->OperIs(GT_STORE_LCL_VAR, GT_LCL_VAR))
            {
                ConvertShadowStackLocalNode(node->AsLclVarCommon());
            }
            else if (node->IsCall())
            {
                GenTreeCall* callNode = node->AsCall();

                if (callNode->IsHelperCall() || callNode->TypeIs(TYP_VOID))
                {
                    // helper calls are built differently
                    continue;
                }

                // we can't do these yet
                if (callNode->gtCallType == CT_INDIRECT || _isRuntimeImport(_thisPtr, callNode->gtCallMethHnd))
                {
                    failFunctionCompilation();
                }

                CORINFO_SIG_INFO calleeSigInfo;
                _compiler->eeGetMethodSig(callNode->gtCallMethHnd, &calleeSigInfo);

                if (needsReturnStackSlot(calleeSigInfo.retType, calleeSigInfo.retTypeClass))
                {
                    // replace the "CALL ref" with a "CALL void" that takes a return address as the first argument
                    var_types callReturnType = callNode->TypeGet();
                    GenTreeLclVar* shadowStackVar = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
                    GenTreeIntCon* offset = _compiler->gtNewIconNode(_shadowStackLocalsSize, TYP_I_IMPL);
                    GenTree* returnValueAddress = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackVar, offset);

                    // create temp for the return address
                    unsigned returnTempNum = _compiler->lvaGrabTemp(false DEBUGARG("return value address"));
                    LclVarDsc* returnAddrVarDsc = _compiler->lvaGetDesc(returnTempNum);
                    returnAddrVarDsc->lvType = TYP_I_IMPL;

                    GenTree* addrStore = _compiler->gtNewStoreLclVar(returnTempNum, returnValueAddress);
                    GenTree* returnAddrLcl = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);

                    if (callNode->gtCallArgs != nullptr)
                    {
                        // TODO-LLVM: out args?  E.g.
                        //    /--*  t398   int    arg4 out+10
                        //    +--*  t399   int    arg5 out + 14
                        //    +--*  t908   byref  arg6 out + 18
                        //    +--*  t397   int    arg3 in r9
                        //    +--*  t395   ref    arg2 in r8
                        //    +--*  t392   byref  arg0 in rcx
                        //    +--*  t393   ref    arg1 in rdx
                        failFunctionCompilation();
                    }
                    GenTreeCall::Use* oldArgs = callNode->gtCallLateArgs;
                    callNode->ResetArgInfo();
                    callNode->gtCallArgs = _compiler->gtPrependNewCallArg(returnAddrLcl, oldArgs);
                    callNode->gtCallLateArgs = nullptr;
                    _compiler->fgInitArgInfo(callNode);

                    GenTree* returnAddrLclAfterCall = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);
                    GenTree* indirNode;
                    if (callReturnType == TYP_STRUCT)
                    {
                        indirNode = new (_compiler, GT_OBJ)
                            GenTreeObj(callReturnType, returnAddrLclAfterCall, _compiler->typGetObjLayout(calleeSigInfo.retTypeClass));
                        indirNode->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
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

                    callNode->gtReturnType = TYP_VOID;
                    callNode->ChangeType(TYP_VOID);

                    CurrentRange().InsertBefore(callNode, shadowStackVar, offset, returnValueAddress, addrStore);
                    CurrentRange().InsertAfter(addrStore, returnAddrLcl);
                    CurrentRange().InsertAfter(callNode, returnAddrLclAfterCall, indirNode);
                }
            }
            else if (node->OperIs(GT_RETURN) && _functionSigHasReturnAddress)
            {
                var_types originalReturnType = node->TypeGet();
                if(node->TypeIs(TYP_VOID))
                {
                    /* TODO-LLVM: retbuf .   compHasRetBuffArg doesn't seem to have an implementation */
                    failFunctionCompilation();
                }

                if (_retAddressLclNum == BAD_VAR_NUM)
                {
                    _retAddressLclNum = _compiler->lvaGrabTemp(true DEBUGARG("shadowstack"));
                }
                LclVarDsc* retAddressVarDsc = _compiler->lvaGetDesc(_retAddressLclNum);
                retAddressVarDsc->lvIsParam = 1;
                retAddressVarDsc->lvType = TYP_I_IMPL;

                GenTreeLclVar* retAddressLocal = _compiler->gtNewLclvNode(_retAddressLclNum, TYP_I_IMPL);
                GenTree* storeNode;
                if (originalReturnType == TYP_STRUCT)
                {
                    storeNode = new (_compiler, GT_STORE_OBJ)
                        GenTreeObj(originalReturnType, retAddressLocal, node->AsOp()->gtGetOp1(), _compiler->typGetObjLayout(_sigInfo.retTypeClass));
                    storeNode->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
                }
                else
                {
                    storeNode = _compiler->gtNewOperNode(GT_STOREIND, originalReturnType, retAddressLocal, node->AsOp()->gtOp1);
                }
                storeNode->gtFlags |= GTF_IND_TGT_NOT_HEAP; // No RhpAssignRef required

                GenTreeOp* retNode = node->AsOp();
                retNode->gtOp1 = nullptr;
                node->ChangeType(TYP_VOID);

                CurrentRange().InsertBefore(node, retAddressLocal, storeNode);
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
;
        offset = padOffset(corInfoType, classHandle, offset);
        varDsc->SetStackOffset(offset);
        offset = padNextOffset(corInfoType, classHandle, offset);
    }
    _shadowStackLocalsSize = offset;

    ConvertShadowStackLocals();
}

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
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
        _function = Function::Create(getFunctionTypeForSigInfo(_sigInfo), Function::ExternalLinkage, 0U, mangledName,
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
