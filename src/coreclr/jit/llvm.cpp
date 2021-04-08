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

Compiler::Info _info;
Compiler* _compiler;
Function* _function;
BlkToLlvmBlkVectorMap *_blkToLlvmBlkVectorMap;
llvm::IRBuilder<>* _builder;
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

void failFunctionCompilation()
{
    _function->dropAllReferences();
    _function->eraseFromParent();
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


FunctionType* getFunctionTypeForMethod(Compiler::Info info)
{
    if (info.compArgsCount != 0 || info.compRetType != TYP_VOID)
    {
        fatal(CORJIT_SKIPPED);
    }
    // all functions have shadow stack as first arg (i8*)
    return FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>(Type::getInt8PtrTy(_llvmContext)), false);
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
                llvmFunc = Function::Create(FunctionType::get(Type::getInt8PtrTy(_llvmContext), ArrayRef<Type*>(Type::getInt8PtrTy(_llvmContext)), false), Function::ExternalLinkage, 0U, symbolName, _module); // TODO: ExternalLinkage forced as linked from old module
            }
            // replacement for _info.compCompHnd->recordRelocation(nullptr, gtCall->gtEntryPoint.handle, IMAGE_REL_BASED_REL32);
            (*_addCodeReloc)(_thisPtr, gtCall->gtEntryPoint.handle);
            return mapTreeIdValue(node->gtTreeID, builder.CreateCall(llvmFunc, _function->getArg(0)));
        }
        else failFunctionCompilation();
    }
    else failFunctionCompilation();

    // unreachable
    fatal(CORJIT_SKIPPED);
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
        if (intCon != 0)
        {
            failFunctionCompilation();
        }

        return mapTreeIdValue(node->gtTreeID, builder.CreateIntToPtr(builder.getInt32(intCon), Type::getInt8PtrTy(_llvmContext))); // TODO: wasm64
    }
    failFunctionCompilation();

    // unreachable
    fatal(CORJIT_SKIPPED);
}

void importStoreInd(llvm::IRBuilder<>& builder, GenTree* node)
{
    Value* address = getTreeIdValue(node->AsOp()->gtOp1);
    Value* toStore = getTreeIdValue(node->AsOp()->gtOp2);
    // TODO: delete this temporary check for supported stores
    if (!address->getType()->isPointerTy() || !toStore->getType()->isPointerTy())
    {
        //RhpAssignRef only
        failFunctionCompilation();
    }

    // RhpAssignRef will never reverse PInvoke, so do not need to store the shadow stack here
    builder.CreateCall(getOrCreateRhpAssignRef(), ArrayRef<Value*>{address, toStore});
}

Value* visitNode(llvm::IRBuilder<>& builder, GenTree* node);

Value* storeLocalVar(llvm::IRBuilder<>& builder, GenTree* tree)
{
    if (tree->gtFlags & GTF_VAR_DEF)
    {
        Value*         valueRef = visitNode(builder, tree);
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
        case GT_NO_OP:
            emitDoNothingCall(builder);
            break;
        case GT_RETURN:
            builder.CreateRetVoid();
            break;
        case GT_STORE_LCL_VAR:
            return storeLocalVar(builder, node->gtGetOp1());
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
    const char* mangledName = (*_getMangledMethodName)(_thisPtr, _info.compMethodHnd);
    _function = Function::Create(getFunctionTypeForMethod(_info), Function::ExternalLinkage, 0U, mangledName, _module); // TODO: ExternalLinkage forced as linked from old module

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
