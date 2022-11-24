// ================================================================================================================
// |                                            LLVM-based codegen                                                |
// ================================================================================================================

#include "llvm.h"

//------------------------------------------------------------------------
// Compile: Compile IR to LLVM, adding to the LLVM Module
//
void Llvm::Compile()
{
    const char* mangledName = GetMangledMethodName(_info.compMethodHnd);
    _function = _module->getFunction(mangledName);
    _debugFunction = nullptr;
    _debugMetadata.diCompileUnit = nullptr;

    if (_function == nullptr)
    {
        _function = Function::Create(getFunctionType(), Function::ExternalLinkage, 0U, mangledName,
                                     _module); // TODO: ExternalLinkage forced as linked from old module
    }

    // mono does this via Javascript (pal_random.js), but prefer not to introduce that dependency as it limits the ability to run out of the browser.
    // Copy the temporary workaround from the IL->LLVM generator for now.
    if (!strcmp(mangledName, "S_P_CoreLib_Interop__GetRandomBytes"))
    {
        // this would normally fill the buffer parameter, but we'll just leave the buffer as is and that will be our "random" data for now
        llvm::BasicBlock* llvmBlock = llvm::BasicBlock::Create(_llvmContext, "", _function);
        _builder.SetInsertPoint(llvmBlock);
        _builder.CreateRetVoid();
        return;
    }

    // TODO-LLVM: enable. Currently broken because RyuJit inserts RPI helpers for RPI methods, then we
    // also create an RPI wrapper stub, resulting in a double transition.
    if (_compiler->opts.IsReversePInvoke())
    {
        failFunctionCompilation();
    }

    if (_compiler->opts.compDbgInfo)
    {
        const char* documentFileName = GetDocumentFileName();
        if (documentFileName && *documentFileName != '\0')
        {
            _debugMetadata = getOrCreateDebugMetadata(documentFileName);
        }
    }

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
            // TODO-LLVM: finret basic blocks
            if (block->bbJumpKind == BBJ_EHFINALLYRET)
            {
                m_llvm->failFunctionCompilation();
            }

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

    fillPhis();

    if (_debugFunction != nullptr)
    {
        _diBuilder->finalizeSubprogram(_debugFunction);
    }

#if DEBUG
    JITDUMP("\n===================================================================================================================\n");
    JITDUMP("LLVM IR for %s after codegen:\n", _compiler->info.compFullName);
    JITDUMP("-------------------------------------------------------------------------------------------------------------------\n\n");
    JITDUMPEXEC(_function->dump());

    if (llvm::verifyFunction(*_function, &llvm::errs()))
    {
        printf("function failed %s\n", mangledName);
    }
#endif
}

void Llvm::generateProlog()
{
    JITDUMP("\n=============== Generating prolog:\n");

    llvm::BasicBlock* prologBlock = llvm::BasicBlock::Create(_llvmContext, "Prolog", _function);
    _prologBuilder.SetInsertPoint(prologBlock);

    initializeLocals();

    llvm::BasicBlock* block0 = getFirstLlvmBlockForBlock(_compiler->fgFirstBB);
    _prologBuilder.SetInsertPoint(_prologBuilder.CreateBr(block0));
    _builder.SetInsertPoint(block0);
}

void Llvm::initializeLocals()
{
    m_allocas = std::vector<Value*>(_compiler->lvaCount, nullptr);

    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        if (varDsc->lvRefCnt() == 0)
        {
            continue;
        }

        // Needed because of "implicitly referenced" locals.
        if (!canStoreLocalOnLlvmStack(varDsc))
        {
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
            initValue = _function->getArg(varDsc->lvLlvmArgNum);
        }
        else
        {
            // If the local is in SSA, things are somewhat simple: we must provide an initial value if there is an
            // "implicit" def, and must not if there is not.
            if (_compiler->lvaInSsa(lclNum))
            {
                // Needed because of "implicitly referenced" locals.
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
            initValue = _prologBuilder.CreateFreeze(initValue);
            JITDUMPEXEC(initValue->dump());
        }

        assert(initValue->getType() == lclLlvmType);

        if (isLlvmFrameLocal(varDsc))
        {
            Instruction* allocaInst = _prologBuilder.CreateAlloca(lclLlvmType);
            m_allocas[lclNum] = allocaInst;
            JITDUMPEXEC(allocaInst->dump());

            Instruction* storeInst = _prologBuilder.CreateStore(initValue, allocaInst);
            JITDUMPEXEC(storeInst->dump());
        }
        else
        {
            assert(_compiler->lvaInSsa(lclNum));
            _localsMap.Set({lclNum, SsaConfig::FIRST_SSA_NUM}, initValue);
        }
    }
}

void Llvm::generateBlock(BasicBlock* block)
{
    JITDUMP("\n=============== Generating ");
    JITDUMPEXEC(block->dspBlockHeader(_compiler, /* showKind */ true, /* showFlags */ true));

    llvm::BasicBlock* llvmBlock = getFirstLlvmBlockForBlock(block);

    _currentBlock = block;
    _builder.SetInsertPoint(llvmBlock);

    for (GenTree* node : LIR::AsRange(block))
    {
        startImportingNode();
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
        default:
            // TODO-LLVM: other jump kinds.
            break;
    }

    llvm::BasicBlock* lastLlvmBlock = _builder.GetInsertBlock();
    if (lastLlvmBlock != llvmBlock)
    {
        setLastLlvmBlockForBlock(block, lastLlvmBlock);

#ifdef DEBUG
        llvm::StringRef blockName = llvmBlock->getName();
        for (unsigned idx = 1; llvmBlock != lastLlvmBlock->getNextNode(); llvmBlock = llvmBlock->getNextNode(), idx++)
        {
            llvmBlock->setName(blockName + "." + llvm::Twine(idx));
        }
#endif // DEBUG
    }
}

