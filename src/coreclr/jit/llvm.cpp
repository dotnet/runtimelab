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

typedef JitHashTable<BasicBlock*, JitPtrKeyFuncs<BasicBlock>, llvm::BasicBlock*> BlkToLlvmBlkVectorMap;

static Module* _module = nullptr;
static LLVMContext _llvmContext;
static void* _thisPtr; // TODO: workaround for not changing the JIT/EE interface.  As this is static, it will probably fail if multithreaded compilation is attempted
static const char* (*_getMangledMethodName)(void*, CORINFO_METHOD_STRUCT_*);
static const char* (*_getMangledSymbolName)(void*, void*);
static const char* (*_addCodeReloc)(void*, void*);
static char* _outputFileName;
static Function* _doNothingFunction;

Compiler::Info                            _info;
Compiler*                                 _compiler;
Function*                                 _function;
BlkToLlvmBlkVectorMap*                    _blkToLlvmBlkVectorMap;
llvm::IRBuilder<>*                        _builder;
std::unordered_map<unsigned int, Value*>* _sdsuMap;
std::unordered_map<unsigned int, Value*>* _localsMap;

extern "C" DLLEXPORT void registerLlvmCallbacks(void* thisPtr, const char* outputFileName, const char* triple, const char* dataLayout,
    const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*),
    const char* (*getMangledSymbolNamePtr)(void*, void*),
    const char* (*addCodeRelocPtr)(void*, void*)
    )
{
    _thisPtr = thisPtr;
    _getMangledMethodName = getMangledMethodNamePtr;
    _getMangledSymbolName = getMangledSymbolNamePtr;
    _addCodeReloc = addCodeRelocPtr;
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
    _function->deleteBody();
    fatal(CORJIT_SKIPPED);
}

Value* mapTreeIdValue(unsigned int treeId, Value* valueRef)
{
    if (_sdsuMap->find(treeId) != _sdsuMap->end())
    {
        fatal(CorJitResult::CORJIT_INTERNALERROR);
    }
    _sdsuMap->insert({ treeId, valueRef });
    return valueRef;
}

Value* getTreeIdValue(GenTree* op)
{
    return _sdsuMap->at(op->gtTreeID);
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

            // these need to go on the shadow stack.  TODO when Ilc module is gone, as a performance improvement can we pass byrefs to non struct value types on the llvm stack?
        case CorInfoType::CORINFO_TYPE_BYREF:
        case CorInfoType::CORINFO_TYPE_CLASS:
            failFunctionCompilation();

        default:
            failFunctionCompilation();
    }
}

