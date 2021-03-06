// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef TARGET_WASM
#include "compiler.h"
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

extern "C" DLLEXPORT void registerLlvmCallbacks(void* thisPtr, const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*))
{
    _thisPtr = thisPtr;
    _getMangledMethodName = getMangledMethodNamePtr;
}

void Llvm::Init()
{
    _module = new Module(llvm::StringRef("netscripten-clrjit"), _llvmContext);
}

void Llvm::llvmShutdown()
{
#if DEBUG
    _module->dump();
#endif //DEBUG
    std::error_code ec;
    llvm::raw_fd_ostream OS("module.bc", ec);
    llvm::WriteBitcodeToFile(*_module, OS);
//    Module.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);

    //Module.WriteBitcodeToFile(_objectFilePath);
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
    // TODO: use of getMethodName is wrong as its only for debug purposes.
    const char* mangledName = (*_getMangledMethodName)(_thisPtr, info.compMethodHnd);
    Function* function = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>(), false), Function::InternalLinkage, 0U, mangledName, _module);

    llvm::IRBuilder<> builder(_llvmContext);
    llvm::BasicBlock* entry = llvm::BasicBlock::Create(_llvmContext, "", function);
    builder.SetInsertPoint(entry);
    builder.CreateRetVoid();
}
#endif
