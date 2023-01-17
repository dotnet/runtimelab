// ================================================================================================================
// |                                            LLVM-based codegen                                                |
// ================================================================================================================

#include "llvm.h"

#define BBNAME(prefix, index) llvm::Twine(prefix) + ((index < 10) ? "0" : "") + llvm::Twine(index)

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
void Llvm::Compile()
{
    // TODO-LLVM: enable. Currently broken because RyuJit inserts RPI helpers for RPI methods, then we
    // also create an RPI wrapper stub, resulting in a double transition.
    if (_compiler->opts.IsReversePInvoke())
    {
        failFunctionCompilation();
    }

    if ((_info.compFlags & CORINFO_FLG_SYNCH) != 0)
    {
        // TODO-LLVM: enable.
        failFunctionCompilation();
    }

    if (initializeFunctions())
    {
        return;
    }

    initializeDebugInfo();

    JITDUMPEXEC(_compiler->fgDispBasicBlocks());
    JITDUMPEXEC(_compiler->fgDispHandlerTab());

    generateProlog();

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

    // Walk all the exceptional code blocks and generate them, since they don't appear in the normal flow graph.
    for (Compiler::AddCodeDsc* add = _compiler->fgGetAdditionalCodeDescriptors(); add != nullptr; add = add->acdNext)
    {
        generateBlock(add->acdDstBlk);
    }

    generateEHDispatch();

    fillPhis();

    if (m_diFunction != nullptr)
    {
        m_diBuilder->finalize();
    }

#if DEBUG
    JITDUMP("\n===================================================================================================================\n");
    JITDUMP("LLVM IR for %s after codegen:\n", _compiler->info.compFullName);
    JITDUMP("-------------------------------------------------------------------------------------------------------------------\n\n");

    for (FunctionInfo& funcInfo : m_functions)
    {
        Function* llvmFunc = funcInfo.LlvmFunction;
        if (llvmFunc != nullptr)
        {
            JITDUMPEXEC(llvmFunc->dump());
            assert(!llvm::verifyFunction(*llvmFunc, &llvm::errs()));
        }
    }
#endif
}

bool Llvm::initializeFunctions()
{
    const char* mangledName = GetMangledMethodName(_info.compMethodHnd);
    Function* rootLlvmFunction = _module->getFunction(mangledName);

    if (rootLlvmFunction == nullptr)
    {
        // TODO: ExternalLinkage forced as linked from old module
        rootLlvmFunction = Function::Create(getFunctionType(), Function::ExternalLinkage, 0U, mangledName, _module);
    }

    // TODO-LLVM: investigate.
    if (!strcmp(mangledName, "S_P_CoreLib_System_Globalization_CalendarData__EnumCalendarInfo"))
    {
        llvm::BasicBlock* llvmBlock = llvm::BasicBlock::Create(_llvmContext, "", rootLlvmFunction);
        _builder.SetInsertPoint(llvmBlock);
        _builder.CreateRet(_builder.getInt8(0));
        return true;
    }

    // First functions is always the root.
    m_functions = std::vector<FunctionInfo>(_compiler->compFuncCount());
    m_functions[ROOT_FUNC_IDX] = {rootLlvmFunction};

    m_EHDispatchLlvmBlocks = std::vector<llvm::BasicBlock*>(_compiler->compHndBBtabCount);

    // Note the iteration order: outer -> inner.
    for (unsigned funcIdx = _compiler->compFuncCount() - 1; funcIdx >= 1; funcIdx--)
    {
        FuncInfoDsc* funcInfo = _compiler->funGetFunc(funcIdx);
        unsigned ehIndex = funcInfo->funEHIndex;
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);

        // We won't generate code for unreachable handlers so we will not create functions for them.
        //
        if (isReachable(getFirstBlockForFunction(funcIdx)))
        {
            // Filter and catch handler funclets return int32. "HasCatchHandler" handles both cases.
            Type* retLlvmType =
                ehDsc->HasCatchHandler() ? Type::getInt32Ty(_llvmContext) : Type::getVoidTy(_llvmContext);

            // All funclets have two arguments: original and actual shadow stacks.
            Type* ptrLlvmType = getPtrLlvmType();
            FunctionType* llvmFuncType =
                FunctionType::get(retLlvmType, {ptrLlvmType, ptrLlvmType}, /* isVarArg */ false);

            const char* kindName;
            switch (ehDsc->ebdHandlerType)
            {
                case EH_HANDLER_CATCH:
                    kindName = "Catch";
                    break;
                case EH_HANDLER_FILTER:
                    kindName = (funcInfo->funKind == FUNC_FILTER) ? "Filter" : "FilteredCatch";
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

            Function* llvmFunc =
                Function::Create(llvmFuncType, Function::InternalLinkage,
                                 mangledName + llvm::Twine("$F") + llvm::Twine(funcIdx) + "_" + kindName, _module);

            m_functions[funcIdx] = {llvmFunc};
        }

        // Note that "mutually-protect" handlers will share the same dispatch block. We only need to associate
        // one dispatch block with one protected region, and so simply skip the logic for filter funclets. We
        // also leave blocks for unreachable dispatches null.
        if ((funcInfo->funKind == FUNC_HANDLER) && isReachable(ehDsc->ExFlowBlock()))
        {
            llvm::BasicBlock* dispatchLlvmBlock = nullptr;

            // See if we have already created the dispatch block for a mutually-protect catch. This works because these
            // handlers form a contiguous "run" in the table.
            unsigned nextEhIndex = ehIndex + 1;
            if ((nextEhIndex < _compiler->compHndBBtabCount) && ehDsc->ebdIsSameTry(_compiler, nextEhIndex))
            {
                assert(_compiler->ehGetDsc(nextEhIndex)->HasCatchHandler());
                dispatchLlvmBlock = m_EHDispatchLlvmBlocks[nextEhIndex];
                assert(dispatchLlvmBlock != nullptr);
            }
            else
            {
                // The dispatch block is part of the function with the protected region.
                unsigned enclosingFuncIdx = getLlvmFunctionIndexForProtectedRegion(ehIndex);
                Function* dispatchLlvmFunc = getLlvmFunctionForIndex(enclosingFuncIdx);
                dispatchLlvmBlock =
                    llvm::BasicBlock::Create(_llvmContext, BBNAME("BT", ehDsc->ebdTryBeg->getTryIndex()),
                                             dispatchLlvmFunc);
            }

            m_EHDispatchLlvmBlocks[ehIndex] = dispatchLlvmBlock;
        }
    }

    return false;
}

void Llvm::initializeDebugInfo()
{
    if (!_compiler->opts.compDbgInfo)
    {
        return;
    }

    const char* documentFileName = GetDocumentFileName();
    if (documentFileName == nullptr)
    {
        return;
    }

    // Check Unix and Windows path styles
    std::string fullPath = documentFileName;
    std::size_t botDirPos = fullPath.find_last_of("/");
    if (botDirPos == std::string::npos)
    {
        botDirPos = fullPath.find_last_of("\\");
    }
    std::string directory = "";
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


    m_diBuilder = new (_compiler->getAllocator(CMK_DebugInfo)) llvm::DIBuilder(*_module);

    // TODO-LLVM: we are allocating a new CU for each compiled function, which is rather inefficient. We should instead
    // allocate one CU per file.
    llvm::DIFile* fileMetadata = m_diBuilder->createFile(fileName, directory);
    m_diBuilder->createCompileUnit(llvm::dwarf::DW_LANG_C /* no dotnet choices in the enum */, fileMetadata, "ILC",
                                   _compiler->opts.OptimizationEnabled(), "", 1, "", llvm::DICompileUnit::FullDebug,
                                   0, false);

    // TODO-LLVM: function parameter types.
    llvm::DISubroutineType* functionType = m_diBuilder->createSubroutineType({});
    uint32_t lineNumber = GetOffsetLineNumber(0);

    // TODO-LLVM: "getMethodName" is meant for (Jit) debugging. Find/add a more suitable API.
    const char* methodName = _info.compCompHnd->getMethodName(_info.compMethodHnd, nullptr);
    m_diFunction =
        m_diBuilder->createFunction(fileMetadata, methodName, methodName, fileMetadata, lineNumber, functionType,
                                    lineNumber, llvm::DINode::FlagZero,
                                    llvm::DISubprogram::SPFlagDefinition | llvm::DISubprogram::SPFlagLocalToUnit);

    // TODO-LLVM-EH: debugging in funclets.
    getRootLlvmFunction()->setSubprogram(m_diFunction);
}

void Llvm::generateProlog()
{
    JITDUMP("\n=============== Generating prolog:\n");

    llvm::BasicBlock* prologLlvmBlock = getOrCreatePrologLlvmBlockForFunction(ROOT_FUNC_IDX);
    _builder.SetInsertPoint(prologLlvmBlock->getTerminator());
    _builder.SetCurrentDebugLocation(llvm::DebugLoc()); // By convention, prologs have no debug info.

    initializeLocals();
}

