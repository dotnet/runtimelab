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

typedef JitHashTable<BasicBlock*, JitPtrKeyFuncs<BasicBlock>, llvm::BasicBlock*> BlkToLlvmBlkVectorMap;

static Module* _module = nullptr;
static LLVMContext _llvmContext;
static void* _thisPtr; // TODO: workaround for not changing the JIT/EE interface.  As this is static, it will probably fail if multithreaded compilation is attempted
static const char* (*_getMangledMethodName)(void*, CORINFO_METHOD_STRUCT_*);
static const char* (*_getMangledSymbolName)(void*, void*);
static const char* (*_addCodeReloc)(void*, void*);
static const uint32_t (*_isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*);
static char*     _outputFileName;
static Function* _doNothingFunction;
Compiler::Info                            _info;
Compiler*                                 _compiler;
Function*                                 _function;
BlkToLlvmBlkVectorMap*                    _blkToLlvmBlkVectorMap;
llvm::IRBuilder<>*                        _builder;
std::unordered_map<GenTree*, Value*>*     _sdsuMap;
std::unordered_map<unsigned int, Value*>* _localsMap;
CORINFO_SIG_INFO                          _sigInfo; // sigInfo of function being compiled
llvm::IRBuilder<>*                        _prologBuilder;

extern "C" DLLEXPORT void registerLlvmCallbacks(void*       thisPtr,
                                                const char* outputFileName,
                                                const char* triple,
                                                const char* dataLayout,
                                                const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*),
                                                const char* (*getMangledSymbolNamePtr)(void*, void*),
                                                const char* (*addCodeRelocPtr)(void*, void*),
                                                const uint32_t (*isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*)
    )
{
    _thisPtr = thisPtr;
    _getMangledMethodName = getMangledMethodNamePtr;
    _getMangledSymbolName = getMangledSymbolNamePtr;
    _addCodeReloc         = addCodeRelocPtr;
    _isRuntimeImport      = isRuntimeImport;
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

void Llvm::llvmShutdown()
{
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
            return Type::getInt1Ty(_llvmContext);

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

    // start vector with shadow stack arg
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

Value* castIfNecessary(llvm::IRBuilder<>& builder, Value* source, Type* valueType)
{
    Type* sourceType = source->getType();
    if (sourceType == valueType)
        return source;

    Type::TypeID sourceTypeID = sourceType->getTypeID();
    Type::TypeID valueTypeKind = valueType->getTypeID();

    if (valueTypeKind == Type::TypeID::PointerTyID)
    {
        switch (sourceTypeID)
        {
            case Type::TypeID::PointerTyID:
                return builder.CreatePointerCast(source, valueType, "CastPtrToPtr");
            case Type::TypeID::IntegerTyID:
                return builder.CreateIntToPtr(source, valueType, "CastPtrToInt");
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

int getTotalLocalOffset(CORINFO_SIG_INFO& sigInfo)
{
    // TODO: need to store some locals on ths shadow stack, either when there are exception blocks, or they are, or have, GC pointers (so the conservative GC knows they are live)
    // For now we don't have any so simply:
    return 0;
}

llvm::Value* getShadowStackOffest(Value* shadowStack, unsigned int offset)
{
    if (offset == 0)
        return shadowStack;

    return _builder->CreateGEP(shadowStack, _builder->getInt32(TARGET_POINTER_SIZE));
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
    if (needsReturnStackSlot(sigInfo.retType))
    {
        // TODO: enough already in this PR, leave calling functions that need a spilled slot for later.
        failFunctionCompilation();
    }

    if (llvmFunc == nullptr)
    {
        CORINFO_ARG_LIST_HANDLE sigArgs = sigInfo.args;

        // assume ExternalLinkage, if the function is defined in the clrjit module, then it is replaced and an extern
        // added to the Ilc module
        llvmFunc = Function::Create(getFunctionTypeForSigInfo(sigInfo), Function::ExternalLinkage,
                                    0U, symbolName, _module);
    }
    std::vector<llvm::Value*> argVec;

    unsigned int offset = getTotalParameterOffset(_sigInfo) + getTotalLocalOffset(sigInfo);

    // shadowstack arg first
    Value* shadowStackForCallee = offset == 0 ? _function->getArg(0) : builder.CreateGEP(_function->getArg(0), builder.getInt32(offset));
    argVec.push_back(shadowStackForCallee);

    unsigned int                      shadowStackUseOffest = 0;
    int                               argIx                = 0;
    GenTree*                          thisArg              = nullptr;
    fgArgInfo*                        argInfo              = call->fgArgInfo;
    unsigned int                      argCount             = argInfo->ArgCount();
    fgArgTabEntry**                   argTable             = argInfo->ArgTable();
    std::vector<struct OperandArgNum> sortedArgs           = std::vector<struct OperandArgNum>(argCount);
    struct OperandArgNum*             sortedData           = sortedArgs.data();

    // "this" goes first
    if (call->gtCallThisArg != nullptr)
    {
        thisArg = _compiler->gtGetThisArg(call);

        // TODO: add throw if this is null
        storeOnShadowStack(*_builder, thisArg, shadowStackForCallee, shadowStackUseOffest);
        shadowStackUseOffest += TARGET_POINTER_SIZE;
    }

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
    return mapGenTreeToValue(call, builder.CreateCall(llvmFunc, ArrayRef<Value*>(argVec)));
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
            return mapGenTreeToValue(node, builder.CreateCall(llvmFunc, _function->getArg(0)));
        }
    }
    else if (call->gtCallType == CT_USER_FUNC)
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
    return mapGenTreeToValue(node, builder.CreateLoad(castIfNecessary(builder, ptr, getLLVMTypeForVarType(genActualType(node->TypeGet()))->getPointerTo())));
}

Value* buildNe(llvm::IRBuilder<>& builder, GenTree* node, Value* op1, Value* op2)
{
    return mapGenTreeToValue(node, builder.CreateICmpNE(op1, op2));
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
            struct LlvmArgInfo llvmArgInfo = getLlvmArgInfoForArgIx(_sigInfo, lclNum);
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

Value* storeLocalVar(llvm::IRBuilder<>& builder, GenTreeLclVar* lclVar)
{
    if (lclVar->gtFlags & GTF_VAR_DEF)
    {
        Value* valueRef = getGenTreeValue(lclVar->gtGetOp1());
        assert(valueRef != nullptr);
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
            builder.CreateRetVoid();
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
    const char *mangledName = (*_getMangledMethodName)(_thisPtr, _info.compMethodHnd);
    _function    = _module->getFunction(mangledName);
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
            visitNode(builder, node);
        }
        endImportingBasicBlock(block);
    }
}
#endif
