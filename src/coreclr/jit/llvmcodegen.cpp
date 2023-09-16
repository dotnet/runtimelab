// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                                            LLVM-based codegen                                                |
// ================================================================================================================

#include "llvm.h"

#define BBNAME(prefix, index) Twine(prefix) + ((index < 10) ? "0" : "") + Twine(index)

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
void Llvm::Compile()
{
    initializeFunctions();
    initializeDebugInfo();

    JITDUMPEXEC(_compiler->fgDispBasicBlocks());
    JITDUMPEXEC(_compiler->fgDispHandlerTab());

    generateProlog();
    generateBlocks();
    generateEHDispatch();

    fillPhis();

    if (m_diFunction != nullptr)
    {
        m_diBuilder->finalize();
    }

    generateAuxiliaryArtifacts();

    displayGeneratedCode();
    verifyGeneratedCode();
}

void Llvm::initializeFunctions()
{
    const char* mangledName = GetMangledMethodName(m_info->compMethodHnd);
    Function* rootLlvmFunction = getOrCreateKnownLlvmFunction(mangledName, [=]() { return createFunctionType(); });
    if (!rootLlvmFunction->isDeclaration())
    {
        BADCODE("Duplicate definition");
    }

    if (_compiler->opts.jitFlags->IsSet(JitFlags::JIT_FLAG_MIN_OPT))
    {
        rootLlvmFunction->addFnAttr(llvm::Attribute::NoInline);
        rootLlvmFunction->addFnAttr(llvm::Attribute::OptimizeNone);
    }
    if ((_compiler->info.compFlags & CORINFO_FLG_DONT_INLINE) != 0)
    {
        rootLlvmFunction->addFnAttr(llvm::Attribute::NoInline);
    }

    // First function is always the root.
    m_functions = std::vector<FunctionInfo>(_compiler->compFuncCount());
    m_functions[ROOT_FUNC_IDX] = {rootLlvmFunction};

    for (unsigned funcIdx = 1; funcIdx < _compiler->compFuncCount(); funcIdx++)
    {
        FuncInfoDsc* funcInfo = _compiler->funGetFunc(funcIdx);
        unsigned ehIndex = funcInfo->funEHIndex;
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);

        // We won't generate code for unreachable handlers so we will not create functions for them.
        if (!isReachable(getFirstBlockForFunction(funcIdx)))
        {
            continue;
        }

        FunctionType* llvmFuncType;
        Type* ptrLlvmType = getPtrLlvmType();
        Type* int32LlvmType = Type::getInt32Ty(m_context->Context);
        if (funcInfo->funKind == FUNC_FILTER)
        {
            // (shadow stack, original shadow stack, exception) -> result.
            llvmFuncType =
                FunctionType::get(int32LlvmType, {ptrLlvmType, ptrLlvmType, ptrLlvmType}, /* isVarArg */ false);
        }
        else if (ehDsc->HasCatchHandler())
        {
            // (shadow stack, exception) -> catchret destination.
            llvmFuncType = FunctionType::get(int32LlvmType, {ptrLlvmType, ptrLlvmType}, /* isVarArg */ false);
        }
        else
        {
            // (shadow stack) -> void.
            assert(ehDsc->HasFinallyOrFaultHandler());
            llvmFuncType = FunctionType::get(Type::getVoidTy(m_context->Context), {ptrLlvmType}, /* isVarArg */ false);
        }

        Function* llvmFunc;
        if (funcInfo->funKind != FUNC_FILTER)
        {
            const char* kindName;
            switch (ehDsc->ebdHandlerType)
            {
                case EH_HANDLER_CATCH:
                    kindName = "Catch";
                    break;
                case EH_HANDLER_FILTER:
                    kindName = "FilteredCatch";
                    break;
                case EH_HANDLER_FAULT:
                case EH_HANDLER_FAULT_WAS_FINALLY:
                    kindName = "Fault";
                    break;
                case EH_HANDLER_FINALLY:
                    kindName = "Finally";
                    break;
                default:
                    unreached();
            }

            llvmFunc = Function::Create(llvmFuncType, Function::InternalLinkage,
                                        mangledName + Twine("$F") + Twine(funcIdx) + "_" + kindName,
                                        &m_context->Module);
            if (!ehDsc->HasFinallyHandler())
            {
                // Always inline funclets that will have exactly one callsite.
                llvmFunc->addFnAttr(llvm::Attribute::AlwaysInline);
            }
        }
        else
        {
            llvmFunc = Function::Create(llvmFuncType, Function::ExternalLinkage,
                                        GetMangledFilterFuncletName(ehIndex), &m_context->Module);
        }

        m_functions[funcIdx] = {llvmFunc};
    }

    // Generate the unwind blocks used to catch native exceptions during the second pass.
    m_EHUnwindLlvmBlocks = std::vector<llvm::BasicBlock*>(_compiler->compHndBBtabCount);

    for (unsigned ehIndex = 0; ehIndex < _compiler->compHndBBtabCount; ehIndex++)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);

        // No need for an unwind block if we know it will be unreachable.
        if (ehDsc->HasCatchHandler() && (m_unwindFrameLclNum == BAD_VAR_NUM))
        {
            continue;
        }
        // See "generateEHDispatch" for why we cannot skip generating unwind blocks for all unreachable handlers.
        if (!isReachable(ehDsc->ebdHndBeg) && !isReachable(ehDsc->ebdTryBeg))
        {
            continue;
        }

        llvm::BasicBlock* dispatchLlvmBlock = nullptr;
        if ((ehIndex > 0) && ehDsc->ebdIsSameTry(_compiler, ehIndex - 1))
        {
            // We will have one dispatch block for the whole run of mutually protecting handlers.
            dispatchLlvmBlock = m_EHUnwindLlvmBlocks[ehIndex - 1];
            assert(dispatchLlvmBlock != nullptr);
        }
        else
        {
            // The dispatch block is part of the function with the protected region.
            unsigned enclosingFuncIdx = getLlvmFunctionIndexForProtectedRegion(ehIndex);
            Function* dispatchLlvmFunc = getLlvmFunctionForIndex(enclosingFuncIdx);
            dispatchLlvmBlock = llvm::BasicBlock::Create(m_context->Context, BBNAME("BT", ehIndex), dispatchLlvmFunc);
        }

        m_EHUnwindLlvmBlocks[ehIndex] = dispatchLlvmBlock;
    }
}

void Llvm::generateProlog()
{
    JITDUMP("\n=============== Generating prolog:\n");

    LlvmBlockRange prologLlvmBlocks(getOrCreatePrologLlvmBlockForFunction(ROOT_FUNC_IDX));
    setCurrentEmitContext(ROOT_FUNC_IDX, EHblkDsc::NO_ENCLOSING_INDEX, &prologLlvmBlocks);
    _builder.SetCurrentDebugLocation(nullptr); // By convention, prologs have no debug info.

    initializeShadowStack();
    initializeLocals();
    declareDebugVariables();
}

void Llvm::initializeShadowStack()
{
    Value* shadowStackValue;
    if (_compiler->opts.IsReversePInvoke())
    {
        shadowStackValue = emitHelperCall(CORINFO_HELP_LLVM_GET_OR_INIT_SHADOW_STACK_TOP);

        JITDUMP("Setting V%02u's initial value to the recovered shadow stack\n", _shadowStackLclNum);
        JITDUMPEXEC(displayValue(shadowStackValue));
    }
    else
    {
        shadowStackValue = getRootLlvmFunction()->getArg(0);
    }

    unsigned alignment = m_shadowFrameAlignment;
    if (alignment != DEFAULT_SHADOW_STACK_ALIGNMENT)
    {
        JITDUMP("Aligning the shadow frame to %u bytes:\n", alignment);
        assert(isPow2(alignment));

        // IR taken from what Clang generates for "__builtin_align_up".
        Value* shadowStackIntValue = _builder.CreatePtrToInt(shadowStackValue, getIntPtrLlvmType());
        JITDUMPEXEC(displayValue(shadowStackIntValue));
        Value* alignedShadowStackIntValue = _builder.CreateAdd(shadowStackIntValue, getIntPtrConst(alignment - 1));
        JITDUMPEXEC(displayValue(alignedShadowStackIntValue));
        alignedShadowStackIntValue = _builder.CreateAnd(alignedShadowStackIntValue, getIntPtrConst(~(alignment - 1)));
        JITDUMPEXEC(displayValue(alignedShadowStackIntValue));
        Value* alignOffset = _builder.CreateSub(alignedShadowStackIntValue, shadowStackIntValue);
        JITDUMPEXEC(displayValue(alignOffset));
        shadowStackValue = _builder.CreateGEP(Type::getInt8Ty(m_context->Context), shadowStackValue, alignOffset);
        JITDUMPEXEC(displayValue(shadowStackValue));

        llvm::CallInst* alignAssume =
            _builder.CreateAlignmentAssumption(m_context->Module.getDataLayout(), shadowStackValue, alignment);
        JITDUMPEXEC(alignAssume);
    }

    m_rootFunctionShadowStackValue = shadowStackValue;
}

void Llvm::initializeLocals()
{
    llvm::AllocaInst** allocas = new (_compiler->getAllocator(CMK_Codegen)) llvm::AllocaInst*[_compiler->lvaCount];
    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        if (isFuncletParameter(lclNum))
        {
            // We model funclet parameters specially because it is not trivial to represent them in IR faithfully.
            continue;
        }

        // Don't look at unreferenced temporaries.
        if (varDsc->lvRefCnt() == 0)
        {
            continue;
        }

        ValueInitKind initValueKind = getInitKindForLocal(lclNum);
        JITDUMPEXEC(displayInitKindForLocal(lclNum, initValueKind));

        Value* initValue;
        Type* lclLlvmType = getLlvmTypeForLclVar(varDsc);
        switch (initValueKind)
        {
            case ValueInitKind::None:
                initValue = nullptr;
                break;
            case ValueInitKind::Param:
                assert(varDsc->lvLlvmArgNum != BAD_LLVM_ARG_NUM);
                initValue = getRootLlvmFunction()->getArg(varDsc->lvLlvmArgNum);
                break;
            case ValueInitKind::Zero:
                initValue = llvm::Constant::getNullValue(lclLlvmType);
                break;
            case ValueInitKind::Uninit:
                // Using a frozen undef value here should ensure we don't run into UB issues
                // with undefined values (which uninitialized allocas produce, see LangRef).
                initValue = llvm::UndefValue::get(lclLlvmType);
                initValue = _builder.CreateFreeze(initValue);
                JITDUMPEXEC(displayValue(initValue));
                break;
            default:
                unreached();
        }

        // Reset the bit so that subsequent dumping reflects our decision here.
        varDsc->lvMustInit = initValueKind == ValueInitKind::Zero;

        assert((initValue == nullptr) || (initValue->getType() == lclLlvmType));
        if (_compiler->lvaInSsa(lclNum))
        {
            if (initValue != nullptr)
            {
                // Make sure to verify that the first definition is implicit as we expect.
                assert(varDsc->GetPerSsaData(SsaConfig::FIRST_SSA_NUM)->GetDefNode() == nullptr);
                _localsMap.Set({lclNum, SsaConfig::FIRST_SSA_NUM}, initValue);
                assignDebugVariable(lclNum, initValue);
            }
        }
        else
        {
            llvm::AllocaInst* allocaInst = _builder.CreateAlloca(lclLlvmType);
            allocas[lclNum] = allocaInst;
            JITDUMPEXEC(displayValue(allocaInst));

            if (initValue != nullptr)
            {
                Instruction* storeInst = _builder.CreateStore(initValue, allocaInst);
                JITDUMPEXEC(displayValue(storeInst));
            }
        }
    }

    getLlvmFunctionInfoForIndex(ROOT_FUNC_IDX).Allocas = allocas;
}

void Llvm::generateBlocks()
{
    // When optimizing, we'll have built SSA and so have to process the blocks in the dominator pre-order
    // for SSA uses to be available at the point we request them.
    if (_compiler->fgSsaDomTree != nullptr)
    {
        class LlvmCompileDomTreeVisitor : public DomTreeVisitor<LlvmCompileDomTreeVisitor>
        {
            Llvm* m_llvm;

        public:
            LlvmCompileDomTreeVisitor(Compiler* compiler, Llvm* llvm)
                : DomTreeVisitor(compiler, compiler->fgSsaDomTree)
                , m_llvm(llvm)
            {
            }

            void PreOrderVisit(BasicBlock* block) const
            {
                m_llvm->generateBlock(block);
            }
        };

        LlvmCompileDomTreeVisitor visitor(_compiler, this);
        visitor.WalkTree();

        // Walk all the exceptional code blocks and generate them since they don't appear in the normal flow graph.
        for (Compiler::AddCodeDsc* add = _compiler->fgGetAdditionalCodeDescriptors(); add != nullptr; add = add->acdNext)
        {
            // if the LLVM function was not created due to the first block not being reachable
            // then don't generate the exceptional code block
            if ((add->acdDstBlk->bbFlags & BBF_MARKED) != 0)
            {
                generateBlock(add->acdDstBlk);
            }
        }
    }
    else
    {
        // When not optimizing, simply generate all of the blocks in layout order.
        for (BasicBlock* block : _compiler->Blocks())
        {
            generateBlock(block);
        }
    }
}

void Llvm::generateBlock(BasicBlock* block)
{
    JITDUMP("\n=============== Generating ");
    JITDUMPEXEC(block->dspBlockHeader(_compiler, /* showKind */ true, /* showFlags */ true));

    setCurrentEmitContextForBlock(block);

    for (GenTree* node : LIR::AsRange(block))
    {
        visitNode(node);
    }

    switch (block->bbJumpKind)
    {
        case BBJ_NONE:
            _builder.CreateBr(getFirstLlvmBlockForBlock(block->bbNext));
            break;
        case BBJ_ALWAYS:
            _builder.CreateBr(getFirstLlvmBlockForBlock(block->bbJumpDest));
            break;
        case BBJ_THROW:
            _builder.CreateUnreachable();
            break;
        case BBJ_CALLFINALLY:
            buildCallFinally(block);
            break;
        case BBJ_EHFINALLYRET:
        case BBJ_EHFAULTRET:
            // "fgCreateMonitorTree" forgets to insert RETFILT nodes for some faults. Compensate.
            if (!block->lastNode()->OperIs(GT_RETFILT))
            {
                assert(block->bbCatchTyp == BBCT_FAULT);
                _builder.CreateRetVoid();
            }
            break;
        default:
            // TODO-LLVM: other jump kinds.
            break;
    }
}

