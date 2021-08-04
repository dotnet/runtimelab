// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef TARGET_WASM
#include <string.h>
#include "alloc.h"
#include "compiler.h"
#include "block.h"
#include "gentree.h"
#include "llvm.h"
#include "llvm/ADT/StringRef.h"
#include "llvm/IR/LLVMContext.h"
#include "llvm/IR/Module.h"
#include "llvm/IR/Function.h"
#include "llvm/IR/IRBuilder.h"
#include "llvm/Bitcode/BitcodeWriter.h"
#include "llvm/IR/DIBuilder.h"
#include "llvm/BinaryFormat/Dwarf.h"

#include <unordered_map>

using llvm::Function;
using llvm::FunctionType;
using llvm::Type;
using llvm::LLVMContext;
using llvm::ArrayRef;
using llvm::Module;
using llvm::Value;

struct OperandArgNum
{
    unsigned int argNum;
    GenTree*     operand;
};

struct LlvmArgInfo
{
    int m_argIx; // -1 indicates not in the LLVM arg list, but on the shadow stack
    unsigned int m_shadowStackOffset;
};

struct DebugMetadata
{
    llvm::DIFile*        fileMetadata; 
    llvm::DICompileUnit* diCompileUnit;
};

// TODO: might need the LLVM Value* in here for exception funclets.
struct SpilledExpressionEntry
{
    CorInfoType m_CorInfoType;
};

typedef JitHashTable<BasicBlock*, JitPtrKeyFuncs<BasicBlock>, llvm::BasicBlock*> BlkToLlvmBlkVectorMap;

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

static char*                              _outputFileName;
static Function*                          _doNothingFunction;
Compiler::Info                            _info;
Compiler*                                 _compiler;
Function*                                 _function;
llvm::DISubprogram*                       _debugFunction;
IL_OFFSETX                                _currentOffset;
BlkToLlvmBlkVectorMap*                    _blkToLlvmBlkVectorMap;
llvm::IRBuilder<>*                        _builder;
std::unordered_map<GenTree*, Value*>*     _sdsuMap;
std::unordered_map<unsigned int, Value*>* _localsMap;

// DWARF
std::unordered_map<std::string, struct DebugMetadata> _debugMetadataMap;

