// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef TARGET_WASM
#include <string.h>
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

using llvm::Function;
using llvm::FunctionType;
using llvm::Type;
using llvm::LLVMContext;
using llvm::ArrayRef;
using llvm::Module;

static Module* _module;
static LLVMContext _llvmContext;
static void* _thisPtr;
static const char* (*_getMangledMethodName)(void*, CORINFO_METHOD_STRUCT_*);
static char* _outputFileName;
static Function* _doNothingFunction;

extern "C" DLLEXPORT void registerLlvmCallbacks(void* thisPtr, const char* outputFileName, const char* triple, const char* dataLayout, const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*))
{
    _thisPtr = thisPtr;
    _getMangledMethodName = getMangledMethodNamePtr;
    _module = new Module(llvm::StringRef("netscripten"), _llvmContext);
    _module->setTargetTriple(triple);
    _module->setDataLayout(dataLayout);

//    _outputFileName = getAllocator(CMK_DebugOnly).allocate<char>(strlen(outputFileName) + 1)
    _outputFileName = (char*)malloc(strlen(outputFileName) + 7);
    strcpy(_outputFileName, "1.txt"); // ??? without this _outputFileName is corrupted
    strcpy(_outputFileName, outputFileName);
    strcpy(_outputFileName + strlen(_outputFileName) - 3, "clrjit"); // use different module output name for now, TODO: delete if old LLVM gen does not create a module
    strcat(_outputFileName, ".bc");
}

void Llvm::Init()
{
}

void Llvm::llvmShutdown()
{
#if DEBUG
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
//    Module.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);
}

void EmitDoNothingCall(llvm::IRBuilder<>& builder)
{
    if (_doNothingFunction == nullptr)
    {
        _doNothingFunction = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>(), false), Function::ExternalLinkage, 0U, "llvm.donothing", _module);
    }
    builder.CreateCall(_doNothingFunction);
}

bool visitNode(llvm::IRBuilder<> &builder, GenTree* node)
{
    switch (node->OperGet())
    {
        case GT_IL_OFFSET:
            break;
        case GT_NO_OP:
            EmitDoNothingCall(builder);
            break;
        case GT_RETURN:
            builder.CreateRetVoid();
            break;
        default:
             return false;
    }
    return true;
}

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
void Llvm::Compile(Compiler* pCompiler)
{
    Compiler::Info info = pCompiler->info;

    //TODO: delete
    if (info.compArgsCount != 0 || info.compRetType != TYP_VOID)
    {
        fatal(CORJIT_SKIPPED);
    }
    const char* mangledName = (*_getMangledMethodName)(_thisPtr, info.compMethodHnd);
    Function* function = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>(), false), Function::InternalLinkage, 0U, mangledName, _module);

    BasicBlock* firstBb = pCompiler->fgFirstBB;
    llvm::IRBuilder<> builder(_llvmContext);
    for (BasicBlock* block = firstBb; block; block = block->bbNext)
    {
        llvm::BasicBlock* entry = llvm::BasicBlock::Create(_llvmContext, "", function);
        builder.SetInsertPoint(entry);
  //      GenTree* firstGt = block->GetFirstLIRNode();
//        firstGt->VisitOperands();
        for (GenTree* node = block->GetFirstLIRNode(); node; node = node->gtNext)
        {
            if (!visitNode(builder, node))
            {
                // delete created function , dont want duplicate symbols
                function->removeFromParent();
                fatal(CORJIT_SKIPPED); // visitNode incomplete
            }
        }
    }
}
#endif