void Llvm::generateEHDispatch()
{
    if (!_compiler->ehHasCallableHandlers())
    {
        // Nothing to do if no EH.
        return;
    }

    struct DispatchData
    {
        llvm::BasicBlock* ResumeLlvmBlock;
        llvm::BasicBlock* UnreachableLlvmBlock;
        llvm::BasicBlock* InsertBeforeLlvmBlock;
        llvm::AllocaInst* CppExcTupleAlloca;
    };

    // Set up various variables used in the loop below.
    Type* ptrLlvmType = getPtrLlvmType();
    llvm::StructType* cppExcTupleLlvmType = llvm::StructType::get(ptrLlvmType, Type::getInt32Ty(m_context->Context));

    CorInfoLlvmEHModel model = m_ehModel;
    llvm::Constant* nullValue = llvm::Constant::getNullValue(ptrLlvmType);
    Function* personalityLlvmFunc = getOrCreatePersonalityLlvmFunction(model);
    Function* wasmRethrowLlvmFunc = nullptr;
    if (model == CorInfoLlvmEHModel::Wasm)
    {
        wasmRethrowLlvmFunc = llvm::Intrinsic::getDeclaration(&m_context->Module, llvm::Intrinsic::wasm_rethrow);
    }
    Function* cppBeginCatchFunc = nullptr;
    if (model == CorInfoLlvmEHModel::Cpp)
    {
        cppBeginCatchFunc = getOrCreateKnownLlvmFunction("__cxa_begin_catch", [ptrLlvmType]() {
            return FunctionType::get(ptrLlvmType, {ptrLlvmType}, /* isVarArg */ false);
        });
    }

    // There is no meaningful source location we can attach to the dispatch blocks. None of them are "user" code.
    llvm::DebugLoc dispatchDebugLoc = getArtificialDebugLocation();
    std::vector<DispatchData> functionData(_compiler->compFuncCount());

    // Note the iteration order: outer -> inner.
    for (unsigned ehIndex = _compiler->compHndBBtabCount - 1; ehIndex != -1; ehIndex--)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
        llvm::BasicBlock* dispatchPadLlvmBlock = m_EHUnwindLlvmBlocks[ehIndex];

        if (dispatchPadLlvmBlock == nullptr)
        {
            // Would have been unreachable.
            continue;
        }

        if (!dispatchPadLlvmBlock->empty())
        {
            // We've already generated code for this block shared between mutually protecting handlers.
            continue;
        }

        unsigned funcIdx = getLlvmFunctionIndexForProtectedRegion(ehIndex);
        Function* llvmFunc = getLlvmFunctionForIndex(funcIdx);
        if ((personalityLlvmFunc != nullptr) && !llvmFunc->hasPersonalityFn())
        {
            llvmFunc->setPersonalityFn(personalityLlvmFunc);
        }

        // The code we will generate uses native unwinding to call second-pass handlers.
        //
        // For CorInfoLlvmEHModel::Cpp:
        //
        // DISPATCH_PAD_INNER:
        //   __cxa_begin_catch(landingPadInst.ExceptionData);
        //   cppExcTuple = landingPadInst;
        //   goto DISPATCH_INNER;
        //
        // DISPATCH_INNER:
        //   exceptionObj = RhpHandleExceptionWasmCatch(<unwind index of the protected region>)
        //   if (catchRetDest == null) {
        //       goto DISPATCH_OUTER / goto RESUME; // Depending on whether the region is top-level.
        //   }
        //   catchRetDest = CatchFunclet(exceptionObj) unwind to DISPATCH_PAD_OUTER
        //   switch (catchRetDest) {
        //       case 0: goto BB01;
        //       case 1: goto BB02;
        //       ...
        //       default: unreachable();
        //   }
        //
        // RESUME:
        //   resume(cppExcTuple); // Rethrow the exception and unwind to caller.
        //
        // CorInfoLlvmEHModel::Wasm has the same structure but uses Windows EH instructions and rethrows:
        //
        // DISPATCH_INNER:
        //   catchswitch unwind to DISPATCH_OUTER
        //   catchpad within DISPATCH_INNER
        //
        //   exceptionObj = RhpHandleExceptionWasmCatch(<unwind index of the protected region>)
        //   if (exceptionObj == null) {
        //       @llvm.wasm.rethrow() unwind to DISPATCH_OUTER;
        //   }
        //   catchRetDest = CatchFunclet(exceptionObj) unwind to DISPATCH_OUTER
        //   switch (catchRetDest) {
        //       case 0: catchret to BB01;
        //       case 1: catchret to BB02;
        //       ...
        //       default: unreachable();
        //   }
        //
        // Create the C++ exception data alloca, to store the active landing pad value.
        DispatchData& funcDispatchData = functionData[funcIdx];
        llvm::AllocaInst* cppExcTupleAlloca = funcDispatchData.CppExcTupleAlloca;
        if ((model == CorInfoLlvmEHModel::Cpp) && (cppExcTupleAlloca == nullptr))
        {
            llvm::BasicBlock* prologLlvmBlock = getOrCreatePrologLlvmBlockForFunction(funcIdx);

            _builder.SetInsertPoint(prologLlvmBlock->getTerminator());
            cppExcTupleAlloca = _builder.CreateAlloca(cppExcTupleLlvmType);

            funcDispatchData.CppExcTupleAlloca = cppExcTupleAlloca;
        }

        // Generate the per-funclet dispatch blocks. Resume block is needed in the C++ and emulated models,
        // unreachable block is always needed.
        //
        llvm::BasicBlock* unreachableLlvmBlock = funcDispatchData.UnreachableLlvmBlock;
        if (unreachableLlvmBlock == nullptr)
        {
            unreachableLlvmBlock = llvm::BasicBlock::Create(m_context->Context, "BBUN", llvmFunc);

            _builder.SetInsertPoint(unreachableLlvmBlock); // No need for a full emit context.
            _builder.CreateUnreachable();

            funcDispatchData.UnreachableLlvmBlock = unreachableLlvmBlock;
            funcDispatchData.InsertBeforeLlvmBlock = unreachableLlvmBlock;
        }

        llvm::BasicBlock* resumeLlvmBlock = funcDispatchData.ResumeLlvmBlock;
        if ((resumeLlvmBlock == nullptr) &&
            ((model == CorInfoLlvmEHModel::Cpp) || (model == CorInfoLlvmEHModel::Emulated)))
        {
            resumeLlvmBlock = llvm::BasicBlock::Create(m_context->Context, "BBRE", llvmFunc, unreachableLlvmBlock);
            LlvmBlockRange resumeLlvmBlocks(resumeLlvmBlock);
            setCurrentEmitContext(funcIdx, EHblkDsc::NO_ENCLOSING_INDEX, &resumeLlvmBlocks);

            if (model == CorInfoLlvmEHModel::Cpp)
            {
                Value* resumeOperandValue = _builder.CreateLoad(cppExcTupleLlvmType, cppExcTupleAlloca);
                _builder.CreateResume(resumeOperandValue);
            }
            else
            {
                _builder.CreateStore(_builder.getInt32(1), getOrCreateExceptionThrownAddressValue());

                llvm::BasicBlock* exceptionReturnLlvmBlock = getOrCreateExceptionThrownReturnBlock();
                exceptionReturnLlvmBlock->moveAfter(resumeLlvmBlock);
                _builder.CreateBr(exceptionReturnLlvmBlock);
            }

            funcDispatchData.ResumeLlvmBlock = resumeLlvmBlock;
            funcDispatchData.InsertBeforeLlvmBlock = resumeLlvmBlock;
        }

        // Dispatch blocks, when initially created, are placed at the start of the function.
        // Here we move them to a more appropriate place so that the entry block is correct.
        dispatchPadLlvmBlock->moveBefore(funcDispatchData.InsertBeforeLlvmBlock);
        _builder.SetCurrentDebugLocation(dispatchDebugLoc);

        LlvmBlockRange dispatchLlvmBlocks(dispatchPadLlvmBlock);
        setCurrentEmitContext(funcIdx, ehDsc->ebdEnclosingTryIndex, &dispatchLlvmBlocks);
        llvm::BasicBlock* outerDispatchLlvmBlock = getUnwindLlvmBlockForCurrentInvoke();

        // Set up entry to the native "catch".
        llvm::CatchPadInst* catchPadInst = nullptr;
        std::vector<llvm::OperandBundleDef> catchPadOpBundle{}; // Empty if we're not using catchpads.
        if ((model == CorInfoLlvmEHModel::Cpp) || (model == CorInfoLlvmEHModel::Emulated))
        {
            if (model == CorInfoLlvmEHModel::Cpp)
            {
                llvm::LandingPadInst* landingPadInst = _builder.CreateLandingPad(cppExcTupleLlvmType, 1);
                landingPadInst->addClause(nullValue); // Catch all C++ exceptions.

                Value* exceptionDataValue = _builder.CreateExtractValue(landingPadInst, 0);
                _builder.CreateCall(cppBeginCatchFunc, exceptionDataValue);
                _builder.CreateStore(landingPadInst, cppExcTupleAlloca);
            }
            else
            {
                _builder.CreateStore(_builder.getInt32(0), getOrCreateExceptionThrownAddressValue());
            }

            // The "actual" dispatch block. Nested dispatches (if any) will branch to it.
            llvm::BasicBlock* dispatchLlvmBlock = createInlineLlvmBlock();
            _builder.CreateBr(dispatchLlvmBlock);
            _builder.SetInsertPoint(dispatchLlvmBlock);
        }
        else
        {
            // We outline all our funclets manually so this dispatch switch is always "within none".
            llvm::CatchSwitchInst* catchSwitchInst =
                _builder.CreateCatchSwitch(llvm::ConstantTokenNone::get(m_context->Context), outerDispatchLlvmBlock, 1);

            llvm::BasicBlock* catchPadLlvmBlock = createInlineLlvmBlock();
            catchSwitchInst->addHandler(catchPadLlvmBlock);

            _builder.SetInsertPoint(catchPadLlvmBlock);
            catchPadInst = _builder.CreateCatchPad(catchSwitchInst, nullValue); // Catch all C++ exceptions.
            catchPadOpBundle.push_back(llvm::OperandBundleDef("funclet", catchPadInst));

            // Emit this intrinsic so that we get "typed" WASM "catch" instructions, which will not catch any foreign
            // exceptions, like "catch_all" would. While foreign exceptions propagating through managed code are UB in
            // the general case, "exit" C call and thus "Environment.Exit" use them and so are exempted.
            _builder.CreateIntrinsic(llvm::Intrinsic::wasm_get_exception, {}, catchPadInst);
        }

        if (((model == CorInfoLlvmEHModel::Cpp) || (model == CorInfoLlvmEHModel::Emulated)) &&
            (outerDispatchLlvmBlock != nullptr))
        {
            // We have the "dispatch pad" block. Since we're generating dispatches from outer to inner, we already have
            // the "actual" dispatch block; it will be the next one.
            outerDispatchLlvmBlock = outerDispatchLlvmBlock->getNextNode();
            assert(outerDispatchLlvmBlock != nullptr);
        }

        // For inner dispatch, jump to the outer one if the handler returned "continue search". Faults / finallys cannot
        // satisfy the first-pass search and so for them this jump is unconditional. In the Wasm model, the jump is done
        // via rethrow and so the top-level dispatch does not have to be handled specially.
        auto emitJmpToOuterDispatch = [=, &catchPadOpBundle]() {
            if (model == CorInfoLlvmEHModel::Wasm)
            {
                emitCallOrInvoke(wasmRethrowLlvmFunc, {}, catchPadOpBundle);
                _builder.CreateUnreachable();
            }
            else if (outerDispatchLlvmBlock != nullptr)
            {
                _builder.CreateBr(outerDispatchLlvmBlock);
            }
            else
            {
                _builder.CreateBr(resumeLlvmBlock);
            }
        };

        if (ehDsc->HasCatchHandler())
        {
            // Find the full set of mutually protecting handlers we have. Since we are generating things outer-to-inner,
            // we are guaranteed to capture them all here.
            unsigned innerEHIndex = ehIndex;
            while ((innerEHIndex > 0) && ehDsc->ebdIsSameTry(_compiler, innerEHIndex - 1))
            {
                innerEHIndex--;
            }

            for (unsigned hndEHIndex = innerEHIndex; hndEHIndex <= ehIndex; hndEHIndex++)
            {
                EHblkDsc* hndDsc = _compiler->ehGetDsc(hndEHIndex);

                // Call the runtime to determine whether this catch should handle the exception. Note how we must do so
                // even if we know the catch handler is statically unreachable. This is both because the runtime assumes
                // we will (in other words, it assumes that for a given first pass, the second pass will visit the exact
                // same set of "unwind sites" as was specified in the EH info), and because we may need to unlink some
                // virtual unwind frames.
                unsigned hndUnwindIndex = m_unwindIndexMap->Bottom(hndEHIndex);
                Value* caughtValue =
                    emitHelperCall(CORINFO_HELP_LLVM_EH_CATCH, getIntPtrConst(hndUnwindIndex), catchPadOpBundle);

                // Yes if we get not-"null" back, otherwise continue unwinding.
                Value* callCatchValue = _builder.CreateIsNotNull(caughtValue);
                llvm::BasicBlock* callCatchLlvmBlock = createInlineLlvmBlock();
                llvm::BasicBlock* continueUnwindLlvmBlock;
                if (hndEHIndex == ehIndex)
                {
                    llvm::BasicBlock* currentLlvmBlock = _builder.GetInsertBlock();

                    continueUnwindLlvmBlock = createInlineLlvmBlock();
                    _builder.SetInsertPoint(continueUnwindLlvmBlock);
                    emitJmpToOuterDispatch();

                    _builder.SetInsertPoint(currentLlvmBlock);
                }
                else
                {
                    continueUnwindLlvmBlock = createInlineLlvmBlock();
                }
                _builder.CreateCondBr(callCatchValue, callCatchLlvmBlock, continueUnwindLlvmBlock);

                _builder.SetInsertPoint(callCatchLlvmBlock);
                if (isReachable(hndDsc->ebdHndBeg))
                {
                    // Call the catch funclet and get its dynamic catchret destination.
                    Function* catchLlvmFunc = getLlvmFunctionForIndex(hndDsc->ebdFuncIndex);
                    Value* catchRetValue =
                        emitCallOrInvoke(catchLlvmFunc, {getShadowStack(), caughtValue}, catchPadOpBundle);

                    // Create the dispatch switch for all possible catchret destinations. Note how we are doing linear
                    // work here because the funclet creation process will hoist nested handlers, flattening the basic
                    // block list.
                    std::vector<llvm::BasicBlock*> catchRetSwitchTargets{};
                    for (BasicBlock* hndBlock : _compiler->Blocks(hndDsc->ebdHndBeg, hndDsc->ebdHndLast))
                    {
                        assert(hndBlock->getHndIndex() == hndEHIndex);
                        if (hndBlock->bbJumpKind == BBJ_EHCATCHRET)
                        {
                            BasicBlock* destBlock = hndBlock->bbJumpDest;
                            llvm::BasicBlock* destLlvmBlock = getFirstLlvmBlockForBlock(destBlock);
                            assert(destLlvmBlock->getParent() == llvmFunc); // No jumping out of a funclet.

                            unsigned destIndex = static_cast<unsigned>(catchRetSwitchTargets.size());
                            llvm::ConstantInt* destIndexValue = _builder.getInt32(destIndex);
                            catchRetSwitchTargets.push_back(destLlvmBlock);

                            llvm::BasicBlock* catchRetLlvmBlock = getLastLlvmBlockForBlock(hndBlock);
                            llvm::ReturnInst::Create(m_context->Context, destIndexValue, catchRetLlvmBlock);
                        }
                    }

                    unsigned catchRetDestCount = static_cast<unsigned>(catchRetSwitchTargets.size());
                    if (catchRetDestCount != 0)
                    {
                        llvm::SwitchInst* catchRetSwitchInst =
                            _builder.CreateSwitch(catchRetValue, unreachableLlvmBlock, catchRetDestCount);

                        for (unsigned destIndex = 0; destIndex < catchRetDestCount; destIndex++)
                        {
                            llvm::ConstantInt* destIndexValue = _builder.getInt32(destIndex);
                            llvm::BasicBlock* destLlvmBlock = catchRetSwitchTargets[destIndex];

                            if (model == CorInfoLlvmEHModel::Wasm)
                            {
                                llvm::BasicBlock* catchRetToDestLlvmBlock = createInlineLlvmBlock();
                                _builder.SetInsertPoint(catchRetToDestLlvmBlock);
                                _builder.CreateCatchRet(catchPadInst, destLlvmBlock);

                                catchRetSwitchInst->addCase(destIndexValue, catchRetToDestLlvmBlock);
                            }
                            else
                            {
                                catchRetSwitchInst->addCase(destIndexValue, destLlvmBlock);
                            }
                        }
                    }
                    else
                    {
                        // This handler always (re)throws.
                        _builder.CreateUnreachable();
                    }
                }
                else
                {
                    // An unreachable handler; the runtime will always continue unwinding.
                    _builder.CreateUnreachable();
                }

                _builder.SetInsertPoint(continueUnwindLlvmBlock);
            }
        }
        else
        {
            // Unlike catches, fault-like handlers can only be made unreachable together with their protected regions.
            assert(ehDsc->HasFinallyOrFaultHandler() && isReachable(ehDsc->ebdHndBeg));

            Function* hndLlvmFunc = getLlvmFunctionForIndex(ehDsc->ebdFuncIndex);
            emitCallOrInvoke(hndLlvmFunc, {getShadowStack()}, catchPadOpBundle);
            if ((ehDsc->ebdEnclosingTryIndex == EHblkDsc::NO_ENCLOSING_INDEX) && (m_unwindFrameLclNum != BAD_VAR_NUM))
            {
                emitHelperCall(CORINFO_HELP_LLVM_EH_POP_UNWOUND_VIRTUAL_FRAMES, {}, catchPadOpBundle);
            }
            emitJmpToOuterDispatch();
        }

        funcDispatchData.InsertBeforeLlvmBlock = dispatchLlvmBlocks.FirstBlock;
    }
}