FunctionType* getFunctionTypeForMethodHandle(CORINFO_METHOD_HANDLE methodHandle)
{
    CORINFO_SIG_INFO sigInfo;
    _compiler->eeGetMethodSig(methodHandle, &sigInfo);
    if (sigInfo.hasExplicitThis() || sigInfo.hasThis() || sigInfo.hasTypeArg())
        failFunctionCompilation();

    llvm::Type* retLlvmType = getLlvmTypeForCorInfoType(sigInfo.retType);
    std::vector<llvm::Type*> argVec(sigInfo.numArgs + 1);
    CORINFO_ARG_LIST_HANDLE  sigArgs = sigInfo.args;
    argVec[0]                        = Type::getInt8PtrTy(_llvmContext); // shadowstack arg

    for (unsigned int i = 0; i < sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CORINFO_CLASS_HANDLE clsHnd;
        CorInfoTypeWithMod   corTypeWithMod = _info.compCompHnd->getArgType(&sigInfo, sigArgs, &clsHnd);
        argVec[i + 1] = getLlvmTypeForCorInfoType(strip(corTypeWithMod));
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

Value* castIfNecessary(llvm::IRBuilder<>& builder, Value* source, Type* valueType)
{
    Type* sourceType = source->getType();
    if (sourceType == valueType)
        return source;

    Type::TypeID toStoreTypeID = sourceType->getTypeID();
    Type::TypeID valueTypeKind = valueType->getTypeID();

    if (toStoreTypeID == Type::TypeID::PointerTyID && valueTypeKind == Type::TypeID::PointerTyID)
    {
        return builder.CreatePointerCast(source, valueType, "CastPtr");
    }
    failFunctionCompilation();
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
    return mapTreeIdValue(node->gtTreeID, builder.CreateGEP(op1, op2));
}

FunctionType* getFunctionSignature(CORINFO_CONST_LOOKUP callTarget)
{
    return FunctionType::get(Type::getInt8PtrTy(_llvmContext), ArrayRef<Type*>(Type::getInt8PtrTy(_llvmContext)), false);
}

Value* genTreeAsLlvmType(GenTree* gt, Type* t)
{
    Value* v = getTreeIdValue(gt);
    if (v->getType() == t)
        return v;

    if (gt->OperGet() == GT_CNS_INT && gt->gtType == var_types::TYP_INT)
    {
        return _builder->getInt({(unsigned int)t->getPrimitiveSizeInBits().getFixedSize(), (uint64_t)gt->AsIntCon()->gtIconVal, true});
    }
    failFunctionCompilation();
}

llvm::Value* buildUserFuncCall(GenTreeCall* gtCall, llvm::IRBuilder<>& builder)
{
    const char* symbolName = (*_getMangledSymbolName)(_thisPtr, gtCall->gtEntryPoint.handle);
    // TODO: how to detect RuntimeImport ?
    if (!strcmp(symbolName, "S_P_CoreLib_System_Runtime_RuntimeImports__MemoryBarrier") ||
        !strcmp(symbolName, "S_P_CoreLib_System_Runtime_RuntimeImports__RhCollect"))
    {
        failFunctionCompilation();
    }

    (*_addCodeReloc)(_thisPtr, gtCall->gtEntryPoint.handle);
    Function* llvmFunc = _module->getFunction(symbolName);
    if (llvmFunc == nullptr)
    {
        CORINFO_SIG_INFO sigInfo;
        _compiler->eeGetMethodSig(gtCall->gtCallMethHnd, &sigInfo);
        CORINFO_ARG_LIST_HANDLE sigArgs = sigInfo.args;

        // assume ExternalLinkage, if the function is defined in the clrjit module, then it is replaced and an extern
        // added to the Ilc module
        llvmFunc = Function::Create(getFunctionTypeForMethodHandle(gtCall->gtCallMethHnd), Function::ExternalLinkage,
                                    0U, symbolName, _module);
    }
    std::vector<llvm::Value*> argVec;

    // shadowstack arg first
    argVec.push_back(_function->getArg(0));

    // TODO Is Args() more appropriate than Operands() ?
    // for (GenTreeCall::Use& arg : gtCall->Args())
    int argIx = 1;
    for (GenTree* operand : gtCall->Operands())
    {
        // copied this logic from gtDispLIRNode
        if (operand->IsArgPlaceHolderNode() || !operand->IsValue())
        {
            // Either of these situations may happen with calls.
            continue;
        }

        argVec.push_back(genTreeAsLlvmType(operand, llvmFunc->getArg(argIx)->getType()));
        argIx++;
    }
    return mapTreeIdValue(gtCall->gtTreeID, builder.CreateCall(llvmFunc, ArrayRef<Value*>(argVec)));
}

Value* buildCall(llvm::IRBuilder<>& builder, GenTree* node)
{
    GenTreeCall* gtCall = node->AsCall();
    if (gtCall->gtCallType == CT_HELPER)
    {
        if (gtCall->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_STATIC_BASE))
        {
            const char* symbolName = (*_getMangledSymbolName)(_thisPtr, gtCall->gtEntryPoint.handle);
            Function* llvmFunc = _module->getFunction(symbolName);
            if (llvmFunc == nullptr)
            {
                llvmFunc = Function::Create(FunctionType::get(Type::getInt8PtrTy(_llvmContext), ArrayRef<Type*>(Type::getInt8PtrTy(_llvmContext)), false), Function::ExternalLinkage, 0U, symbolName, _module); // TODO: ExternalLinkage forced as defined in ILC module
            }
            // replacement for _info.compCompHnd->recordRelocation(nullptr, gtCall->gtEntryPoint.handle, IMAGE_REL_BASED_REL32);
            (*_addCodeReloc)(_thisPtr, gtCall->gtEntryPoint.handle);
            return mapTreeIdValue(node->gtTreeID, builder.CreateCall(llvmFunc, _function->getArg(0)));
        }
    }
    else if (gtCall->gtCallType == CT_USER_FUNC)
    {
        return buildUserFuncCall(gtCall, builder);
    }
    failFunctionCompilation();
}

Value* buildCnsInt(llvm::IRBuilder<>& builder, GenTree* node)
{
    if (node->gtType == var_types::TYP_INT)
    {
        return mapTreeIdValue(node->gtTreeID, builder.getInt32(node->AsIntCon()->gtIconVal));
    }
    if (node->gtType == var_types::TYP_REF)
    {
        //TODO: delete this check, just handling null ptr stores for now, other TYP_REFs include string constants which are not implemented
        ssize_t intCon = node->AsIntCon()->gtIconVal;
        if (node->IsIconHandle(GTF_ICON_STR_HDL))
        {
            const char* symbolName = (*_getMangledSymbolName)(_thisPtr, (void *)(node->AsIntCon()->IconValue()));
            return mapTreeIdValue(node->gtTreeID, builder.CreateLoad(getOrCreateExternalSymbol(symbolName)));
        }
        if (intCon != 0)
        {
            failFunctionCompilation();
        }

        return mapTreeIdValue(node->gtTreeID, builder.CreateIntToPtr(builder.getInt32(intCon), Type::getInt8PtrTy(_llvmContext))); // TODO: wasm64
    }
    failFunctionCompilation();
}

Type*  getLLVMTypeForVarType(var_types type)
{
    // TODO: ill out with missing type mappings and when all code done via clrjit, default should fail with useful message
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
        default:
            failFunctionCompilation();
    }
}