CORINFO_SIG_INFO                          _sigInfo; // sigInfo of function being compiled
llvm::IRBuilder<>*                        _prologBuilder;
std::vector<SpilledExpressionEntry>       _spilledExpressions;

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
                                                const uint32_t (*getOffsetLineNumber)(void*, unsigned int))
{
    _thisPtr = thisPtr;
    _getMangledMethodName = getMangledMethodNamePtr;
    _getMangledSymbolName = getMangledSymbolNamePtr;
    _addCodeReloc         = addCodeRelocPtr;
    _isRuntimeImport      = isRuntimeImport;
    _getDocumentFileName  = getDocumentFileName;
    _firstSequencePointLineNumber = firstSequencePointLineNumber;
    _getOffsetLineNumber          = getOffsetLineNumber;

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

void Llvm::Init()
{
}

void emitDebugMetadata(LLVMContext& context)
{
    _module->addModuleFlag(llvm::Module::Warning, "Dwarf Version", 4);
    _module->addModuleFlag(llvm::Module::Warning, "Debug Info Version", 3);
    _diBuilder->finalize();
}

void Llvm::llvmShutdown()
{
    if (_diBuilder != nullptr)
    {
        emitDebugMetadata(_llvmContext);
    }
#if DEBUG
    if (_outputFileName == nullptr) return; // nothing generated
    std::error_code ec;
    char* txtFileName = (char *)malloc(strlen(_outputFileName) + 2); // .txt is longer than .bc
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

[[noreturn]] void failFunctionCompilation()
{
    if (_function != nullptr)
    {
        _function->deleteBody();
    }
    fatal(CORJIT_SKIPPED);
}

Value* mapGenTreeToValue(GenTree* genTree, Value* valueRef)
{
    if (_sdsuMap->find(genTree) != _sdsuMap->end())
    {
        fatal(CorJitResult::CORJIT_INTERNALERROR);
    }
    _sdsuMap->insert({genTree, valueRef});
    return valueRef;
}

Value* getGenTreeValue(GenTree* op)
{
    return _sdsuMap->at(op);
}

// Copy of logic from ILImporter.GetLLVMTypeForTypeDesc
llvm::Type* getLlvmTypeForCorInfoType(CorInfoType corInfoType) {
    switch (corInfoType)
    {
        case CorInfoType::CORINFO_TYPE_VOID:
            return Type::getVoidTy(_llvmContext);

        case CorInfoType::CORINFO_TYPE_BOOL:
        case CorInfoType::CORINFO_TYPE_UBYTE:
        case CorInfoType::CORINFO_TYPE_BYTE:
            return Type::getInt8Ty(_llvmContext);

        case CorInfoType::CORINFO_TYPE_INT:
        case CorInfoType::CORINFO_TYPE_NATIVEINT:  // TODO: Wasm64 - what does NativeInt mean for Wasm64
            return Type::getInt32Ty(_llvmContext);

        case CorInfoType::CORINFO_TYPE_ULONG:
            return Type::getInt64Ty(_llvmContext);

        case CorInfoType::CORINFO_TYPE_BYREF:
        case CorInfoType::CORINFO_TYPE_CLASS:
            return Type::getInt8PtrTy(_llvmContext);

        default:
            failFunctionCompilation();
    }
}


unsigned int padOffset(CorInfoType corInfoType, unsigned int atOffset)
{
    unsigned int alignment;
    if (corInfoType == CorInfoType::CORINFO_TYPE_BYREF || corInfoType == CorInfoType::CORINFO_TYPE_CLASS)
    {
        // simplified for just pointers
        alignment = TARGET_POINTER_SIZE; // TODO Wasm64 aligns pointers at 4 or 8?
    }
    else
    {
        // TODO: value type field alignment - this is the ILToLLVMImporter logic:
        //var fieldAlignment = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldAlignment
        //                                                         : type.Context.Target.LayoutPointerSize;
        //var alignment      = LayoutInt.Min(fieldAlignment, new LayoutInt(ComputePackingSize(type))).AsInt;
        //var padding        = (atOffset + (alignment - 1)) & ~(alignment - 1);
        failFunctionCompilation();
    }
    return roundUp(atOffset, alignment);
}

unsigned int padNextOffset(CorInfoType corInfoType, unsigned int atOffset)
{
    unsigned int size;
    if (corInfoType == CorInfoType::CORINFO_TYPE_BYREF || corInfoType == CorInfoType::CORINFO_TYPE_CLASS)
    {
        size = TARGET_POINTER_SIZE;
    }
    else
    {
        // TODO: value type field size - this is the ILToLLVMImporter logic:
        // var size = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldSize
        //                                               : type.Context.Target.LayoutPointerSize;
        failFunctionCompilation(); // TODO value type sizes and alignment
    }
    return padOffset(corInfoType, atOffset) + size;
}

/// <summary>
/// Returns true if the type can be stored on the LLVM stack
/// instead of the shadow stack in this method.
/// </summary>
bool canStoreTypeOnLlvmStack(CorInfoType corInfoType)
{
    // structs with no GC pointers can go on LLVM stack.
    if (corInfoType == CorInfoType::CORINFO_TYPE_VALUECLASS)
    {
        // TODO: the equivalent of this c# goes here
        //if (type is DefType defType)
        //{
        //    if (!defType.IsGCPointer && !defType.ContainsGCPointers)
        //    {
        //        return true;
        //    }
        //}
        failFunctionCompilation();
    }

    if (corInfoType == CorInfoType::CORINFO_TYPE_REFANY)
    {
        return false;
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
bool needsReturnStackSlot(CorInfoType corInfoType)
{
    return corInfoType != CorInfoType::CORINFO_TYPE_VOID && !canStoreTypeOnLlvmStack(corInfoType);
}

CorInfoType getCorInfoTypeForArg(CORINFO_SIG_INFO& sigInfo, CORINFO_ARG_LIST_HANDLE& arg)
{
    CORINFO_CLASS_HANDLE clsHnd;
    CorInfoTypeWithMod   corTypeWithMod = _info.compCompHnd->getArgType(&sigInfo, arg, &clsHnd);
    return strip(corTypeWithMod);
}

FunctionType* getFunctionTypeForSigInfo(CORINFO_SIG_INFO& sigInfo)
{
    if (sigInfo.hasExplicitThis() || sigInfo.hasTypeArg())
        failFunctionCompilation();

    // start vector with shadow stack arg, this might reduce the number of bitcasts as a i8**, TODO: try it and check LLVM bitcode size
    std::vector<llvm::Type*> argVec{Type::getInt8PtrTy(_llvmContext)};
    llvm::Type*              retLlvmType;

    if (needsReturnStackSlot(sigInfo.retType))
    {
        argVec.push_back(Type::getInt8PtrTy(_llvmContext));
        retLlvmType = Type::getVoidTy(_llvmContext);
    }
    else
    {
        retLlvmType   = getLlvmTypeForCorInfoType(sigInfo.retType);
    }

    CORINFO_ARG_LIST_HANDLE  sigArgs = sigInfo.args;

    //TODO: not attempting to compile generic signatures with context arg via clrjit yet
    if (sigInfo.hasTypeArg())
    {
        failFunctionCompilation();
        //signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // *EEType
    }

    for (unsigned int i = 0; i < sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CorInfoType corInfoType = getCorInfoTypeForArg(sigInfo, sigArgs);
        if (canStoreTypeOnLlvmStack(corInfoType))
        {
            argVec.push_back(getLlvmTypeForCorInfoType(corInfoType));
        }
    }

    return FunctionType::get(retLlvmType, ArrayRef<Type*>(argVec), false);
}

Value* getOrCreateExternalSymbol(const char* symbolName)
{
    Value* symbol = _module->getGlobalVariable(symbolName);
    if (symbol == nullptr)
    {
        symbol = new llvm::GlobalVariable(*_module, Type::getInt32PtrTy(_llvmContext), true, llvm::GlobalValue::LinkageTypes::ExternalLinkage, (llvm::Constant*)nullptr, symbolName);
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

Type* getLLVMTypeForVarType(var_types type)
{
    // TODO: Fill out with missing type mappings and when all code done via clrjit, default should fail with useful
    // message
    switch (type)
    {
        case var_types::TYP_BOOL:
        case var_types::TYP_BYTE:
        case var_types::TYP_UBYTE:
            return Type::getInt8Ty(_llvmContext);
        case var_types::TYP_SHORT:
        case var_types::TYP_USHORT:
            return Type::getInt16Ty(_llvmContext);
        case var_types::TYP_INT:
            return Type::getInt32Ty(_llvmContext);
        case var_types::TYP_REF:
            return Type::getInt8PtrTy(_llvmContext);
        default:
            failFunctionCompilation();
    }
}

Value* castIfNecessary(llvm::IRBuilder<>& builder, Value* source, Type* targetType)
{
    Type* sourceType = source->getType();
    if (sourceType == targetType)
        return source;

    Type::TypeID sourceTypeID = sourceType->getTypeID();
    Type::TypeID targetTypeId = targetType->getTypeID();

    if (targetTypeId == Type::TypeID::PointerTyID)
    {
        switch (sourceTypeID)
        {
            case Type::TypeID::PointerTyID:
                return builder.CreatePointerCast(source, targetType, "CastPtrToPtr");
            case Type::TypeID::IntegerTyID:
                return builder.CreateIntToPtr(source, targetType, "CastPtrToInt");
            default:
                failFunctionCompilation();
        }
    }
    if(targetTypeId == Type::TypeID::IntegerTyID)
    {
        switch (sourceTypeID)
        {
            case Type::TypeID::IntegerTyID:
                if (sourceType->getPrimitiveSizeInBits() > targetType->getPrimitiveSizeInBits())
                {
                    return builder.CreateTrunc(source, targetType, "truncInt");
                }
            default:
                failFunctionCompilation();
        }
    }

    failFunctionCompilation();
}

Value* castToPointerToLlvmType(llvm::IRBuilder<>& builder, Value* address, llvm::Type* llvmType)
{
    return castIfNecessary(builder, address, llvmType->getPointerTo());
}

void castingStore(llvm::IRBuilder<>& builder, Value* toStore, Value* address, llvm::Type* llvmType)
{
    builder.CreateStore(castIfNecessary(builder, toStore, llvmType),
                        castToPointerToLlvmType(builder, address, llvmType));
}

void castingStore(llvm::IRBuilder<>& builder, Value* toStore, Value* address, var_types type)
{
    castingStore(builder, toStore, address, getLLVMTypeForVarType(type));
}

/// <summary>
/// Returns the llvm arg number or shadow stack offset for the corresponding local which must be loaded from an argument
/// </summary>
struct LlvmArgInfo getLlvmArgInfoForArgIx(CORINFO_SIG_INFO& sigInfo, unsigned int lclNum)
{
    if (sigInfo.hasExplicitThis() || sigInfo.hasTypeArg())
        failFunctionCompilation();

    unsigned int llvmArgNum    = 1; // skip shadow stack arg
    bool         returnOnStack = false;

    if (needsReturnStackSlot(sigInfo.retType))
    {
        llvmArgNum++;
    }

    CORINFO_ARG_LIST_HANDLE sigArgs     = sigInfo.args;
    struct LlvmArgInfo      llvmArgInfo = {
        -1 /* default to not an LLVM arg*/, sigInfo.hasThis() ? TARGET_POINTER_SIZE : 0 /* this is the first pointer on
                                                                                           the shadow stack */
    };
    unsigned int shadowStackOffset = llvmArgInfo.m_shadowStackOffset;

    unsigned int i = 0;
    for (; i < sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CorInfoType corInfoType = getCorInfoTypeForArg(sigInfo, sigArgs);
        if (canStoreTypeOnLlvmStack(corInfoType))
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

void emitDoNothingCall(llvm::IRBuilder<>& builder)
{
    if (_doNothingFunction == nullptr)
    {
        _doNothingFunction = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>(), false), Function::ExternalLinkage, 0U, "llvm.donothing", _module);
    }
    builder.CreateCall(_doNothingFunction);
}

Value* buildAdd(llvm::IRBuilder<>& builder, GenTree* node, Value* op1, Value* op2)
{
    if (!op1->getType()->isPointerTy() || !op2->getType()->isIntegerTy())
    {
        //only support gep
        failFunctionCompilation();
    }
    return mapGenTreeToValue(node, builder.CreateGEP(op1, op2));
}

Value* genTreeAsLlvmType(GenTree* tree, Type* type)
{
    Value* v = getGenTreeValue(tree);
    if (v->getType() == type)
        return v;

    if (tree->IsIntegralConst() && tree->TypeIs(TYP_INT))
    {
        if (type->isPointerTy())
        {
            return _builder->CreateIntToPtr(v, type);
        }
        return _builder->getInt({(unsigned int)type->getPrimitiveSizeInBits().getFixedSize(), (uint64_t)tree->AsIntCon()->IconValue(), true});
    }
    failFunctionCompilation();
}

int getTotalParameterOffset(CORINFO_SIG_INFO& sigInfo)
{
    unsigned int offset = 0;

    if (sigInfo.hasThis())
    {
        // If this is a struct, then it's a pointer on the stack
        //if (_thisType.IsValueType)
        //{
        //    offset = PadNextOffset(_thisType.MakeByRefType(), offset);
        //}
        //else
        //{
        //    offset = PadNextOffset(_thisType, offset);
        //}
        // TODO: not as correct as the above, but don't know how to get all the field alignment values needed to implement the
        // equivalent here.  How to get InstanceFieldSize, InstanceFieldAlignment, ComputePackingSize
        offset = TARGET_POINTER_SIZE;
    }

    CORINFO_ARG_LIST_HANDLE sigArgs = sigInfo.args;
    for (unsigned int i = 0; i < sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CorInfoType corInfoType = getCorInfoTypeForArg(sigInfo, sigArgs);
        if (!canStoreTypeOnLlvmStack(corInfoType))
        {
            offset = padNextOffset(corInfoType, offset);
        }
    }

    return AlignUp(offset, TARGET_POINTER_SIZE);
}


unsigned int getTotalRealLocalOffset()
{
    unsigned int offset = 0;
    // TODO: might need this IL->LLVM function for locals and exception funclets
    // for (int i = 0; i < _locals.Length; i++)
    //{
    //    TypeDesc localType = _locals[i].Type;
    //    if (!CanStoreVariableOnStack(localType))
    //    {
    //        offset = padNextOffset(localType, offset);
    //    }
    //}
    return AlignUp(offset, TARGET_POINTER_SIZE);
}

unsigned int getTotalLocalOffset()
{
    unsigned int offset = getTotalRealLocalOffset();
    for (unsigned int i = 0; i < _spilledExpressions.size(); i++)
    {
        offset = padNextOffset(_spilledExpressions[i].m_CorInfoType, offset);
    }
    return AlignUp(offset, TARGET_POINTER_SIZE);
}

unsigned int getSpillOffsetAtIndex(unsigned int index, unsigned int offset)
{
    struct SpilledExpressionEntry spill = _spilledExpressions[index];

    for (unsigned int i = 0; i < index; i++)
    {
        offset = padNextOffset(_spilledExpressions[i].m_CorInfoType, offset);
    }
    offset = padOffset(spill.m_CorInfoType, offset);
    return offset;
}

llvm::Value* getShadowStackOffest(Value* shadowStack, unsigned int offset)
{
    if (offset == 0)
        return shadowStack;

    return _builder->CreateGEP(shadowStack, _builder->getInt32(offset));
}

bool isThisArg(GenTreeCall* call, GenTree* operand)
{
    if (call->gtCallThisArg == nullptr)
    {
        return false;
    }

    return _compiler->gtGetThisArg(call) == operand;
}

void storeOnShadowStack(llvm::IRBuilder<>& builder, GenTree* operand, Value* shadowStackForCallee, unsigned int offset)
{
    castingStore(*_builder, genTreeAsLlvmType(operand, Type::getInt8PtrTy(_llvmContext)),
                 getShadowStackOffest(shadowStackForCallee, offset), Type::getInt8PtrTy(_llvmContext));
}

// shadow stack moved up to avoid overwriting anything on the stack in the compiling method
llvm::Value* getShadowStackForCallee(llvm::IRBuilder<>& builder)
{
    unsigned int offset = getTotalParameterOffset(_sigInfo) + getTotalLocalOffset();

    return offset == 0 ? _function->getArg(0) : builder.CreateGEP(_function->getArg(0), builder.getInt32(offset));
}

llvm::Value* buildUserFuncCall(GenTreeCall* call, llvm::IRBuilder<>& builder)
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

    unsigned int offset = getTotalParameterOffset(_sigInfo) + getTotalLocalOffset();
    // shadowstack arg first
    Value* shadowStackForCallee = getShadowStackForCallee(builder);
    argVec.push_back(shadowStackForCallee);

    Value* returnAddress = nullptr;
    if (needsReturnStackSlot(sigInfo.retType))
    {
        unsigned int returnIndex = _spilledExpressions.size();

        _spilledExpressions.push_back({sigInfo.retType});
        unsigned int varOffset = getSpillOffsetAtIndex(returnIndex, getTotalRealLocalOffset()) + getTotalParameterOffset(_sigInfo);
        returnAddress = _prologBuilder->CreateGEP(_function->getArg(0), builder.getInt32(varOffset), "temp_");

        // TOOD: as per the shadow stack, i8** might be a better type for any spilled return args, as a load normally will follow the call, and then the bitcast can be removed
        argVec.push_back(returnAddress);
    }

    unsigned int                      shadowStackUseOffest = 0;
    int                               argIx                = 0;
    GenTree*                          thisArg              = nullptr;
    fgArgInfo*                        argInfo              = call->fgArgInfo;
    unsigned int                      argCount             = argInfo->ArgCount();
    fgArgTabEntry**                   argTable             = argInfo->ArgTable();
    std::vector<struct OperandArgNum> sortedArgs           = std::vector<struct OperandArgNum>(argCount);
    struct OperandArgNum*             sortedData           = sortedArgs.data();

    for (unsigned i = 0; i < argCount; i++)
    {
        fgArgTabEntry* curArgTabEntry = argTable[i];
        unsigned int   argNum         = curArgTabEntry->argNum;
        struct OperandArgNum opAndArg = {argNum, curArgTabEntry->GetNode()};
        sortedData[argNum]            = opAndArg;
    }

    for (struct OperandArgNum opAndArg : sortedArgs)
    {
        struct LlvmArgInfo llvmArgInfo = getLlvmArgInfoForArgIx(sigInfo, argIx);
        if (llvmArgInfo.m_argIx >= 0)
        {
            // pass the parameter on the LLVM stack
            argVec.push_back(genTreeAsLlvmType(opAndArg.operand, llvmFunc->getArg(llvmArgInfo.m_argIx)->getType()));
        }
        else
        {
            // pass on shadow stack
            storeOnShadowStack(*_builder, opAndArg.operand, shadowStackForCallee, shadowStackUseOffest);
            shadowStackUseOffest += TARGET_POINTER_SIZE;
        }
        argIx++;
    }
    Value* llvmCall = builder.CreateCall(llvmFunc, ArrayRef<Value*>(argVec));
    // TODO: creating the load for the return slot here is perhaps not the most efficient and should be done lazily
    return mapGenTreeToValue(call, returnAddress != nullptr ? builder.CreateLoad(builder.CreateBitCast(returnAddress, Type::getInt8PtrTy(_llvmContext)->getPointerTo())) : llvmCall);
}

Value* buildCall(llvm::IRBuilder<>& builder, GenTree* node)
{
    GenTreeCall* call = node->AsCall();
    if (call->gtCallType == CT_HELPER)
    {
        if (call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_STATIC_BASE))
        {
            const char* symbolName = (*_getMangledSymbolName)(_thisPtr, call->gtEntryPoint.handle);
            Function* llvmFunc = _module->getFunction(symbolName);
            if (llvmFunc == nullptr)
            {
                llvmFunc = Function::Create(FunctionType::get(Type::getInt8PtrTy(_llvmContext), ArrayRef<Type*>(Type::getInt8PtrTy(_llvmContext)), false), Function::ExternalLinkage, 0U, symbolName, _module); // TODO: ExternalLinkage forced as defined in ILC module
            }

            // replacement for _info.compCompHnd->recordRelocation(nullptr, gtCall->gtEntryPoint.handle, IMAGE_REL_BASED_REL32);
            (*_addCodeReloc)(_thisPtr, call->gtEntryPoint.handle);

            return mapGenTreeToValue(node, builder.CreateCall(llvmFunc, getShadowStackForCallee(builder)));
        }
    }
    else if (call->gtCallType == CT_USER_FUNC && !call->IsVirtualStub() /* TODO: Virtual stub not implemented */)
    {
        return buildUserFuncCall(call, builder);
    }
    failFunctionCompilation();
}

Value* buildCnsInt(llvm::IRBuilder<>& builder, GenTree* node)
{
    if (node->gtType == var_types::TYP_INT)
    {
        return mapGenTreeToValue(node, builder.getInt32(node->AsIntCon()->gtIconVal));
    }
    if (node->gtType == var_types::TYP_REF)
    {
        ssize_t intCon = node->AsIntCon()->gtIconVal;
        if (node->IsIconHandle(GTF_ICON_STR_HDL))
        {
            const char* symbolName = (*_getMangledSymbolName)(_thisPtr, (void *)(node->AsIntCon()->IconValue()));
            return mapGenTreeToValue(node, builder.CreateLoad(getOrCreateExternalSymbol(symbolName)));
        }
        // TODO: delete this check, just handling string constants and null ptr stores for now, other TYP_REFs not implemented yet
        if (intCon != 0)
        {
            failFunctionCompilation();
        }

        return mapGenTreeToValue(node, builder.CreateIntToPtr(builder.getInt32(intCon), Type::getInt8PtrTy(_llvmContext))); // TODO: wasm64
    }
    failFunctionCompilation();
}

Value* buildInd(llvm::IRBuilder<>& builder, GenTree* node, Value* ptr)
{
    // TODO: Simplify genActualType(node->TypeGet()) to just genActualType(node) when main is merged
    // first cast the pointer to create the correct load instructions, then cast the result incase we are loading a small int into an int32
    return mapGenTreeToValue(node,
                             castIfNecessary(builder, builder.CreateLoad(
                                 castIfNecessary(builder, ptr,
                                                 getLLVMTypeForVarType(node->TypeGet())->getPointerTo())), getLLVMTypeForVarType(genActualType(node->TypeGet()))));
}

Value* buildNe(llvm::IRBuilder<>& builder, GenTree* node, Value* op1, Value* op2)
{
    return mapGenTreeToValue(node, builder.CreateICmpNE(op1, op2));
}

void buildReturn(llvm::IRBuilder<>& builder, GenTree* node)
{
    switch (node->gtType)
    {
        case var_types::TYP_INT:
            builder.CreateRet(castIfNecessary(builder, getGenTreeValue(node->gtGetOp1()), getLlvmTypeForCorInfoType(_sigInfo.retType)));
            return;
        case var_types::TYP_VOID: 
            builder.CreateRetVoid();
            return;
        default:
            failFunctionCompilation();
    }
}

void importStoreInd(llvm::IRBuilder<>& builder, GenTreeStoreInd* storeIndOp)
{
    Value* address = getGenTreeValue(storeIndOp->Addr());
    Value* toStore = getGenTreeValue(storeIndOp->Data());
    if (toStore->getType()->isPointerTy())
    {
        // RhpAssignRef will never reverse PInvoke, so do not need to store the shadow stack here
        builder.CreateCall(getOrCreateRhpAssignRef(), ArrayRef<Value*>{address, castIfNecessary(builder, toStore, Type::getInt8PtrTy(_llvmContext))});
    }
    else
    {
        castingStore(builder, toStore, address, storeIndOp->gtType);
    }
}

Value* localVar(llvm::IRBuilder<>& builder, GenTreeLclVar* lclVar)
{
    Value*       llvmRef;
    unsigned int lclNum = lclVar->GetLclNum();

    if (_localsMap->find(lclNum) == _localsMap->end())
    {
        if (_compiler->lvaIsParameter(lclNum))
        {
            if (!_info.compIsStatic && _info.compThisArg == lclNum)
            {
                // this is always the first pointer on the shadowstack (LLVM arg 0).  Dont need the gep in this case
                llvmRef = builder.CreateLoad(builder.CreateBitCast(_function->getArg(0), (Type::getInt8PtrTy(_llvmContext)->getPointerTo())));
            }
            else
            {
                unsigned int       argIx       = _info.compIsStatic ? lclNum : lclNum - 1;
                struct LlvmArgInfo llvmArgInfo = getLlvmArgInfoForArgIx(_sigInfo, argIx);
                if (llvmArgInfo.m_argIx >= 0)
                {
                    llvmRef = _function->getArg(llvmArgInfo.m_argIx);
                }
                else
                {
                    // TODO: store argAddress in a map in case multiple IR locals are to the same argument - we only want one gep in the prolog
                    Value* argAddress = _prologBuilder->CreateGEP(_function->getArg(0), builder.getInt32(llvmArgInfo.m_shadowStackOffset), "Argument");
                    llvmRef = builder.CreateLoad(builder.CreateBitCast(argAddress, (Type::getInt8PtrTy(_llvmContext)->getPointerTo())));
                }
            }

            _localsMap->insert({lclNum, llvmRef});
        }
        else
        {
            // unhandled scenario, local is not defined already, and is not a parameter
            failFunctionCompilation();
        }
    }
    else
    {
        llvmRef = _localsMap->at(lclNum);
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
Value* widenIntIfNecessary(llvm::IRBuilder<>& builder, Value* intValue)
{
    if (intValue->getType()->getPrimitiveSizeInBits() < TARGET_POINTER_SIZE * 8)
    {
        return builder.CreateIntCast(intValue, Type::getInt32Ty(_llvmContext), true);
    }
    return intValue;
}

Value* storeLocalVar(llvm::IRBuilder<>& builder, GenTreeLclVar* lclVar)
{
    if (lclVar->gtFlags & GTF_VAR_DEF)
    {
        Value* valueRef = getGenTreeValue(lclVar->gtGetOp1());
        assert(valueRef != nullptr);
        // This could be done in the NE operator, but sometimes that would be needless, e.g. when followed by JTRUE
        if (valueRef->getType()->isIntegerTy())
        {
            valueRef = widenIntIfNecessary(builder, valueRef);
        }

        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar);

        _localsMap->insert({lclVar->GetLclNum(), valueRef});
        return valueRef;
    }
    else
    {
        failFunctionCompilation();
    }
}

Value* visitNode(llvm::IRBuilder<>& builder, GenTree* node)
{
    switch (node->OperGet())
    {
        case GT_ADD:
            return buildAdd(builder, node, getGenTreeValue(node->AsOp()->gtOp1), getGenTreeValue(node->AsOp()->gtOp2));
        case GT_CALL:
            return buildCall(builder, node);
        case GT_CNS_INT:
            return buildCnsInt(builder, node);
        case GT_IL_OFFSET:
            _currentOffset = node->AsILOffset()->gtStmtILoffsx;
            break;
        case GT_IND:
            return buildInd(builder, node, getGenTreeValue(node->AsOp()->gtOp1));
        case GT_LCL_VAR:
            return localVar(builder, node->AsLclVar());
        case GT_NE:
            return buildNe(builder, node, getGenTreeValue(node->AsOp()->gtOp1), getGenTreeValue(node->AsOp()->gtOp2));
        case GT_NO_OP:
            emitDoNothingCall(builder);
            break;
        case GT_RETURN:
            buildReturn(builder, node);
            break;
        case GT_STORE_LCL_VAR:
            return storeLocalVar(builder, node->AsLclVar());
            break;
        case GT_STOREIND:
            importStoreInd(builder, (GenTreeStoreInd*)node);
            break;
        default:
            failFunctionCompilation();
    }
    return nullptr;
}

llvm::BasicBlock* getLLVMBasicBlockForBlock(BasicBlock* block)
{
    llvm::BasicBlock* llvmBlock;
    if (_blkToLlvmBlkVectorMap->Lookup(block, &llvmBlock)) return llvmBlock;

    llvmBlock = llvm::BasicBlock::Create(_llvmContext, "", _function);
    _blkToLlvmBlkVectorMap->Set(block, llvmBlock);
    return llvmBlock;
}

void endImportingBasicBlock(BasicBlock* block)
{
    if (block->bbJumpKind == BBjumpKinds::BBJ_NONE && block->bbNext)
    {
        _builder->CreateBr(getLLVMBasicBlockForBlock(block->bbNext));
    }
}

void generateProlog()
{
    // create a prolog block to store arguments passed on shadow stack, TODO: other things from ILToLLVMImporter to come
    _prologBuilder = new llvm::IRBuilder<>(_llvmContext);
    llvm::BasicBlock* prologBlock = llvm::BasicBlock::Create(_llvmContext, "Prolog", _function);
    _prologBuilder->SetInsertPoint(prologBlock);

    llvm::BasicBlock* block0 = getLLVMBasicBlockForBlock(_compiler->fgFirstBB);
    _prologBuilder->SetInsertPoint(_prologBuilder->CreateBr(block0)); // position _prologBuilder to add locals and arguments
    _builder->SetInsertPoint(block0);
}

struct DebugMetadata getOrCreateDebugMetadata(const char* documentFileName)
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

llvm::DILocation* createDebugFunctionAndDiLocation(struct DebugMetadata debugMetadata, unsigned int lineNo)
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

void startImportingNode(llvm::IRBuilder<>& builder)
{
    if (_compiler->opts.compDbgInfo)
    {
        const char* documentFileName = _getDocumentFileName(_thisPtr);

        if (documentFileName && *documentFileName != '\0')
        {
            unsigned int lineNo = _getOffsetLineNumber(_thisPtr, _currentOffset);

            struct DebugMetadata debugMetadata = getOrCreateDebugMetadata(documentFileName);

            llvm::DILocation* diLocation = createDebugFunctionAndDiLocation(debugMetadata, lineNo);
            builder.SetCurrentDebugLocation(diLocation);
        }
    }
}

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
void Llvm::Compile(Compiler* pCompiler)
{
    _compiler = pCompiler;
    _info = pCompiler->info;
    CompAllocator allocator = pCompiler->getAllocator();
    BlkToLlvmBlkVectorMap blkToLlvmBlkVectorMap(allocator);
    _blkToLlvmBlkVectorMap = &blkToLlvmBlkVectorMap;
    std::unordered_map<GenTree*, Value*> sdsuMap;
    _sdsuMap = &sdsuMap;
    _localsMap = new std::unordered_map<unsigned int, Value*>();
    _spilledExpressions.clear();
    const char* mangledName = (*_getMangledMethodName)(_thisPtr, _info.compMethodHnd);
    _function               = _module->getFunction(mangledName);
    _debugFunction          = nullptr;
    _compiler->eeGetMethodSig(_info.compMethodHnd, &_sigInfo);

    if (_function == nullptr)
    {
        _function = Function::Create(getFunctionTypeForSigInfo(_sigInfo), Function::ExternalLinkage, 0U, mangledName,
                                     _module); // TODO: ExternalLinkage forced as linked from old module
    }

    llvm::IRBuilder<> builder(_llvmContext);
    _builder = &builder;

    generateProlog();

    for (BasicBlock* block = pCompiler->fgFirstBB; block; block = block->bbNext)
    {
        if (block->hasTryIndex())
        {
            failFunctionCompilation();
        }

        llvm::BasicBlock* entry = getLLVMBasicBlockForBlock(block);
        builder.SetInsertPoint(entry);
        for (GenTree* node = block->GetFirstLIRNode(); node; node = node->gtNext)
        {
            startImportingNode(builder);
            visitNode(builder, node);
        }
        endImportingBasicBlock(block);
    }
    if (_debugFunction != nullptr)
    {
        _diBuilder->finalizeSubprogram(_debugFunction);
    }
}
#endif