void Llvm::fillPhis()
{
    // LLVM requires PHI inputs to match the list of predecessors exactly, which is different from IR in two ways:
    //
    // 1. IR doesn't insert inputs for the same definition coming from multiple blocks (it picks the first block
    //    renamer encounters as the "gtPredBB" one). We deal with this by disabling this behavior in SSA builder
    //    directly.
    // 2. IR doesn't insert inputs for different outgoing edges from the same block. For conditional branches,
    //    we simply don't generate the degenerate case. For switches, we compensate for this here, by inserting
    //    "duplicate" entries into PHIs in case the count of incoming LLVM edges did not match the count of IR
    //    entries. This is simpler to do here than in SSA builder because SSA builder uses successor iterators
    //    which explicitly filter out duplicates; creating those that do not would be an intrusive change. This
    //    can (should) be reconsidered this once/if we are integrated directly into upstream.
    //
    struct PredEdge
    {
        BasicBlock* PredBlock;
        BasicBlock* SuccBlock;

        static bool Equals(const PredEdge& left, const PredEdge& right)
        {
            return (left.PredBlock == right.PredBlock) && (left.SuccBlock == right.SuccBlock);
        }

        static unsigned GetHashCode(const PredEdge& edge)
        {
            return edge.PredBlock->bbNum ^ edge.SuccBlock->bbNum;
        }
    };

    SmallHashTable<PredEdge, unsigned, 8, PredEdge> predCountMap(_compiler->getAllocator(CMK_Codegen));
    auto getPhiPredCount = [&](BasicBlock* predBlock, BasicBlock* phiBlock) -> unsigned {
        if (predBlock->bbJumpKind != BBJ_SWITCH)
        {
            return 1;
        }

        unsigned predCount = 0;
        if (!predCountMap.TryGetValue({predBlock, phiBlock}, &predCount))
        {
            // Eagerly memoize all of the switch edge counts to avoid quadratic behavior.
            for (FlowEdge* edge : phiBlock->PredEdges())
            {
                BasicBlock* edgePredBlock = edge->getSourceBlock();
                if (edgePredBlock->bbJumpKind == BBJ_SWITCH)
                {
                    predCountMap.AddOrUpdate({edgePredBlock, phiBlock}, edge->getDupCount());

                    if (edgePredBlock == predBlock)
                    {
                        predCount = edge->getDupCount();
                    }
                }
            }
        }

        assert(predCount != 0);
        return predCount;
    };

    for (PhiPair phiPair : _phiPairs)
    {
        llvm::PHINode* llvmPhiNode = phiPair.LlvmPhiNode;
        GenTreeLclVar* phiStore = phiPair.StoreNode;

        unsigned lclNum = phiStore->GetLclNum();
        BasicBlock* phiBlock = _compiler->lvaGetDesc(lclNum)->GetPerSsaData(phiStore->GetSsaNum())->GetBlock();

        for (GenTreePhi::Use& use : phiStore->Data()->AsPhi()->Uses())
        {
            GenTreePhiArg* phiArg = use.GetNode()->AsPhiArg();
            Value* phiArgValue = _localsMap[{lclNum, phiArg->GetSsaNum()}];
            BasicBlock* predBlock = phiArg->gtPredBB;
            llvm::BasicBlock* llvmPredBlock = getLastLlvmBlockForBlock(predBlock);

            unsigned llvmPredCount = getPhiPredCount(predBlock, phiBlock);
            for (unsigned i = 0; i < llvmPredCount; i++)
            {
                llvmPhiNode->addIncoming(phiArgValue, llvmPredBlock);
            }
        }
    }
}

void Llvm::generateAuxiliaryArtifacts()
{
    // Currently, the only auxiliary artifact we may need is an alternative exported name for the compiled function.
    const char* alternativeName = GetAlternativeFunctionName();
    if (alternativeName != nullptr)
    {
        llvm::GlobalAlias::create(alternativeName, getRootLlvmFunction());
    }
}

void Llvm::verifyGeneratedCode()
{
#ifdef DEBUG
    for (FunctionInfo& funcInfo : m_functions)
    {
        Function* llvmFunc = funcInfo.LlvmFunction;
        if (llvmFunc != nullptr)
        {
            assert(!llvm::verifyFunction(*llvmFunc, &llvm::errs()));
        }
    }
#endif // DEBUG
}

void Llvm::displayGeneratedCode()
{
    if (VERBOSE || _compiler->opts.disAsm)
    {
        JITDUMP("\n===================================================================================================================\n");
        JITDUMP("LLVM IR for %s after codegen:\n", _compiler->info.compFullName);
        JITDUMP("-------------------------------------------------------------------------------------------------------------------\n\n");

        for (FunctionInfo& funcInfo : m_functions)
        {
            Function* llvmFunc = funcInfo.LlvmFunction;
            if (llvmFunc != nullptr)
            {
                displayValue(llvmFunc);
            }
        }
    }
}