void Llvm::fillPhis()
{
    for (PhiPair phiPair : _phiPairs)
    {
        llvm::PHINode* llvmPhiNode = phiPair.llvmPhiNode;

        for (GenTreePhi::Use& use : phiPair.irPhiNode->Uses())
        {
            GenTreePhiArg* phiArg = use.GetNode()->AsPhiArg();
            unsigned lclNum = phiArg->GetLclNum();
            unsigned ssaNum = phiArg->GetSsaNum();
            llvm::BasicBlock* llvmPredBlock = getLastLlvmBlockForBlock(phiArg->gtPredBB);

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

            llvmPhiNode->addIncoming(phiRealArgValue, llvmPredBlock);
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
//    The normalized value, of "targetLlvmType" type. If the latter wasn't
//    provided, the raw value is returned, except for small types, which
//    are still extended to INT.
//
Value* Llvm::consumeValue(GenTree* node, Type* targetLlvmType)
{
    Value* nodeValue = getGenTreeValue(node);
    Value* finalValue = nodeValue;

    if (targetLlvmType == nullptr)
    {
        if (!nodeValue->getType()->isIntegerTy())
        {
            return finalValue;
        }

        targetLlvmType = getLlvmTypeForVarType(genActualType(node));
    }

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

        // i32* e.g symbols, to i8*
        if (nodeValue->getType()->isPointerTy() && targetLlvmType->isPointerTy())
        {
            return _builder.CreateBitCast(nodeValue, targetLlvmType);
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

void Llvm::startImportingNode()
{
    if (_debugMetadata.diCompileUnit != nullptr && _currentOffsetDiLocation == nullptr)
    {
        unsigned int lineNo = GetOffsetLineNumber(_currentOffset.GetLocation().GetOffset());

        _currentOffsetDiLocation = createDebugFunctionAndDiLocation(_debugMetadata, lineNo);
        _builder.SetCurrentDebugLocation(_currentOffsetDiLocation);
    }
}

void Llvm::visitNode(GenTree* node)
{
#ifdef DEBUG
    JITDUMPEXEC(_compiler->gtDispLIRNode(node, "Generating: "));
    auto lastInstrIter = --_builder.GetInsertPoint();
    llvm::BasicBlock* lastLlvmBlock = _builder.GetInsertBlock(); // For instructions spanning multiple blocks.
#endif // DEBUG

    switch (node->OperGet())
    {
        case GT_ADD:
            buildAdd(node->AsOp());
            break;
        case GT_DIV:
        case GT_MOD:
        case GT_UDIV:
        case GT_UMOD:
            buildDivMod(node);
            break;
        case GT_CALL:
            buildCall(node);
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
            buildCnsInt(node);
            break;
        case GT_CNS_LNG:
            buildCnsLng(node);
            break;
        case GT_IL_OFFSET:
            _currentOffset = node->AsILOffset()->gtStmtDI;
            _currentOffsetDiLocation = nullptr;
            break;
        case GT_IND:
            buildInd(node->AsIndir());
            break;
        case GT_JTRUE:
            buildJTrue(node, getGenTreeValue(node->AsOp()->gtOp1));
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
        case GT_AND:
        case GT_OR:
        case GT_XOR:
            buildBinaryOperation(node);
            break;
        case GT_FIELD_LIST:
        case GT_INIT_VAL:
            // These ('contained') nodes aways generate code as part of their parent.
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

    if (isLlvmFrameLocal(varDsc))
    {
        llvmRef = _builder.CreateLoad(m_allocas[lclNum]);
    }
    else
    {
        llvmRef = _localsMap[{lclNum, ssaNum}];
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

    if (isLlvmFrameLocal(varDsc))
    {
        _builder.CreateStore(localValue, m_allocas[lclNum]);
    }
    else
    {
        _localsMap.Set({lclNum, lclVar->GetSsaNum()}, localValue);
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

    unsigned   lclNum = lclFld->GetLclNum();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    assert(isLlvmFrameLocal(varDsc));

    // TODO-LLVM: if this is an only value type field, or at offset 0, we can optimize.
    Value* structAddrValue = m_allocas[lclNum];
    Value* structAddrInt8Ptr = castIfNecessary(structAddrValue, Type::getInt8PtrTy(_llvmContext));
    Value* fieldAddressValue = gepOrAddr(structAddrInt8Ptr, lclFld->GetLclOffs());
    Value* fieldAddressTypedValue =
        castIfNecessary(fieldAddressValue, getLlvmTypeForVarType(lclFld->TypeGet())->getPointerTo());

    mapGenTreeToValue(lclFld, _builder.CreateLoad(fieldAddressTypedValue));
}

void Llvm::buildLocalVarAddr(GenTreeLclVarCommon* lclAddr)
{
    unsigned int lclNum = lclAddr->GetLclNum();
    if (lclAddr->isLclField())
    {
        Value* bytePtr = castIfNecessary(m_allocas[lclNum], Type::getInt8PtrTy(_llvmContext));
        mapGenTreeToValue(lclAddr, gepOrAddr(bytePtr, lclAddr->GetLclOffs()));
    }
    else
    {
        mapGenTreeToValue(lclAddr, m_allocas[lclNum]);
    }
}

void Llvm::buildAdd(GenTreeOp* node)
{
    Value* op1Value = consumeValue(node->gtGetOp1());
    Value* op2Value = consumeValue(node->gtGetOp2());
    Type* op1Type = op1Value->getType();
    Type* op2Type = op2Value->getType();

    Value* addValue;
    if (op1Type->isPointerTy() && op2Type->isIntegerTy())
    {
        // GEPs scale indices, bitcasting to i8* makes them equivalent to the raw offsets we have in IR
        addValue = _builder.CreateGEP(castIfNecessary(op1Value, Type::getInt8PtrTy(_llvmContext)), op2Value);
    }
    else if (op1Type->isIntegerTy() && (op1Type == op2Type))
    {
        addValue = _builder.CreateAdd(op1Value, op2Value);
    }
    else
    {
        // unsupported add type combination
        failFunctionCompilation();
    }

    mapGenTreeToValue(node, addValue);
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
                    failFunctionCompilation(); // NYI
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
            failFunctionCompilation(); // NYI
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
    Value* op1Value = consumeValue(node->gtGetOp1());
    Value* op2Value = consumeValue(node->gtGetOp2());
    Type* op1Type = op1Value->getType();
    Type* op2Type = op2Value->getType();
    if (op1Type != op2Type)
    {
        assert((op1Type->isPointerTy() && op2Type->isIntegerTy()) ||
               (op1Type->isIntegerTy() && op2Type->isPointerTy()));
        if (op1Type->isPointerTy())
        {
            op2Value = _builder.CreateIntToPtr(op2Value, op1Type);
        }
        else
        {
            op1Value = _builder.CreateIntToPtr(op1Value, op2Type);
        }
    }

    mapGenTreeToValue(node, _builder.CreateCmp(predicate, op1Value, op2Value));
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

void Llvm::buildCnsInt(GenTree* node)
{
    if (node->gtType == TYP_INT)
    {
        if (node->IsIconHandle())
        {
            // TODO-LLVM : consider lowering these to "IND(CLS_VAR_ADDR)"
            if (node->IsIconHandle(GTF_ICON_TOKEN_HDL) || node->IsIconHandle(GTF_ICON_CLASS_HDL) ||
                node->IsIconHandle(GTF_ICON_METHOD_HDL) || node->IsIconHandle(GTF_ICON_FIELD_HDL))
            {
                const char* symbolName = GetMangledSymbolName((void*)(node->AsIntCon()->IconValue()));
                AddCodeReloc((void*)node->AsIntCon()->IconValue());
                mapGenTreeToValue(node, _builder.CreateLoad(getOrCreateExternalSymbol(symbolName)));
            }
            else
            {
                //TODO-LLVML: other ICON handle types
                failFunctionCompilation();
            }
        }
        else
        {
            mapGenTreeToValue(node, _builder.getInt32(node->AsIntCon()->IconValue()));
        }
        return;
    }
    if (node->gtType == TYP_REF)
    {
        ssize_t intCon = node->AsIntCon()->gtIconVal;
        if (node->IsIconHandle(GTF_ICON_STR_HDL))
        {
            const char* symbolName = GetMangledSymbolName((void *)(node->AsIntCon()->IconValue()));
            AddCodeReloc((void*)node->AsIntCon()->IconValue());
            mapGenTreeToValue(node, _builder.CreateLoad(getOrCreateExternalSymbol(symbolName)));
            return;
        }
        // TODO: delete this check, just handling string constants and null ptr stores for now, other TYP_REFs not implemented yet
        if (intCon != 0)
        {
            failFunctionCompilation();
        }

        mapGenTreeToValue(node, _builder.CreateIntToPtr(_builder.getInt32(intCon), Type::getInt8PtrTy(_llvmContext))); // TODO: wasm64
        return;
    }
    failFunctionCompilation();
}

void Llvm::buildCnsLng(GenTree* node)
{
    mapGenTreeToValue(node, _builder.getInt64(node->AsLngCon()->LngValue()));
}

void Llvm::buildCall(GenTree* node)
{
    GenTreeCall* call = node->AsCall();
    if (call->IsHelperCall())
    {
        buildHelperFuncCall(call);
    }
    else
    {
        if (call->IsVirtualStub())
        {
            // TODO-LLVM: VSD.
            failFunctionCompilation();
        }

        buildUserFuncCall(call);
    }
}

void Llvm::buildHelperFuncCall(GenTreeCall* call)
{
    if (call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_GENERIC_HANDLE) ||
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE) ||
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_GVMLOOKUP_FOR_SLOT) || /* generates an extra parameter in the signature */
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE) || /* misses an arg in the signature somewhere, not the shadow stack */
        call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_DELEGATE_CTOR))
    {
        // TODO-LLVM: implement.
        failFunctionCompilation();
    }

    Value* callValue;
    if (call->gtCallMethHnd == _compiler->eeFindHelper(CORINFO_HELP_READYTORUN_STATIC_BASE))
    {
        const char* symbolName = GetMangledSymbolName(CORINFO_METHOD_HANDLE(call->gtEntryPoint.handle));
        Function* llvmFunc = _module->getFunction(symbolName);
        if (llvmFunc == nullptr)
        {
            llvmFunc = Function::Create(buildHelperLlvmFunctionType(call, true), Function::ExternalLinkage, 0U, symbolName, _module); // TODO: ExternalLinkage forced as defined in ILC module
        }

        // Replacement for _info.compCompHnd->recordRelocation(nullptr, gtCall->gtEntryPoint.handle, IMAGE_REL_BASED_REL32);
        AddCodeReloc(call->gtEntryPoint.handle);

        callValue = _builder.CreateCall(llvmFunc, getShadowStackForCallee());
    }
    else
    {
        fgArgInfo* argInfo = call->fgArgInfo;
        unsigned int argCount = argInfo->ArgCount();
        fgArgTabEntry** argTable = argInfo->ArgTable();
        std::vector<OperandArgNum> sortedArgs = std::vector<OperandArgNum>(argCount);
        OperandArgNum* sortedData = sortedArgs.data();

        //TODO-LLVM: refactor calling code with user calls.
        for (unsigned i = 0; i < argCount; i++)
        {
            fgArgTabEntry* curArgTabEntry = argTable[i];
            unsigned int   argNum = curArgTabEntry->argNum;
            OperandArgNum  opAndArg = { argNum, curArgTabEntry->GetNode() };
            sortedData[argNum] = opAndArg;
        }

        CorInfoHelpFunc helperNum = _compiler->eeGetHelperNum(call->gtCallMethHnd);
        void* pAddr = nullptr;
        void* addr = _compiler->compGetHelperFtn(helperNum, &pAddr);
        const char* symbolName = GetMangledSymbolName(addr);
        Function* llvmFunc = _module->getFunction(symbolName);

        bool requiresShadowStack = helperRequiresShadowStack(helperNum);
        if (llvmFunc == nullptr)
        {
            llvmFunc = Function::Create(buildHelperLlvmFunctionType(call, requiresShadowStack), Function::ExternalLinkage, 0U, symbolName, _module);
        }

        AddCodeReloc(addr);

        std::vector<llvm::Value*> argVec;
        unsigned argIx = 0;

        Value* shadowStackForCallee = getShadowStackForCallee();
        if (requiresShadowStack)
        {
            argVec.push_back(shadowStackForCallee);
            argIx++;
        }
        else
        {
            // we may come back into managed from the unmanaged call so store the shadowstack
            _builder.CreateStore(shadowStackForCallee, getOrCreateExternalSymbol("t_pShadowStackTop", Type::getInt8PtrTy(_llvmContext)));
        }

        for (OperandArgNum opAndArg : sortedArgs)
        {
            argVec.push_back(consumeValue(opAndArg.operand, llvmFunc->getArg(argIx)->getType()));
            argIx++;
        }

        callValue = emitCallOrInvoke(llvmFunc, argVec);
    }

    mapGenTreeToValue(call, callValue);
}

void Llvm::buildUserFuncCall(GenTreeCall* call)
{
    assert(!call->IsHelperCall()); // Either "USER_FUNC" or "INDIRECT".

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
        const char* symbolName = GetMangledSymbolName(call->gtEntryPoint.handle);
        AddCodeReloc(call->gtEntryPoint.handle);

        llvmFuncCallee = getOrCreateLlvmFunction(symbolName, call);
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

    if (llvmType->isStructTy())
    {
        Value* alloca = _builder.CreateAlloca(llvmType);
        Value* allocaAsBytePtr = _builder.CreatePointerCast(alloca, Type::getInt8PtrTy(_llvmContext));

        for (GenTreeFieldList::Use& use : fieldList->Uses())
        {
            Value* fieldAddr = gepOrAddr(allocaAsBytePtr, use.GetOffset());
            Type*  fieldType = getLlvmTypeForVarType(use.GetType());
            fieldAddr        = castIfNecessary(fieldAddr, fieldType->getPointerTo());
            _builder.CreateStore(consumeValue(use.GetNode(), fieldType), fieldAddr);
        }

        return _builder.CreateLoad(alloca);
    }

    // single primitive type wrapped in struct
    assert(fieldList->Uses().begin()->GetNext() == nullptr);

    return consumeValue(fieldList->Uses().begin()->GetNode(), llvmType);
}

void Llvm::buildInd(GenTreeIndir* indNode)
{
    Type* loadLlvmType = getLlvmTypeForVarType(indNode->TypeGet());
    Value* addrValue = consumeValue(indNode->Addr(), loadLlvmType->getPointerTo());

    emitNullCheckForIndir(indNode, addrValue);
    Value* loadValue = _builder.CreateLoad(loadLlvmType, addrValue);

    mapGenTreeToValue(indNode, loadValue);
}

void Llvm::buildBlk(GenTreeBlk* blkNode)
{
    Type* blkLlvmType = getLlvmTypeForStruct(blkNode->GetLayout());
    Value* addrValue = consumeValue(blkNode->Addr(), blkLlvmType->getPointerTo());

    emitNullCheckForIndir(blkNode, addrValue);
    Value* blkValue = _builder.CreateLoad(blkLlvmType, addrValue);

    mapGenTreeToValue(blkNode, blkValue);
}

void Llvm::buildStoreInd(GenTreeStoreInd* storeIndOp)
{
    GCInfo::WriteBarrierForm wbf = getGCInfo()->gcIsWriteBarrierCandidate(storeIndOp, storeIndOp->Data());

    Type* storeLlvmType = getLlvmTypeForVarType(storeIndOp->TypeGet());
    Type* addrLlvmType = (wbf == GCInfo::WBF_NoBarrier) ? storeLlvmType->getPointerTo() : Type::getInt8PtrTy(_llvmContext);
    Value* addrValue = consumeValue(storeIndOp->Addr(), addrLlvmType);;
    Value* dataValue = consumeValue(storeIndOp->Data(), storeLlvmType);

    emitNullCheckForIndir(storeIndOp, addrValue);

    switch (wbf)
    {
        case GCInfo::WBF_BarrierUnchecked:
            _builder.CreateCall(getOrCreateRhpAssignRef(), {addrValue, dataValue});
            break;

        case GCInfo::WBF_BarrierChecked:
        case GCInfo::WBF_BarrierUnknown:
            _builder.CreateCall(getOrCreateRhpCheckedAssignRef(), {addrValue, dataValue});
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
        _builder.CreateStore(dataValue, castIfNecessary(addrValue, dataValue->getType()->getPointerTo()));
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
    Value* op1 = consumeValue(node->gtGetOp1(), targetType);
    Value* op2 = consumeValue(node->gtGetOp2(), targetType);

    switch (node->OperGet())
    {
        case GT_AND:
            result = _builder.CreateAnd(op1, op2, "and");
            break;
        case GT_OR:
            result = _builder.CreateOr(op1, op2, "or");
            break;
        case GT_XOR:
            result = _builder.CreateXor(op1, op2, "xor");
            break;
        default:
            failFunctionCompilation();  // TODO-LLVM: other binary operaions
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
    if (node->TypeIs(TYP_VOID))
    {
        _builder.CreateRetVoid();
        return;
    }

    GenTree* retValNode = node->gtGetOp1();
    Type* retLlvmType = getLlvmTypeForCorInfoType(_sigInfo.retType, _sigInfo.retTypeClass);
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

void Llvm::buildJTrue(GenTree* node, Value* opValue)
{
    _builder.CreateCondBr(opValue, getFirstLlvmBlockForBlock(_currentBlock->bbJumpDest), getFirstLlvmBlockForBlock(_currentBlock->bbNext));
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
            if (fieldDesc->isGcPointer())
            {
                // we can't be sure the address is on the heap, it could be the result of pointer arithmetic on a local var
                _builder.CreateCall(getOrCreateRhpCheckedAssignRef(),
                                    ArrayRef<Value*>{address,
                                    castIfNecessary(fieldData, Type::getInt8PtrTy(_llvmContext))});
                bytesStored += TARGET_POINTER_SIZE;
            }
            else
            {
                _builder.CreateStore(fieldData, castIfNecessary(address, fieldData->getType()->getPointerTo()));

                bytesStored += fieldData->getType()->getPrimitiveSizeInBits() / BITS_PER_BYTE;
            }
        }
    }

    unsigned llvmStructSize = data->getType()->getPrimitiveSizeInBits() / BITS_PER_BYTE;
    if (structDesc->hasSignificantPadding() && llvmStructSize > bytesStored)
    {
        Value* srcAddress = _builder.CreateGEP(baseAddress, _builder.getInt32(bytesStored));

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
        // For code with throw helper blocks, find and use the shared helper block for raising the exception.
        unsigned throwIndex = _compiler->bbThrowIndex(_currentBlock);
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
        // TODO-LLVM: actually emit the throw helper.
        _builder.CreateUnreachable();

        _builder.SetInsertPoint(nextLlvmBlock);

        failFunctionCompilation();
    }
}

void Llvm::emitNullCheckForIndir(GenTreeIndir* indir, Value* addrValue)
{
    if ((indir->gtFlags & GTF_IND_NONFAULTING) == 0)
    {
        Function* throwIfNullFunc = getOrCreateThrowIfNullFunction();
        addrValue = castIfNecessary(addrValue, Type::getInt8PtrTy(_llvmContext));

        // TODO-LLVM: this shadow stack passing is not efficient.
        emitCallOrInvoke(throwIfNullFunc, {getShadowStackForCallee(), addrValue});
    }
}

void Llvm::buildThrowException(llvm::IRBuilder<>& builder, const char* helperClass, const char* helperMethodName, Value* shadowStack)
{
    CORINFO_METHOD_HANDLE methodHandle = GetCompilerHelpersMethodHandle(helperClass, helperMethodName);
    const char* mangledName = GetMangledMethodName(methodHandle);

    Function* llvmFunc = _module->getFunction(mangledName);

    if (llvmFunc == nullptr)
    {
        // assume ExternalLinkage, if the function is defined in the clrjit module, then it is replaced and an
        // extern added to the Ilc module
        llvmFunc = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext), {Type::getInt8PtrTy(_llvmContext)},
                                                      false),
                                    Function::ExternalLinkage, 0U, mangledName, _module);
       AddCodeReloc(methodHandle);
    }

    builder.CreateCall(llvmFunc, {shadowStack});
    builder.CreateUnreachable();
}

Value* Llvm::emitCallOrInvoke(llvm::FunctionCallee callee, ArrayRef<Value*> args)
{
    // TODO-LLVM: invoke if callsite has exception handler
    return _builder.CreateCall(callee, args);
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

    return FunctionType::get(retLlvmType, ArrayRef<Type*>(argVec), false);
}

FunctionType* Llvm::buildHelperLlvmFunctionType(GenTreeCall* call, bool withShadowStack)
{
    Type* retLlvmType = getLlvmTypeForVarType(call->TypeGet());
    std::vector<llvm::Type*> argVec;

    if (withShadowStack)
    {
        argVec.push_back(Type::getInt8PtrTy(_llvmContext));
    }

    for (GenTreeCall::Use& use : call->Args())
    {
        Type* argLlvmType = getLlvmTypeForVarType(use.GetNode()->TypeGet());
        argVec.push_back(argLlvmType);
    }

    return FunctionType::get(retLlvmType, ArrayRef<llvm::Type*>(argVec), false);
}

bool Llvm::helperRequiresShadowStack(CorInfoHelpFunc helperFunc)
{
    // TODO-LLVM: communicate this through a Jit-EE API.
    // Current version of the mappings taken primarily from "tools\aot\ILCompiler.Compiler\Compiler\JitHelper.cs" and
    // "tools\aot\ILCompiler.RyuJit\JitInterface\CorInfoImpl.RyuJit.cs".
    switch (helperFunc)
    {
        case CORINFO_HELP_DIV:
        case CORINFO_HELP_MOD:
        case CORINFO_HELP_UDIV:
        case CORINFO_HELP_UMOD:
        case CORINFO_HELP_LMUL_OVF:
        case CORINFO_HELP_ULMUL_OVF:
        case CORINFO_HELP_LDIV:
        case CORINFO_HELP_LMOD:
        case CORINFO_HELP_ULDIV:
        case CORINFO_HELP_ULMOD:
        case CORINFO_HELP_DBL2INT_OVF:
        case CORINFO_HELP_DBL2LNG_OVF:
        case CORINFO_HELP_DBL2UINT_OVF:
        case CORINFO_HELP_DBL2ULNG_OVF:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\MathHelpers.cs".
            return true;

        case CORINFO_HELP_LLSH:
        case CORINFO_HELP_LRSH:
        case CORINFO_HELP_LRSZ:
        case CORINFO_HELP_LMUL:
        case CORINFO_HELP_LNG2DBL:
        case CORINFO_HELP_ULNG2DBL:
        case CORINFO_HELP_DBL2INT:
        case CORINFO_HELP_DBL2LNG:
        case CORINFO_HELP_DBL2UINT:
        case CORINFO_HELP_DBL2ULNG:
        case CORINFO_HELP_FLTREM:
        case CORINFO_HELP_DBLREM:
        case CORINFO_HELP_FLTROUND:
        case CORINFO_HELP_DBLROUND:
            // Implemented in "Runtime\MathHelpers.cpp".
            return false;

        case CORINFO_HELP_NEWFAST:
        case CORINFO_HELP_NEWSFAST:
        case CORINFO_HELP_NEWSFAST_FINALIZE:
        case CORINFO_HELP_NEWSFAST_ALIGN8:
        case CORINFO_HELP_NEWSFAST_ALIGN8_VC:
        case CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE:
        case CORINFO_HELP_NEW_MDARR:
        case CORINFO_HELP_NEWARR_1_DIRECT:
        case CORINFO_HELP_NEWARR_1_OBJ:
        case CORINFO_HELP_NEWARR_1_VC:
        case CORINFO_HELP_NEWARR_1_ALIGN8:
            // Allocators, implemented in "Runtime\portable.cpp".
            return false;

        case CORINFO_HELP_STRCNS:
        case CORINFO_HELP_STRCNS_CURRENT_MODULE:
        case CORINFO_HELP_INITCLASS:
        case CORINFO_HELP_INITINSTCLASS:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_ISINSTANCEOFINTERFACE:
        case CORINFO_HELP_ISINSTANCEOFARRAY:
        case CORINFO_HELP_ISINSTANCEOFCLASS:
        case CORINFO_HELP_ISINSTANCEOFANY:
        case CORINFO_HELP_CHKCASTINTERFACE:
        case CORINFO_HELP_CHKCASTARRAY:
        case CORINFO_HELP_CHKCASTCLASS:
        case CORINFO_HELP_CHKCASTANY:
        case CORINFO_HELP_CHKCASTCLASS_SPECIAL:
        case CORINFO_HELP_BOX:
        case CORINFO_HELP_BOX_NULLABLE:
        case CORINFO_HELP_UNBOX:
        case CORINFO_HELP_UNBOX_NULLABLE:
        case CORINFO_HELP_ARRADDR_ST:
        case CORINFO_HELP_LDELEMA_REF:
            // Runtime exports, i. e. implemented in managed code with an unmanaged signature.
            // See "Runtime.Base\src\System\Runtime\RuntimeExports.cs", "Runtime.Base\src\System\Runtime\TypeCast.cs",
            return false;

        case CORINFO_HELP_GETREFANY:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
            return true;

        case CORINFO_HELP_THROW:
        case CORINFO_HELP_RETHROW:
            // For WASM, currently implemented in the bootstrapper...
            return false;

        case CORINFO_HELP_USER_BREAKPOINT:
            // Implemented in "Runtime\MiscHelpers.cpp".
            return false;

        case CORINFO_HELP_RNGCHKFAIL:
        case CORINFO_HELP_OVERFLOW:
        case CORINFO_HELP_THROWDIVZERO:
        case CORINFO_HELP_THROWNULLREF:
            // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
            // Note on "CORINFO_HELP_THROWNULLREF": ***this helpers has been deleted upstream***.
            // We need it. When merging upstream, revert its deletion!
            return true;

        case CORINFO_HELP_VERIFICATION:
            // Verification is in the process of being deleted from RyuJit.
            unreached();

        case CORINFO_HELP_FAIL_FAST:
            // Implemented in "Runtime\EHHelpers.cpp".
            return false;

        case CORINFO_HELP_METHOD_ACCESS_EXCEPTION:
        case CORINFO_HELP_FIELD_ACCESS_EXCEPTION:
        case CORINFO_HELP_CLASS_ACCESS_EXCEPTION:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_ENDCATCH:
            // Not used with funclet-based EH.
            unreached();

        case CORINFO_HELP_MON_ENTER:
        case CORINFO_HELP_MON_EXIT:
        case CORINFO_HELP_MON_ENTER_STATIC:
        case CORINFO_HELP_MON_EXIT_STATIC:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\SynchronizedMethodHelpers.cs".
            return true;

        case CORINFO_HELP_GETCLASSFROMMETHODPARAM:
        case CORINFO_HELP_GETSYNCFROMCLASSHANDLE:
        case CORINFO_HELP_STOP_FOR_GC:
            // Apparently NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_POLL_GC:
            // Implemented in "Runtime\portable.cpp".
            return false;

        case CORINFO_HELP_STRESS_GC:
        case CORINFO_HELP_CHECK_OBJ:
            // Debug-only helpers NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_ASSIGN_REF:
        case CORINFO_HELP_CHECKED_ASSIGN_REF:
        case CORINFO_HELP_ASSIGN_REF_ENSURE_NONHEAP:
        case CORINFO_HELP_ASSIGN_BYREF:
            // Write barriers, implemented in "Runtime\portable.cpp".
            return false;

        case CORINFO_HELP_ASSIGN_STRUCT:
        case CORINFO_HELP_GETFIELD8:
        case CORINFO_HELP_SETFIELD8:
        case CORINFO_HELP_GETFIELD16:
        case CORINFO_HELP_SETFIELD16:
        case CORINFO_HELP_GETFIELD32:
        case CORINFO_HELP_SETFIELD32:
        case CORINFO_HELP_GETFIELD64:
        case CORINFO_HELP_SETFIELD64:
        case CORINFO_HELP_GETFIELDOBJ:
        case CORINFO_HELP_SETFIELDOBJ:
        case CORINFO_HELP_GETFIELDSTRUCT:
        case CORINFO_HELP_SETFIELDSTRUCT:
        case CORINFO_HELP_GETFIELDFLOAT:
        case CORINFO_HELP_SETFIELDFLOAT:
        case CORINFO_HELP_GETFIELDDOUBLE:
        case CORINFO_HELP_SETFIELDDOUBLE:
        case CORINFO_HELP_GETFIELDADDR:
        case CORINFO_HELP_GETSTATICFIELDADDR_TLS:
        case CORINFO_HELP_GETGENERICS_GCSTATIC_BASE:
        case CORINFO_HELP_GETGENERICS_NONGCSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_GCSTATIC_BASE_DYNAMICCLASS:
        case CORINFO_HELP_GETSHARED_NONGCSTATIC_BASE_DYNAMICCLASS:
        case CORINFO_HELP_CLASSINIT_SHARED_DYNAMICCLASS:
        case CORINFO_HELP_GETGENERICS_GCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETGENERICS_NONGCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE:
        case CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_NOCTOR:
        case CORINFO_HELP_GETSHARED_GCTHREADSTATIC_BASE_DYNAMICCLASS:
        case CORINFO_HELP_GETSHARED_NONGCTHREADSTATIC_BASE_DYNAMICCLASS:
            // Not used in NativeAOT (or at all in some cases).
            unreached();

        case CORINFO_HELP_DBG_IS_JUST_MY_CODE:
        case CORINFO_HELP_PROF_FCN_ENTER:
        case CORINFO_HELP_PROF_FCN_LEAVE:
        case CORINFO_HELP_PROF_FCN_TAILCALL:
        case CORINFO_HELP_BBT_FCN_ENTER:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_PINVOKE_CALLI:
            // TODO-LLVM: this is not a real "helper"; investigate what needs to be done to enable it.
            failFunctionCompilation();

        case CORINFO_HELP_TAILCALL:
            // NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_GETCURRENTMANAGEDTHREADID:
            // Implemented as "Environment.CurrentManagedThreadId".
            return true;

        case CORINFO_HELP_INIT_PINVOKE_FRAME:
            // Part of the inlined PInvoke frame construction feature which is NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_MEMSET:
        case CORINFO_HELP_MEMCPY:
            // Implemented as plain "memset"/"memcpy".
            return false;

        case CORINFO_HELP_RUNTIMEHANDLE_METHOD:
        case CORINFO_HELP_RUNTIMEHANDLE_METHOD_LOG:
        case CORINFO_HELP_RUNTIMEHANDLE_CLASS:
        case CORINFO_HELP_RUNTIMEHANDLE_CLASS_LOG:
            // Not used in NativeAOT.
            unreached();

        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE:
        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
            return true;

        case CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD:
        case CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD:
        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\LdTokenHelpers.cs".
            return true;

        case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL:
            // Implemented in "CoreLib\src\Internal\Runtime\CompilerHelpers\TypedReferenceHelpers.cs".
            return true;

        case CORINFO_HELP_ARE_TYPES_EQUIVALENT:
            // Another runtime export from "TypeCast.cs".
            return false;

        case CORINFO_HELP_VIRTUAL_FUNC_PTR:
        case CORINFO_HELP_READYTORUN_NEW:
        case CORINFO_HELP_READYTORUN_NEWARR_1:
            // Not used in NativeAOT.
            unreached();

        case CORINFO_HELP_READYTORUN_ISINSTANCEOF:
        case CORINFO_HELP_READYTORUN_CHKCAST:
        case CORINFO_HELP_READYTORUN_STATIC_BASE:
        case CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR:
        case CORINFO_HELP_READYTORUN_GENERIC_HANDLE:
        case CORINFO_HELP_READYTORUN_DELEGATE_CTOR:
        case CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE:
            // Not static methods; currently we "inline" them for LLVM.
            // Should be "unreached" once all are handled.
            failFunctionCompilation();

        case CORINFO_HELP_EE_PRESTUB:
        case CORINFO_HELP_EE_PRECODE_FIXUP:
        case CORINFO_HELP_EE_PINVOKE_FIXUP:
        case CORINFO_HELP_EE_VSD_FIXUP:
        case CORINFO_HELP_EE_EXTERNAL_FIXUP:
        case CORINFO_HELP_EE_VTABLE_FIXUP:
        case CORINFO_HELP_EE_REMOTING_THUNK:
        case CORINFO_HELP_EE_PERSONALITY_ROUTINE:
        case CORINFO_HELP_EE_PERSONALITY_ROUTINE_FILTER_FUNCLET:
            // NGEN/R2R-specific marker helpers.
            unreached();

        case CORINFO_HELP_ASSIGN_REF_EAX:
        case CORINFO_HELP_ASSIGN_REF_EBX:
        case CORINFO_HELP_ASSIGN_REF_ECX:
        case CORINFO_HELP_ASSIGN_REF_ESI:
        case CORINFO_HELP_ASSIGN_REF_EDI:
        case CORINFO_HELP_ASSIGN_REF_EBP:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EAX:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EBX:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_ECX:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_ESI:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EDI:
        case CORINFO_HELP_CHECKED_ASSIGN_REF_EBP:
            // x86-specific write barriers.
            unreached();

        case CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR:
        case CORINFO_HELP_DEBUG_LOG_LOOP_CLONING:
            // Debug-only functionality NYI in NativeAOT.
            unreached();

        case CORINFO_HELP_THROW_ARGUMENTEXCEPTION:
        case CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION:
        case CORINFO_HELP_THROW_NOT_IMPLEMENTED:
        case CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED:
            // Implemented in "Runtime.Base\src\System\ThrowHelpers.cs".
            return true;

        case CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED:
            // Dead code.
            unreached();

        case CORINFO_HELP_JIT_PINVOKE_BEGIN:
        case CORINFO_HELP_JIT_PINVOKE_END:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT:
        case CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS:
            // [R]PI helpers, implemented in "Runtime\thread.cpp".
            return false;

        case CORINFO_HELP_GVMLOOKUP_FOR_SLOT:
            // TODO-LLVM: fix.
            failFunctionCompilation();

        case CORINFO_HELP_STACK_PROBE:
        case CORINFO_HELP_PATCHPOINT:
        case CORINFO_HELP_CLASSPROFILE32:
        case CORINFO_HELP_CLASSPROFILE64:
        case CORINFO_HELP_PARTIAL_COMPILATION_PATCHPOINT:
            unreached();

        default:
            // Add new helpers to the above as necessary.
            unreached();
    }
}

Value* Llvm::getOrCreateExternalSymbol(const char* symbolName, Type* symbolType)
{
    if (symbolType == nullptr)
    {
        symbolType = Type::getInt32PtrTy(_llvmContext);
    }

    Value* symbol = _module->getGlobalVariable(symbolName);
    if (symbol == nullptr)
    {
        symbol = new llvm::GlobalVariable(*_module, symbolType, false, llvm::GlobalValue::LinkageTypes::ExternalLinkage, (llvm::Constant*)nullptr, symbolName);
    }
    return symbol;
}

Function* Llvm::getOrCreateRhpAssignRef()
{
    Function* llvmFunc = _module->getFunction("RhpAssignRef");
    if (llvmFunc == nullptr)
    {
        llvmFunc = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext),
                                                      {Type::getInt8PtrTy(_llvmContext), Type::getInt8PtrTy(_llvmContext)},
                                                      /* isVarArg */ false),
                                    Function::ExternalLinkage, 0U, "RhpAssignRef",
                                    _module); // TODO: ExternalLinkage forced as linked from old module
    }
    return llvmFunc;
}

