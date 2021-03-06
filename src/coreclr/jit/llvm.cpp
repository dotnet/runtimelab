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

extern "C" DLLEXPORT void registerLlvmCallbacks(void* thisPtr, const char* outputFileName, const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*))
{
    _thisPtr = thisPtr;
    _getMangledMethodName = getMangledMethodNamePtr;
//    _outputFileName = getAllocator(CMK_DebugOnly).allocate<char>(strlen(outputFileName) + 1)
    _outputFileName = (char*)malloc(strlen(outputFileName) + 7);
    strcpy(_outputFileName, "1.txt"); // ??? without this _outputFileName is corrupted
    strcpy(_outputFileName, outputFileName);
    strcpy(_outputFileName + strlen(_outputFileName) - 4, "clrjit"); // use different module output name for now, TODO: delete if old LLVM gen does not create a module
    strcat(_outputFileName, ".bc");
}

void Llvm::Init()
{
    _module = new Module(llvm::StringRef("netscripten-clrjit"), _llvmContext);
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

bool visitNode(llvm::IRBuilder<> &builder, GenTree* node)
{
    switch (node->gtOper)
    {
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

        for (Statement* stmt = block->bbStmtList; stmt; stmt = stmt->GetNextStmt())
        {
            if (!visitNode(builder, stmt->GetRootNode()))
            {
                // delete created function , dont want duplicate symbols
                function->removeFromParent();
                fatal(CORJIT_SKIPPED); // visitNode incomplete
            }
        }
    }
    builder.CreateRetVoid();
}
#endif