Value* Llvm::getGenTreeValue(GenTree* op)
{
    return _sdsuMap[op];
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
    assert(!node->isContained());
    Value* nodeValue = getGenTreeValue(node);
    Value* finalValue = nodeValue;

    if (nodeValue->getType() != targetLlvmType)
    {
        Type* intPtrLlvmType = getIntPtrLlvmType();

        // Integer -> pointer.
        if ((nodeValue->getType() == intPtrLlvmType) && targetLlvmType->isPointerTy())
        {
            return _builder.CreateIntToPtr(nodeValue, targetLlvmType);
        }

        // Pointer -> integer.
        if (nodeValue->getType()->isPointerTy() && (targetLlvmType == intPtrLlvmType))
        {
            return _builder.CreatePtrToInt(nodeValue, intPtrLlvmType);
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
                    trueNodeType = JITtype2varType(node->AsCall()->gtCorInfoType);
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
                    assert(nodeValue->getType() == Type::getInt1Ty(m_context->Context));
                    trueNodeType = TYP_UBYTE;
                    break;

                case GT_CAST:
                    trueNodeType = node->AsCast()->CastToType();
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

void Llvm::mapGenTreeToValue(GenTree* node, Value* nodeValue)
{
    if (node->IsValue())
    {
        _sdsuMap.Set(node, nodeValue);
    }
}

void Llvm::visitNode(GenTree* node)
{
#ifdef DEBUG
    JITDUMPEXEC(_compiler->gtDispLIRNode(node, "Generating: "));
    auto lastInstrIter = --_builder.GetInsertPoint();
    llvm::BasicBlock* lastLlvmBlock = _builder.GetInsertBlock(); // For instructions spanning multiple blocks.
#endif // DEBUG

    if (node->isContained())
    {
        // Contained nodes generate code as part of the parent.
        return;
    }

    switch (node->OperGet())
    {
        case GT_ADD:
            buildAdd(node->AsOp());
            break;
        case GT_SUB:
            buildSub(node->AsOp());
            break;
        case GT_LEA:
            buildAddrMode(node->AsAddrMode());
            break;
        case GT_DIV:
        case GT_MOD:
        case GT_UDIV:
        case GT_UMOD:
            buildDivMod(node);
            break;
        case GT_ROL:
        case GT_ROR:
            buildRotate(node->AsOp());
            break;
        case GT_CALL:
            buildCall(node->AsCall());
            break;
        case GT_CAST:
            buildCast(node->AsCast());
            break;
        case GT_LCLHEAP:
            buildLclHeap(node->AsUnOp());
            break;
        case GT_CNS_DBL:
            buildCnsDouble(node->AsDblCon());
            break;
        case GT_CNS_INT:
        case GT_CNS_LNG:
            buildIntegralConst(node->AsIntConCommon());
            break;
        case GT_IND:
            buildInd(node->AsIndir());
            break;
        case GT_JTRUE:
            buildJTrue(node);
            break;
        case GT_SWITCH:
            buildSwitch(node->AsUnOp());
            break;
        case GT_LCL_FLD:
            buildLocalField(node->AsLclFld());
            break;
        case GT_STORE_LCL_FLD:
            buildStoreLocalField(node->AsLclFld());
            break;
        case GT_LCL_VAR:
            buildLocalVar(node->AsLclVar());
            break;
        case GT_STORE_LCL_VAR:
            buildStoreLocalVar(node->AsLclVar());
            break;
        case GT_LCL_ADDR:
            buildLocalVarAddr(node->AsLclVarCommon());
            break;
        case GT_LSH:
        case GT_RSH:
        case GT_RSZ:
            buildShift(node->AsOp());
            break;
        case GT_INTRINSIC:
            buildIntrinsic(node->AsIntrinsic());
            break;
        case GT_XAND:
        case GT_XORR:
        case GT_XADD:
        case GT_XCHG:
        case GT_CMPXCHG:
            // TODO-LLVM-CQ: enable these as intrinsics.
            unreached();
        case GT_MEMORYBARRIER:
            buildMemoryBarrier(node);
            break;
        case GT_EQ:
        case GT_NE:
        case GT_LE:
        case GT_LT:
        case GT_GE:
        case GT_GT:
            buildCmp(node->AsOp());
            break;
        case GT_NEG:
        case GT_NOT:
        case GT_BITCAST:
            buildUnaryOperation(node);
            break;
        case GT_NULLCHECK:
            buildNullCheck(node->AsIndir());
            break;
        case GT_BOUNDS_CHECK:
            buildBoundsCheck(node->AsBoundsChk());
            break;
        case GT_CKFINITE:
            buildCkFinite(node->AsUnOp());
            break;
        case GT_BLK:
            buildBlk(node->AsBlk());
            break;
        case GT_PHI:
            buildEmptyPhi(node->AsPhi());
            break;
        case GT_PHI_ARG:
            break;
        case GT_CATCH_ARG:
            buildCatchArg(node);
            break;
        case GT_RETURN:
        case GT_RETFILT:
            buildReturn(node);
            break;
        case GT_STOREIND:
            buildStoreInd(node->AsStoreInd());
            break;
        case GT_STORE_BLK:
            buildStoreBlk(node->AsBlk());
            break;
        case GT_STORE_DYN_BLK:
            buildStoreDynBlk(node->AsStoreDynBlk());
            break;
        case GT_MUL:
        case GT_AND:
        case GT_OR:
        case GT_XOR:
            buildBinaryOperation(node);
            break;
        case GT_KEEPALIVE:
            buildKeepAlive(node->AsUnOp());
            break;
        case GT_IL_OFFSET:
            buildILOffset(node->AsILOffset());
            break;
        case GT_NO_OP:
        case GT_NOP:
            // NOP is a true no-op, while NO_OP is usually used to help generate correct debug info.
            // The latter use case is not representable in LLVM, so we don't need to do anything.
            break;
        case GT_JMP:
            NYI("LLVM/GT_JMP"); // Requires support for explicit tailcalls.
        default:
            unreached();
    }

#ifdef DEBUG
    // Dump all instructions that contributed to the code generated by this node.
    //
    if (_compiler->verbose)
    {
        for (llvm::BasicBlock* llvmBlock = lastLlvmBlock; llvmBlock != _builder.GetInsertBlock()->getNextNode();
             llvmBlock = llvmBlock->getNextNode())
        {
            for (auto instrIter = (llvmBlock == lastLlvmBlock) ? ++lastInstrIter : llvmBlock->begin();
                 instrIter != llvmBlock->end(); ++instrIter)
            {
                displayValue(&*instrIter);
            }
        }
    }
#endif // DEBUG
}

void Llvm::buildLocalVar(GenTreeLclVar* lclVar)
{
    Value*       llvmRef;
    unsigned int lclNum = lclVar->GetLclNum();
    unsigned int ssaNum = lclVar->GetSsaNum();
    LclVarDsc*   varDsc = _compiler->lvaGetDesc(lclVar);

    // We model funclet parameters specially - it is simpler then representing them faithfully in IR.
    if (lclNum == _shadowStackLclNum)
    {
        assert((ssaNum == SsaConfig::FIRST_SSA_NUM) || (ssaNum == SsaConfig::RESERVED_SSA_NUM));
        llvmRef = getShadowStack();
    }
    else if (lclNum == _originalShadowStackLclNum)
    {
        assert((ssaNum == SsaConfig::FIRST_SSA_NUM) || (ssaNum == SsaConfig::RESERVED_SSA_NUM));
        llvmRef = getOriginalShadowStack();
    }
    else if (lclVar->HasSsaName())
    {
        llvmRef = _localsMap[{lclNum, ssaNum}];
    }
    else
    {
        llvmRef = _builder.CreateLoad(getLlvmTypeForLclVar(varDsc), getLocalAddr(lclNum));
    }

    // Implicit truncating from long to int.
    if ((varDsc->TypeGet() == TYP_LONG) && lclVar->TypeIs(TYP_INT))
    {
        llvmRef = _builder.CreateTrunc(llvmRef, Type::getInt32Ty(m_context->Context));
    }

    mapGenTreeToValue(lclVar, llvmRef);
}

void Llvm::buildStoreLocalVar(GenTreeLclVar* lclVar)
{
    unsigned lclNum = lclVar->GetLclNum();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar);
    Type* destLlvmType = getLlvmTypeForLclVar(varDsc);
    Value* localValue = nullptr;

    // zero initialization check
    if (lclVar->TypeIs(TYP_STRUCT) && lclVar->gtGetOp1()->IsIntegralConst(0))
    {
        localValue = llvm::Constant::getNullValue(destLlvmType);
    }
    else
    {
        localValue = consumeValue(lclVar->Data(), destLlvmType);
    }

    if (lclVar->HasSsaName())
    {
        if (lclVar->Data()->OperIs(GT_PHI))
        {
            _phiPairs.push_back({lclVar, llvm::cast<llvm::PHINode>(localValue)});
        }

        _localsMap.Set({lclNum, lclVar->GetSsaNum()}, localValue);
        assignDebugVariable(lclNum, localValue);
    }
    else
    {
        _builder.CreateStore(localValue, getLocalAddr(lclNum));
    }
}

// in case we haven't seen the phi args yet, create just the phi nodes and fill in the args at the end
void Llvm::buildEmptyPhi(GenTreePhi* phi)
{
    LclVarDsc* varDsc = _compiler->lvaGetDesc(phi->Uses().begin()->GetNode()->AsPhiArg());
    Type* lclLlvmType = getLlvmTypeForLclVar(varDsc);
    llvm::PHINode* llvmPhiNode = _builder.CreatePHI(lclLlvmType, 2);

    mapGenTreeToValue(phi, llvmPhiNode);
}

void Llvm::buildLocalField(GenTreeLclFld* lclFld)
{
    unsigned lclNum = lclFld->GetLclNum();

    ClassLayout* layout = lclFld->TypeIs(TYP_STRUCT) ? lclFld->GetLayout() : nullptr;
    Type* llvmLoadType = (layout != nullptr) ? getLlvmTypeForStruct(lclFld->GetLayout())
                                             : getLlvmTypeForVarType(lclFld->TypeGet());

    // TODO-LLVM: if this is an only value type field, or at offset 0, we can optimize.
    Value* structAddrValue = getLocalAddr(lclNum);
    Value* fieldAddressValue = gepOrAddrInBounds(structAddrValue, lclFld->GetLclOffs());

    mapGenTreeToValue(lclFld, _builder.CreateLoad(llvmLoadType, fieldAddressValue));
}

void Llvm::buildStoreLocalField(GenTreeLclFld* lclFld)
{
    GenTree* data = lclFld->Data();
    ClassLayout* layout = lclFld->TypeIs(TYP_STRUCT) ? lclFld->GetLayout() : nullptr;
    Value* addrValue = gepOrAddrInBounds(getLocalAddr(lclFld->GetLclNum()), lclFld->GetLclOffs());

    if (lclFld->TypeIs(TYP_STRUCT) && genActualTypeIsInt(data))
    {
        Value* fillValue = consumeInitVal(data);
        Value* sizeValue = _builder.getInt32(layout->GetSize());
        _builder.CreateMemSet(addrValue, fillValue, sizeValue, llvm::MaybeAlign());
    }
    else
    {
        Type* llvmStoreType =
            (layout != nullptr) ? getLlvmTypeForStruct(layout) : getLlvmTypeForVarType(lclFld->TypeGet());
        Value* dataValue = consumeValue(data, llvmStoreType);
        _builder.CreateStore(dataValue, addrValue);
    }
}

void Llvm::buildLocalVarAddr(GenTreeLclVarCommon* lclAddr)
{
    unsigned int lclNum = lclAddr->GetLclNum();
    Value* localAddr = getLocalAddr(lclNum);
    mapGenTreeToValue(lclAddr, gepOrAddrInBounds(localAddr, lclAddr->GetLclOffs()));
}

void Llvm::buildAdd(GenTreeOp* node)
{
    GenTree* op1 = node->gtGetOp1();
    GenTree* op2 = node->gtGetOp2();
    Type* op1RawType = getGenTreeValue(op1)->getType();
    Type* op2RawType = getGenTreeValue(op2)->getType();

    Value* addValue;
    if (!node->gtOverflow() && (op1RawType->isPointerTy() || op2RawType->isPointerTy()))
    {
        Value* baseValue = consumeValue(op1RawType->isPointerTy() ? op1 : op2, getPtrLlvmType());
        Value* offsetValue = consumeValue(op1RawType->isPointerTy() ? op2 : op1, getIntPtrLlvmType());

        // GEPs scale indices, use type i8 makes them equivalent to the raw offsets we have in IR
        addValue = _builder.CreateGEP(Type::getInt8Ty(m_context->Context), baseValue, offsetValue);
    }
    else
    {
        Type* addLlvmType = getLlvmTypeForVarType(node->TypeGet());
        if (addLlvmType->isPointerTy())
        {
            // ADD<byref>(native int, native int) is valid IR.
            addLlvmType = getIntPtrLlvmType();
        }
        Value* op1Value = consumeValue(op1, addLlvmType);
        Value* op2Value = consumeValue(op2, addLlvmType);

        if (varTypeIsFloating(node))
        {
            addValue = _builder.CreateFAdd(op1Value, op2Value);
        }
        else if (node->gtOverflow())
        {
            llvm::Intrinsic::ID intrinsicId =
                node->IsUnsigned() ? llvm::Intrinsic::uadd_with_overflow : llvm::Intrinsic::sadd_with_overflow;
            addValue = emitCheckedArithmeticOperation(intrinsicId, op1Value, op2Value);
        }
        else
        {
            addValue = _builder.CreateAdd(op1Value, op2Value);
        }
    }

    mapGenTreeToValue(node, addValue);
}

void Llvm::buildSub(GenTreeOp* node)
{
    GenTree* op1 = node->gtGetOp1();
    GenTree* op2 = node->gtGetOp2();

    Value* subValue;
    if (!node->gtOverflow() && getGenTreeValue(op1)->getType()->isPointerTy())
    {
        Value* baseValue = consumeValue(op1, getPtrLlvmType());
        Value* subOffsetValue = consumeValue(op2, getIntPtrLlvmType());
        Value* addOffsetValue = _builder.CreateNeg(subOffsetValue);

        // GEPs scale indices, use type i8 makes them equivalent to the raw offsets we have in IR
        subValue = _builder.CreateGEP(Type::getInt8Ty(m_context->Context), baseValue, addOffsetValue);
    }
    else
    {
        Type* subLlvmType = getLlvmTypeForVarType(node->TypeGet());
        if (subLlvmType->isPointerTy())
        {
            // SUB<byref>(native int, ...) is valid (if rare) IR.
            subLlvmType = getIntPtrLlvmType();
        }
        Value* op1Value = consumeValue(op1, subLlvmType);
        Value* op2Value = consumeValue(op2, subLlvmType);

        if (varTypeIsFloating(node))
        {
            subValue = _builder.CreateFSub(op1Value, op2Value);
        }
        else if (node->gtOverflow())
        {
            llvm::Intrinsic::ID intrinsicId =
                node->IsUnsigned() ? llvm::Intrinsic::usub_with_overflow: llvm::Intrinsic::ssub_with_overflow;
            subValue = emitCheckedArithmeticOperation(intrinsicId, op1Value, op2Value);
        }
        else
        {
            subValue = _builder.CreateSub(op1Value, op2Value);
        }
    }

    mapGenTreeToValue(node, subValue);
}

void Llvm::buildAddrMode(GenTreeAddrMode* addrMode)
{
    // Address mode nodes (LEAs) as used in this backend signify two assumptions:
    //  1) The base address points (dynamically) at an allocated object (not null).
    //  2) The offset addition will never overflow.
    // Using LEAs in such a manner allows us to translate them to inbounds geps.
    //
    assert(addrMode->HasBase() && !addrMode->HasIndex());

    Value* baseValue = consumeValue(addrMode->Base(), getPtrLlvmType());
    Value* addrModeValue = gepOrAddrInBounds(baseValue, addrMode->Offset());

    mapGenTreeToValue(addrMode, addrModeValue);
}

void Llvm::buildDivMod(GenTree* node)
{
    GenTree* dividendNode = node->gtGetOp1();
    GenTree* divisorNode = node->gtGetOp2();
    Type* llvmType = getLlvmTypeForVarType(node->TypeGet());
    Value* dividendValue = consumeValue(dividendNode, llvmType);
    Value* divisorValue  = consumeValue(divisorNode, llvmType);
    Value* divModValue   = nullptr;

    ExceptionSetFlags exceptions = node->OperExceptions(_compiler);
    if ((exceptions & ExceptionSetFlags::DivideByZeroException) != ExceptionSetFlags::None)
    {
        Value* isDivisorZeroValue = _builder.CreateICmpEQ(divisorValue, llvm::ConstantInt::get(llvmType, 0));
        emitJumpToThrowHelper(isDivisorZeroValue, SCK_DIV_BY_ZERO);
    }
    if ((exceptions & ExceptionSetFlags::ArithmeticException) != ExceptionSetFlags::None)
    {
        // Check for "INT_MIN / -1" (which throws ArithmeticException).
        int64_t minDividend = node->TypeIs(TYP_LONG) ? INT64_MIN : INT32_MIN;
        Value* isDivisorMinusOneValue = _builder.CreateICmpEQ(divisorValue, llvm::ConstantInt::get(llvmType, -1));
        Value* isDividendMinValue = _builder.CreateICmpEQ(dividendValue, llvm::ConstantInt::get(llvmType, minDividend));
        Value* isOverflowValue = _builder.CreateAnd(isDivisorMinusOneValue, isDividendMinValue);
        emitJumpToThrowHelper(isOverflowValue, SCK_ARITH_EXCPN);
    }

    switch (node->OperGet())
    {
        case GT_DIV:
            divModValue = varTypeIsFloating(node) ? _builder.CreateFDiv(dividendValue, divisorValue)
                                                  : _builder.CreateSDiv(dividendValue, divisorValue);
            break;
        case GT_MOD:
            divModValue = varTypeIsFloating(node) ? _builder.CreateFRem(dividendValue, divisorValue)
                                                  : _builder.CreateSRem(dividendValue, divisorValue);
            break;
        case GT_UDIV:
            divModValue = _builder.CreateUDiv(dividendValue, divisorValue);
            break;
        case GT_UMOD:
            divModValue = _builder.CreateURem(dividendValue, divisorValue);
            break;
        default:
            unreached();
    }

    mapGenTreeToValue(node, divModValue);
}

void Llvm::buildRotate(GenTreeOp* node)
{
    assert(node->OperIs(GT_ROL, GT_ROR));

    Type* rotateLlvmType = getLlvmTypeForVarType(node->TypeGet());
    Value* srcValue = consumeValue(node->gtGetOp1(), rotateLlvmType);
    Value* indexValue = consumeValue(node->gtGetOp2(), Type::getInt32Ty(m_context->Context));
    if (indexValue->getType() != rotateLlvmType)
    {
        // The intrinsics require all operands have the same type.
        indexValue = _builder.CreateZExt(indexValue, rotateLlvmType);
    }

    // "Funnel shifts" are the recommended way to implement rotates in LLVM.
    llvm::Intrinsic::ID intrinsicId = node->OperIs(GT_ROL) ? llvm::Intrinsic::fshl : llvm::Intrinsic::fshr;
    Value* rotateValue = _builder.CreateIntrinsic(intrinsicId, rotateLlvmType, {srcValue, srcValue, indexValue});

    mapGenTreeToValue(node, rotateValue);
}

void Llvm::buildCast(GenTreeCast* cast)
{
    var_types castFromType = genActualType(cast->CastOp());
    var_types castToType = cast->CastToType();
    Type* castToLlvmType = getLlvmTypeForVarType(castToType);
    Type* castFromLlvmType = getLlvmTypeForVarType(castFromType);
    Value* castFromValue = consumeValue(cast->CastOp(), castFromLlvmType);
    Value* castValue = nullptr;

    if (cast->gtOverflow())
    {
        Value* isOverflowValue;
        if (varTypeIsFloating(castFromType))
        {
            // Algorithm and values taken verbatim from "utils.cpp", 'Casting from floating point to integer types',
            // with the modification to produce "!isNotOverflow" value directly (via condition reversal).
            double lowerBound;
            double upperBound;
            llvm::CmpInst::Predicate lowerCond = llvm::CmpInst::FCMP_ULE;
            llvm::CmpInst::Predicate upperCond = llvm::CmpInst::FCMP_UGE;
            switch (castToType)
            {
                case TYP_BYTE:
                    lowerBound = -129.0;
                    upperBound = 128.0;
                    break;
                case TYP_BOOL:
                case TYP_UBYTE:
                    lowerBound = -1.0;
                    upperBound = 256.0;
                    break;
                case TYP_SHORT:
                    lowerBound = -32769.0;
                    upperBound = 32768.0;
                    break;
                case TYP_USHORT:
                    lowerBound = -1.0;
                    upperBound = 65536.0;
                    break;
                case TYP_INT:
                    if (castFromType == TYP_FLOAT)
                    {
                        lowerCond = llvm::CmpInst::FCMP_ULT;
                        lowerBound = -2147483648.0;
                    }
                    else
                    {
                        lowerBound = -2147483649.0;
                    }
                    upperBound = 2147483648.0;
                    break;
                case TYP_UINT:
                    lowerBound = -1.0;
                    upperBound = 4294967296.0;
                    break;
                case TYP_LONG:
                    lowerCond = llvm::CmpInst::FCMP_ULT;
                    lowerBound = -9223372036854775808.0;
                    upperBound = 9223372036854775808.0;
                    break;
                case TYP_ULONG:
                    lowerBound = -1.0;
                    upperBound = 18446744073709551616.0;
                    break;
                default:
                    unreached();
            }

            Value* lowerBoundValue = llvm::ConstantFP::get(castFromLlvmType, lowerBound);
            Value* upperBoundValue = llvm::ConstantFP::get(castFromLlvmType, upperBound);
            Value* lowerTestValue = _builder.CreateCmp(lowerCond, castFromValue, lowerBoundValue);
            Value* upperTestValue = _builder.CreateCmp(upperCond, castFromValue, upperBoundValue);
            isOverflowValue = _builder.CreateOr(lowerTestValue, upperTestValue);
        }
        else
        {
            // There are no checked casts to FP types.
            assert(varTypeIsIntegralOrI(castFromType) && varTypeIsIntegral(castToType));

            IntegralRange checkedRange = IntegralRange::ForCastInput(cast);
            int64_t lowerBound = IntegralRange::SymbolicToRealValue(checkedRange.GetLowerBound());
            int64_t upperBound = IntegralRange::SymbolicToRealValue(checkedRange.GetUpperBound());

            Value* checkedValue = castFromValue;
            if (checkedValue->getType()->isPointerTy())
            {
                // Checked casts with byref sources are legal.
                checkedValue = _builder.CreatePtrToInt(checkedValue, getIntPtrLlvmType());
            }

            if (lowerBound != 0)
            {
                // This "add" checking technique was taken from the IR clang generates for "(l <= x) && (x <= u)".
                int64_t addDelta = -lowerBound;
                Value* deltaValue = llvm::ConstantInt::get(castFromLlvmType, addDelta);
                checkedValue = _builder.CreateAdd(checkedValue, deltaValue);

                upperBound += addDelta;
            }

            Value* upperBoundValue = llvm::ConstantInt::get(castFromLlvmType, upperBound);
            isOverflowValue = _builder.CreateCmp(llvm::CmpInst::ICMP_UGT, checkedValue, upperBoundValue);
        }

        emitJumpToThrowHelper(isOverflowValue, SCK_OVERFLOW);
    }

    switch (castFromType)
    {
        case TYP_BYREF:
            assert(castFromValue->getType()->isPointerTy());
            if (castToType == TYP_I_IMPL)
            {
                // The user is likely to consume this as a pointer; leave the value unchanged.
                castValue = castFromValue;
                break;
            }
            castFromValue = _builder.CreatePtrToInt(castFromValue, getIntPtrLlvmType());
            FALLTHROUGH;

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
                    castValue = _builder.CreateTrunc(castFromValue, castToLlvmType);
                    break;

                case TYP_LONG:
                case TYP_ULONG:
                    castValue = cast->IsUnsigned()
                        ? _builder.CreateZExt(castFromValue, castToLlvmType)
                        : _builder.CreateSExt(castFromValue, castToLlvmType);
                    break;

                case TYP_FLOAT:
                case TYP_DOUBLE:
                    castValue = cast->IsUnsigned()
                        ? _builder.CreateUIToFP(castFromValue, castToLlvmType)
                        : _builder.CreateSIToFP(castFromValue, castToLlvmType);
                    break;

                default:
                    unreached();
            }
            break;

        case TYP_FLOAT:
        case TYP_DOUBLE:
            switch (castToType)
            {
                case TYP_FLOAT:
                case TYP_DOUBLE:
                    castValue = _builder.CreateFPCast(castFromValue, castToLlvmType);
                    break;

                case TYP_BYTE:
                case TYP_SHORT:
                case TYP_INT:
                case TYP_LONG:
                    castValue = _builder.CreateFPToSI(castFromValue, castToLlvmType);
                    break;

                case TYP_BOOL:
                case TYP_UBYTE:
                case TYP_USHORT:
                case TYP_UINT:
                case TYP_ULONG:
                    castValue = _builder.CreateFPToUI(castFromValue, castToLlvmType);
                    break;

                default:
                    unreached();
            }
            break;

        default:
            unreached();
    }

    mapGenTreeToValue(cast, castValue);
}

void Llvm::buildLclHeap(GenTreeUnOp* lclHeap)
{
    GenTree* sizeNode = lclHeap->gtGetOp1();
    assert(genActualTypeIsIntOrI(sizeNode));

    Value* sizeValue = consumeValue(sizeNode, getLlvmTypeForVarType(genActualType(sizeNode)));
    Value* lclHeapValue;

    // A zero-sized LCLHEAP yields a null pointer.
    if (sizeNode->IsIntegralConst(0))
    {
        lclHeapValue = llvm::Constant::getNullValue(getPtrLlvmType());
    }
    else
    {
        llvm::BasicBlock* beforeAllocLlvmBlock = nullptr;
        llvm::BasicBlock* joinLlvmBlock = nullptr;
        if (!sizeNode->IsIntegralConst())
        {
            beforeAllocLlvmBlock = _builder.GetInsertBlock();
            llvm::BasicBlock* allocLlvmBlock = createInlineLlvmBlock();
            joinLlvmBlock = createInlineLlvmBlock();

            Value* zeroSizeValue = llvm::Constant::getNullValue(sizeValue->getType());
            Value* isSizeZeroValue = _builder.CreateICmpEQ(sizeValue, zeroSizeValue);
            _builder.CreateCondBr(isSizeZeroValue, joinLlvmBlock, allocLlvmBlock);
            _builder.SetInsertPoint(allocLlvmBlock);
        }

        // LCLHEAP (aka IL's "localloc") is specified to return a pointer "...aligned so that any built-in
        // data type can be stored there using the stind instructions"; that means 8 bytes for a double.
        llvm::Align lclHeapAlignment(genTypeSize(TYP_DOUBLE));
        llvm::AllocaInst* allocaInst = _builder.CreateAlloca(Type::getInt8Ty(m_context->Context), sizeValue);
        allocaInst->setAlignment(lclHeapAlignment);
        lclHeapValue = allocaInst;

        // "If the localsinit flag on the method is true, the block of memory returned is initialized to 0".
        if (_compiler->info.compInitMem)
        {
            _builder.CreateMemSet(lclHeapValue, _builder.getInt8(0), sizeValue, lclHeapAlignment);
        }

        if (joinLlvmBlock != nullptr)
        {
            llvm::BasicBlock* allocLlvmBlock = _builder.GetInsertBlock();
            _builder.CreateBr(joinLlvmBlock);

            _builder.SetInsertPoint(joinLlvmBlock);
            llvm::PHINode* lclHeapPhi = _builder.CreatePHI(lclHeapValue->getType(), 2);
            lclHeapPhi->addIncoming(lclHeapValue, allocLlvmBlock);
            lclHeapPhi->addIncoming(llvm::Constant::getNullValue(getPtrLlvmType()), beforeAllocLlvmBlock);

            lclHeapValue = lclHeapPhi;
        }
    }

    mapGenTreeToValue(lclHeap, lclHeapValue);
}

void Llvm::buildCmp(GenTreeOp* node)
{
    using Predicate = llvm::CmpInst::Predicate;

    bool isIntOrPtr = varTypeIsIntegralOrI(node->gtGetOp1());
    bool isUnsigned = node->IsUnsigned();
    bool isUnordered = (node->gtFlags & GTF_RELOP_NAN_UN) != 0;
    Predicate predicate;
    switch (node->OperGet())
    {
        case GT_EQ:
            predicate = isIntOrPtr ? Predicate::ICMP_EQ : (isUnordered ? Predicate::FCMP_UEQ : Predicate::FCMP_OEQ);
            break;
        case GT_NE:
            predicate = isIntOrPtr ? Predicate::ICMP_NE : (isUnordered ? Predicate::FCMP_UNE : Predicate::FCMP_ONE);
            break;
        case GT_LE:
            predicate = isIntOrPtr ? (isUnsigned ? Predicate::ICMP_ULE : Predicate::ICMP_SLE)
                                   : (isUnordered ? Predicate::FCMP_ULE : Predicate::FCMP_OLE);
            break;
        case GT_LT:
            predicate = isIntOrPtr ? (isUnsigned ? Predicate::ICMP_ULT : Predicate::ICMP_SLT)
                                   : (isUnordered ? Predicate::FCMP_ULT : Predicate::FCMP_OLT);
            break;
        case GT_GE:
            predicate = isIntOrPtr ? (isUnsigned ? Predicate::ICMP_UGE : Predicate::ICMP_SGE)
                                   : (isUnordered ? Predicate::FCMP_UGE : Predicate::FCMP_OGE);
            break;
        case GT_GT:
            predicate = isIntOrPtr ? (isUnsigned ? Predicate::ICMP_UGT : Predicate::ICMP_SGT)
                                   : (isUnordered ? Predicate::FCMP_UGT : Predicate::FCMP_OGT);
            break;
        default:
            unreached();
    }

    // Comparing refs and ints is valid LIR, but not LLVM so handle that case by converting the int to a ref.
    GenTree* op1 = node->gtGetOp1();
    GenTree* op2 = node->gtGetOp2();
    Type* op1RawType = getGenTreeValue(op1)->getType();
    Type* op2RawType = getGenTreeValue(op2)->getType();
    Type* cmpLlvmType =
        (op1RawType->isPointerTy() && (op1RawType == op2RawType)) ? op1RawType
                                                                  : getLlvmTypeForVarType(genActualType(op1));

    Value* op1Value = consumeValue(op1, cmpLlvmType);
    Value* op2Value = consumeValue(op2, cmpLlvmType);
    Value* cmpValue = _builder.CreateCmp(predicate, op1Value, op2Value);

    mapGenTreeToValue(node, cmpValue);
}

void Llvm::buildCnsDouble(GenTreeDblCon* node)
{
    mapGenTreeToValue(node, llvm::ConstantFP::get(getLlvmTypeForVarType(node->TypeGet()), node->DconValue()));
}

void Llvm::buildIntegralConst(GenTreeIntConCommon* node)
{
    var_types constType = node->TypeGet();
    Type* constLlvmType = getLlvmTypeForVarType(constType);

    Value* constValue;
    if (node->IsCnsIntOrI() && node->IsIconHandle()) // TODO-LLVM: change to simply "IsIconHandle" once upstream does.
    {
        constValue = getOrCreateSymbol(CORINFO_GENERIC_HANDLE(node->AsIntCon()->IconValue()));
    }
    else
    {
        llvm::APInt llvmConst(genTypeSize(constType) * BITS_PER_BYTE, node->IntegralValue());
        constValue = llvm::Constant::getIntegerValue(constLlvmType, llvmConst);
    }

    mapGenTreeToValue(node, constValue);
}

void Llvm::buildCall(GenTreeCall* call)
{
    std::vector<Value*> argVec = std::vector<Value*>();
    for (CallArg& arg : call->gtArgs.Args())
    {
        Type* argLlvmType = getLlvmTypeForCorInfoType(getLlvmArgTypeForCallArg(&arg), arg.GetSignatureClassHandle());
        Value* argValue = consumeValue(arg.GetNode(), argLlvmType);

        argVec.push_back(argValue);
    }

    // We may come back into managed from the unmanaged call so store the shadow stack.
    if (callRequiresShadowStackSave(call))
    {
        // TODO-LLVM-CQ: fold it into the PI helper call when possible.
        emitHelperCall(CORINFO_HELP_LLVM_SET_SHADOW_STACK_TOP, getShadowStackForCallee());
    }

    llvm::FunctionCallee llvmFuncCallee = consumeCallTarget(call);
    Value* callValue;
    if (call->IsUnmanaged())
    {
        // We do not support exceptions propagating through native<->managed boundaries.
        llvm::CallInst* callInst = _builder.CreateCall(llvmFuncCallee, argVec);
        callInst->addFnAttr(llvm::Attribute::NoUnwind);

        callValue = callInst;
    }
    else
    {
        callValue = emitCallOrInvoke(llvmFuncCallee, argVec);
    }

    mapGenTreeToValue(call, callValue);
}

void Llvm::buildInd(GenTreeIndir* indNode)
{
    Type* loadLlvmType = getLlvmTypeForVarType(indNode->TypeGet());
    Value* addrValue = consumeAddressAndEmitNullCheck(indNode);
    Value* loadValue = _builder.CreateLoad(loadLlvmType, addrValue);

    mapGenTreeToValue(indNode, loadValue);
}

void Llvm::buildBlk(GenTreeBlk* blkNode)
{
    Type* blkLlvmType = getLlvmTypeForStruct(blkNode->GetLayout());
    Value* addrValue = consumeAddressAndEmitNullCheck(blkNode);
    Value* blkValue = _builder.CreateLoad(blkLlvmType, addrValue);

    mapGenTreeToValue(blkNode, blkValue);
}

void Llvm::buildStoreInd(GenTreeStoreInd* storeIndOp)
{
    GCInfo::WriteBarrierForm wbf = getGCInfo()->gcIsWriteBarrierCandidate(storeIndOp);

    Type* storeLlvmType = getLlvmTypeForVarType(storeIndOp->TypeGet());
    Value* addrValue = consumeAddressAndEmitNullCheck(storeIndOp);
    Value* dataValue = consumeValue(storeIndOp->Data(), storeLlvmType);;

    switch (wbf)
    {
        case GCInfo::WBF_BarrierUnchecked:
            emitHelperCall(CORINFO_HELP_ASSIGN_REF, {addrValue, dataValue});
            break;

        case GCInfo::WBF_BarrierChecked:
        case GCInfo::WBF_BarrierUnknown:
            emitHelperCall(CORINFO_HELP_CHECKED_ASSIGN_REF, {addrValue, dataValue});
            break;

        case GCInfo::WBF_NoBarrier:
            _builder.CreateStore(dataValue, addrValue);
            break;

        default:
            unreached();
    }
}

void Llvm::buildStoreBlk(GenTreeBlk* blockOp)
{
    ClassLayout* layout = blockOp->GetLayout();
    GenTree* addrNode = blockOp->Addr();
    GenTree* dataNode = blockOp->Data();
    Value* addrValue = consumeAddressAndEmitNullCheck(blockOp);

    // Check for the "initblk" operation ("dataNode" is either INIT_VAL or constant zero).
    if (blockOp->OperIsInitBlkOp())
    {
        Value* fillValue = consumeInitVal(dataNode);
        _builder.CreateMemSet(addrValue, fillValue, _builder.getInt32(layout->GetSize()), llvm::Align());
        return;
    }

    Value* dataValue = consumeValue(dataNode, getLlvmTypeForStruct(layout));
    if (layout->HasGCPtr() && ((blockOp->gtFlags & GTF_IND_TGT_NOT_HEAP) == 0) && !addrNode->OperIs(GT_LCL_ADDR))
    {
        storeObjAtAddress(addrValue, dataValue, getStructDesc(layout->GetClassHandle()));
    }
    else
    {
        _builder.CreateStore(dataValue, addrValue);
    }
}

void Llvm::buildStoreDynBlk(GenTreeStoreDynBlk* blockOp)
{
    bool isCopyBlock = blockOp->OperIsCopyBlkOp();
    GenTree* srcNode = blockOp->Data();
    GenTree* sizeNode = blockOp->gtDynamicSize;

    // STORE_DYN_BLK accepts native-sized size operands.
    Type* sizeLlvmType = getIntPtrLlvmType();
    Value* sizeValue = consumeValue(sizeNode, sizeLlvmType);

    // STORE_DYN_BLK's contract is that it must not throw any exceptions in case the dynamic size is zero and must throw
    // NRE otherwise.
    bool dstAddrMayBeNull = (blockOp->gtFlags & GTF_IND_NONFAULTING) == 0;
    bool srcAddrMayBeNull = isCopyBlock && ((srcNode->gtFlags & GTF_IND_NONFAULTING) == 0);
    llvm::BasicBlock* checkSizeLlvmBlock = nullptr;
    llvm::BasicBlock* nullChecksLlvmBlock = nullptr;

    // TODO-LLVM-CQ: we should use CORINFO_HELP_MEMCPY/CORINFO_HELP_MEMSET here if we need to do the size check (it will
    // result in smaller code). But currently we cannot because ILC maps these to native "memcpy/memset", which do not
    // have the right semantics (don't throw NREs).
    if (dstAddrMayBeNull || srcAddrMayBeNull)
    {
        //
        // if (sizeIsZeroValue) goto PASSED; else goto CHECK_DST; (we'll add this below)
        // CHECK_DST:
        //   if (dst is null) Throw();
        // CHECK_SRC:
        //   if (src is null) Throw();
        // COPY:
        //   memcpy/memset
        // PASSED:
        //
        checkSizeLlvmBlock = _builder.GetInsertBlock();
        nullChecksLlvmBlock = createInlineLlvmBlock();
        _builder.SetInsertPoint(nullChecksLlvmBlock);
    }

    // Technically cpblk/initblk specify that they expect their sources/destinations to be aligned, but in
    // practice these instructions are used like memcpy/memset, which do not require this. So we do not try
    // to be more precise with the alignment specification here as well.
    // TODO-LLVM: volatile STORE_DYN_BLK.
    Value* dstAddrValue = consumeAddressAndEmitNullCheck(blockOp);
    if (isCopyBlock)
    {
        Value* srcAddrValue = consumeAddressAndEmitNullCheck(srcNode->AsIndir());
        _builder.CreateMemCpy(dstAddrValue, llvm::MaybeAlign(), srcAddrValue, llvm::MaybeAlign(), sizeValue);
    }
    else
    {
        Value* initValue = consumeInitVal(srcNode);
        _builder.CreateMemSet(dstAddrValue, initValue, sizeValue, llvm::MaybeAlign());
    }

    if (checkSizeLlvmBlock != nullptr)
    {
        llvm::BasicBlock* skipOperationLlvmBlock = createInlineLlvmBlock();
        _builder.CreateBr(skipOperationLlvmBlock);

        _builder.SetInsertPoint(checkSizeLlvmBlock);
        Value* sizeIsZeroValue = _builder.CreateICmpEQ(sizeValue, llvm::ConstantInt::getNullValue(sizeLlvmType));
        _builder.CreateCondBr(sizeIsZeroValue, skipOperationLlvmBlock, nullChecksLlvmBlock);

        _builder.SetInsertPoint(skipOperationLlvmBlock);
    }
}

void Llvm::buildUnaryOperation(GenTree* node)
{
    GenTree* op1 = node->gtGetOp1();
    Type* op1Type = getLlvmTypeForVarType(genActualType(op1));
    Value* op1Value = consumeValue(op1, op1Type);

    Value* nodeValue;
    switch (node->OperGet())
    {
        case GT_NEG:
            if (varTypeIsFloating(node))
            {
                nodeValue = _builder.CreateFNeg(op1Value);
            }
            else
            {
                nodeValue = _builder.CreateNeg(op1Value);
            }
            break;
        case GT_NOT:
            nodeValue = _builder.CreateNot(op1Value);
            break;
        case GT_BITCAST:
            nodeValue = _builder.CreateBitCast(op1Value, getLlvmTypeForVarType(node->TypeGet()));
            break;
        default:
            unreached();
    }

    mapGenTreeToValue(node, nodeValue);
}

void Llvm::buildBinaryOperation(GenTree* node)
{
    Value* result;
    Type*  targetType = getLlvmTypeForVarType(node->TypeGet());
    Value* op1Value = consumeValue(node->gtGetOp1(), targetType);
    Value* op2Value = consumeValue(node->gtGetOp2(), targetType);

    switch (node->OperGet())
    {
        case GT_MUL:
            if (varTypeIsFloating(node))
            {
                result = _builder.CreateFMul(op1Value, op2Value);
            }
            else if (node->gtOverflow())
            {
                llvm::Intrinsic::ID intrinsicId =
                    node->IsUnsigned() ? llvm::Intrinsic::umul_with_overflow : llvm::Intrinsic::smul_with_overflow;
                result = emitCheckedArithmeticOperation(intrinsicId, op1Value, op2Value);
            }
            else
            {
                result = _builder.CreateMul(op1Value, op2Value);
            }
            break;
        case GT_AND:
            result = _builder.CreateAnd(op1Value, op2Value);
            break;
        case GT_OR:
            result = _builder.CreateOr(op1Value, op2Value);
            break;
        case GT_XOR:
            result = _builder.CreateXor(op1Value, op2Value);
            break;
        default:
            unreached();
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
            unreached();
    }

    mapGenTreeToValue(node, result);
}

void Llvm::buildIntrinsic(GenTreeIntrinsic* intrinsicNode)
{
    llvm::Intrinsic::ID intrinsicId = getLlvmIntrinsic(intrinsicNode->gtIntrinsicName);
    noway_assert(intrinsicId != llvm::Intrinsic::not_intrinsic);
    assert(varTypeIsFloating(intrinsicNode));

    Type* opLlvmType = getLlvmTypeForVarType(intrinsicNode->TypeGet());
    GenTree* op1 = intrinsicNode->gtGetOp1();
    GenTree* op2 = intrinsicNode->gtGetOp2();
    Value* op1Value = consumeValue(op1, opLlvmType);
    Value* op2Value = (op2 != nullptr) ? consumeValue(op2, opLlvmType) : nullptr;

    Value* intrinsicValue;
    if (op2 == nullptr)
    {
        intrinsicValue = _builder.CreateIntrinsic(intrinsicId, opLlvmType, op1Value);
    }
    else
    {
        intrinsicValue = _builder.CreateIntrinsic(intrinsicId, opLlvmType, {op1Value, op2Value});
    }

    mapGenTreeToValue(intrinsicNode, intrinsicValue);
}

void Llvm::buildMemoryBarrier(GenTree* node)
{
    assert(node->OperIs(GT_MEMORYBARRIER));
    _builder.CreateFence(llvm::AtomicOrdering::AcquireRelease);
}

void Llvm::buildCatchArg(GenTree* catchArg)
{
    assert(catchArg->OperIs(GT_CATCH_ARG) && handlerGetsXcptnObj(CurrentBlock()->bbCatchTyp));
    assert(catchArg == LIR::AsRange(CurrentBlock()).FirstNonPhiNode());

    unsigned exceptionArgIndex = isBlockInFilter(CurrentBlock()) ? 2 : 1;
    Value* catchArgValue = getCurrentLlvmFunction()->getArg(exceptionArgIndex);
    mapGenTreeToValue(catchArg, catchArgValue);
}

void Llvm::buildReturn(GenTree* node)
{
    assert(node->OperIs(GT_RETURN, GT_RETFILT));

    if (node->OperIs(GT_RETURN) && _compiler->opts.IsReversePInvoke())
    {
        emitHelperCall(CORINFO_HELP_LLVM_SET_SHADOW_STACK_TOP, getShadowStack());
    }

    if (node->TypeIs(TYP_VOID))
    {
        _builder.CreateRetVoid();
        return;
    }

    GenTree* retValNode = node->gtGetOp1();
    Type* retLlvmType = getCurrentLlvmFunction()->getFunctionType()->getReturnType();

    Value* retValValue;
    // Special-case returning zero-initialized structs.
    if (node->TypeIs(TYP_STRUCT) && retValNode->IsIntegralConst(0))
    {
        retValValue = llvm::Constant::getNullValue(retLlvmType);
    }
    else
    {
        retValValue = consumeValue(retValNode, retLlvmType);
    }

    _builder.CreateRet(retValValue);
}

void Llvm::buildJTrue(GenTree* node)
{
    Value* condValue = getGenTreeValue(node->gtGetOp1());
    assert(condValue->getType() == Type::getInt1Ty(m_context->Context)); // Only relops expected.

    BasicBlock* srcBlock = CurrentBlock();
    llvm::BasicBlock* jmpLlvmBlock = getFirstLlvmBlockForBlock(srcBlock->bbJumpDest);
    llvm::BasicBlock* nextLlvmBlock = getFirstLlvmBlockForBlock(srcBlock->bbNext);

    // Handle the degenerate case specially. PHI code depends on us not generating duplicate outgoing edges here.
    if (jmpLlvmBlock == nextLlvmBlock)
    {
        _builder.CreateBr(nextLlvmBlock);
    }
    else
    {
        _builder.CreateCondBr(condValue, jmpLlvmBlock, nextLlvmBlock);
    }
}

void Llvm::buildSwitch(GenTreeUnOp* switchNode)
{
    // While in IL "switch" can only take INTs, RyuJit has historically allowed native ints as well.
    // We follow suit and allow any value LLVM would.
    GenTree* destOp = switchNode->gtGetOp1();
    llvm::IntegerType* switchLlvmType = llvm::cast<llvm::IntegerType>(getLlvmTypeForVarType(genActualType(destOp)));
    Value* destValue = consumeValue(destOp, switchLlvmType);

    BasicBlock* srcBlock = CurrentBlock();
    assert(srcBlock->bbJumpKind == BBJ_SWITCH);

    BBswtDesc* switchDesc = srcBlock->bbJumpSwt;
    unsigned casesCount = switchDesc->bbsCount - 1;
    noway_assert(switchDesc->bbsHasDefault);

    BasicBlock* defaultDestBlock = switchDesc->getDefault();
    llvm::BasicBlock* defaultDestLlvmBlock = getFirstLlvmBlockForBlock(defaultDestBlock);
    llvm::SwitchInst* switchInst = _builder.CreateSwitch(destValue, defaultDestLlvmBlock, casesCount);

    for (unsigned destIndex = 0; destIndex < casesCount; destIndex++)
    {
        llvm::ConstantInt* destIndexValue = llvm::ConstantInt::get(switchLlvmType, destIndex);
        llvm::BasicBlock* destLlvmBlock = getFirstLlvmBlockForBlock(switchDesc->bbsDstTab[destIndex]);

        switchInst->addCase(destIndexValue, destLlvmBlock);
    }
}

void Llvm::buildNullCheck(GenTreeIndir* nullCheckNode)
{
    consumeAddressAndEmitNullCheck(nullCheckNode);
}

void Llvm::buildBoundsCheck(GenTreeBoundsChk* boundsCheckNode)
{
    Type* checkLlvmType = getLlvmTypeForVarType(genActualType(boundsCheckNode->GetIndex()));
    Value* indexValue = consumeValue(boundsCheckNode->GetIndex(), checkLlvmType);
    Value* lengthValue = consumeValue(boundsCheckNode->GetArrayLength(), checkLlvmType);

    Value* indexOutOfRangeValue = _builder.CreateCmp(llvm::CmpInst::ICMP_UGE, indexValue, lengthValue);
    emitJumpToThrowHelper(indexOutOfRangeValue, boundsCheckNode->gtThrowKind);
}

void Llvm::buildCkFinite(GenTreeUnOp* ckNode)
{
    assert(varTypeIsFloating(ckNode));
    Type* fpLlvmType = getLlvmTypeForVarType(ckNode->TypeGet());
    Value* opValue = consumeValue(ckNode->gtGetOp1(), fpLlvmType);

    // Taken from IR Clang generates for "isfinite".
    Value* absOpValue = _builder.CreateIntrinsic(llvm::Intrinsic::fabs, fpLlvmType, opValue);
    Value* isNotFiniteValue = _builder.CreateFCmpUEQ(absOpValue, llvm::ConstantFP::get(fpLlvmType, INFINITY));
    emitJumpToThrowHelper(isNotFiniteValue, SCK_ARITH_EXCPN);;

    mapGenTreeToValue(ckNode, opValue);
}

void Llvm::buildKeepAlive(GenTreeUnOp* keepAliveNode)
{
    // KEEPALIVE is used to represent implicit uses of GC-visible values, e. g.:
    //
    //  ObjWithFinalizer obj = new ObjWithFinalizer();
    //  NativeResource handle = obj.NativeResource;
    //  <-- Here the compiler could think liveness of "obj" ends and permit its finalization. -->
    //  NativeCall(handle);
    //  <-- We insert KeepAlive s.t. we don't finalize away "handle" while it is still in use by the native call. -->
    //  GC.KeepAlive(obj)
    //
    // In the shadow stack model this is handled in lowering so we don't need to do anything here.
}

void Llvm::buildILOffset(GenTreeILOffset* ilOffsetNode)
{
    if (m_diFunction == nullptr)
    {
        return;
    }

    // TODO-LLVM: support accurate debug info for inlinees.
    DebugInfo debugInfo = ilOffsetNode->gtStmtDI.GetRoot();
    if (!debugInfo.IsValid())
    {
        // Leave the current DI location unchanged.
        return;
    }

    unsigned ilOffset = debugInfo.GetLocation().GetOffset();
    unsigned lineNo = getLineNumberForILOffset(ilOffset);
    llvm::DILocation* diLocation = getDebugLocation(lineNo);

    _builder.SetCurrentDebugLocation(diLocation);
}

void Llvm::buildCallFinally(BasicBlock* block)
{
    assert(block->bbJumpKind == BBJ_CALLFINALLY);

    // Callfinally blocks always come in pairs, where the first block (BBJ_CALLFINALLY itself)
    // calls the finally (its "bbJumpDest") while the second block (BBJ_ALWAYS) provides in its
    // "bbJumpDest" the target to which the finally call (if not "retless") should return.
    // Other backends will simply skip generating the second block, while we will branch to it.
    //
    Function* finallyLlvmFunc = getLlvmFunctionForIndex(getLlvmFunctionIndexForBlock(block->bbJumpDest));
    emitCallOrInvoke(finallyLlvmFunc, getShadowStack());

    // Some tricky EH flow configurations can make the ALWAYS part of the pair unreachable without
    // marking "block" "BBF_RETLESS_CALL". Detect this case by checking if the next block is reachable
    // at all.
    if (((block->bbFlags & BBF_RETLESS_CALL) != 0) || !isReachable(block->bbNext))
    {
        _builder.CreateUnreachable();
    }
    else
    {
        assert(block->isBBCallAlwaysPair());
        _builder.CreateBr(getFirstLlvmBlockForBlock(block->bbNext));
    }
}

Value* Llvm::consumeAddressAndEmitNullCheck(GenTreeIndir* indir)
{
    GenTree* addr = indir->Addr();
    unsigned offset = 0;
    if (addr->isContained())
    {
        assert(addr->OperIs(GT_LEA) && addr->AsAddrMode()->HasBase() && !addr->AsAddrMode()->HasIndex());
        offset = addr->AsAddrMode()->Offset();
        addr = addr->AsAddrMode()->Base();
    }

    Value* addrValue = consumeValue(addr, getPtrLlvmType());

    if ((indir->gtFlags & GTF_IND_NONFAULTING) == 0)
    {
        // Note how we emit the check **before** the inbounds GEP so as to avoid the latter producing poison.
        emitNullCheckForAddress(addr, addrValue);
    }

    addrValue = gepOrAddrInBounds(addrValue, offset);
    return addrValue;
}

void Llvm::emitNullCheckForAddress(GenTree* addr, Value* addrValue)
{
    // The frontend's contract with the backend is that it will not insert null checks for accesses which
    // are inside the "[0..compMaxUncheckedOffsetForNullObject]" range. Thus, we usually need to check not
    // just for "null", but "null + small offset". However, for TYP_REF, we know it will either be a valid
    // object on heap, or null, and can utilize the more direct form.
    Value* isNullValue;
    if (addr->TypeIs(TYP_REF))
    {
        isNullValue = _builder.CreateIsNull(addrValue);
    }
    else
    {
        Value* checkValue = getIntPtrConst(_compiler->compMaxUncheckedOffsetForNullObject + 1, addrValue->getType());
        isNullValue = _builder.CreateICmpULT(addrValue, checkValue);
    }

    emitJumpToThrowHelper(isNullValue, SCK_NULL_REF_EXCPN);
}

Value* Llvm::consumeInitVal(GenTree* initVal)
{
    assert(initVal->isContained());
    if (initVal->IsIntegralConst())
    {
        assert(initVal->IsIntegralConst(0));
        return _builder.getInt8(0);
    }

    assert(initVal->OperIsInitVal());
    return consumeValue(initVal->gtGetOp1(), Type::getInt8Ty(m_context->Context));
}

void Llvm::storeObjAtAddress(Value* baseAddress, Value* data, StructDesc* structDesc)
{
    unsigned fieldCount = structDesc->getFieldCount();
    unsigned bytesStored = 0;

    for (unsigned i = 0; i < fieldCount; i++)
    {
        FieldDesc* fieldDesc = structDesc->getFieldDesc(i);
        unsigned   fieldOffset = fieldDesc->getFieldOffset();
        Value*     address     = gepOrAddr(baseAddress, fieldOffset);

        if (structDesc->hasSignificantPadding() && fieldOffset > bytesStored)
        {
            bytesStored += buildMemCpy(baseAddress, bytesStored, fieldOffset, address);
        }

        Value* fieldData = nullptr;
        if (data->getType()->isStructTy())
        {
            const llvm::StructLayout* structLayout = m_context->Module.getDataLayout().getStructLayout(static_cast<llvm::StructType*>(data->getType()));

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
            if (fieldDesc->getCorType() == CORINFO_TYPE_CLASS)
            {
                // We can't be sure the address is on the heap, it could be the result of pointer arithmetic on a local var.
                emitHelperCall(CORINFO_HELP_CHECKED_ASSIGN_REF, {address, fieldData});
                bytesStored += TARGET_POINTER_SIZE;
            }
            else
            {
                _builder.CreateStore(fieldData, address);
                bytesStored += fieldData->getType()->getPrimitiveSizeInBits() / BITS_PER_BYTE;
            }
        }
    }

    unsigned llvmStructSize = data->getType()->getPrimitiveSizeInBits() / BITS_PER_BYTE;
    if (structDesc->hasSignificantPadding() && llvmStructSize > bytesStored)
    {
        Value* srcAddress = gepOrAddr(baseAddress, bytesStored);

        buildMemCpy(baseAddress, bytesStored, llvmStructSize, srcAddress);
    }
}

// Copies endOffset - startOffset bytes, endOffset is exclusive.
unsigned Llvm::buildMemCpy(Value* baseAddress, unsigned startOffset, unsigned endOffset, Value* srcAddress)
{
    Value* destAddress = gepOrAddr(baseAddress, startOffset);
    unsigned size = endOffset - startOffset;

    _builder.CreateMemCpy(destAddress, llvm::Align(), srcAddress, llvm::Align(), size);

    return size;
}

void Llvm::emitJumpToThrowHelper(Value* jumpCondValue, SpecialCodeKind throwKind)
{
    if (_compiler->fgUseThrowHelperBlocks())
    {
        assert(CurrentBlock() != nullptr);

        // For code with throw helper blocks, find and use the shared helper block for raising the exception.
        unsigned throwIndex = _compiler->bbThrowIndex(CurrentBlock());
        BasicBlock* throwBlock = _compiler->fgFindExcptnTarget(throwKind, throwIndex)->acdDstBlk;
        throwBlock->bbFlags |= BBF_MARKED;

        // Jump to the exception-throwing block on error.
        llvm::BasicBlock* nextLlvmBlock = createInlineLlvmBlock();
        llvm::BasicBlock* throwLlvmBlock = getFirstLlvmBlockForBlock(throwBlock);
        _builder.CreateCondBr(jumpCondValue, throwLlvmBlock, nextLlvmBlock);
        _builder.SetInsertPoint(nextLlvmBlock);
    }
    else
    {
        // The code to throw the exception will be generated inline; we will jump around it in the non-exception case.
        llvm::BasicBlock* jumpCondLlvmBlock = _builder.GetInsertBlock();

        llvm::BasicBlock* throwLlvmBlock = createInlineLlvmBlock();
        _builder.SetInsertPoint(throwLlvmBlock);
        emitHelperCall(static_cast<CorInfoHelpFunc>(_compiler->acdHelper(throwKind)));
        _builder.CreateUnreachable();

        llvm::BasicBlock* nextLlvmBlock = createInlineLlvmBlock();
        _builder.SetInsertPoint(jumpCondLlvmBlock);
        _builder.CreateCondBr(jumpCondValue, throwLlvmBlock, nextLlvmBlock);

        _builder.SetInsertPoint(nextLlvmBlock);
    }
}

Value* Llvm::emitCheckedArithmeticOperation(llvm::Intrinsic::ID intrinsicId, Value* op1Value, Value* op2Value)
{
    assert(op1Value->getType()->isIntegerTy() && op2Value->getType()->isIntegerTy());

    Value* checkedValue = _builder.CreateIntrinsic(intrinsicId, op1Value->getType(), {op1Value, op2Value});
    Value* isOverflowValue = _builder.CreateExtractValue(checkedValue, 1);
    emitJumpToThrowHelper(isOverflowValue, SCK_OVERFLOW);

    return _builder.CreateExtractValue(checkedValue, 0);
}

llvm::CallBase* Llvm::emitHelperCall(CorInfoHelpFunc                  helperFunc,
                                     ArrayRef<Value*>                 sigArgs,
                                     ArrayRef<llvm::OperandBundleDef> opBundles,
                                     bool                             doTailCall)
{
    assert(!helperCallRequiresShadowStackSave(helperFunc));

    void* handle = getSymbolHandleForHelperFunc(helperFunc);
    const char* symbolName = GetMangledSymbolName(handle);
    AddCodeReloc(handle);

    Function* helperLlvmFunc = getOrCreateKnownLlvmFunction(symbolName, [this, helperFunc]() {
        return createFunctionTypeForHelper(helperFunc);
    }, [this, helperFunc](Function* llvmFunc) {
        annotateHelperFunction(helperFunc, llvmFunc);
    });

    llvm::CallBase* call;
    if (helperCallHasShadowStackArg(helperFunc))
    {
        std::vector<Value*> args = sigArgs.vec();
        args.insert(args.begin(), doTailCall ? getShadowStack() : getShadowStackForCallee());

        call = emitCallOrInvoke(helperLlvmFunc, args, opBundles);
    }
    else
    {
        call = emitCallOrInvoke(helperLlvmFunc, sigArgs, opBundles);
    }

    return call;
}

llvm::CallBase* Llvm::emitCallOrInvoke(
    llvm::FunctionCallee callee, ArrayRef<Value*> args, ArrayRef<llvm::OperandBundleDef> bundles)
{
    Function* llvmFunc = llvm::dyn_cast<Function>(callee.getCallee());
    bool isThrowingCall = (llvmFunc == nullptr) || !llvmFunc->doesNotThrow();
    llvm::BasicBlock* catchLlvmBlock = isThrowingCall ? getUnwindLlvmBlockForCurrentInvoke() : nullptr;

    llvm::CallBase* callInst;
    if ((catchLlvmBlock != nullptr) && (m_ehModel != CorInfoLlvmEHModel::Emulated))
    {
        llvm::BasicBlock* nextLlvmBlock = createInlineLlvmBlock();
        callInst = _builder.CreateInvoke(callee, nextLlvmBlock, catchLlvmBlock, args, bundles);
        _builder.SetInsertPoint(nextLlvmBlock);
    }
    else
    {
        callInst = _builder.CreateCall(callee, args, bundles);
    }

    if (isThrowingCall && (m_ehModel == CorInfoLlvmEHModel::Emulated))
    {
        // In the emulated EH model, top-level calls also need to return early if they throw.
        if (catchLlvmBlock == nullptr)
        {
            catchLlvmBlock = getOrCreateExceptionThrownReturnBlock();
        }

        llvm::BasicBlock* nextLlvmBlock = createInlineLlvmBlock();
        Value* doUnwindValueAddr = getOrCreateExceptionThrownAddressValue();
        Value* doUnwindValue = _builder.CreateLoad(Type::getInt32Ty(m_context->Context), doUnwindValueAddr);
        Value* doUnwindValueRelop = _builder.CreateICmpNE(doUnwindValue, _builder.getInt32(0));
        _builder.CreateCondBr(doUnwindValueRelop, catchLlvmBlock, nextLlvmBlock);
        _builder.SetInsertPoint(nextLlvmBlock);
    }

    return callInst;
}

llvm::BasicBlock* Llvm::getOrCreateExceptionThrownReturnBlock()
{
    assert(m_ehModel == CorInfoLlvmEHModel::Emulated);

    FunctionInfo& funcInfo = getLlvmFunctionInfoForIndex(getCurrentLlvmFunctionIndex());
    if (funcInfo.ExceptionThrownReturnLlvmBlock == nullptr)
    {
        llvm::BasicBlock* block = llvm::BasicBlock::Create(m_context->Context, "BBRE", funcInfo.LlvmFunction);
        Type* llvmRetType = funcInfo.LlvmFunction->getReturnType();
        if (!llvmRetType->isVoidTy())
        {
            Value* zeroValue = llvm::Constant::getNullValue(llvmRetType);
            llvm::ReturnInst::Create(m_context->Context, zeroValue, block);
        }
        else
        {
            llvm::ReturnInst::Create(m_context->Context, block);
        }
        funcInfo.ExceptionThrownReturnLlvmBlock = block;
    }

    return funcInfo.ExceptionThrownReturnLlvmBlock;
}

FunctionType* Llvm::createFunctionType()
{
    std::vector<Type*> argVec(_llvmArgCount);
    for (unsigned i = 0; i < _compiler->lvaCount; i++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(i);
        if (varDsc->lvIsParam)
        {
            assert(varDsc->lvLlvmArgNum != BAD_LLVM_ARG_NUM);
            argVec[varDsc->lvLlvmArgNum] = getLlvmTypeForLclVar(varDsc);
        }
    }

    CORINFO_SIG_INFO* sig = &m_info->compMethodInfo->args;
    CorInfoType retType = getLlvmReturnType(sig->retType, sig->retTypeClass);
    Type* retLlvmType = getLlvmTypeForCorInfoType(retType, sig->retTypeClass);

    return FunctionType::get(retLlvmType, argVec, /* isVarArg */ false);
}

llvm::FunctionCallee Llvm::consumeCallTarget(GenTreeCall* call)
{
    llvm::FunctionCallee callee;
    if (call->IsVirtualVtable() || call->IsDelegateInvoke() || (call->gtCallType == CT_INDIRECT))
    {
        FunctionType* calleeFuncType = createFunctionTypeForCall(call);
        GenTree* calleeNode = (call->gtCallType == CT_INDIRECT) ? call->gtCallAddr : call->gtControlExpr;
        Value* calleeValue = consumeValue(calleeNode, getPtrLlvmType());

        callee = {calleeFuncType, calleeValue};
    }
    else
    {
        CORINFO_GENERIC_HANDLE handle = call->gtEntryPoint.handle;
        CorInfoHelpFunc helperFunc = _compiler->eeGetHelperNum(call->gtCallMethHnd);
        if (handle == nullptr)
        {
            handle = getSymbolHandleForHelperFunc(helperFunc);
        }
        else
        {
            assert(call->gtEntryPoint.accessType == IAT_VALUE);
        }

        const char* symbolName = GetMangledSymbolName(handle);
        AddCodeReloc(handle); // Replacement for _info.compCompHnd->recordRelocation.

        if (call->IsUnmanaged()) // External functions.
        {
            FunctionType* callFuncType = createFunctionTypeForCall(call);
            Function* calleeAccessorFunc = getOrCreateExternalLlvmFunctionAccessor(symbolName);
            Value* calleeValue = _builder.CreateCall(calleeAccessorFunc);

            callee = {callFuncType, calleeValue};
        }
        else // Known functions.
        {
            callee = getOrCreateKnownLlvmFunction(symbolName, [this, call]() -> FunctionType* {
                return createFunctionTypeForCall(call);
            }, [this, helperFunc](Function* llvmFunc) {
                if (helperFunc != CORINFO_HELP_UNDEF)
                {
                    annotateHelperFunction(helperFunc, llvmFunc);
                }
            });
        }
    }

    return callee;
}

FunctionType* Llvm::createFunctionTypeForSignature(CORINFO_SIG_INFO* pSig)
{
    assert(!pSig->isVarArg()); // We do not support varargs.
    bool isManagedCallConv = pSig->getCallConv() == CORINFO_CALLCONV_DEFAULT;

    bool isReturnByRef;
    CorInfoType retType = getLlvmReturnType(pSig->retType, pSig->retTypeClass, &isReturnByRef);
    Type* retLlvmType = getLlvmTypeForCorInfoType(retType, pSig->retTypeClass);

    std::vector<Type*> llvmParamTypes{};
    if (isManagedCallConv)
    {
        llvmParamTypes.push_back(getPtrLlvmType()); // The shadow stack.
    }

    if (pSig->hasImplicitThis())
    {
        llvmParamTypes.push_back(getPtrLlvmType());
    }

    if (isReturnByRef)
    {
        llvmParamTypes.push_back(getPtrLlvmType());
    }

    if (pSig->hasTypeArg())
    {
        llvmParamTypes.push_back(getPtrLlvmType());
    }

    CORINFO_ARG_LIST_HANDLE sigArgs = pSig->args;
    for (unsigned i = 0; i < pSig->numArgs; i++, sigArgs = m_info->compCompHnd->getArgNext(sigArgs))
    {
        CORINFO_CLASS_HANDLE argSigClass;
        CorInfoType argSigType = strip(m_info->compCompHnd->getArgType(pSig, sigArgs, &argSigClass));
        CorInfoType argType = getLlvmArgTypeForArg(argSigType, argSigClass);

        llvmParamTypes.push_back(getLlvmTypeForCorInfoType(argType, argSigClass));
    }

    return FunctionType::get(retLlvmType, llvmParamTypes, /* isVarArg */ false);
}

FunctionType* Llvm::createFunctionTypeForCall(GenTreeCall* call)
{
    llvm::Type* retLlvmType = getLlvmTypeForCorInfoType(call->gtCorInfoType, call->gtRetClsHnd);

    std::vector<llvm::Type*> argVec = std::vector<llvm::Type*>();
    for (CallArg& arg : call->gtArgs.Args())
    {
        argVec.push_back(getLlvmTypeForCorInfoType(getLlvmArgTypeForCallArg(&arg), arg.GetSignatureClassHandle()));
    }

    return FunctionType::get(retLlvmType, argVec, /* isVarArg */ false);
}

FunctionType* Llvm::createFunctionTypeForHelper(CorInfoHelpFunc helperFunc)
{
    const bool isManagedHelper = helperCallHasManagedCallingConvention(helperFunc);
    const HelperFuncInfo& helperInfo = getHelperFuncInfo(helperFunc);
    std::vector<Type*> argVec = std::vector<Type*>();

    if (helperCallHasShadowStackArg(helperFunc))
    {
        argVec.push_back(getPtrLlvmType());
    }

    size_t sigArgCount = helperInfo.GetSigArgCount();
    for (size_t i = 0; i < sigArgCount; i++)
    {
        CorInfoType argSigType = helperInfo.GetSigArgType(i);
        CORINFO_CLASS_HANDLE argSigClass = helperInfo.GetSigArgClass(_compiler, i);

        bool isArgPassedByRef;
        CorInfoType argType = getLlvmArgTypeForArg(argSigType, argSigClass, &isArgPassedByRef);
        assert(!isArgPassedByRef);

        argVec.push_back(getLlvmTypeForCorInfoType(argType, argSigClass));
    }

    bool isReturnByRef;
    CORINFO_CLASS_HANDLE sigRetClass = helperInfo.GetSigReturnClass(_compiler);
    CorInfoType retType = getLlvmReturnType(helperInfo.GetSigReturnType(), sigRetClass, &isReturnByRef);
    assert(!isReturnByRef);

    Type* retLlvmType = getLlvmTypeForCorInfoType(retType, sigRetClass);
    FunctionType* llvmFuncType = FunctionType::get(retLlvmType, argVec, /* isVarArg */ false);

    return llvmFuncType;
}

void Llvm::annotateHelperFunction(CorInfoHelpFunc helperFunc, Function* llvmFunc)
{
    if (!llvmFunc->getReturnType()->isVoidTy())
    {
        // Assume helpers won't return uninitialized memory or the like.
        llvmFunc->addRetAttr(llvm::Attribute::NoUndef);
    }

    HelperCallProperties& properties = Compiler::s_helperCallProperties;
    const bool isEmulatedEH = m_ehModel == CorInfoLlvmEHModel::Emulated;
    const bool mayThrow = helperCallMayPhysicallyThrow(helperFunc);

    if (!mayThrow)
    {
        llvmFunc->setDoesNotThrow();
    }
    if (properties.AlwaysThrow(helperFunc) && !isEmulatedEH)
    {
        llvmFunc->setDoesNotReturn();
    }
    if (properties.NonNullReturn(helperFunc) && llvmFunc->getReturnType()->isPointerTy())
    {
        // In the emulated EH model, "exceptional" returns may return zero.
        if (!isEmulatedEH || !mayThrow)
        {
            llvmFunc->addRetAttr(llvm::Attribute::NonNull);
        }
    }
    if (properties.IsAllocator(helperFunc))
    {
        llvmFunc->addRetAttr(llvm::Attribute::NoAlias);
    }
}

Function* Llvm::getOrCreateKnownLlvmFunction(
    StringRef name, std::function<FunctionType*()> createFunctionType, std::function<void(Function*)> annotateFunction)
{
    Function* llvmFunc = m_context->Module.getFunction(name);
    if (llvmFunc == nullptr)
    {
        assert(m_context->Module.getNamedValue(name) == nullptr); // No duplicate symbols!
        llvmFunc = Function::Create(createFunctionType(), Function::ExternalLinkage, name, m_context->Module);
        annotateFunction(llvmFunc);
    }

    return llvmFunc;
}

Function* Llvm::getOrCreateExternalLlvmFunctionAccessor(StringRef name)
{
    Function* accessorFuncRef = m_context->Module.getFunction(name);
    if (accessorFuncRef == nullptr)
    {
        FunctionType* accessorFuncType = FunctionType::get(getPtrLlvmType(), /* isVarArg */ false);
        accessorFuncRef = Function::Create(accessorFuncType, Function::ExternalLinkage, name, m_context->Module);
    }

    return accessorFuncRef;
}

Function* Llvm::getOrCreatePersonalityLlvmFunction(CorInfoLlvmEHModel ehModel)
{
    switch (ehModel)
    {
        case CorInfoLlvmEHModel::Cpp:
            return getOrCreateKnownLlvmFunction("__gxx_personality_v0", [this]() {
                Type* ptrLlvmType = getPtrLlvmType();
                Type* int32LlvmType = Type::getInt32Ty(m_context->Context);
                Type* cppExcTupleLlvmType = llvm::StructType::get(ptrLlvmType, int32LlvmType);
                return FunctionType::get(cppExcTupleLlvmType, {int32LlvmType, ptrLlvmType, ptrLlvmType}, /* isVarArg */ true);
            });
            break;
        case CorInfoLlvmEHModel::Wasm:
            return getOrCreateKnownLlvmFunction("__gxx_wasm_personality_v0", [this]() {
                return FunctionType::get(Type::getInt32Ty(m_context->Context), /* isVarArg */ true);
            });
        case CorInfoLlvmEHModel::Emulated:
            return nullptr;
        default:
            unreached();
    }
}

Value* Llvm::getOrCreateExceptionThrownAddressValue()
{
    assert(m_ehModel == CorInfoLlvmEHModel::Emulated);
    if (m_exceptionThrownAddressValue == nullptr)
    {
        m_exceptionThrownAddressValue = getOrCreateSymbol(GetExceptionThrownVariable(), /* isThreadLocal */ true);
    }

    return m_exceptionThrownAddressValue;
}

llvm::GlobalVariable* Llvm::getOrCreateDataSymbol(StringRef symbolName, bool isThreadLocal)
{
    llvm::GlobalVariable* symbol = m_context->Module.getGlobalVariable(symbolName);
    if (symbol == nullptr)
    {
        assert(m_context->Module.getNamedValue(symbolName) == nullptr); // No duplicate symbols!
        Type* symbolLlvmType = getPtrLlvmType();
        symbol = new llvm::GlobalVariable(m_context->Module, symbolLlvmType, false, llvm::GlobalValue::ExternalLinkage,
                                          nullptr, symbolName);
        symbol->setThreadLocal(isThreadLocal);
    }
    return symbol;
}

llvm::GlobalValue* Llvm::getOrCreateSymbol(CORINFO_GENERIC_HANDLE symbolHandle, bool isThreadLocal)
{
    StringRef symbolName = GetMangledSymbolName(symbolHandle);
    AddCodeReloc(symbolHandle);

    CORINFO_SIG_INFO sig;
    llvm::GlobalValue* symbol;
    if (GetSignatureForMethodSymbol(symbolHandle, &sig)) // Is this a data symbol or a function symbol?
    {
        symbol = getOrCreateKnownLlvmFunction(symbolName, [this, &sig]() {
            return createFunctionTypeForSignature(&sig);
        });
    }
    else
    {
        symbol = getOrCreateDataSymbol(symbolName, isThreadLocal);
    }

    return symbol;
}

llvm::Constant* Llvm::getIntPtrConst(target_size_t value, Type* llvmType)
{
    if (llvmType == nullptr)
    {
        llvmType = getIntPtrLlvmType();
    }
    return llvm::Constant::getIntegerValue(llvmType, llvm::APInt(TARGET_POINTER_BITS, value));
}

// We assume that all the GEPs are for elements of size Int8 (byte)
Value* Llvm::gepOrAddr(Value* addr, unsigned offset)
{
    if (offset == 0)
    {
        return addr;
    }

    return _builder.CreateGEP(Type::getInt8Ty(m_context->Context), addr, _builder.getInt32(offset));
}

Value* Llvm::gepOrAddrInBounds(Value* addr, unsigned offset)
{
    if (offset == 0)
    {
        return addr;
    }

    return _builder.CreateInBoundsGEP(Type::getInt8Ty(m_context->Context), addr, _builder.getInt32(offset));
}

Value* Llvm::getShadowStack()
{
    if (getCurrentLlvmFunctionIndex() == ROOT_FUNC_IDX)
    {
        assert(m_rootFunctionShadowStackValue != nullptr);
        return m_rootFunctionShadowStackValue;
    }

    // Note that funclets have the shadow stack arg in the 0th position.
    return getCurrentLlvmFunction()->getArg(0);
}

// Shadow stack moved up to avoid overwriting anything on the stack in the compiling method
Value* Llvm::getShadowStackForCallee()
{
    unsigned shadowFrameSize = getShadowFrameSize(getCurrentLlvmFunctionIndex());
    return gepOrAddrInBounds(getShadowStack(), shadowFrameSize);
}

Value* Llvm::getOriginalShadowStack()
{
    if (_compiler->funGetFunc(getCurrentLlvmFunctionIndex())->funKind == FUNC_FILTER)
    {
        // The original shadow stack pointer is the second filter parameter.
        return getCurrentLlvmFunction()->getArg(1);
    }

    return getShadowStack();
}

void Llvm::setCurrentEmitContextForBlock(BasicBlock* block)
{
    unsigned funcIdx = getLlvmFunctionIndexForBlock(block);
    unsigned tryIndex = block->hasTryIndex() ? block->getTryIndex() : EHblkDsc::NO_ENCLOSING_INDEX;
    LlvmBlockRange* llvmBlocks = getLlvmBlocksForBlock(block);

    setCurrentEmitContext(funcIdx, tryIndex, llvmBlocks);
    m_currentBlock = block;
}

void Llvm::setCurrentEmitContext(unsigned funcIdx, unsigned tryIndex, LlvmBlockRange* llvmBlocks)
{
    assert(getLlvmFunctionForIndex(funcIdx) == llvmBlocks->LastBlock->getParent());

    llvm::BasicBlock* insertLlvmBlock = llvmBlocks->LastBlock;
    if (insertLlvmBlock->getTerminator() != nullptr)
    {
        _builder.SetInsertPoint(insertLlvmBlock->getTerminator());
    }
    else
    {
        _builder.SetInsertPoint(insertLlvmBlock);
    }
    m_currentLlvmFunctionIndex = funcIdx;
    m_currentProtectedRegionIndex = tryIndex;
    m_currentLlvmBlocks = llvmBlocks;

    // "Raw" emission contexts do not have a current IR block.
    m_currentBlock = nullptr;
}

unsigned Llvm::getCurrentLlvmFunctionIndex() const
{
    return m_currentLlvmFunctionIndex;
}

//------------------------------------------------------------------------
// getCurrentProtectedRegionIndex: Get the current protected region's index.
//
// Return Value:
//    Index of the EH descriptor for the (innermost) protected region ("try")
//    enclosing code in the current emit context.
//
unsigned Llvm::getCurrentProtectedRegionIndex() const
{
    return m_currentProtectedRegionIndex;
}

LlvmBlockRange* Llvm::getCurrentLlvmBlocks() const
{
    assert(m_currentLlvmBlocks != nullptr);
    return m_currentLlvmBlocks;
}

Function* Llvm::getRootLlvmFunction()
{
    return getLlvmFunctionForIndex(ROOT_FUNC_IDX);
}

Function* Llvm::getCurrentLlvmFunction()
{
    return getLlvmFunctionForIndex(getCurrentLlvmFunctionIndex());
}

Function* Llvm::getLlvmFunctionForIndex(unsigned funcIdx)
{
    return getLlvmFunctionInfoForIndex(funcIdx).LlvmFunction;
}

FunctionInfo& Llvm::getLlvmFunctionInfoForIndex(unsigned funcIdx)
{
    FunctionInfo& funcInfo = m_functions[funcIdx];
    assert(funcInfo.LlvmFunction != nullptr);

    return funcInfo;
}

unsigned Llvm::getLlvmFunctionIndexForBlock(BasicBlock* block) const
{
    unsigned funcIdx = ROOT_FUNC_IDX;

    // We cannot just use "funGetFuncIdx" here because it only handles the first blocks for funclets.
    if (block->hasHndIndex())
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(block->getHndIndex());
        funcIdx = ehDsc->ebdFuncIndex;

        if (isBlockInFilter(block))
        {
            funcIdx--;
            assert(_compiler->funGetFunc(funcIdx)->funKind == FUNC_FILTER);
        }
    }

    return funcIdx;
}