void Llvm::initializeLocals()
{
    llvm::AllocaInst** allocas = new (_compiler->getAllocator(CMK_Codegen)) llvm::AllocaInst*[_compiler->lvaCount];

    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        // Using the "raw" ref count here helps us to not create useless allocas for implicitly references locals
        // that live on the shadow stack (especially in debug codegen).
        if (varDsc->lvRawRefCnt() == 0)
        {
            continue;
        }

        if (lclNum == _originalShadowStackLclNum)
        {
            // We model funclet parameters specially because it is not trivial to represent them in IR faithfully.
            continue;
        }

        // See "genCheckUseBlockInit", "fgInterBlockLocalVarLiveness" and "SsaBuilder::RenameVariables" as references
        // for the zero-init logic.
        //
        Type* lclLlvmType = getLlvmTypeForLclVar(varDsc);
        Value* initValue = nullptr;
        Value* zeroValue = llvm::Constant::getNullValue(lclLlvmType);
        if (varDsc->lvIsParam)
        {
            assert(varDsc->lvLlvmArgNum != BAD_LLVM_ARG_NUM);
            initValue = getRootLlvmFunction()->getArg(varDsc->lvLlvmArgNum);
        }
        else
        {
            // If the local is in SSA, things are somewhat simple: we must provide an initial value if there is an
            // "implicit" def, and must not if there is not.
            if (_compiler->lvaInSsa(lclNum))
            {
                // Filter out "implicitly" referenced local that the ref count check above didn't catch.
                if (varDsc->lvPerSsaData.GetCount() == 0)
                {
                    continue;
                }

                bool hasImplicitDef = varDsc->GetPerSsaData(SsaConfig::FIRST_SSA_NUM)->GetAssignment() == nullptr;
                if (!hasImplicitDef)
                {
                    // Nothing else needs to be done for this local.
                    assert(!varDsc->lvMustInit);
                    continue;
                }

                // SSA locals are always tracked; use liveness' determination on whether we need to zero-init.
                if (varDsc->lvMustInit)
                {
                    initValue = zeroValue;
                }
            }
            else if (!varDsc->lvHasExplicitInit) // We do not need to zero-init locals with explicit inits.
            {
                // This reduces to, essentially, "!isTemp && compInitMem", the general test for whether
                // we need to zero-initialize, under the assumption there are use-before-def references.
                if (!_compiler->fgVarNeedsExplicitZeroInit(lclNum, /* bbInALoop */ false, /* bbIsReturn */ false))
                {
                    // For untracked locals, we have to be conservative. For tracked ones, we can query the
                    // "lvMustInit" bit liveness has set.
                    if (!varDsc->lvTracked || varDsc->lvMustInit)
                    {
                        initValue = zeroValue;
                    }
                }
            }

            JITDUMP("Setting V%02u's initial value to %s\n", lclNum, (initValue == zeroValue) ? "zero" : "uninit");
        }

        // Reset the bit so that subsequent dumping reflects our decision here.
        varDsc->lvMustInit = initValue == zeroValue;

        // If we're not zero-initializing, use a frozen undef value. This will ensure we don't run
        // into UB issues with undefined values (which uninitialized allocas produce, see LangRef)
        if (initValue == nullptr)
        {
            initValue = llvm::UndefValue::get(lclLlvmType);
            initValue = _builder.CreateFreeze(initValue);
            JITDUMPEXEC(initValue->dump());
        }

        assert(initValue->getType() == lclLlvmType);

        if (_compiler->lvaInSsa(lclNum))
        {
            _localsMap.Set({lclNum, SsaConfig::FIRST_SSA_NUM}, initValue);
        }
        else
        {
            llvm::AllocaInst* allocaInst = _builder.CreateAlloca(lclLlvmType);
            allocas[lclNum] = allocaInst;
            JITDUMPEXEC(allocaInst->dump());

            Instruction* storeInst = _builder.CreateStore(initValue, allocaInst);
            JITDUMPEXEC(storeInst->dump());
        }
    }

    getLlvmFunctionInfoForIndex(ROOT_FUNC_IDX).Allocas = allocas;
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
    if (!_compiler->ehAnyFunclets())
    {
        // Nothing to do if no EH.
        return;
    }

    // Recover the C++ personality function.
    Type* ptrLlvmType = getPtrLlvmType();
    Type* int32LlvmType = Type::getInt32Ty(_llvmContext);
    Type* cppExcTupleLlvmType = llvm::StructType::get(ptrLlvmType, int32LlvmType);
    llvm::StructType* dispatchDataLlvmType = llvm::StructType::get(cppExcTupleLlvmType, ptrLlvmType);

    static const char* const GXX_PERSONALITY_NAME = "__gxx_personality_v0";
    Function* gxxPersonalityLlvmFunc = _module->getFunction(GXX_PERSONALITY_NAME);
    if (gxxPersonalityLlvmFunc == nullptr)
    {
        FunctionType* gxxPersonalityLlvmFuncType =
            FunctionType::get(cppExcTupleLlvmType, {int32LlvmType, ptrLlvmType, ptrLlvmType}, /* isVarArg */ true);
        gxxPersonalityLlvmFunc =
            Function::Create(gxxPersonalityLlvmFuncType, Function::ExternalLinkage, GXX_PERSONALITY_NAME, _module);
    }

    // Recover the runtime helper routines. TODO-LLVM: use proper "CorInfoHelpFunc"s for these.
    Function* dispatchLlvmFuncs[EH_HANDLER_COUNT]{};
    auto getDispatchLlvmFunc = [&](EHblkDsc* hndDsc) {
        Function** pDispatchLlvmFunc;
        if (hndDsc == nullptr)
        {
            static_assert_no_msg(EH_HANDLER_CATCH == 1); // We rely on zero being invalid.
            pDispatchLlvmFunc = &dispatchLlvmFuncs[0];
        }
        else
        {
            pDispatchLlvmFunc = &dispatchLlvmFuncs[hndDsc->ebdHandlerType];
        }

        Function* dispatchLlvmFunc = *pDispatchLlvmFunc;
        if (dispatchLlvmFunc == nullptr)
        {
            CORINFO_EH_CLAUSE_FLAGS eeHndType = (hndDsc == nullptr) ? CORINFO_EH_CLAUSE_SAMETRY
                                                                    : ToCORINFO_EH_CLAUSE_FLAGS(hndDsc->ebdHandlerType);
            const char* dispatchFuncName = GetEHDispatchFunctionName(eeHndType);
            dispatchLlvmFunc = _module->getFunction(dispatchFuncName);
            if (dispatchLlvmFunc == nullptr)
            {
                FunctionType* dispatchLlvmFuncType;
                switch (eeHndType)
                {
                    case CORINFO_EH_CLAUSE_NONE:
                    case CORINFO_EH_CLAUSE_FILTER:
                        dispatchLlvmFuncType = FunctionType::get(
                            int32LlvmType, {ptrLlvmType, ptrLlvmType, ptrLlvmType, ptrLlvmType, ptrLlvmType},
                            /* isVarArg */ false);
                        break;
                    case CORINFO_EH_CLAUSE_SAMETRY:
                        dispatchLlvmFuncType = FunctionType::get(
                            int32LlvmType, {ptrLlvmType, ptrLlvmType, ptrLlvmType, ptrLlvmType}, /* isVarArg */ false);
                        break;
                    case CORINFO_EH_CLAUSE_FAULT:
                    case CORINFO_EH_CLAUSE_FINALLY:
                        dispatchLlvmFuncType = FunctionType::get(
                            Type::getVoidTy(_llvmContext), {ptrLlvmType, ptrLlvmType, ptrLlvmType, ptrLlvmType},
                            /* isVarArg */ false);
                        break;
                    default:
                        unreached();
                }

                dispatchLlvmFunc =
                    Function::Create(dispatchLlvmFuncType, Function::ExternalLinkage, dispatchFuncName, _module);
                *pDispatchLlvmFunc = dispatchLlvmFunc;
            }
        }

        return dispatchLlvmFunc;
    };

    BitVecTraits blockVecTraits(_compiler->fgBBNumMax + 1, _compiler);

    struct DispatchData
    {
        llvm::SwitchInst* DispatchSwitchInst;
        BitVec DispatchSwitchTargets;
        unsigned LastDispatchSwitchTargetIndex;
        llvm::BasicBlock* ResumeLlvmBlock;
        Value* DispatchDataRefValue;

        llvm::BasicBlock* GetDispatchSwitchLlvmBlock() const
        {
            return (DispatchSwitchInst != nullptr) ? DispatchSwitchInst->getParent() : nullptr;
        }
    };

    // There is no meaningful source location we can attach to the dispatch blocks. None of them are "user" code.
    llvm::DebugLoc dispatchDebugLoc = getArtificialDebugLocation();
    std::vector<DispatchData> functionData(_compiler->compFuncCount());

    // Note the iteration order: outer -> inner.
    for (unsigned ehIndex = _compiler->compHndBBtabCount - 1; ehIndex != -1; ehIndex--)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
        llvm::BasicBlock* dispatchPadLlvmBlock = m_EHDispatchLlvmBlocks[ehIndex];

        if (dispatchPadLlvmBlock == nullptr)
        {
            // Would have been unreachable.
            continue;
        }

        if (!dispatchPadLlvmBlock->empty())
        {
            // We've already generated code for this dispatch shared between "mutual protect" handlers.
            continue;
        }

        unsigned funcIdx = getLlvmFunctionIndexForProtectedRegion(ehIndex);
        Function* llvmFunc = getLlvmFunctionForIndex(funcIdx);
        if (!llvmFunc->hasPersonalityFn())
        {
            llvmFunc->setPersonalityFn(gxxPersonalityLlvmFunc);
        }

        // The code we will generate effectively inlines the usual runtime dispatch logic. The main reason this
        // scheme was chosen is the fact (re)throwing funclets are handled by it seamlessly and efficiently. The
        // downside to it is the code size overhead of the calls made for each protected region.
        //
        // DISPATCH_PAD_INNER:
        //   dispatchData.CppExceptionTuple = landingPadInst
        //   dispatchData.DispatcherData = null
        //   goto DISPATCH_INNER;
        //
        // DISPATCH_INNER:
        //   dispatchDest = DispatchFunction(FuncletShadowStack(), &dispatchData, &HandlerFunclet, ...)
        //                  unwind to DISPATCH_PAD_OUTER
        //   if (dispatchDest == 0)
        //      goto DISPATCH_OUTER; // For nested regions; top-level ones will use the "switch".
        //   goto UNIFIED_DISPATCH;
        //
        // UNIFIED_DISPATCH:
        //   switch (dispatchDest) {
        //       case 0: goto RESUME;
        //       case 1: goto BB01;
        //       case 2: goto BB02;
        //       ...
        //       default: goto FAIL_FAST;
        //   }
        //
        // RESUME:
        //   resume(dispatchData.CppExceptionTuple); // Rethrow the exception and unwind to caller.
        //
        // FAIL_FAST:
        //   FailFast();
        //
        // What is the possibe set of dispatch destinations (aka why have "UNIFIED_DISPATCH")?
        //
        // We consider the tree of active protected regions above this one, that are also contained in the same funclet.
        // For each region with a (possibly filtered) catch handler, we consider successors of all "catchret" blocks.
        // The union of these will form the set of all possible dispatch destinations for the current protected region.
        // However, we do not actually emit the "switch" code for each individual region, as it would mean quadratic
        // code size growth (number of dispatch destinations X number of protected regions) for deeply nested EH trees.
        // Instead, we create one "universal" dispatch block for each funclet, and jump to it from each dispatch. Note
        // that thanks to the step blocks inserted by "impImportLeave", we do not need to consider cases where a jump
        // from a funclet to its caller would be required.

        // Create the dispatch data alloca. Its structure is a contract between codegen and runtime. The runtime may
        // not modify the part where codegen stores the landing pad value, while the other part will be solely under
        // runtime's control (currently, this is just one pointer-sized field).
        DispatchData& funcDispatchData = functionData[funcIdx];
        Value* dispatchDataRefValue = funcDispatchData.DispatchDataRefValue;
        if (dispatchDataRefValue == nullptr)
        {
            llvm::BasicBlock* prologLlvmBlock = getOrCreatePrologLlvmBlockForFunction(funcIdx);

            _builder.SetInsertPoint(prologLlvmBlock->getTerminator());
            dispatchDataRefValue = _builder.CreateAlloca(dispatchDataLlvmType);

            funcDispatchData.DispatchDataRefValue = dispatchDataRefValue;
        }

        // Dispatch blocks, when initially created, are placed at the start of the function.
        // Here we move them to a more appropriate place so that the entry block is correct.
        if (funcDispatchData.GetDispatchSwitchLlvmBlock() != nullptr)
        {
            dispatchPadLlvmBlock->moveBefore(funcDispatchData.GetDispatchSwitchLlvmBlock());
        }
        else if (funcDispatchData.ResumeLlvmBlock != nullptr)
        {
            dispatchPadLlvmBlock->moveBefore(funcDispatchData.ResumeLlvmBlock);
        }
        else
        {
            dispatchPadLlvmBlock->moveAfter(&llvmFunc->back());
        }
        _builder.SetCurrentDebugLocation(dispatchDebugLoc);

        LlvmBlockRange dispatchLlvmBlocks(dispatchPadLlvmBlock);
        setCurrentEmitContext(funcIdx, ehDsc->ebdEnclosingTryIndex, &dispatchLlvmBlocks);

        llvm::LandingPadInst* landingPadInst = _builder.CreateLandingPad(cppExcTupleLlvmType, 1);
        landingPadInst->addClause(llvm::Constant::getNullValue(ptrLlvmType)); // Catch all C++ exceptions.

        _builder.CreateStore(landingPadInst, dispatchDataRefValue);

        // The dispatchers rely on this being set to null to detect whether the ongoing dispatch is already "active".
        unsigned dispatcherDataFieldOffset =
            _module->getDataLayout().getStructLayout(dispatchDataLlvmType)->getElementOffset(1);
        Value* dispatchDataFieldRefValue = gepOrAddr(dispatchDataRefValue, dispatcherDataFieldOffset);
        _builder.CreateStore(llvm::Constant::getNullValue(ptrLlvmType), dispatchDataFieldRefValue);

        // The "actual" dispatch block. Nested dispatches (if any) will branch to it.
        llvm::BasicBlock* dispatchLlvmBlock = createInlineLlvmBlock();
        _builder.CreateBr(dispatchLlvmBlock);
        _builder.SetInsertPoint(dispatchLlvmBlock);

        // The dispatcher uses the passed-in shadow stack pointer to call funclets. All funclets (no matter how
        // nested) share the same original shadow frame, thus we need to pass the original shadow stack in case
        // the exception is being dispatched out of a funclet.
        Value* dispatcherShadowStackValue = getShadowStackForCallee();
        Value* funcletShadowStackValue = getOriginalShadowStack();

        // Do we only have one (catch) handler? We will use specialized dispatchers for this case as an optimization:
        // about 2/3 of all EH handlers in optimized code are finallys/faults, ~28% - single catches, with the rest
        // (single filters / 2+ mutually protecting handlers) comprising less than 5% of cases. We could drop the
        // specialized filter dispatcher here, but it doesn't cost us much to have one, and it is considerably more
        // efficient than the general table-based one (and more than 4/5 of all filters are "single").
        //
        unsigned innerEHIndex = ehIndex;
        while ((innerEHIndex > 0) && ehDsc->ebdIsSameTry(_compiler, innerEHIndex - 1))
        {
            innerEHIndex--;
        }

        llvm::CallBase* dispatchDestValue = nullptr;
        if (innerEHIndex == ehIndex)
        {
            Function* dispatchLlvmFunc = getDispatchLlvmFunc(ehDsc);
            Value* handlerValue = getLlvmFunctionForIndex(ehDsc->ebdFuncIndex);

            if (ehDsc->ebdHandlerType == EH_HANDLER_CATCH)
            {
                Value* typeSymbolRefValue = getOrCreateSymbol(getSymbolHandleForClassToken(ehDsc->ebdTyp));
                dispatchDestValue =
                    emitCallOrInvoke(dispatchLlvmFunc, {dispatcherShadowStackValue, funcletShadowStackValue,
                                     dispatchDataRefValue, handlerValue, typeSymbolRefValue});
            }
            else if (ehDsc->ebdHandlerType == EH_HANDLER_FILTER)
            {
                Value* filterValue = getLlvmFunctionForIndex(ehDsc->ebdFuncIndex - 1);
                dispatchDestValue =
                    emitCallOrInvoke(dispatchLlvmFunc, {dispatcherShadowStackValue, funcletShadowStackValue,
                                     dispatchDataRefValue, handlerValue, filterValue});
            }
            else
            {
                dispatchDestValue = emitCallOrInvoke(dispatchLlvmFunc, {dispatcherShadowStackValue,
                                                     funcletShadowStackValue, dispatchDataRefValue, handlerValue});
            }
        }
        else
        {
            Function* dispatchLlvmFunc = getDispatchLlvmFunc(nullptr);
            Value* dispatchTableRefValue = generateEHDispatchTable(llvmFunc, innerEHIndex, ehIndex);
            dispatchDestValue =
                emitCallOrInvoke(dispatchLlvmFunc, {dispatcherShadowStackValue, funcletShadowStackValue,
                                 dispatchDataRefValue, dispatchTableRefValue});
        }

        // Generate code for per-funclet dispatch blocks. The dispatch switch block is only needed if we have
        // catch handlers. The resume block is always needed.
        //
        llvm::BasicBlock* resumeLlvmBlock = funcDispatchData.ResumeLlvmBlock;
        if (resumeLlvmBlock == nullptr)
        {
            resumeLlvmBlock = llvm::BasicBlock::Create(_llvmContext, "BBDR", llvmFunc);

            _builder.SetInsertPoint(resumeLlvmBlock); // No need for a full emit context.
            Value* resumeOperandValue = _builder.CreateLoad(landingPadInst->getType(), dispatchDataRefValue);
            _builder.CreateResume(resumeOperandValue);

            funcDispatchData.ResumeLlvmBlock = resumeLlvmBlock;
        }

        const int EH_CONTINUE_SEARCH = 0;

        llvm::BasicBlock* dispatchSwitchLlvmBlock = funcDispatchData.GetDispatchSwitchLlvmBlock();
        if (ehDsc->HasCatchHandler() && (dispatchSwitchLlvmBlock == nullptr))
        {
            dispatchSwitchLlvmBlock = llvm::BasicBlock::Create(_llvmContext, "BBDS", llvmFunc, resumeLlvmBlock);
            llvm::BasicBlock* failFastLlvmBlock = llvm::BasicBlock::Create(_llvmContext, "BBFF", llvmFunc);

            LlvmBlockRange dispatchSwitchLlvmBlocks(dispatchSwitchLlvmBlock);
            setCurrentEmitContext(funcIdx, EHblkDsc::NO_ENCLOSING_INDEX, &dispatchSwitchLlvmBlocks);

            llvm::SwitchInst* switchInst = _builder.CreateSwitch(dispatchDestValue, failFastLlvmBlock);
            switchInst->addCase(_builder.getInt32(EH_CONTINUE_SEARCH), resumeLlvmBlock);

            LlvmBlockRange failFastLlvmBlocks(failFastLlvmBlock);
            setCurrentEmitContext(funcIdx, EHblkDsc::NO_ENCLOSING_INDEX, &failFastLlvmBlocks);

            emitHelperCall(CORINFO_HELP_FAIL_FAST);
            _builder.CreateUnreachable();

            funcDispatchData.DispatchSwitchInst = switchInst;
            funcDispatchData.DispatchSwitchTargets = BitVecOps::MakeEmpty(&blockVecTraits);
        }

        llvm::BasicBlock* outerDispatchLlvmBlock = nullptr;
        if (llvm::isa<llvm::InvokeInst>(dispatchDestValue))
        {
            // This will be the "dispatch pad" block. Since we're generating dispatches from outer to inner, we already
            // have the "actual" dispatch block; it will be the next one.
            outerDispatchLlvmBlock = llvm::cast<llvm::InvokeInst>(dispatchDestValue)->getUnwindDest();
            outerDispatchLlvmBlock = outerDispatchLlvmBlock->getNextNode();
            assert(outerDispatchLlvmBlock != nullptr);
        }

        // Reset context back to the dispatch block.
        setCurrentEmitContext(funcIdx, ehDsc->ebdEnclosingTryIndex, &dispatchLlvmBlocks);

        // For inner dispatch, jump to the outer one if the handler returned "continue search". Faults / finallys cannot
        // satisfy the first-pass search and so for them this jump is unconditional.
        llvm::BasicBlock* lastDispatchLlvmBlock = dispatchLlvmBlocks.LastBlock;
        if (ehDsc->HasCatchHandler())
        {
            Value* unifiedDispatchDestValue = funcDispatchData.DispatchSwitchInst->getOperand(0);
            if (unifiedDispatchDestValue != dispatchDestValue)
            {
                llvm::PHINode* phiNode = llvm::dyn_cast<llvm::PHINode>(unifiedDispatchDestValue);
                if (phiNode == nullptr)
                {
                    phiNode =
                        llvm::PHINode::Create(dispatchDestValue->getType(), 2, "", funcDispatchData.DispatchSwitchInst);
                    phiNode->addIncoming(unifiedDispatchDestValue, dispatchSwitchLlvmBlock->getUniquePredecessor());

                    funcDispatchData.DispatchSwitchInst->setOperand(0, phiNode);
                }

                phiNode->addIncoming(dispatchDestValue, lastDispatchLlvmBlock);
            }

            if (outerDispatchLlvmBlock != nullptr)
            {
                Value* doContinueSearchValue =
                    _builder.CreateICmpEQ(dispatchDestValue, _builder.getInt32(EH_CONTINUE_SEARCH));
                _builder.CreateCondBr(doContinueSearchValue, outerDispatchLlvmBlock, dispatchSwitchLlvmBlock);
            }
            else
            {
                _builder.CreateBr(dispatchSwitchLlvmBlock);
            }
        }
        else
        {
            if (outerDispatchLlvmBlock != nullptr)
            {
                _builder.CreateBr(outerDispatchLlvmBlock);
            }
            else
            {
                _builder.CreateBr(resumeLlvmBlock);
            }
        }

        // Finally, add in the possible "catchret" destinations. Do not forget to consider all of the mutally protecting
        // handlers, since there is only one dispatch block for all of them. Note how we are only doing linear work here
        // because the funclet creating process will hoist nested handlers, "flattening" the basic block list. Also, we
        // check for the reachability of the handler here, even as we've already checked for whether the dispatch itself
        // is reachable. The reason for this is a possibility of a dispatch with a reachable filter but an unreachable
        // handler (where the filter always returns false). This is currently, technically, redundant, because RyuJit
        // doesn't perform flow optimizations which would expose the handler as unreachable. We choose to be resilient
        // against this anyway.
        //
        if (ehDsc->HasCatchHandler() && isReachable(ehDsc->ebdHndBeg))
        {
            llvm::SwitchInst* switchInst = funcDispatchData.DispatchSwitchInst;
            BitVec& dispatchSwitchTargets = funcDispatchData.DispatchSwitchTargets;
            for (unsigned hndIndex = innerEHIndex; hndIndex <= ehIndex; hndIndex++)
            {
                EHblkDsc* hndDsc = _compiler->ehGetDsc(hndIndex);
                for (BasicBlock* hndBlock : _compiler->Blocks(hndDsc->ebdHndBeg, hndDsc->ebdHndLast))
                {
                    assert((hndDsc->HasCatchHandler()) && (hndBlock->getHndIndex() == hndIndex));
                    if (hndBlock->bbJumpKind == BBJ_EHCATCHRET)
                    {
                        BasicBlock* destBlock = hndBlock->bbJumpDest;
                        llvm::BasicBlock* destLlvmBlock = getFirstLlvmBlockForBlock(destBlock);
                        assert(destLlvmBlock->getParent() == llvmFunc); // No jumping out of a funclet.

                        // We use a bitset to avoid quadratic behavior associated with checking if we have already added
                        // this dispatch destination - multiple sets of "catchret"s may target the same set of blocks.
                        unsigned destBlockNum = destBlock->bbNum;
                        if (!BitVecOps::IsMember(&blockVecTraits, dispatchSwitchTargets, destBlockNum))
                        {
                            unsigned destIndex = ++funcDispatchData.LastDispatchSwitchTargetIndex;
                            llvm::ConstantInt* destIndexValue = _builder.getInt32(destIndex);

                            switchInst->addCase(destIndexValue, destLlvmBlock);

                            // Complete the catch return blocks (this one and all the others with the same target).
                            for (BasicBlock* predBlock : destBlock->PredBlocks())
                            {
                                if (predBlock->bbJumpKind == BBJ_EHCATCHRET)
                                {
                                    llvm::BasicBlock* catchRetLlvmBlock = getLastLlvmBlockForBlock(predBlock);
                                    llvm::ReturnInst::Create(_llvmContext, destIndexValue, catchRetLlvmBlock);
                                }
                            }

                            BitVecOps::AddElemD(&blockVecTraits, dispatchSwitchTargets, destBlockNum);
                        }
                    }
                }
            }
        }
    }
}