Value* castToPointerToVarType(llvm::IRBuilder<>& builder, Value* address, var_types type)
{
    return castIfNecessary(builder, address, getLLVMTypeForVarType(type)->getPointerTo());
}

void castingStore(llvm::IRBuilder<>& builder, Value* toStore, Value* address, var_types type)
{
    builder.CreateStore(castIfNecessary(builder, toStore, getLLVMTypeForVarType(type)), castToPointerToVarType(builder, address, type));
}

void importStoreInd(llvm::IRBuilder<>& builder, GenTree* node)
{
    Value* address = getTreeIdValue(node->AsOp()->gtOp1);
    Value* toStore = getTreeIdValue(node->AsOp()->gtOp2);
    // TODO: delete this temporary check for supported stores
    // assert address->getType()->isPointerTy()
    if (toStore->getType()->isPointerTy())
    {
        // RhpAssignRef will never reverse PInvoke, so do not need to store the shadow stack here
        builder.CreateCall(getOrCreateRhpAssignRef(), ArrayRef<Value*>{address, castIfNecessary(builder, toStore, Type::getInt8PtrTy(_llvmContext))});
    }
    else
    {
        castingStore(builder, toStore, address, node->gtType);
    }
}

Value* localVar(llvm::IRBuilder<>& builder, GenTree* tree)
{
    GenTreeLclVar* lclVar = tree->AsLclVar();
    Value*         llvmRef = _localsMap->at(lclVar->GetLclNum());
    mapTreeIdValue(tree->gtTreeID, llvmRef);
    return llvmRef;
}

Value* storeLocalVar(llvm::IRBuilder<>& builder, GenTree* tree)
{
    if (tree->gtFlags & GTF_VAR_DEF)
    {
        Value*         valueRef = getTreeIdValue(tree->gtGetOp1());
        assert(valueRef != nullptr);
        GenTreeLclVar* lclVar   = tree->AsLclVar();
        LclVarDsc*     varDsc   = _compiler->lvaGetDesc(lclVar);

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
            return buildAdd(builder, node, getTreeIdValue(node->AsOp()->gtOp1), getTreeIdValue(node->AsOp()->gtOp2));
        case GT_CALL:
            return buildCall(builder, node);
        case GT_CNS_INT:
            return buildCnsInt(builder, node);
        case GT_IL_OFFSET:
            break;
        case GT_LCL_VAR:
            return localVar(builder, node);
        case GT_NO_OP:
            emitDoNothingCall(builder);
            break;
        case GT_RETURN:
            builder.CreateRetVoid();
            break;
        case GT_STORE_LCL_VAR:
            return storeLocalVar(builder, node);
            break;
        case GT_STOREIND:
            importStoreInd(builder, node);
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
    std::unordered_map<unsigned int, Value*> sdsuMap;
    _sdsuMap = &sdsuMap;
    _localsMap = new std::unordered_map<unsigned int, Value*>();
    const char *mangledName = (*_getMangledMethodName)(_thisPtr, _info.compMethodHnd);
    _function    = _module->getFunction(mangledName);
    if (_function == nullptr)
    {
        _function = Function::Create(getFunctionTypeForMethodHandle(_info.compMethodHnd), Function::ExternalLinkage, 0U, mangledName,
                                     _module); // TODO: ExternalLinkage forced as linked from old module
    }

    BasicBlock* firstBb = pCompiler->fgFirstBB;
    llvm::IRBuilder<> builder(_llvmContext);
    _builder = &builder;
    for (BasicBlock* block = firstBb; block; block = block->bbNext)
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