unsigned Llvm::getLlvmFunctionIndexForProtectedRegion(unsigned tryIndex) const
{
    unsigned funcIdx = ROOT_FUNC_IDX;
    if (tryIndex != EHblkDsc::NO_ENCLOSING_INDEX)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(tryIndex);
        if (ehDsc->ebdEnclosingHndIndex != EHblkDsc::NO_ENCLOSING_INDEX)
        {
            // Note here we will correctly get the "filter handler" part of filter.
            // There can be no protected regions in the "filter" parts of filters.
            funcIdx = _compiler->ehGetDsc(ehDsc->ebdEnclosingHndIndex)->ebdFuncIndex;
        }
    }

    return funcIdx;
}

llvm::BasicBlock* Llvm::createInlineLlvmBlock()
{
    Function* llvmFunc = getCurrentLlvmFunction();
    LlvmBlockRange* llvmBlocks = getCurrentLlvmBlocks();
    llvm::BasicBlock* insertBefore = llvmBlocks->LastBlock->getNextNode();
    llvm::BasicBlock* inlineLlvmBlock = llvm::BasicBlock::Create(m_context->Context, "", llvmFunc, insertBefore);

#ifdef DEBUG
    StringRef blocksName = llvmBlocks->FirstBlock->getName();
    if (llvmBlocks->Count == 1)
    {
        llvmBlocks->FirstBlock->setName(blocksName + ".1");
    }
    else
    {
        blocksName = blocksName.take_front(blocksName.find_last_of('.'));
    }

    llvmBlocks->Count++;
    inlineLlvmBlock->setName(blocksName + "." + Twine(llvmBlocks->Count));
#endif // DEBUG

    llvmBlocks->LastBlock = inlineLlvmBlock;
    return inlineLlvmBlock;
}