Function* Llvm::getOrCreateRhpCheckedAssignRef()
{
    Function* llvmFunc = _module->getFunction("RhpCheckedAssignRef");
    if (llvmFunc == nullptr)
    {
        llvmFunc = Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext),
                                                      {Type::getInt8PtrTy(_llvmContext), Type::getInt8PtrTy(_llvmContext)},
                                                      /* isVarArg */ false),
                                    Function::ExternalLinkage, 0U, "RhpCheckedAssignRef",
                                    _module); // TODO: ExternalLinkage forced as linked from old module
    }
    return llvmFunc;
}

Function* Llvm::getOrCreateThrowIfNullFunction()
{
    const char* funcName = "nativeaot.throwifnull";
    Function* llvmFunc = _module->getFunction(funcName);
    if (llvmFunc == nullptr)
    {
        llvmFunc =
            Function::Create(FunctionType::get(Type::getVoidTy(_llvmContext),
                                               {Type::getInt8PtrTy(_llvmContext), Type::getInt8PtrTy(_llvmContext)},
                                               /* isVarArg */ false),
                             Function::InternalLinkage, 0U, funcName, _module);

        llvm::IRBuilder<> builder(_llvmContext);
        llvm::BasicBlock* block = llvm::BasicBlock::Create(_llvmContext, "Block", llvmFunc);
        llvm::BasicBlock* throwBlock = llvm::BasicBlock::Create(_llvmContext, "ThrowBlock", llvmFunc);
        llvm::BasicBlock* retBlock = llvm::BasicBlock::Create(_llvmContext, "RetBlock", llvmFunc);

        builder.SetInsertPoint(block);

        builder.CreateCondBr(builder.CreateICmp(llvm::CmpInst::Predicate::ICMP_EQ, llvmFunc->getArg(1),
                                                llvm::ConstantPointerNull::get(Type::getInt8PtrTy(_llvmContext)),
                                                "nullCheck"), throwBlock, retBlock);
        builder.SetInsertPoint(throwBlock);

        buildThrowException(builder, u8"ThrowHelpers", u8"ThrowNullReferenceException", llvmFunc->getArg(0));

        builder.SetInsertPoint(retBlock);
        builder.CreateRetVoid();
    }

    return llvmFunc;
}