Value* Llvm::generateEHDispatchTable(Function* llvmFunc, unsigned innerEHIndex, unsigned outerEHIndex)
{
    // We only generate this table for a run of mutually protecting handlers.
    assert(outerEHIndex > innerEHIndex);

    // The table will have the following format:
    //
    // [2 (4) bytes: size of table in pointer units] (Means we don't support > ~2^15 clauses)
    // [2 (4) bytes: bitmap of clause kinds, 0 - typed, 1 - filter]
    // [up to 16 (32) clauses: { void* "Data", void* "Handler" }]
    //
    //  - "Data": exception type symbol pointer / filter handler.
    //  - "Handler": pointer to the handler
    //
    // [4 (8) bytes: bitmap of clause kinds] [32 (64) clauses], ...
    //
    // This is "optimal" for the purposes of targeting WASM, where we cannot encode funclet pointers
    // more efficiently using native code offsets.
    //
    const int LARGE_SECTION_CLAUSE_COUNT = TARGET_POINTER_SIZE * BITS_PER_BYTE;
    const int FIRST_SECTION_CLAUSE_COUNT = LARGE_SECTION_CLAUSE_COUNT / 2;

    Type* firstClauseMaskType = Type::getIntNTy(_llvmContext, FIRST_SECTION_CLAUSE_COUNT);
    Type* largeClauseMaskType = getIntPtrLlvmType();

    unsigned clauseCount = outerEHIndex - innerEHIndex + 1;
    ArrayStack<llvm::Constant*> data(_compiler->getAllocator(CMK_Codegen));

    data.Push(nullptr); // Placeholder for size.
    data.Push(nullptr); // Placeholder for the first mask.

    target_size_t clauseKindMask = 0;
    unsigned baseSectionIndex = 0;
    unsigned nextSectionIndex = FIRST_SECTION_CLAUSE_COUNT;
    for (unsigned index = 0; index < clauseCount; index++)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(innerEHIndex + index);
        unsigned clauseIndex = index - baseSectionIndex;

        llvm::Constant* dataValue;
        if (ehDsc->HasFilter())
        {
            clauseKindMask |= (target_size_t(1) << clauseIndex);
            dataValue = getLlvmFunctionForIndex(ehDsc->ebdFuncIndex - 1);
        }
        else
        {
            // Otherwise we need a type symbol reference.
            CORINFO_GENERIC_HANDLE typeSymbolHandle = getSymbolHandleForClassToken(ehDsc->ebdTyp);
            dataValue = getOrCreateSymbol(typeSymbolHandle);
        }

        data.Push(dataValue);
        data.Push(getLlvmFunctionForIndex(ehDsc->ebdFuncIndex));

        // Is this the last entry in the current section? Initialize the mask if so.
        bool isEndOfTable = (index + 1) == clauseCount;
        bool isEndOfSection = (index + 1) == nextSectionIndex;
        if (isEndOfTable || isEndOfSection)
        {
            Type* clauseMaskType = (baseSectionIndex == 0) ? firstClauseMaskType : largeClauseMaskType;
            data.TopRef(2 * (clauseIndex + 1)) = llvm::ConstantInt::get(clauseMaskType, clauseKindMask);

            // Start the next section if needed.
            if (!isEndOfTable)
            {
                clauseKindMask = 0;
                data.Push(nullptr);

                baseSectionIndex = nextSectionIndex;
                nextSectionIndex += LARGE_SECTION_CLAUSE_COUNT;
            }
        }
    }

    data.BottomRef(0) = llvm::ConstantInt::get(firstClauseMaskType, data.Height() - 1);

    ArrayStack<Type*> llvmTypeBuilder(_compiler->getAllocator(CMK_Codegen), data.Height());
    for (size_t i = 0; i < data.Height(); i++)
    {
        llvmTypeBuilder.Push(data.Bottom(i)->getType());
    }
    llvm::StructType* tableLlvmType = llvm::StructType::get(_llvmContext, {&llvmTypeBuilder.BottomRef(0),
                                                            static_cast<size_t>(llvmTypeBuilder.Height())});
    llvm::Constant* tableValue = llvm::ConstantStruct::get(tableLlvmType, {&data.BottomRef(0),
                                                           static_cast<size_t>(data.Height())});

    llvm::GlobalVariable* tableRef = new llvm::GlobalVariable(*_module, tableLlvmType, /* isConstant */ true,
                                                             llvm::GlobalVariable::InternalLinkage, tableValue,
                                                             llvmFunc->getName() + "__EHTable");
    tableRef->setAlignment(llvm::MaybeAlign(TARGET_POINTER_SIZE));

    JITDUMP("\nGenerated EH dispatch table for mutually protecting handlers:\n", innerEHIndex, outerEHIndex);
    for (unsigned ehIndex = innerEHIndex; ehIndex <= outerEHIndex; ehIndex++)
    {
        JITDUMPEXEC(_compiler->ehGetDsc(ehIndex)->DispEntry(ehIndex));
    }
    JITDUMPEXEC(tableRef->dump());

    return tableRef;
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
            for (flowList* edge : phiBlock->PredEdges())
            {
                BasicBlock* edgePredBlock = edge->getBlock();
                if (edgePredBlock->bbJumpKind == BBJ_SWITCH)
                {
                    predCountMap.AddOrUpdate({predBlock, phiBlock}, edge->flDupCount);

                    if (edgePredBlock == predBlock)
                    {
                        predCount = edge->flDupCount;
                    }
                }
            }
        }

        assert(predCount != 0);
        return predCount;
    };

    for (PhiPair phiPair : _phiPairs)
    {
        llvm::PHINode* llvmPhiNode = phiPair.llvmPhiNode;
        GenTreePhi* phiNode = phiPair.irPhiNode;

        GenTreeLclVar* phiStore = phiNode->gtNext->AsLclVar();
        unsigned lclNum = phiStore->GetLclNum();
        BasicBlock* phiBlock = _compiler->lvaGetDesc(lclNum)->GetPerSsaData(phiStore->GetSsaNum())->GetBlock();

        for (GenTreePhi::Use& use : phiNode->Uses())
        {
            GenTreePhiArg* phiArg = use.GetNode()->AsPhiArg();
            unsigned ssaNum = phiArg->GetSsaNum();
            BasicBlock* predBlock = phiArg->gtPredBB;
            llvm::BasicBlock* llvmPredBlock = getLastLlvmBlockForBlock(predBlock);

            Value* localPhiArg = _localsMap[{lclNum, ssaNum}];
            Value* phiRealArgValue;
            Instruction* castRequired = getCast(localPhiArg, llvmPhiNode->getType());
            if (castRequired != nullptr)
            {
                // This cast is needed when
                // 1) The phi arg real type is short and the definition is the actual longer type, e.g. for bool/int
                // 2) There is a pointer difference, e.g. i8* v i32* and perhaps different levels of indirection: i8** and i8*
                //
                _builder.SetInsertPoint(llvmPredBlock->getTerminator());
                phiRealArgValue = _builder.Insert(castRequired);
            }
            else
            {
                phiRealArgValue = localPhiArg;
            }

            unsigned llvmPredCount = getPhiPredCount(predBlock, phiBlock);
            for (unsigned i = 0; i < llvmPredCount; i++)
            {
                llvmPhiNode->addIncoming(phiRealArgValue, llvmPredBlock);
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
        // int to pointer type (TODO-LLVM: WASM64: use POINTER_BITs when set correctly, also below for getInt32Ty)
        if (nodeValue->getType() == Type::getInt32Ty(_llvmContext) && targetLlvmType->isPointerTy())
        {
            return _builder.CreateIntToPtr(nodeValue, targetLlvmType);
        }

        // pointer to ints
        if (nodeValue->getType()->isPointerTy() && targetLlvmType == Type::getInt32Ty(_llvmContext))
        {
            return _builder.CreatePtrToInt(nodeValue, Type::getInt32Ty(_llvmContext));
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
                    trueNodeType = static_cast<var_types>(node->AsCall()->gtReturnType);
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
                    assert(nodeValue->getType() == Type::getInt1Ty(_llvmContext));
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
        case GT_LCL_VAR:
            buildLocalVar(node->AsLclVar());
            break;
        case GT_LCL_VAR_ADDR:
        case GT_LCL_FLD_ADDR:
            buildLocalVarAddr(node->AsLclVarCommon());
            break;
        case GT_LSH:
        case GT_RSH:
        case GT_RSZ:
            buildShift(node->AsOp());
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
            buildUnaryOperation(node);
            break;
        case GT_NO_OP:
            emitDoNothingCall();
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
        case GT_OBJ:
        case GT_BLK:
            buildBlk(node->AsBlk());
            break;
        case GT_PHI:
            buildEmptyPhi(node->AsPhi());
            break;
        case GT_PHI_ARG:
        case GT_PUTARG_TYPE:
            break;
        case GT_RETURN:
        case GT_RETFILT:
            buildReturn(node);
            break;
        case GT_STORE_LCL_VAR:
            buildStoreLocalVar(node->AsLclVar());
            break;
        case GT_STOREIND:
            buildStoreInd(node->AsStoreInd());
            break;
        case GT_STORE_BLK:
        case GT_STORE_OBJ:
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
        case GT_IL_OFFSET:
            buildILOffset(node->AsILOffset());
            break;
        default:
            failFunctionCompilation();
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
                instrIter->dump();
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
        llvmRef = _builder.CreateTrunc(llvmRef, Type::getInt32Ty(_llvmContext));
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
        localValue = consumeValue(lclVar->gtGetOp1(), destLlvmType);
    }

    if (lclVar->HasSsaName())
    {
        _localsMap.Set({lclNum, lclVar->GetSsaNum()}, localValue);
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
    _phiPairs.push_back({ phi, llvmPhiNode });

    mapGenTreeToValue(phi, llvmPhiNode);
}

void Llvm::buildLocalField(GenTreeLclFld* lclFld)
{
    assert(!lclFld->TypeIs(TYP_STRUCT));

    unsigned lclNum = lclFld->GetLclNum();

    // TODO-LLVM: if this is an only value type field, or at offset 0, we can optimize.
    Value* structAddrValue = getLocalAddr(lclNum);
    Value* fieldAddressValue = gepOrAddr(structAddrValue, lclFld->GetLclOffs());

    mapGenTreeToValue(lclFld, _builder.CreateLoad(getLlvmTypeForVarType(lclFld->TypeGet()), fieldAddressValue));
}

void Llvm::buildLocalVarAddr(GenTreeLclVarCommon* lclAddr)
{
    unsigned int lclNum = lclAddr->GetLclNum();
    Value* localAddr = getLocalAddr(lclNum);
    mapGenTreeToValue(lclAddr, gepOrAddr(localAddr, lclAddr->GetLclOffs()));
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
        addValue = _builder.CreateGEP(Type::getInt8Ty(_llvmContext), baseValue, offsetValue);
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
        subValue = _builder.CreateGEP(Type::getInt8Ty(_llvmContext), baseValue, addOffsetValue);
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

void Llvm::buildDivMod(GenTree* node)
{
    GenTree* dividendNode = node->gtGetOp1();
    GenTree* divisorNode = node->gtGetOp2();
    Type* llvmType = getLlvmTypeForVarType(node->TypeGet());
    Value* dividendValue = consumeValue(dividendNode, llvmType);
    Value* divisorValue  = consumeValue(divisorNode, llvmType);
    Value* divModValue   = nullptr;

    // TODO-LLVM: use OperExceptions here when enough of upstream is merged.
    if (varTypeIsIntegral(node))
    {
        // First, check for divide by zero.
        if (!divisorNode->IsIntegralConst() || divisorNode->IsIntegralConst(0))
        {
            Value* isDivisorZeroValue =
                _builder.CreateCmp(llvm::CmpInst::ICMP_EQ, divisorValue, llvm::ConstantInt::get(llvmType, 0));
            emitJumpToThrowHelper(isDivisorZeroValue, SCK_DIV_BY_ZERO);
        }

        // Second, check for "INT_MIN / -1" (which throws ArithmeticException).
        if (node->OperIs(GT_DIV, GT_MOD) && (!divisorNode->IsIntegralConst() || divisorNode->IsIntegralConst(-1)))
        {
            int64_t minDividend = node->TypeIs(TYP_LONG) ? INT64_MIN : INT32_MIN;
            if (!dividendNode->IsIntegralConst() || (dividendNode->AsIntConCommon()->IntegralValue() == minDividend))
            {
                Value* isDivisorMinusOneValue =
                    _builder.CreateCmp(llvm::CmpInst::ICMP_EQ, divisorValue, llvm::ConstantInt::get(llvmType, -1));
                Value* isDividendMinValue = _builder.CreateCmp(llvm::CmpInst::ICMP_EQ, dividendValue,
                                                               llvm::ConstantInt::get(llvmType, minDividend));
                Value* isOverflowValue = _builder.CreateAnd(isDivisorMinusOneValue, isDividendMinValue);
                emitJumpToThrowHelper(isOverflowValue, SCK_ARITH_EXCPN);
            }
        }
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
    Value* indexValue = consumeValue(node->gtGetOp2(), Type::getInt32Ty(_llvmContext));
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
            assert(varTypeIsIntegral(castFromType) && varTypeIsIntegral(castToType));

            IntegralRange checkedRange = IntegralRange::ForCastInput(cast);
            int64_t lowerBound = IntegralRange::SymbolicToRealValue(checkedRange.GetLowerBound());
            int64_t upperBound = IntegralRange::SymbolicToRealValue(checkedRange.GetUpperBound());

            Value* checkedValue = castFromValue;
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
        lclHeapValue = llvm::Constant::getNullValue(Type::getInt8PtrTy(_llvmContext));
    }
    else
    {
        llvm::AllocaInst* allocaInst = _builder.CreateAlloca(Type::getInt8Ty(_llvmContext), sizeValue);

        // LCLHEAP (aka IL's "localloc") is specified to return a pointer "...aligned so that any built-in data type
        // can be stored there using the stind instructions", so we'll be a bit conservative and align it maximally.
        llvm::Align allocaAlignment = llvm::Align(genTypeSize(TYP_DOUBLE));
        allocaInst->setAlignment(allocaAlignment);

        // "If the localsinit flag on the method is true, the block of memory returned is initialized to 0".
        if (_compiler->info.compInitMem)
        {
            _builder.CreateMemSet(allocaInst, _builder.getInt8(0), sizeValue, allocaAlignment);
        }

        if (!sizeNode->IsIntegralConst()) // Build: %lclHeapValue = (%sizeValue != 0) ? "alloca" : "null".
        {
            Value* zeroSizeValue = llvm::Constant::getNullValue(sizeValue->getType());
            Value* isSizeNotZeroValue = _builder.CreateCmp(llvm::CmpInst::ICMP_NE, sizeValue, zeroSizeValue);
            Value* nullValue = llvm::Constant::getNullValue(Type::getInt8PtrTy(_llvmContext));

            lclHeapValue = _builder.CreateSelect(isSizeNotZeroValue, allocaInst, nullValue);
        }
        else
        {
            lclHeapValue = allocaInst;
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

void Llvm::buildIntegralConst(GenTreeIntConCommon* node)
{
    var_types constType = node->TypeGet();
    Type* constLlvmType = getLlvmTypeForVarType(constType);

    Value* constValue;
    if (node->IsCnsIntOrI() && node->IsIconHandle()) // TODO-LLVM: change to simply "IsIconHandle" once upstream does.
    {
        if (node->IsIconHandle(GTF_ICON_FTN_ADDR))
        {
            // TODO-LLVM: we need to reference the proper function symbol here.
            failFunctionCompilation();
        }

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
    if (call->IsHelperCall())
    {
        switch (_compiler->eeGetHelperNum(call->gtCallMethHnd))
        {
            case CORINFO_HELP_READYTORUN_GENERIC_HANDLE:
            case CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE:
            case CORINFO_HELP_GVMLOOKUP_FOR_SLOT: /* generates an extra parameter in the signature */
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE: /* misses an arg in the signature somewhere, not the shadow stack */
            case CORINFO_HELP_READYTORUN_DELEGATE_CTOR:
                failFunctionCompilation();

            default:
                break;
        }
    }
    else if (call->IsVirtualStub())
    {
        // TODO-LLVM: VSD.
        failFunctionCompilation();
    }

    llvm::FunctionCallee llvmFuncCallee;
    if (call->IsVirtualVtable() || (call->gtCallType == CT_INDIRECT))
    {
        FunctionType* functionType = createFunctionTypeForCall(call);
        GenTree* targetNode = call->IsVirtualVtable() ? call->gtControlExpr : call->gtCallAddr;
        Value* targetValue = consumeValue(targetNode, functionType->getPointerTo());

        llvmFuncCallee = {functionType, targetValue};
    }
    else
    {
        void* handle;
        if (call->gtEntryPoint.handle != nullptr)
        {
            // Note some helpers (e. g. CORINFO_HELP_READYTORUN_STATIC_BASE) do not represent singular methods and so
            // will go through this path.
            assert(call->gtEntryPoint.accessType == IAT_VALUE);
            handle = call->gtEntryPoint.handle;
        }
        else
        {
            assert(call->IsHelperCall());
            CorInfoHelpFunc helperFunc = _compiler->eeGetHelperNum(call->gtCallMethHnd);
            void* pAddr = nullptr;
            handle = _compiler->compGetHelperFtn(helperFunc, &pAddr);
            assert(pAddr == nullptr);
        }

        const char* symbolName = GetMangledSymbolName(handle);
        AddCodeReloc(handle); // Replacement for _info.compCompHnd->recordRelocation.

        llvmFuncCallee = getOrCreateLlvmFunction(symbolName, call);
    }

    // We may come back into managed from the unmanaged call so store the shadowstack.
    if (!callHasShadowStackArg(call))
    {
        _builder.CreateStore(getShadowStackForCallee(), getOrCreateExternalSymbol("t_pShadowStackTop"));
    }

    std::vector<Value*> argVec = std::vector<Value*>();

    GenTreePutArgType* lastArg = nullptr;
    for (GenTreeCall::Use& use : call->Args())
    {
        lastArg = use.GetNode()->AsPutArgType();

        GenTree* argNode     = lastArg->gtGetOp1();
        Type*    argLlvmType = getLlvmTypeForCorInfoType(lastArg->GetCorInfoType(), lastArg->GetClsHnd());
        Value*   argValue;

        if (argNode->OperIs(GT_FIELD_LIST))
        {
            assert(lastArg->GetCorInfoType() == CORINFO_TYPE_VALUECLASS);
            argValue = buildFieldList(argNode->AsFieldList(), argLlvmType);
        }
        else
        {
            argValue = consumeValue(argNode, argLlvmType);
        }

        argVec.push_back(argValue);
    }

    Value* callValue = emitCallOrInvoke(llvmFuncCallee, argVec);
    mapGenTreeToValue(call, callValue);
}

Value* Llvm::buildFieldList(GenTreeFieldList* fieldList, Type* llvmType)
{
    assert(fieldList->TypeIs(TYP_STRUCT));

    if (llvmType->isStructTy() || fieldList->Uses().begin()->GetNext() != nullptr)
    {
        Value* alloca = _builder.CreateAlloca(llvmType);

        for (GenTreeFieldList::Use& use : fieldList->Uses())
        {
            Value* fieldAddr = gepOrAddr(alloca, use.GetOffset());
            Type*  fieldType = getLlvmTypeForVarType(use.GetType());
            _builder.CreateStore(consumeValue(use.GetNode(), fieldType), fieldAddr);
        }

        return _builder.CreateLoad(llvmType, alloca);
    }

    return consumeValue(fieldList->Uses().begin()->GetNode(), llvmType);
}

void Llvm::buildInd(GenTreeIndir* indNode)
{
    Type* loadLlvmType = getLlvmTypeForVarType(indNode->TypeGet());
    Value* addrValue = consumeValue(indNode->Addr(), llvm::PointerType::getUnqual(_llvmContext));

    emitNullCheckForIndir(indNode, addrValue);
    Value* loadValue = _builder.CreateLoad(loadLlvmType, addrValue);

    mapGenTreeToValue(indNode, loadValue);
}

void Llvm::buildBlk(GenTreeBlk* blkNode)
{
    Type* blkLlvmType = getLlvmTypeForStruct(blkNode->GetLayout());
    Value* addrValue = consumeValue(blkNode->Addr(), llvm::PointerType::getUnqual(_llvmContext));

    emitNullCheckForIndir(blkNode, addrValue);
    Value* blkValue = _builder.CreateLoad(blkLlvmType, addrValue);

    mapGenTreeToValue(blkNode, blkValue);
}

// TODO-LLVM: delete when https://github.com/dotnet/runtime/pull/70518 from upstream is merged.
bool storeIndRequiresTrunc(var_types storeType, var_types dataType)
{
    return varTypeIsSmall(storeType) && dataType == TYP_LONG;
}

void Llvm::buildStoreInd(GenTreeStoreInd* storeIndOp)
{
    GCInfo::WriteBarrierForm wbf = getGCInfo()->gcIsWriteBarrierCandidate(storeIndOp, storeIndOp->Data());

    Type* storeLlvmType = getLlvmTypeForVarType(storeIndOp->TypeGet());
    Value* addrValue = consumeValue(storeIndOp->Addr(), llvm::PointerType::getUnqual(_llvmContext));

    Value* dataValue;
    if (storeIndRequiresTrunc(storeIndOp->TypeGet(), storeIndOp->Data()->TypeGet()))
    {
        dataValue = consumeValue(storeIndOp->Data(), getLlvmTypeForVarType(storeIndOp->Data()->TypeGet()));
        dataValue = _builder.CreateTrunc(dataValue, storeLlvmType);
    }
    else
    {
        dataValue = consumeValue(storeIndOp->Data(), storeLlvmType);
    }

    emitNullCheckForIndir(storeIndOp, addrValue);

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
    Value* addrValue = consumeValue(addrNode, Type::getInt8PtrTy(_llvmContext));

    emitNullCheckForIndir(blockOp, addrValue);

    // Check for the "initblk" operation ("dataNode" is either INIT_VAL or constant zero).
    if (blockOp->OperIsInitBlkOp())
    {
        Value* fillValue = dataNode->OperIsInitVal() ? consumeValue(dataNode->gtGetOp1(), Type::getInt8Ty(_llvmContext))
                                                     : _builder.getInt8(0);
        _builder.CreateMemSet(addrValue, fillValue, _builder.getInt32(layout->GetSize()), llvm::Align());
        return;
    }

    Value* dataValue = consumeValue(dataNode, getLlvmTypeForStruct(layout));
    if (layout->HasGCPtr() && ((blockOp->gtFlags & GTF_IND_TGT_NOT_HEAP) == 0) && !addrNode->OperIsLocalAddr())
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

    Value* dstAddrValue = consumeValue(blockOp->Addr(), getPtrLlvmType());
    Value* srcValue;
    if (isCopyBlock)
    {
        srcValue = consumeValue(srcNode->AsIndir()->Addr(), getPtrLlvmType());
    }
    else
    {
        srcValue = srcNode->OperIsInitVal() ? consumeValue(srcNode->AsUnOp()->gtGetOp1(), Type::getInt8Ty(_llvmContext))
                                            : _builder.getInt8(0);
    }
    // Per ECMA 335, cpblk/initblk only allow int32-sized operands. We'll be a bit more permissive and allow native ints
    // as well (as do other backends).
    Type* sizeLlvmType = genActualTypeIsInt(sizeNode) ? Type::getInt32Ty(_llvmContext) : getIntPtrLlvmType();
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
        checkSizeLlvmBlock = _builder.GetInsertBlock();
        nullChecksLlvmBlock = createInlineLlvmBlock();
        _builder.SetInsertPoint(nullChecksLlvmBlock);
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
        if (dstAddrMayBeNull)
        {
            emitNullCheckForIndir(blockOp, dstAddrValue);
        }
        if (srcAddrMayBeNull)
        {
            emitNullCheckForIndir(srcNode->AsIndir(), srcValue);
        }
    }

    // Technically cpblk/initblk specify that they expect their sources/destinations to be aligned, but in
    // practice these instructions are used like memcpy/memset, which do not require this. So we do not try
    // to be more precise with the alignment specification here as well.
    // TODO-LLVM: volatile STORE_DYN_BLK.
    if (isCopyBlock)
    {
        _builder.CreateMemCpy(dstAddrValue, llvm::MaybeAlign(), srcValue, llvm::MaybeAlign(), sizeValue);
    }
    else
    {
        _builder.CreateMemSet(dstAddrValue, srcValue, sizeValue, llvm::MaybeAlign());
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
    Value* result;
    Value* op1Value = consumeValue(node->gtGetOp1(), getLlvmTypeForVarType(node->TypeGet()));

    switch (node->OperGet())
    {
        case GT_NEG:
            if (op1Value->getType()->isFloatingPointTy())
            {
                result = _builder.CreateFNeg(op1Value, "fneg");
            }
            else
            {
                result = _builder.CreateNeg(op1Value, "neg");
            }
            break;
        case GT_NOT:
            result = _builder.CreateNot(op1Value, "not");
            break;
        default:
            failFunctionCompilation();  // TODO-LLVM: other binary operators
    }
    mapGenTreeToValue(node, result);
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
            failFunctionCompilation();  // TODO-LLVM: other shift types
    }
    mapGenTreeToValue(node, result);
}

void Llvm::buildReturn(GenTree* node)
{
    assert(node->OperIs(GT_RETURN, GT_RETFILT));

    if (node->TypeIs(TYP_VOID))
    {
        _builder.CreateRetVoid();
        return;
    }

    GenTree* retValNode = node->gtGetOp1();
    Type* retLlvmType = node->OperIs(GT_RETURN) ? getLlvmTypeForCorInfoType(_sigInfo.retType, _sigInfo.retTypeClass)
                                                : Type::getInt32Ty(_llvmContext);
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
    assert(condValue->getType() == Type::getInt1Ty(_llvmContext)); // We only expect relops to appear as JTRUE operands.

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
    Value* addrValue = consumeValue(nullCheckNode->Addr(), Type::getInt8PtrTy(_llvmContext));
    emitNullCheckForIndir(nullCheckNode, addrValue);
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
    unsigned lineNo = GetOffsetLineNumber(ilOffset);
    llvm::DILocation* diLocation = createDebugLocation(lineNo);

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
    emitCallOrInvoke(finallyLlvmFunc, {getShadowStackForCallee(), getOriginalShadowStack()});

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
            const llvm::StructLayout* structLayout = _module->getDataLayout().getStructLayout(static_cast<llvm::StructType*>(data->getType()));

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
                emitHelperCall(CORINFO_HELP_CHECKED_ASSIGN_REF,
                               {address, castIfNecessary(fieldData, llvm::PointerType::getUnqual(_llvmContext))});

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

void Llvm::emitDoNothingCall()
{
    if (_doNothingFunction == nullptr)
    {
        _doNothingFunction = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), ArrayRef<Type*>(), false), Function::ExternalLinkage, 0U, "llvm.donothing", _module);
    }
    _builder.CreateCall(_doNothingFunction);
}

void Llvm::emitJumpToThrowHelper(Value* jumpCondValue, SpecialCodeKind throwKind)
{
    if (_compiler->fgUseThrowHelperBlocks())
    {
        assert(CurrentBlock() != nullptr);

        // For code with throw helper blocks, find and use the shared helper block for raising the exception.
        unsigned throwIndex = _compiler->bbThrowIndex(CurrentBlock());
        BasicBlock* throwBlock = _compiler->fgFindExcptnTarget(throwKind, throwIndex)->acdDstBlk;

        // Jump to the exception-throwing block on error.
        llvm::BasicBlock* nextLlvmBlock = createInlineLlvmBlock();
        llvm::BasicBlock* throwLlvmBlock = getFirstLlvmBlockForBlock(throwBlock);
        _builder.CreateCondBr(jumpCondValue, throwLlvmBlock, nextLlvmBlock);
        _builder.SetInsertPoint(nextLlvmBlock);
    }
    else
    {
        // The code to throw the exception will be generated inline; we will jump around it in the non-exception case.
        llvm::BasicBlock* throwLlvmBlock = createInlineLlvmBlock();
        llvm::BasicBlock* nextLlvmBlock = createInlineLlvmBlock();
        _builder.CreateCondBr(jumpCondValue, throwLlvmBlock, nextLlvmBlock);

        _builder.SetInsertPoint(throwLlvmBlock);
        emitHelperCall(static_cast<CorInfoHelpFunc>(_compiler->acdHelper(throwKind)));
        _builder.CreateUnreachable();

        _builder.SetInsertPoint(nextLlvmBlock);
    }
}

void Llvm::emitNullCheckForIndir(GenTreeIndir* indir, Value* addrValue)
{
    if ((indir->gtFlags & GTF_IND_NONFAULTING) == 0)
    {
        assert(addrValue->getType()->isPointerTy());

        Value* nullValue = llvm::Constant::getNullValue(addrValue->getType());
        Value* isNullValue = _builder.CreateCmp(llvm::CmpInst::ICMP_EQ, addrValue, nullValue);
        emitJumpToThrowHelper(isNullValue, SCK_NULL_REF_EXCPN);
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

Value* Llvm::emitHelperCall(CorInfoHelpFunc helperFunc, ArrayRef<Value*> sigArgs)
{
    void* pAddr = nullptr;
    void* handle = _compiler->compGetHelperFtn(helperFunc, &pAddr);
    assert(pAddr == nullptr);

    const char* symbolName = GetMangledSymbolName(handle);
    AddCodeReloc(handle);

    Function* helperLlvmFunc = _module->getFunction(symbolName);
    if (helperLlvmFunc == nullptr)
    {
        FunctionType* llvmFuncType = createFunctionTypeForHelper(helperFunc);
        helperLlvmFunc = Function::Create(llvmFuncType, Function::ExternalLinkage, symbolName, _module);
    }

    Value* callValue;
    if (getHelperFuncInfo(helperFunc).HasFlags(HFIF_SS_ARG))
    {
        std::vector<Value*> args = sigArgs.vec();
        args.insert(args.begin(), getShadowStackForCallee());

        callValue = emitCallOrInvoke(helperLlvmFunc, args);
    }
    else
    {
        callValue = emitCallOrInvoke(helperLlvmFunc, sigArgs);
    }

    return callValue;
}

llvm::CallBase* Llvm::emitCallOrInvoke(llvm::FunctionCallee callee, ArrayRef<Value*> args)
{
    llvm::BasicBlock* catchLlvmBlock = nullptr;
    if (getCurrentProtectedRegionIndex() != EHblkDsc::NO_ENCLOSING_INDEX)
    {
        catchLlvmBlock = m_EHDispatchLlvmBlocks[getCurrentProtectedRegionIndex()];

        // Protected region index that is set in the emit context refers to the "logical" enclosing
        // protected region, i. e. the one before funclet creation. But we do not need to (in fact,
        // cannot) emit an invoke targeting block inside a different LLVM function.
        if (catchLlvmBlock->getParent() != getCurrentLlvmFunction())
        {
            catchLlvmBlock = nullptr;
        }
    }

    llvm::CallBase* callInst;
    if (catchLlvmBlock != nullptr)
    {
        llvm::BasicBlock* nextLlvmBlock = createInlineLlvmBlock();

        callInst = _builder.CreateInvoke(callee, nextLlvmBlock, catchLlvmBlock, args);

        _builder.SetInsertPoint(nextLlvmBlock);
    }
    else
    {
        callInst = _builder.CreateCall(callee, args);
    }

    return callInst;
}

FunctionType* Llvm::getFunctionType()
{
    // TODO-LLVM: delete this when these signatures implemented
    if (_sigInfo.hasExplicitThis() || _sigInfo.hasTypeArg())
        failFunctionCompilation();

    std::vector<llvm::Type*> argVec(_llvmArgCount);
    llvm::Type*              retLlvmType;

    for (unsigned i = 0; i < _compiler->lvaCount; i++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(i);
        if (varDsc->lvIsParam)
        {
            assert(varDsc->lvLlvmArgNum != BAD_LLVM_ARG_NUM);
            argVec[varDsc->lvLlvmArgNum] = getLlvmTypeForLclVar(varDsc);
        }
    }

    retLlvmType = _retAddressLclNum == BAD_VAR_NUM
        ? getLlvmTypeForCorInfoType(_sigInfo.retType, _sigInfo.retTypeClass)
        : Type::getVoidTy(_llvmContext);

    return FunctionType::get(retLlvmType, ArrayRef<Type*>(argVec), false);
}

Function* Llvm::getOrCreateLlvmFunction(const char* symbolName, GenTreeCall* call)
{
    Function* llvmFunc = _module->getFunction(symbolName);

    if (llvmFunc == nullptr)
    {
        // assume ExternalLinkage, if the function is defined in the clrjit module, then it is replaced and an
        // extern added to the Ilc module
        llvmFunc =
            Function::Create(createFunctionTypeForCall(call), Function::ExternalLinkage, 0U, symbolName, _module);
    }
    return llvmFunc;
}

FunctionType* Llvm::createFunctionTypeForCall(GenTreeCall* call)
{
    llvm::Type* retLlvmType = getLlvmTypeForCorInfoType(call->gtCorInfoType, call->gtRetClsHnd);

    std::vector<llvm::Type*> argVec = std::vector<llvm::Type*>();

    for (GenTreeCall::Use& use : call->Args())
    {
        GenTreePutArgType* putArg = use.GetNode()->AsPutArgType();
        argVec.push_back(getLlvmTypeForCorInfoType(putArg->GetCorInfoType(), putArg->GetClsHnd()));
    }

    return FunctionType::get(retLlvmType, argVec, /* isVarArg */ false);
}

FunctionType* Llvm::createFunctionTypeForHelper(CorInfoHelpFunc helperFunc)
{
    const HelperFuncInfo& helperInfo = getHelperFuncInfo(helperFunc);

    std::vector<Type*> argVec = std::vector<Type*>();

    if (helperInfo.HasFlags(HFIF_SS_ARG))
    {
        argVec.push_back(Type::getInt8PtrTy(_llvmContext));
    }

    size_t sigArgCount = helperInfo.GetSigArgCount();
    for (size_t i = 0; i < sigArgCount; i++)
    {
        CorInfoType argType = helperInfo.GetSigArgType(i);
        assert(argType != TYP_STRUCT);

        argVec.push_back(getLlvmTypeForCorInfoType(argType, NO_CLASS_HANDLE));
    }

    Type* retLlvmType = getLlvmTypeForCorInfoType(helperInfo.GetSigReturnType(), NO_CLASS_HANDLE);
    FunctionType* llvmFuncType = FunctionType::get(retLlvmType, argVec, /* isVarArg */ false);

    return llvmFuncType;
}

llvm::GlobalVariable* Llvm::getOrCreateExternalSymbol(const char* symbolName)
{
    llvm::GlobalVariable* symbol = _module->getGlobalVariable(symbolName);
    if (symbol == nullptr)
    {
        Type* symbolLlvmType = getPtrLlvmType();
        symbol = new llvm::GlobalVariable(*_module, symbolLlvmType, false, llvm::GlobalValue::ExternalLinkage,
                                          nullptr, symbolName);
    }
    return symbol;
}

llvm::GlobalVariable* Llvm::getOrCreateSymbol(CORINFO_GENERIC_HANDLE symbolHandle)
{
    const char* symbolName = GetMangledSymbolName(symbolHandle);
    AddCodeReloc(symbolHandle);
    llvm::GlobalVariable* symbol = getOrCreateExternalSymbol(symbolName);

    return symbol;
}

CORINFO_GENERIC_HANDLE Llvm::getSymbolHandleForClassToken(mdToken token)
{
    // The importer call here relies on RyuJit not inlining EH (which it currently does not).
    CORINFO_RESOLVED_TOKEN resolvedToken;
    _compiler->impResolveToken((BYTE*)&token, &resolvedToken, CORINFO_TOKENKIND_Class);

    void* pIndirection = nullptr;
    CORINFO_CLASS_HANDLE typeSymbolHandle = _info.compCompHnd->embedClassHandle(resolvedToken.hClass, &pIndirection);
    assert(pIndirection == nullptr);

    return CORINFO_GENERIC_HANDLE(typeSymbolHandle);
}

Instruction* Llvm::getCast(Value* source, Type* targetType)
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
                return nullptr;
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

Value* Llvm::castIfNecessary(Value* source, Type* targetType, llvm::IRBuilder<>* builder)
{
    if (builder == nullptr)
    {
        builder = &_builder;
    }

    llvm::Instruction* castInst = getCast(source, targetType);
    if (castInst == nullptr)
        return source;

    return builder->Insert(castInst);
}

// We assume that all the GEPs are for elements of size Int8 (byte)
Value* Llvm::gepOrAddr(Value* addr, unsigned offset)
{
    if (offset == 0)
    {
        return addr;
    }

    return _builder.CreateGEP(Type::getInt8Ty(_llvmContext), addr, _builder.getInt32(offset));
}

Value* Llvm::getShadowStack()
{
    // Note that funclets have the shadow stack arg in the same position (0) as the main function.
    return getCurrentLlvmFunction()->getArg(0);
}

// Shadow stack moved up to avoid overwriting anything on the stack in the compiling method
Value* Llvm::getShadowStackForCallee()
{
    unsigned funcIdx = getCurrentLlvmFunctionIndex();
    unsigned hndIndex =
        (funcIdx == ROOT_FUNC_IDX) ? EHblkDsc::NO_ENCLOSING_INDEX : _compiler->funGetFunc(funcIdx)->funEHIndex;

    return gepOrAddr(getShadowStack(), getShadowFrameSize(hndIndex));
}

Value* Llvm::getOriginalShadowStack()
{
    if (getCurrentLlvmFunctionIndex() == ROOT_FUNC_IDX)
    {
        return getShadowStack();
    }

    // The original shadow stack pointer is the second funclet parameter.
    return getCurrentLlvmFunction()->getArg(1);
}

llvm::DILocation* Llvm::createDebugLocation(unsigned lineNo)
{
    assert(m_diFunction != nullptr);
    return llvm::DILocation::get(_llvmContext, lineNo, 0, m_diFunction);
}

llvm::DILocation* Llvm::getArtificialDebugLocation()
{
    if (m_diFunction == nullptr)
    {
        return nullptr;
    }

    // Line number "0" is used to represent non-user code in DWARF.
    return createDebugLocation(0);
}

llvm::BasicBlock* Llvm::getCurrentLlvmBlock() const
{
    return getCurrentLlvmBlocks()->LastBlock;
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

    _builder.SetInsertPoint(llvmBlocks->LastBlock);
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

        if (ehDsc->InFilterRegionBBRange(block))
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
    llvm::BasicBlock* inlineLlvmBlock = llvm::BasicBlock::Create(_llvmContext, "", llvmFunc, insertBefore);

#ifdef DEBUG
    llvm::StringRef blocksName = llvmBlocks->FirstBlock->getName();
    if (llvmBlocks->Count == 1)
    {
        llvmBlocks->FirstBlock->setName(blocksName + ".1");
    }
    else
    {
        blocksName = blocksName.take_front(blocksName.find_last_of('.'));
    }

    inlineLlvmBlock->setName(blocksName + "." + llvm::Twine(++llvmBlocks->Count));
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
        llvm::BasicBlock* llvmBlock = llvm::BasicBlock::Create(_llvmContext, BBNAME("BB", block->bbNum), llvmFunc);

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
        prologLlvmBlock = llvm::BasicBlock::Create(_llvmContext, PROLOG_BLOCK_NAME, llvmFunc, firstLlvmUserBlock);

        // Eagerly insert jump to the user block to simplify calling code.
        llvm::BranchInst::Create(firstLlvmUserBlock, prologLlvmBlock);
    }

    return prologLlvmBlock;
}

//------------------------------------------------------------------------
// isReachable: Does this block have an immediate dominator?
//
// Arguments:
//    block - The block to check
//
// Return Value:
//    Whether "block" has an immediate dominator, i. e. is statically
//    reachable, not the first block, and not a throw helper block.
//
bool Llvm::isReachable(BasicBlock* block) const
{
    return block->bbIDom != nullptr;
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
// to be tracked and have its address taken (but not exposed!). Such locals
// are rare, and it is not cheap to indentify their set precisely before
// the code has been generated. We therefore use a lazy strategy for their
// materialization in the funclet prologs.
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
    assert(varDsc->lvTracked); // Untracked locals in functions with funclets live on the shadow frame.

    unsigned funcIdx = getCurrentLlvmFunctionIndex();
    assert(funcIdx != ROOT_FUNC_IDX); // The root's prolog is generated eagerly.
    assert(!VarSetOps::IsMember(_compiler, getFirstBlockForFunction(funcIdx)->bbLiveIn, varDsc->lvVarIndex));

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