LlvmBlockRange* Llvm::getLlvmBlocksForBlock(BasicBlock* block)
{
    // We should never be asking for unreachable blocks here since we won't generate code for them.
    assert(isReachable(block) || (block == _compiler->fgFirstBB) || _compiler->fgIsThrowHlpBlk(block));

    LlvmBlockRange* llvmBlockRange = _blkToLlvmBlksMap.LookupPointer(block);
    if (llvmBlockRange == nullptr)
    {
        Function* llvmFunc = getLlvmFunctionForIndex(getLlvmFunctionIndexForBlock(block));
        llvm::BasicBlock* llvmBlock =
            llvm::BasicBlock::Create(m_context->Context, BBNAME("BB", block->bbNum), llvmFunc);

        llvmBlockRange = _blkToLlvmBlksMap.Emplace(block, llvmBlock);
    }

    return llvmBlockRange;
}

llvm::BasicBlock* Llvm::getFirstLlvmBlockForBlock(BasicBlock* block)
{
    return getLlvmBlocksForBlock(block)->FirstBlock;
}

//------------------------------------------------------------------------
// getLastLlvmBlockForBlock: Get the last LLVM basic block for "block".
//
// During code generation, a given IR block can be split into multiple
// LLVM blocks, due to, e. g., inline branches. This function returns
// the last of these generated blocks.
//
// Arguments:
//    block - The IR block
//
// Return Value:
//    LLVM block containing "block"'s terminator instruction.
//
llvm::BasicBlock* Llvm::getLastLlvmBlockForBlock(BasicBlock* block)
{
    return getLlvmBlocksForBlock(block)->LastBlock;
}

