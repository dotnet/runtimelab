// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifdef TARGET_WASM
#include "llvm.h"
#include "llvm/IR/Module.h"
#include "llvm/IR/Function.h"
#include "compiler.h"

using llvm::Function;
using llvm::FunctionType;
using llvm::Type;
using llvm::LLVMContext;
using llvm::ArrayRef;
using llvm::Module;

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
void Llvm::Compile(Compiler* pCompiler)
{
    Module* llvmModule = (Module *)pCompiler->info.compCompHnd->getLlvmModule();
    LLVMContext& llvmContext = llvmModule->getContext();

    const char* mangledName = pCompiler->info.compCompHnd->getMethodName(pCompiler->info.compMethodHnd, NULL);
    std::vector<Type*> argTypes;
    Function* function = Function::Create(FunctionType::get(Type::getInt32Ty(llvmContext), ArrayRef<Type *>(argTypes), false), Function::InternalLinkage, 0U, mangledName, llvmModule);
}

#endif