llvm::Instruction* Llvm::getCast(llvm::Value* source, Type* targetType)
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
                return new llvm::BitCastInst(source, targetType, "CastPtrToPtr");
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

llvm::Value* Llvm::gepOrAddr(Value* addr, unsigned offset)
{
    if (offset == 0)
    {
        return addr;
    }

    return _builder.CreateGEP(addr, _builder.getInt32(offset));
}

// shadow stack moved up to avoid overwriting anything on the stack in the compiling method
llvm::Value* Llvm::getShadowStackForCallee()
{
    unsigned int offset = getTotalLocalOffset();

    return gepOrAddr(_function->getArg(0), offset);
}

DebugMetadata Llvm::getOrCreateDebugMetadata(const char* documentFileName)
{
    std::string fullPath = documentFileName;
    DebugMetadata debugMetadata;
    if (!_debugMetadataMap.Lookup(fullPath, &debugMetadata))
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

        llvm::DICompileUnit* compileUnit =
            _diBuilder->createCompileUnit(llvm::dwarf::DW_LANG_C /* no dotnet choices in the enum */, fileMetadata,
                                          "ILC", _compiler->opts.OptimizationEnabled(), "", 1, "",
                                          llvm::DICompileUnit::DebugEmissionKind::FullDebug, 0, 0, 0,
                                          llvm::DICompileUnit::DebugNameTableKind::Default, false, "");

        debugMetadata = {fileMetadata, compileUnit};
        _debugMetadataMap.Set(fullPath, debugMetadata);
    }

    return debugMetadata;
}