llvm::BasicBlock* Llvm::getOrCreatePrologLlvmBlockForFunction(unsigned funcIdx)
{
    const char* const PROLOG_BLOCK_NAME = "BB00";

    BasicBlock* firstUserBlock = getFirstBlockForFunction(funcIdx);
    llvm::BasicBlock* firstLlvmUserBlock = getFirstLlvmBlockForBlock(firstUserBlock);
    llvm::BasicBlock* prologLlvmBlock = firstLlvmUserBlock->getPrevNode();
    if ((prologLlvmBlock == nullptr) || !prologLlvmBlock->getName().startswith(PROLOG_BLOCK_NAME))
    {
        Function* llvmFunc = firstLlvmUserBlock->getParent();
        prologLlvmBlock = llvm::BasicBlock::Create(m_context->Context, PROLOG_BLOCK_NAME, llvmFunc, firstLlvmUserBlock);

        // Eagerly insert jump to the user block to simplify calling code.
        llvm::BranchInst::Create(firstLlvmUserBlock, prologLlvmBlock);
    }

    return prologLlvmBlock;
}

llvm::BasicBlock* Llvm::getUnwindLlvmBlockForCurrentInvoke()
{
    llvm::BasicBlock* catchLlvmBlock = nullptr;
    unsigned tryIndex = getCurrentProtectedRegionIndex();
    if (tryIndex != EHblkDsc::NO_ENCLOSING_INDEX)
    {
        // Due to unreachable code, we may not have unwind blocks for the innermost region.
        do
        {
            catchLlvmBlock = m_EHUnwindLlvmBlocks[tryIndex];
            tryIndex = _compiler->ehGetEnclosingTryIndex(tryIndex);
        }
        while ((catchLlvmBlock == nullptr) && (tryIndex != EHblkDsc::NO_ENCLOSING_INDEX));

        // Protected region index that is set in the emit context refers to the "logical" enclosing
        // protected region, i. e. the one before funclet creation. But we do not need to (in fact,
        // cannot) emit an invoke targeting block inside a different LLVM function.
        if ((catchLlvmBlock != nullptr) && (catchLlvmBlock->getParent() != getCurrentLlvmFunction()))
        {
            catchLlvmBlock = nullptr;
        }
    }

    return catchLlvmBlock;
}