llvm::DILocation* Llvm::createDebugFunctionAndDiLocation(DebugMetadata debugMetadata, unsigned int lineNo)
{
    if (_debugFunction == nullptr)
    {
        llvm::DISubroutineType* functionMetaType = _diBuilder->createSubroutineType({} /* TODO - function parameter types*/, llvm::DINode::DIFlags::FlagZero);
        uint32_t lineNumber = FirstSequencePointLineNumber();

        const char* methodName = _info.compCompHnd->getMethodName(_info.compMethodHnd, nullptr);
        _debugFunction = _diBuilder->createFunction(debugMetadata.fileMetadata, methodName,
                                                    methodName, debugMetadata.fileMetadata, lineNumber,
                                                    functionMetaType, lineNumber, llvm::DINode::DIFlags::FlagZero,
                                                    llvm::DISubprogram::DISPFlags::SPFlagDefinition |
                                                    llvm::DISubprogram::DISPFlags::SPFlagLocalToUnit);
        _function->setSubprogram(_debugFunction);
    }
    return llvm::DILocation::get(_llvmContext, lineNo, 0, _debugFunction);
}

llvm::BasicBlock* Llvm::createInlineLlvmBlock()
{
    llvm::BasicBlock* inlineLlvmBlock =
        llvm::BasicBlock::Create(_llvmContext, "", _function, _builder.GetInsertBlock()->getNextNode());

    return inlineLlvmBlock;
}