//------------------------------------------------------------------------
// isReachable: Does this block have an immediate dominator?
//
// Arguments:
//    block - The block to check
//
// Return Value:
//    Whether "block" has an immediate dominator, i. e. is statically
//    reachable, not the first block, and not a throw helper block. If
//    we do not have dominators built, all blocks are assumed reachable.
//
bool Llvm::isReachable(BasicBlock* block) const
{
    return (_compiler->fgSsaDomTree != nullptr) ? (block->bbIDom != nullptr) : true;
}

BasicBlock* Llvm::getFirstBlockForFunction(unsigned funcIdx) const
{
    if (funcIdx == ROOT_FUNC_IDX)
    {
        return _compiler->fgFirstBB;
    }

    FuncInfoDsc* funcInfo = _compiler->funGetFunc(funcIdx);
    EHblkDsc* ehDsc = _compiler->ehGetDsc(funcInfo->funEHIndex);
    return (funcInfo->funKind == FUNC_FILTER) ? ehDsc->ebdFilter : ehDsc->ebdHndBeg;
}

Value* Llvm::getLocalAddr(unsigned lclNum)
{
    Value* addrValue;
    if (getCurrentLlvmFunctionIndex() == ROOT_FUNC_IDX)
    {
        addrValue = getLlvmFunctionInfoForIndex(ROOT_FUNC_IDX).Allocas[lclNum];
    }
    else
    {
        addrValue = getOrCreateAllocaForLocalInFunclet(lclNum);
    }

    assert(addrValue != nullptr);
    return addrValue;
}

//------------------------------------------------------------------------
// getOrCreateAllocaForLocalInFunclet: Get an address for a funclet local.
//
// For a local to be (locally) live on the LLVM frame in a funclet, it has
// to be tracked and have its address taken (but not exposed!), or be one
// of locals lowering adds after shadow frame layout. Such locals are rare,
// and it is not cheap to indentify their set precisely before the code has
// been generated. We therefore materialize them in funclet prologs lazily.
//
// Arguments:
//    lclNum - The local for which to get the allocated home
//
// Return Value:
//    Address on the LLVM frame "lclNum" has in the current funclet.
//
Value* Llvm::getOrCreateAllocaForLocalInFunclet(unsigned lclNum)
{
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    unsigned funcIdx = getCurrentLlvmFunctionIndex();

    // Untracked locals in functions with funclets live on the shadow frame, except if they're temporaries
    // created by lowering, known to only be live inside the funclet.
    assert(varDsc->lvTracked || varDsc->lvIsTemp);
    assert(!varDsc->lvTracked ||
           !VarSetOps::IsMember(_compiler, getFirstBlockForFunction(funcIdx)->bbLiveIn, varDsc->lvVarIndex));
    assert(funcIdx != ROOT_FUNC_IDX); // The root's prolog is generated eagerly.

    FunctionInfo& funcInfo = getLlvmFunctionInfoForIndex(funcIdx);
    AllocaMap* allocaMap = funcInfo.AllocaMap;
    if (allocaMap == nullptr)
    {
        allocaMap = new (_compiler->getAllocator(CMK_Codegen)) AllocaMap(_compiler->getAllocator(CMK_Codegen));
        funcInfo.AllocaMap = allocaMap;
    }

    llvm::AllocaInst* allocaInst;
    if (!allocaMap->Lookup(lclNum, &allocaInst))
    {
        llvm::BasicBlock* prologLlvmBlock = getOrCreatePrologLlvmBlockForFunction(funcIdx);
        allocaInst = new llvm::AllocaInst(getLlvmTypeForLclVar(varDsc), 0, "", prologLlvmBlock->getTerminator());

        allocaMap->Set(lclNum, allocaInst);
    }

    return allocaInst;
}

bool Llvm::IsLlvmIntrinsic(NamedIntrinsic intrinsicName) const
{
    return getLlvmIntrinsic(intrinsicName) != llvm::Intrinsic::not_intrinsic;
}

llvm::Intrinsic::ID Llvm::getLlvmIntrinsic(NamedIntrinsic intrinsicName) const
{
    switch (intrinsicName)
    {
        case NI_System_Math_Abs:
            return llvm::Intrinsic::fabs;
        case NI_System_Math_Ceiling:
            return llvm::Intrinsic::ceil;
        case NI_System_Math_Cos:
            return llvm::Intrinsic::cos;
        case NI_System_Math_Exp:
            return llvm::Intrinsic::exp;
        case NI_System_Math_Floor:
            return llvm::Intrinsic::floor;
        case NI_System_Math_Log:
            return llvm::Intrinsic::log;
        case NI_System_Math_Log2:
            return llvm::Intrinsic::log2;
        case NI_System_Math_Log10:
            return llvm::Intrinsic::log10;
        case NI_System_Math_Max:
            return llvm::Intrinsic::maximum;
        case NI_System_Math_Min:
            return llvm::Intrinsic::minimum;
        case NI_System_Math_Pow:
            return llvm::Intrinsic::pow;
        case NI_System_Math_Round:
            return llvm::Intrinsic::round;
        case NI_System_Math_Sin:
            return llvm::Intrinsic::sin;
        case NI_System_Math_Sqrt:
            return llvm::Intrinsic::sqrt;
        case NI_System_Math_Truncate:
            return llvm::Intrinsic::trunc;
        default:
            return llvm::Intrinsic::not_intrinsic;
    }
}

void Llvm::displayValue(Value* value)
{
    // TODO-LLVM: support JitStdOutFile here.
    value->print(llvm::outs());
    printf("\n");
}