llvm::BasicBlock* Llvm::getFirstLlvmBlockForBlock(BasicBlock* block)
{
    assert(block != nullptr);

    llvm::BasicBlock* llvmBlock;
    LlvmBlockRange llvmBlockRange;
    if (!_blkToLlvmBlksMap.Lookup(block, &llvmBlockRange))
    {
        unsigned bbNum = block->bbNum;
        llvmBlock = llvm::BasicBlock::Create(
            _llvmContext, (bbNum >= 10) ? ("BB" + llvm::Twine(bbNum)) : ("BB0" + llvm::Twine(bbNum)), _function);

        _blkToLlvmBlksMap.Set(block, {llvmBlock, llvmBlock});
    }
    else
    {
        llvmBlock = llvmBlockRange.FirstBlock;
    }

    return llvmBlock;
}

//------------------------------------------------------------------------
// getLastLlvmBlockForBlock: Get the last LLVM basic block for "block".
//
// During code generation, a given IR block can be split into multiple
// LLVM blocks, due to, e. g., inline branches. This function returns
// the last of these generated blocks. Note it is only available after
// "block" has been fully generated.
//
// Arguments:
//    block - The IR block
//
// Return Value:
//    LLVM block containing "block"'s terminator instruction.
//
llvm::BasicBlock* Llvm::getLastLlvmBlockForBlock(BasicBlock* block)
{
    return _blkToLlvmBlksMap[block].LastBlock;
}

void Llvm::setLastLlvmBlockForBlock(BasicBlock* block, llvm::BasicBlock* llvmBlock)
{
    _blkToLlvmBlksMap[block].LastBlock = llvmBlock;
}

bool Llvm::isLlvmFrameLocal(LclVarDsc* varDsc)
{
    assert(canStoreLocalOnLlvmStack(varDsc) && (_compiler->fgSsaPassesCompleted >= 1));
    return !varDsc->lvInSsa && varDsc->lvRefCnt() > 0;
}

unsigned int Llvm::getTotalLocalOffset()
{
    assert((_shadowStackLocalsSize % TARGET_POINTER_SIZE) == 0);
    return _shadowStackLocalsSize;
}
