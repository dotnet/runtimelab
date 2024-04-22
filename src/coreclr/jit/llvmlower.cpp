// ================================================================================================================
// |                                       Lowering for the LLVM backend                                          |
// ================================================================================================================

#include "llvm.h"

void Llvm::AddUnhandledExceptionHandler()
{
    if (!_compiler->opts.IsReversePInvoke())
    {
        return;
    }

    BasicBlock* firstTryBlock = _compiler->fgFirstBB;
    BasicBlock* lastTryBlock = _compiler->fgLastBB;

    // Make sure the first block is not in a protected region to uphold the invariant that no
    // two such regions share the first block.
    if (firstTryBlock->hasTryIndex())
    {
        _compiler->fgEnsureFirstBBisScratch();
        firstTryBlock = _compiler->fgFirstBBScratch;
    }
    _compiler->fgFirstBBScratch = nullptr;

    // Create a block for the filter and filter handler. The handler part is unreachable, but
    // we need it for the EH table to be well-formed.
    BasicBlock* filterBlock = _compiler->fgNewBBafter(BBJ_THROW, lastTryBlock, false);
    BasicBlock* handlerBlock = _compiler->fgNewBBafter(BBJ_THROW, filterBlock, false);

    // Add the new EH region at the end, since it is the least nested, and thus should be last.
    unsigned newEhIndex = _compiler->compHndBBtabCount;
    EHblkDsc* newEhDsc = _compiler->fgAddEHTableEntry(newEhIndex);

    // Initialize the new entry.
    newEhDsc->ebdHandlerType = EH_HANDLER_FILTER;
    newEhDsc->ebdTryBeg = firstTryBlock;
    newEhDsc->ebdTryLast = lastTryBlock;
    newEhDsc->ebdFilter = filterBlock;
    newEhDsc->ebdHndBeg = handlerBlock;
    newEhDsc->ebdHndLast = handlerBlock;

    newEhDsc->ebdEnclosingTryIndex = EHblkDsc::NO_ENCLOSING_INDEX;
    newEhDsc->ebdEnclosingHndIndex = EHblkDsc::NO_ENCLOSING_INDEX;

    newEhDsc->ebdTryBegOffset = firstTryBlock->bbCodeOffs;
    newEhDsc->ebdTryEndOffset = lastTryBlock->bbCodeOffsEnd;
    newEhDsc->ebdFilterBegOffset = 0; // Filter doesn't correspond to any IL.
    newEhDsc->ebdHndBegOffset = 0; // Handler doesn't correspond to any IL.
    newEhDsc->ebdHndEndOffset = 0; // Handler doesn't correspond to any IL.

    // Set some flags on the new region. This is the same as when we set up
    // EH regions in fgFindBasicBlocks(). Note that the try has no enclosing
    // handler, and the filter with filter handler have no enclosing try.
    firstTryBlock->SetFlags(BBF_DONT_REMOVE | BBF_IMPORTED);
    firstTryBlock->setTryIndex(newEhIndex);
    firstTryBlock->clearHndIndex();

    filterBlock->SetFlags(BBF_DONT_REMOVE | BBF_IMPORTED);
    filterBlock->bbCatchTyp = BBCT_FILTER;
    filterBlock->clearTryIndex();
    filterBlock->setHndIndex(newEhIndex);

    handlerBlock->SetFlags(BBF_DONT_REMOVE | BBF_IMPORTED);
    handlerBlock->bbCatchTyp = BBCT_FILTER_HANDLER;
    handlerBlock->clearTryIndex();
    handlerBlock->setHndIndex(newEhIndex);

    // Walk the user code blocks and set all blocks that don't already have a try handler
    // to point to the new try handler.
    for (BasicBlock* block : _compiler->Blocks(firstTryBlock, lastTryBlock))
    {
        if (!block->hasTryIndex())
        {
            block->setTryIndex(newEhIndex);
        }
    }

    // Walk the EH table. Make every EH entry that doesn't already have an enclosing try
    // index mark this new entry as their enclosing try index.
    for (unsigned ehIndex = 0; ehIndex < newEhIndex; ehIndex++)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
        if (ehDsc->ebdEnclosingTryIndex == EHblkDsc::NO_ENCLOSING_INDEX)
        {
            // This EH region wasn't previously nested, but now it is.
            ehDsc->ebdEnclosingTryIndex = static_cast<unsigned short>(newEhIndex);
        }
    }

    GenTree* catchArg = new (_compiler, GT_CATCH_ARG) GenTree(GT_CATCH_ARG, TYP_REF);
    catchArg->gtFlags |= GTF_ORDER_SIDEEFF;

    GenTree* handlerCall = _compiler->gtNewHelperCallNode(CORINFO_HELP_LLVM_EH_UNHANDLED_EXCEPTION, TYP_VOID, catchArg);
    Statement* handlerStmt = _compiler->gtNewStmt(handlerCall);
    _compiler->fgInsertStmtAtEnd(filterBlock, handlerStmt);

#ifdef DEBUG
    if (_compiler->verbose)
    {
        printf("ReversePInvoke method - created additional EH descriptor EH#%u for the unhandled exception filter\n",
               newEhIndex);
        _compiler->fgDispBasicBlocks();
        _compiler->fgDispHandlerTab();
    }

    _compiler->fgVerifyHandlerTab();
#endif // DEBUG
}

void Llvm::Lower()
{
    initializeFunclets();
    initializeLlvmArgInfo();
    lowerBlocks();
    lowerDissolveDependentlyPromotedLocals();
    lowerCanonicalizeFirstBlock();
}

void Llvm::initializeFunclets()
{
    if (!_compiler->ehAnyFunclets())
    {
        return;
    }

    // Our exception handling only needs a subset of handlers to be true funclets.
    unsigned funcIdx = 1;
    for (unsigned ehIndex = _compiler->compHndBBtabCount - 1; ehIndex != -1; ehIndex--)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
        if (ehDsc->HasFilter())
        {
            // Filters are funclets because they must be invokable by the first pass.
            ehDsc->ebdFilterFuncIndex = funcIdx++;
            FuncInfoDsc* funcInfo = _compiler->funGetFunc(ehDsc->ebdFilterFuncIndex);
            funcInfo->funKind = FUNC_FILTER;
            funcInfo->funEHIndex = ehIndex;

            m_anyFilterFunclets = true;
        }

        if (ehDsc->HasFinallyHandler())
        {
            // Finallys are funclets because they have multiple (EH and normal) entries and exits.
            ehDsc->ebdFuncIndex = funcIdx++;
            FuncInfoDsc* funcInfo = _compiler->funGetFunc(ehDsc->ebdFuncIndex);
            funcInfo->funKind = FUNC_HANDLER;
            funcInfo->funEHIndex = ehIndex;
        }
        // It is up to us to decide what "ebdFuncIndex" should mean for handlers that
        // are not funclets. It is convenient to make it refer to the enclosing funclet.
        else if (ehDsc->ebdEnclosingHndIndex != EHblkDsc::NO_ENCLOSING_INDEX)
        {
            ehDsc->ebdFuncIndex = _compiler->ehGetDsc(ehDsc->ebdEnclosingHndIndex)->ebdFuncIndex;
        }
        else
        {
            ehDsc->ebdFuncIndex = ROOT_FUNC_IDX;
        }
    }

    _compiler->compFuncInfoCount = funcIdx;
}

// LLVM Arg layout:
//    - Shadow stack (if required)
//    - This pointer (if required)
//    - Return buffer (if required)
//    - Generic context (if required)
//    - Rest of the args passed as LLVM parameters
//
void Llvm::initializeLlvmArgInfo()
{
    if (m_anyFilterFunclets)
    {
        _originalShadowStackLclNum = _compiler->lvaGrabTemp(true DEBUGARG("original shadowstack"));
        LclVarDsc* originalShadowStackVarDsc = _compiler->lvaGetDesc(_originalShadowStackLclNum);
        originalShadowStackVarDsc->lvType = TYP_I_IMPL;
        originalShadowStackVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
    }

    unsigned nextLlvmArgNum = 0;
    bool isManagedAbi = !_compiler->opts.IsReversePInvoke();

    _shadowStackLclNum = _compiler->lvaGrabTempWithImplicitUse(true DEBUGARG("shadowstack"));
    LclVarDsc* shadowStackVarDsc = _compiler->lvaGetDesc(_shadowStackLclNum);
    shadowStackVarDsc->lvType = TYP_I_IMPL;
    shadowStackVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
    if (isManagedAbi)
    {
        shadowStackVarDsc->lvLlvmArgNum = nextLlvmArgNum++;
        shadowStackVarDsc->lvIsParam = true;
    }

    if (m_info->compThisArg != BAD_VAR_NUM)
    {
        LclVarDsc* thisVarDsc = _compiler->lvaGetDesc(m_info->compThisArg);
        thisVarDsc->lvCorInfoType = toCorInfoType(thisVarDsc->TypeGet());
    }

    if (m_info->compRetBuffArg != BAD_VAR_NUM)
    {
        // The return buffer is always pinned in our calling convetion, so that we can pass it as an LLVM argument.
        LclVarDsc* retBufVarDsc = _compiler->lvaGetDesc(m_info->compRetBuffArg);
        assert(retBufVarDsc->TypeGet() == TYP_BYREF);
        retBufVarDsc->lvType = TYP_I_IMPL;
        retBufVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
    }

    if (m_info->compTypeCtxtArg != BAD_VAR_NUM)
    {
        _compiler->lvaGetDesc(m_info->compTypeCtxtArg)->lvCorInfoType = CORINFO_TYPE_PTR;
    }

    for (unsigned lclNum = 0; lclNum < m_info->compArgsCount; lclNum++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        if (_compiler->lvaIsImplicitByRefLocal(lclNum))
        {
            // Implicit byrefs in our calling convention always point to the stack.
            assert(varDsc->TypeGet() == TYP_BYREF);
            varDsc->lvType = TYP_I_IMPL;
            varDsc->lvCorInfoType = CORINFO_TYPE_PTR;
        }

        varDsc->lvLlvmArgNum = nextLlvmArgNum++;
    }

    _llvmArgCount = nextLlvmArgNum;
}

void Llvm::lowerBlocks()
{
    for (BasicBlock* block : _compiler->Blocks())
    {
        lowerBlock(block);
    }
}

void Llvm::lowerBlock(BasicBlock* block)
{
    LIR::Range& blockRange = LIR::AsRange(block);

    // Explicit void-returning tailcalls omit the importation of GT_RETURN. Compensate for this quirk.
    if (block->KindIs(BBJ_RETURN) && (blockRange.IsEmpty() || !blockRange.LastNode()->OperIs(GT_RETURN)))
    {
        noway_assert(_compiler->info.compRetType == TYP_VOID);
        blockRange.InsertAtEnd(new (_compiler, GT_RETURN) GenTreeOp(GT_RETURN, TYP_VOID));
    }

    lowerRange(block, blockRange);
}

void Llvm::lowerRange(BasicBlock* block, LIR::Range& range)
{
    m_currentBlock = block;
    m_currentRange = &range;

    for (GenTree* node : range)
    {
        lowerNode(node);
    }

    INDEBUG(range.CheckLIR(_compiler, /* checkUnusedValues */ true));

    m_currentBlock = nullptr;
    m_currentRange = nullptr;
}

void Llvm::lowerNode(GenTree* node)
{
    switch (node->OperGet())
    {
        case GT_LCL_VAR:
        case GT_LCL_FLD:
        case GT_LCL_ADDR:
        case GT_STORE_LCL_VAR:
        case GT_STORE_LCL_FLD:
            lowerLocal(node->AsLclVarCommon());
            break;

        case GT_CALL:
            lowerCall(node->AsCall());
            break;

        case GT_XAND:
        case GT_XORR:
        case GT_XADD:
        case GT_XCHG:
        case GT_CMPXCHG:
        case GT_IND:
        case GT_BLK:
        case GT_NULLCHECK:
        case GT_STOREIND:
            lowerIndir(node->AsIndir());
            break;

        case GT_STORE_BLK:
            lowerStoreBlk(node->AsBlk());
            break;

        case GT_STORE_DYN_BLK:
            lowerStoreDynBlk(node->AsStoreDynBlk());
            break;

        case GT_ARR_LENGTH:
        case GT_MDARR_LENGTH:
        case GT_MDARR_LOWER_BOUND:
            lowerArrLength(node->AsArrCommon());
            break;

        case GT_RETURN:
            lowerReturn(node->AsUnOp());
            break;

        default:
            break;
    }
}

void Llvm::lowerLocal(GenTreeLclVarCommon* node)
{
    lowerFieldOfDependentlyPromotedStruct(node);

    if (node->OperIs(GT_STORE_LCL_VAR))
    {
        lowerStoreLcl(node->AsLclVarCommon());
    }

    if (node->TypeIs(TYP_STRUCT) && node->OperIsLocalStore())
    {
        GenTree* value = node->Data();
        if (genActualTypeIsInt(value))
        {
            value->SetContained();
        }
        else if (node->OperIs(GT_STORE_LCL_FLD))
        {
            // "STORE_LCL_VAR"s already normalized by "lowerStoreLcl".
            node->AsLclFld()->SetLayout(value->GetLayout(_compiler));
        }
    }

    if (node->OperIsLocalField() || node->OperIs(GT_LCL_ADDR))
    {
        // Indicates that this local is to live on the LLVM frame, and will not participate in SSA.
        _compiler->lvaGetDesc(node)->lvHasLocalAddr = 1;
    }
}

void Llvm::lowerStoreLcl(GenTreeLclVarCommon* storeLclNode)
{
    assert(storeLclNode->OperIs(GT_STORE_LCL_VAR));
    unsigned lclNum = storeLclNode->GetLclNum();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    GenTree* data = storeLclNode->gtGetOp1();

    unsigned convertToStoreLclFldLclNum = BAD_VAR_NUM;
    if (varDsc->CanBeReplacedWithItsField(_compiler))
    {
        convertToStoreLclFldLclNum = varDsc->lvFieldLclStart;
    }
    else if (storeLclNode->TypeIs(TYP_STRUCT))
    {
        if (data->TypeIs(TYP_STRUCT))
        {
            LIR::Use dataUse(CurrentRange(), &storeLclNode->gtOp1, storeLclNode);
            data = normalizeStructUse(dataUse, varDsc->GetLayout());
        }
        else if (data->OperIsInitVal())
        {
            // We need the local's address to create a memset.
            convertToStoreLclFldLclNum = lclNum;
        }
    }

    if (convertToStoreLclFldLclNum != BAD_VAR_NUM)
    {
        storeLclNode->SetOper(GT_STORE_LCL_FLD);
        storeLclNode->SetLclNum(convertToStoreLclFldLclNum);
        storeLclNode->AsLclFld()->SetLclOffs(0);
        storeLclNode->AsLclFld()->SetLayout(varDsc->GetLayout());

        if (storeLclNode->IsPartialLclFld(_compiler))
        {
            storeLclNode->gtFlags |= GTF_VAR_USEASG;
        }
    }
}

void Llvm::lowerFieldOfDependentlyPromotedStruct(GenTree* node)
{
    if (node->OperIsLocal() || node->OperIs(GT_LCL_ADDR))
    {
        GenTreeLclVarCommon* lclVar = node->AsLclVarCommon();
        uint16_t             offset = lclVar->GetLclOffs();
        LclVarDsc*           varDsc = _compiler->lvaGetDesc(lclVar->GetLclNum());

        if (_compiler->lvaIsFieldOfDependentlyPromotedStruct(varDsc))
        {
            switch (node->OperGet())
            {
                case GT_LCL_VAR:
                    lclVar->SetOper(GT_LCL_FLD);
                    break;

                case GT_STORE_LCL_VAR:
                    lclVar->SetOper(GT_STORE_LCL_FLD);
                    if (lclVar->IsPartialLclFld(_compiler))
                    {
                        lclVar->gtFlags |= GTF_VAR_USEASG;
                    }
                    break;
            }

            lclVar->SetLclNum(varDsc->lvParentLcl);
            lclVar->AsLclFld()->SetLclOffs(varDsc->lvFldOffset + offset);

            if ((node->gtFlags & GTF_VAR_DEF) != 0)
            {
                if (lclVar->IsPartialLclFld(_compiler))
                {
                    node->gtFlags |= GTF_VAR_USEASG;
                }
            }
        }
    }
}

void Llvm::lowerCall(GenTreeCall* callNode)
{
    // TODO-LLVM-CQ: enable fast shadow tail calls. Requires correct ABI handling.
    assert(!callNode->IsTailCall());

    if (callNode->IsHelperCall(_compiler, CORINFO_HELP_RETHROW))
    {
        lowerRethrow(callNode);
    }
    // "gtFoldExprConst" can attach a superflous argument to the overflow helper. Remove it.
    else if (callNode->IsHelperCall(_compiler, CORINFO_HELP_OVERFLOW) && !callNode->gtArgs.IsEmpty())
    {
        // TODO-LLVM: fix upstream to not attach this argument.
        CallArg* arg = callNode->gtArgs.GetArgByIndex(0);
        CurrentRange().Remove(arg->GetNode());
        callNode->gtArgs.Remove(arg);
    }

    // Doing this early simplifies code below.
    callNode->gtArgs.MoveLateToEarly();

    if (callNode->NeedsNullCheck() || callNode->IsVirtualStub())
    {
        // Virtual stub calls: our stubs don't handle null "this", as we presume doing
        // the check here has better chances for its elimination as redundant (by LLVM).
        insertNullCheckForCall(callNode);
    }

    if (callNode->IsVirtualStub())
    {
        lowerVirtualStubCall(callNode);
    }
    else if (callNode->IsDelegateInvoke())
    {
        lowerDelegateInvoke(callNode);
    }

    lowerCallReturn(callNode);
    lowerCallToShadowStack(callNode);

    if (callNode->IsUnmanaged())
    {
        lowerUnmanagedCall(callNode);
    }

    // If there is a no return, or always throw call, delete the dead code so we can add unreachable
    // statement immediately, and not after any dead RET.
    if (_compiler->fgIsThrow(callNode) || callNode->IsNoReturn())
    {
        while (CurrentRange().LastNode() != callNode)
        {
            CurrentRange().Remove(CurrentRange().LastNode(), /* markOperandsUnused */ true);
        }

        if (!CurrentBlock()->KindIs(BBJ_THROW))
        {
            _compiler->fgConvertBBToThrowBB(CurrentBlock());
        }
    }
}

void Llvm::lowerRethrow(GenTreeCall* callNode)
{
    assert(callNode->IsHelperCall(_compiler, CORINFO_HELP_RETHROW));

    // Language in ECMA 335 I.12.4.2.8.2.2 clearly states that rethrows nested inside finallys are
    // legal, however, neither C# nor the old verification system allow this. CoreCLR behavior was
    // not tested. Implementing this is possible, but for now we punt.
    EHblkDsc* ehDsc = _compiler->ehGetDsc(CurrentBlock()->getHndIndex());
    if (!ehDsc->HasCatchHandler())
    {
        IMPL_LIMITATION("Nested rethrow");
    }

    // A rethrow is a special throw that preserves the stack trace. Our helper we use for rethrow has
    // the equivalent of a managed signature "void (object)", i. e. takes the caught exception object
    // explicitly. Add it here, before the general call lowering.
    assert(callNode->gtArgs.IsEmpty());

    // By IR invariants, CATCH_ARG must either be the first node in a handler, or not present at all.
    BasicBlock* catchArgBlock = ehDsc->ebdHndBeg;
    LIR::Range& catchArgRange = LIR::AsRange(catchArgBlock);
    GenTree* nonPhiNode = catchArgRange.FirstNonPhiNode();
    GenTree* catchArgNode;
    if ((nonPhiNode == nullptr) || !nonPhiNode->OperIs(GT_CATCH_ARG))
    {
#ifdef DEBUG
        for (GenTree* node : catchArgRange)
        {
            assert(!node->OperIs(GT_CATCH_ARG));
        }
#endif // DEBUG

        catchArgNode = new (_compiler, GT_CATCH_ARG) GenTree(GT_CATCH_ARG, TYP_REF);
        catchArgNode->gtFlags |= GTF_ORDER_SIDEEFF;
        catchArgNode->SetUnusedValue();
        catchArgRange.InsertBefore(nonPhiNode, catchArgNode);
    }
    else
    {
        catchArgNode = nonPhiNode;
    }

    LIR::Use use;
    GenTree* excObj;
    bool isUsedAlready = catchArgRange.TryGetUse(catchArgNode, &use);
    if (!isUsedAlready && (catchArgBlock == CurrentBlock()))
    {
        excObj = catchArgNode;
    }
    else
    {
        unsigned catchArgLclNum = _compiler->lvaGrabTemp(true DEBUGARG("exception object for rethrow"));
        if (isUsedAlready)
        {
            use.ReplaceWithLclVar(_compiler, catchArgLclNum);
        }
        else
        {
            GenTree* store = _compiler->gtNewTempStore(catchArgLclNum, catchArgNode);
            catchArgRange.InsertAfter(catchArgNode, store);
        }

        excObj = _compiler->gtNewLclVarNode(catchArgLclNum);
        CurrentRange().InsertBefore(callNode, excObj);
    }

    catchArgNode->ClearUnusedValue();
    callNode->gtArgs.PushFront(_compiler, NewCallArg::Primitive(excObj, CORINFO_TYPE_CLASS));
}

void Llvm::lowerIndir(GenTreeIndir* indirNode)
{
    lowerAddressToAddressMode(indirNode);
}

void Llvm::lowerStoreBlk(GenTreeBlk* storeBlkNode)
{
    assert(storeBlkNode->OperIs(GT_STORE_BLK));

    GenTree* src = storeBlkNode->Data();

    if (storeBlkNode->OperIsCopyBlkOp())
    {
        storeBlkNode->SetLayout(src->GetLayout(_compiler));
    }
    else
    {
        src->SetContained();
    }

    lowerIndir(storeBlkNode);
}

void Llvm::lowerStoreDynBlk(GenTreeStoreDynBlk* storeDynBlkNode)
{
    storeDynBlkNode->Data()->SetContained();
    lowerIndir(storeDynBlkNode);
}

// TODO-LLVM: Almost a direct copy from lower.cpp which is not included for Wasm.
//------------------------------------------------------------------------
// LowerArrLength: lower an array length
//
// Arguments:
//    node - the array length node we are lowering.
//
// Notes:
//    If base array is nullptr, this effectively
//    turns into a nullcheck.
//
void Llvm::lowerArrLength(GenTreeArrCommon* node)
{
    GenTree* const arr       = node->ArrRef();
    int            lenOffset = 0;

    switch (node->OperGet())
    {
        case GT_ARR_LENGTH:
        {
            lenOffset = node->AsArrLen()->ArrLenOffset();
            noway_assert(lenOffset == OFFSETOF__CORINFO_Array__length ||
                         lenOffset == OFFSETOF__CORINFO_String__stringLen);
            break;
        }

        case GT_MDARR_LENGTH:
            lenOffset = (int)_compiler->eeGetMDArrayLengthOffset(node->AsMDArr()->Rank(), node->AsMDArr()->Dim());
            break;

        case GT_MDARR_LOWER_BOUND:
            lenOffset = (int)_compiler->eeGetMDArrayLowerBoundOffset(node->AsMDArr()->Rank(), node->AsMDArr()->Dim());
            break;

        default:
            unreached();
    }

    // Create the expression `*(array_addr + lenOffset)`

    GenTree* addr;
    noway_assert(arr->gtNext == node);

    if ((arr->gtOper == GT_CNS_INT) && (arr->AsIntCon()->gtIconVal == 0))
    {
        // If the array is NULL, then we should get a NULL reference
        // exception when computing its length.  We need to maintain
        // an invariant where there is no sum of two constants node, so
        // let's simply return an indirection of NULL.

        addr = arr;
    }
    else
    {
        // TODO-LLVM-CQ: use LEAs here.
        GenTree* con = _compiler->gtNewIconNode(lenOffset, TYP_I_IMPL);
        addr         = _compiler->gtNewOperNode(GT_ADD, TYP_BYREF, arr, con);

        CurrentRange().InsertAfter(arr, con, addr);
    }

    // Change to a GT_IND.
    node->ChangeOper(GT_IND);
    GenTreeIndir* indirNode = node->AsIndir();
    indirNode->Addr()       = addr;
    lowerIndir(indirNode);
}

void Llvm::lowerReturn(GenTreeUnOp* retNode)
{
    m_anyReturns = true;

    if (retNode->TypeIs(TYP_VOID))
    {
        // Nothing to do.
        return;
    }

    GenTree* retVal = retNode->gtGetOp1();
    LIR::Use retValUse(CurrentRange(), &retNode->gtOp1, retNode);
    ClassLayout* layout =
        retNode->TypeIs(TYP_STRUCT) ? _compiler->typGetObjLayout(m_info->compMethodInfo->args.retTypeClass) : nullptr;
    if (retNode->TypeIs(TYP_STRUCT) && retVal->TypeIs(TYP_STRUCT))
    {
        normalizeStructUse(retValUse, layout);
    }

    // Morph can create pretty much any type mismatch here (struct <-> primitive, primitive <-> struct, etc).
    // Fix these by spilling to a temporary (we could do better but it is not worth it, upstream will get rid
    // of the important cases). Exclude zero-init-ed structs (codegen supports them directly).
    bool isStructZero = retNode->TypeIs(TYP_STRUCT) && retVal->IsIntegralConst(0);
    if ((retNode->TypeGet() != genActualType(retVal)) && !isStructZero)
    {
        retValUse.ReplaceWithLclVar(_compiler);

        GenTreeLclVar* lclVarNode = retValUse.Def()->AsLclVar();
        _compiler->lvaGetDesc(lclVarNode)->lvHasLocalAddr = true;

        lclVarNode->SetOper(GT_LCL_FLD);
        lclVarNode->ChangeType(m_info->compRetType);
        if (layout != nullptr)
        {
            lclVarNode->AsLclFld()->SetLayout(layout);
        }
    }
}

void Llvm::lowerVirtualStubCall(GenTreeCall* callNode)
{
    assert(callNode->IsVirtualStub() && (callNode->gtControlExpr == nullptr) && !callNode->NeedsNullCheck());
    //
    // We transform:
    //  Call(SS, pCell, @this, args...)
    // Into:
    //  delegate* pTarget = ResolveTarget(SS, @this, pCell)
    //  pTarget(SS, @this, args...)
    //
    LIR::Use thisArgUse(CurrentRange(), &callNode->gtArgs.GetThisArg()->EarlyNodeRef(), callNode);
    unsigned thisArgLclNum = representAsLclVar(thisArgUse);
    GenTree* thisForStub = _compiler->gtNewLclvNode(thisArgLclNum, TYP_REF);
    CurrentRange().InsertBefore(callNode, thisForStub);

    CallArg* cellArg = callNode->gtArgs.FindWellKnownArg(WellKnownArg::VirtualStubCell);
    callNode->gtArgs.Remove(cellArg);

    GenTreeCall* stubCall = _compiler->gtNewHelperCallNode(
        CORINFO_HELP_LLVM_RESOLVE_INTERFACE_CALL_TARGET, TYP_I_IMPL, thisForStub, cellArg->GetNode());
    CurrentRange().InsertBefore(callNode, stubCall);

    // This call could be indirect (in case this is shared code and the cell address needed to be resolved dynamically).
    // Discard the now-not-needed address in that case.
    if (callNode->gtCallType == CT_INDIRECT)
    {
        GenTree* addr = callNode->gtCallAddr;
        if (addr->OperIs(GT_LCL_VAR))
        {
            CurrentRange().Remove(addr);
        }
        else
        {
            addr->SetUnusedValue();
        }
    }

    // Finally, retarget our call. It is no longer VSD.
    callNode->gtCallType = CT_INDIRECT;
    callNode->gtCallAddr = stubCall;
    callNode->gtStubCallStubAddr = nullptr;
    callNode->gtCallCookie = nullptr;
    callNode->gtFlags &= ~GTF_CALL_VIRT_STUB;
    callNode->gtCallMoreFlags &= ~GTF_CALL_M_VIRTSTUB_REL_INDIRECT;

    // Lower the newly introduced stub call.
    lowerCall(stubCall);
}

void Llvm::insertNullCheckForCall(GenTreeCall* callNode)
{
    assert(callNode->gtArgs.HasThisPointer());

    CallArg* thisArg = callNode->gtArgs.GetThisArg();
    if (_compiler->fgAddrCouldBeNull(thisArg->GetNode()))
    {
        LIR::Use thisArgUse(CurrentRange(), &thisArg->EarlyNodeRef(), callNode);
        unsigned thisArgLclNum = representAsLclVar(thisArgUse);

        GenTree* thisArgNode = _compiler->gtNewLclvNode(thisArgLclNum, _compiler->lvaGetDesc(thisArgLclNum)->TypeGet());
        GenTree* thisArgNullCheck = _compiler->gtNewNullCheck(thisArgNode, CurrentBlock());
        CurrentRange().InsertBefore(callNode, thisArgNode, thisArgNullCheck);

        lowerIndir(thisArgNullCheck->AsIndir());
    }

    callNode->gtFlags &= ~GTF_CALL_NULLCHECK;
}

void Llvm::lowerDelegateInvoke(GenTreeCall* callNode)
{
    // Copy of the corresponding "Lower::LowerDelegateInvoke".
    assert(callNode->IsDelegateInvoke());

    // We're going to use the 'this' expression multiple times, so make a local to copy it.
    LIR::Use thisArgUse(CurrentRange(), &callNode->gtArgs.GetThisArg()->EarlyNodeRef(), callNode);
    unsigned delegateThisLclNum = representAsLclVar(thisArgUse);

    CORINFO_EE_INFO* eeInfo = _compiler->eeGetEEInfo();

    // Replace original expression feeding into "this" with [originalThis + offsetOfDelegateInstance].
    GenTree* delegateThis = thisArgUse.Def();
    GenTree* targetThisOffet = _compiler->gtNewIconNode(eeInfo->offsetOfDelegateInstance, TYP_I_IMPL);
    GenTree* targetThisAddr = _compiler->gtNewOperNode(GT_ADD, TYP_BYREF, delegateThis, targetThisOffet);
    GenTree* targetThis = _compiler->gtNewIndir(TYP_REF, targetThisAddr);

    // Insert the new nodes just before the call. This is important to prevent the target "this" from being
    // moved by the GC while arguments after the original "this" are being evaluated.
    CurrentRange().InsertBefore(callNode, targetThisOffet, targetThisAddr, targetThis);
    thisArgUse.ReplaceWith(targetThis);

    // This indirection will null-check the original "this".
    assert(!callNode->NeedsNullCheck());
    lowerIndir(targetThis->AsIndir());

    // The new control target is [originalThis + firstTgtOffs].
    delegateThis = _compiler->gtNewLclvNode(delegateThisLclNum, TYP_REF);
    GenTree* callTargetOffset = _compiler->gtNewIconNode(eeInfo->offsetOfDelegateFirstTarget, TYP_I_IMPL);
    GenTree* callTargetAddr = _compiler->gtNewOperNode(GT_ADD, TYP_BYREF, delegateThis, callTargetOffset);
    GenTree* callTarget = _compiler->gtNewIndir(TYP_I_IMPL, callTargetAddr, GTF_IND_NONFAULTING);
    callTarget->gtFlags |= GTF_ORDER_SIDEEFF;

    CurrentRange().InsertBefore(callNode, delegateThis, callTargetOffset, callTargetAddr, callTarget);
    callNode->gtControlExpr = callTarget;

    lowerIndir(callTarget->AsIndir());
}

void Llvm::lowerUnmanagedCall(GenTreeCall* callNode)
{
    assert(callNode->IsUnmanaged());

    // Insert the GC transitions if required. TODO-LLVM-CQ: batch these if there are no safe points between
    // two or more consecutive PI calls.
    if (!callNode->IsSuppressGCTransition())
    {
        assert(_compiler->opts.ShouldUsePInvokeHelpers()); // No inline transition support yet.
        assert(_compiler->lvaInlinedPInvokeFrameVar != BAD_VAR_NUM);

        // Insert CORINFO_HELP_JIT_PINVOKE_BEGIN.
        GenTreeLclFld* frameAddr = _compiler->gtNewLclVarAddrNode(_compiler->lvaInlinedPInvokeFrameVar);
        GenTreeCall* helperCall = _compiler->gtNewHelperCallNode(CORINFO_HELP_JIT_PINVOKE_BEGIN, TYP_VOID, frameAddr);
        CurrentRange().InsertBefore(callNode, frameAddr, helperCall);
        lowerLocal(frameAddr);
        lowerCall(helperCall);

        // Insert CORINFO_HELP_JIT_PINVOKE_END. No need to explicitly lower the call/local address as the
        // normal lowering loop will pick them up.
        frameAddr = _compiler->gtNewLclVarAddrNode(_compiler->lvaInlinedPInvokeFrameVar);
        helperCall = _compiler->gtNewHelperCallNode(CORINFO_HELP_JIT_PINVOKE_END, TYP_VOID, frameAddr);
        CurrentRange().InsertAfter(callNode, frameAddr, helperCall);
    }

    if (callNode->gtCallType != CT_INDIRECT)
    {
        // We cannot easily handle varargs as we do not know which args are the fixed ones.
        assert((callNode->gtCallType == CT_USER_FUNC) && !callNode->IsVarargs());

        ArrayStack<TargetAbiType> sig(_compiler->getAllocator(CMK_Codegen));
        sig.Push(getAbiTypeForType(JITtype2varType(callNode->gtCorInfoType)));
        for (CallArg& arg : callNode->gtArgs.Args())
        {
            sig.Push(getAbiTypeForType(JITtype2varType(getLlvmArgTypeForCallArg(&arg))));
        }

        // WASM requires the callee and caller signature to match. At the LLVM level, "callee type" is the function
        // type attached of the called operand and "caller" - that of its callsite. The problem, then, is that for a
        // given module, we can only have one function declaration, thus, one callee type. And we cannot know whether
        // this type will be the right one until, in general, runtime (this is the case for WASM imports provided by
        // the host environment). Thus, to achieve the experience of runtime erros on signature mismatches, we "hide"
        // the target behind an external function from another module, turning this call into an indirect one.
        GenTreeCall* getTargetCall =
            _compiler->gtNewHelperCallNode(CORINFO_HELP_LLVM_GET_EXTERNAL_CALL_TARGET, TYP_I_IMPL);
        getTargetCall->gtEntryPoint.handle =
            GetExternalMethodAccessor(callNode->gtCallMethHnd, &sig.BottomRef(), sig.Height());
        getTargetCall->gtEntryPoint.accessType = IAT_VALUE;

        callNode->gtCallType = CT_INDIRECT;
        callNode->gtCallAddr = getTargetCall;
        callNode->gtCallCookie = nullptr;
        CurrentRange().InsertBefore(callNode, getTargetCall);
        lowerCall(getTargetCall);
    }
}

//------------------------------------------------------------------------
// lowerCallToShadowStack: Initialize AbiInfo for signature building.
//
void Llvm::lowerCallToShadowStack(GenTreeCall* callNode)
{
    const HelperFuncInfo* helperInfo = nullptr;
    if (callNode->IsHelperCall())
    {
        helperInfo = &getHelperFuncInfo(_compiler->eeGetHelperNum(callNode->gtCallMethHnd));
    }

    int sigArgIdx = 0;
    for (CallArg& callArg : callNode->gtArgs.Args())
    {
        GenTree* argNode = callArg.GetNode();
        CorInfoType argSigType;
        CORINFO_CLASS_HANDLE argSigClass;
        if (helperInfo == nullptr)
        {
            if (callArg.GetWellKnownArg() == WellKnownArg::ThisPointer)
            {
                argSigType = argNode->TypeIs(TYP_REF) ? CORINFO_TYPE_CLASS : CORINFO_TYPE_BYREF;
            }
            else if ((callArg.GetWellKnownArg() == WellKnownArg::InstParam) ||
                     (callArg.GetWellKnownArg() == WellKnownArg::RetBuffer))
            {
                argSigType = CORINFO_TYPE_PTR;
            }
            else if (callArg.GetSignatureCorInfoType() != CORINFO_TYPE_UNDEF)
            {
                argSigType = callArg.GetSignatureCorInfoType();
            }
            else
            {
                assert(callArg.GetSignatureType() != TYP_I_IMPL);
                argSigType = toCorInfoType(callArg.GetSignatureType());
            }

            argSigClass = callArg.GetSignatureClassHandle();
        }
        else
        {
            argSigType = helperInfo->GetSigArgType(sigArgIdx);
            argSigClass = helperInfo->GetSigArgClass(_compiler, sigArgIdx);
        }

        if (argNode->TypeIs(TYP_STRUCT))
        {
            LIR::Use argNodeUse(CurrentRange(), &callArg.EarlyNodeRef(), callNode);
            argNode = normalizeStructUse(argNodeUse, _compiler->typGetObjLayout(argSigClass));
        }

        CorInfoType argType = getLlvmArgTypeForArg(argSigType, argSigClass);
        callArg.AbiInfo.IsPointer = argType == CORINFO_TYPE_PTR;
        callArg.AbiInfo.ArgType = JITtype2varType(argType);

        sigArgIdx++;
    }
}

// Assigns "callNode->gtCorInfoType". After this method, "gtCorInfoType" switches meaning from
// "the signature return type" to "the ABI return type".
//
void Llvm::lowerCallReturn(GenTreeCall* callNode)
{
    CorInfoType sigRetType;
    if (callNode->IsHelperCall())
    {
        sigRetType = getHelperFuncInfo(callNode->GetHelperNum()).GetSigReturnType();
    }
    else if (callNode->gtCorInfoType == CORINFO_TYPE_UNDEF)
    {
        assert(callNode->TypeGet() != TYP_I_IMPL);
        sigRetType = toCorInfoType(callNode->TypeGet());
    }
    else
    {
        sigRetType = callNode->gtCorInfoType;
    }

    callNode->gtCorInfoType = getLlvmReturnType(sigRetType, callNode->gtRetClsHnd);
}

void Llvm::lowerAddressToAddressMode(GenTreeIndir* indir)
{
    GenTree* addr = indir->Addr();
    if (!addr->OperIs(GT_ADD))
    {
        return;
    }

    // Transform this addition into a LEA if possible. This will help us do two things:
    //  1. Null-check using a straight comparison with "null".
    //  2. Generate an inbounds GEP, allowing for the folding of address computation into the load/store.
    //
    GenTree* baseAddr = addr;
    FieldSeq* fieldSeq = nullptr;
    target_size_t offset = 0;
    ArrayStack<GenTree*> addrModeNodes(_compiler->getAllocator(CMK_Codegen));
    while (baseAddr->OperIs(GT_ADD) && !baseAddr->gtOverflow())
    {
        GenTree* offsetNode = baseAddr->gtGetOp2();
        if (!offsetNode->IsCnsIntOrI() || offsetNode->IsIconHandle())
        {
            break;
        }

        target_size_t newOffset = offset + static_cast<target_size_t>(offsetNode->AsIntCon()->IconValue());
        if (_compiler->fgIsBigOffset(newOffset))
        {
            break;
        }

        addrModeNodes.Push(offsetNode);
        addrModeNodes.Push(baseAddr);

        baseAddr = baseAddr->gtGetOp1();
        fieldSeq = offsetNode->AsIntCon()->gtFieldSeq;
        offset = newOffset;

        // If we have found a field sequence, abort, since the offset it contains is relative to this exact "baseAddr".
        if (fieldSeq != nullptr)
        {
            break;
        }
    }

    if (addr == baseAddr)
    {
        return;
    }

    JITDUMP("Converting [%06u] into LEA([%06u], %zu): ", Compiler::dspTreeID(addr), Compiler::dspTreeID(baseAddr),
            (size_t)offset);

    // Invariant access can be assumed to be in bounds by construction.
    if (((indir->gtFlags & GTF_IND_INVARIANT) == 0) && !isAddressInBounds(baseAddr, fieldSeq, offset))
    {
        JITDUMP("no, not in bounds\n");
        return;
    }

    // TODO-LLVM-CQ: there are two types of LEAs: those we **must** contain because they depend on the null checking
    // to be in bounds, and those where the base is known to not be null. We could form the latter kind independently
    // from indirection lowering.
    if (!isInvariantInRange(addrModeNodes.Top(), indir))
    {
        JITDUMP("no, not containable\n");
        return;
    }

    addr->ChangeOper(GT_LEA);
    addr->AsAddrMode()->SetBase(baseAddr);
    addr->AsAddrMode()->SetIndex(nullptr);
    addr->AsAddrMode()->SetScale(0);
    addr->AsAddrMode()->SetOffset(static_cast<unsigned>(offset));
    addr->AsAddrMode()->SetContained();
    JITDUMP("\n");
    DISPNODE(addr);

    // Remove all of the nodes that contributed to this address mode from LIR.
    while (!addrModeNodes.Empty())
    {
        GenTree* node = addrModeNodes.Pop();
        if (node != addr)
        {
            CurrentRange().Remove(node);
        }
    }
}

//------------------------------------------------------------------------
// isAddressInBounds: Can this address computation be marked "in bounds"?
//
// The definition of "in bounds" here is the same as LLVM's, i. e.:
//  1. "addr" points to an allocated object.
//  2. "addr" + "offset" points to that same object, or one past its end.
// Notably, here we assume that the caller ensured "addr" cannot be null.
//
// Arguments:
//    addr     - The base address
//    fieldSeq - The field sequence associated with "offset"
//    offset   - Offset to be added to "addr"
//
// Return Value:
//    Whether "addr" + "offset" can be considered "in bounds".
//
bool Llvm::isAddressInBounds(GenTree* addr, FieldSeq* fieldSeq, target_size_t offset)
{
    // Static fields as well as instance fields on objects.
    if (fieldSeq != nullptr)
    {
        assert(fieldSeq->GetKind() != FieldSeq::FieldKind::SimpleStaticKnownAddress);
        target_size_t fieldAccessOffset = offset - static_cast<target_size_t>(fieldSeq->GetOffset());
        if (fieldAccessOffset == 0)
        {
            // Throughput: no need to check the field size if we are accessing it at zero offset.
            return true;
        }

        CORINFO_CLASS_HANDLE fieldStructType;
        CorInfoType fieldType = m_info->compCompHnd->getFieldType(fieldSeq->GetFieldHandle(), &fieldStructType);
        target_size_t fieldSize = _compiler->compGetTypeSize(fieldType, fieldStructType);

        // Note the "<=" that allows one-past-the-end access.
        return fieldAccessOffset <= fieldSize;
    }

    if (addr->TypeIs(TYP_REF))
    {
        // Here we are primarily concerned with array access.
        return offset <= getObjectSizeBound(addr);
    }

    if (addr->OperIs(GT_LCL_VAR))
    {
        unsigned lclNum = addr->AsLclVar()->GetLclNum();
        if (lclNum == m_info->compRetBuffArg)
        {
            // Access to the return buffer is always generated by the compiler.
            assert(offset < m_info->compCompHnd->getClassSize(m_info->compMethodInfo->args.retTypeClass));
            return true;
        }
        if (lclNum == static_cast<unsigned>(m_info->compTypeCtxtArg))
        {
            // Same as above, except the context argument cannot be null.
            return true;
        }

        if (_compiler->lvaIsImplicitByRefLocal(lclNum))
        {
            // Implicit byrefs can be accessed by the user so we must validate the offset.
            return offset <= _compiler->lvaGetDesc(lclNum)->GetLayout()->GetSize();
        }
    }

    // TODO-LLVM-CQ: VTable access.
    return false;
}

//------------------------------------------------------------------------
// getObjectSizeBound: Get the uppermost estimate for an object's size.
//
// Arguments:
//    obj - Node representing the object in question
//
// Return Value:
//    The number of bytes of this object, counting starting from (and
//    including) the VTable pointer, that is known to be allocated.
//
unsigned Llvm::getObjectSizeBound(GenTree* obj)
{
    assert(obj->TypeIs(TYP_REF));

    // TODO-LLVM-CQ: improve this estimate using "gtGetClassHandle".
    return MIN_HEAP_OBJ_SIZE;
}

//------------------------------------------------------------------------
// normalizeStructUse: Retype "node" to have the exact type of "layout".
//
// LLVM has a strict constraint on uses and users of structs: they must
// have the exact same type, while IR only requires "layout compatibility".
// So in lowering we retype uses (and users) to match LLVM's expectations.
//
// Arguments:
//    use    - Use of the struct node to retype
//    layout - The target layout
//
// Return Value:
//    The retyped node.
//
GenTree* Llvm::normalizeStructUse(LIR::Use& use, ClassLayout* layout)
{
    GenTree* node = use.Def();
    assert(node->TypeIs(TYP_STRUCT)); // Note on SIMD: we will support it in codegen via bitcasts.

    ClassLayout* useLayout = node->GetLayout(_compiler);

    if ((useLayout != layout) && (getLlvmTypeForStruct(useLayout) != getLlvmTypeForStruct(layout)))
    {
        switch (node->OperGet())
        {
            case GT_BLK:
                node->AsBlk()->SetLayout(layout);
                break;

            case GT_LCL_FLD:
                node->AsLclFld()->SetLayout(layout);
                break;

            case GT_CALL:
                use.ReplaceWithLclVar(_compiler);
                node = use.Def();
                FALLTHROUGH;

            case GT_LCL_VAR:
                node->SetOper(GT_LCL_FLD);
                node->AsLclFld()->SetLayout(layout);
                _compiler->lvaGetDesc(node->AsLclFld())->lvHasLocalAddr = true;
                break;

            default:
                unreached();
        }
    }

    return node;
}

unsigned Llvm::representAsLclVar(LIR::Use& use)
{
    GenTree* node = use.Def();
    if (node->OperIs(GT_LCL_VAR))
    {
        return node->AsLclVar()->GetLclNum();
    }

    return use.ReplaceWithLclVar(_compiler);
}

GenTree* Llvm::insertShadowStackAddr(GenTree* insertBefore, unsigned offset, unsigned shadowStackLclNum)
{
    assert(isShadowStackLocal(shadowStackLclNum));

    GenTree* shadowStackLcl = _compiler->gtNewLclvNode(shadowStackLclNum, TYP_I_IMPL);
    CurrentRange().InsertBefore(insertBefore, shadowStackLcl);

    if (offset == 0)
    {
        return shadowStackLcl;
    }

    // Using an address mode node here explicitizes our assumption that the shadow stack does not overflow.
    assert(offset <= getShadowFrameSize(ROOT_FUNC_IDX));
    GenTree* addrModeNode = createAddrModeNode(shadowStackLcl, offset);
    CurrentRange().InsertBefore(insertBefore, addrModeNode);

    return addrModeNode;
}

//------------------------------------------------------------------------
// createAddrModeNode: Create an address mode node.
//
// Note that LEAs as used in this backend correspond to "inbounds" GEPs
// and so have additional semantic restrictions - see "buildAddrMode".
//
// Arguments:
//    base   - The base address
//    offset - The offset
//
// Return Value:
//    The new GT_LEA node.
//
GenTreeAddrMode* Llvm::createAddrModeNode(GenTree* base, unsigned offset)
{
    return new (_compiler, GT_LEA)
        GenTreeAddrMode(varTypeIsGC(base) ? TYP_BYREF : TYP_I_IMPL, base, nullptr, 0, offset);
}

//------------------------------------------------------------------------
// isInvariantInRange: Check if a node is invariant in the specified range. In
// other words, can 'node' be moved to right before 'endExclusive' without its
// computation changing values?
//
// Arguments:
//    node         -  The node.
//    endExclusive -  The exclusive end of the range to check invariance for.
//
// Returns:
//    True if 'node' can be evaluated at any point between its current
//    location and 'endExclusive' without giving a different result; otherwise
//    false.
//
// Notes:
//    (Almost) exact copy of Lowering::IsInvariantInRange.
//
bool Llvm::isInvariantInRange(GenTree* node, GenTree* endExclusive)
{
    assert((node != nullptr) && (endExclusive != nullptr));

    // Quick early-out for unary cases
    //
    if (node->gtNext == endExclusive)
    {
        return true;
    }

    m_scratchSideEffects.Clear();
    m_scratchSideEffects.AddNode(_compiler, node);

    for (GenTree* cur = node->gtNext; cur != endExclusive; cur = cur->gtNext)
    {
        assert((cur != nullptr) && "Expected first node to precede end node");
        const bool strict = true;
        if (m_scratchSideEffects.InterferesWith(_compiler, cur, strict))
        {
            return false;
        }
    }

    return true;
}

void Llvm::lowerDissolveDependentlyPromotedLocals()
{
    // We have now rewritten all references to dependently promoted fields to reference the parent instead.
    // Drop the annotations such that the subsequent SSA pass will only produce normal (non-composite) names.
    //
    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        if (_compiler->lvaGetPromotionType(lclNum) == Compiler::PROMOTION_TYPE_DEPENDENT)
        {
            dissolvePromotedLocal(lclNum);
        }
    }
}

void Llvm::dissolvePromotedLocal(unsigned lclNum)
{
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    assert(varDsc->lvPromoted);

    for (unsigned index = 0; index < varDsc->lvFieldCnt; index++)
    {
        LclVarDsc* fieldVarDsc = _compiler->lvaGetDesc(varDsc->lvFieldLclStart + index);

        fieldVarDsc->lvIsStructField = false;
        fieldVarDsc->lvParentLcl = BAD_VAR_NUM;
        fieldVarDsc->lvIsParam = false;
    }

    varDsc->lvPromoted = false;
    varDsc->lvFieldLclStart = BAD_VAR_NUM;
    varDsc->lvFieldCnt = 0;
}

void Llvm::lowerCanonicalizeFirstBlock()
{
    // Insert a block suitable for prolog code here so that subsequent phases don't have to alter the flowgraph.
    if (!isFirstBlockCanonical())
    {
        JITDUMP("\nCanonicalizing the first block for later prolog insertion\n");
        assert(!_compiler->fgFirstBBisScratch());
        _compiler->fgEnsureFirstBBisScratch();
    }
}

bool Llvm::isFirstBlockCanonical()
{
    // Note this must use conditions at least as broad as "SsaBuilder::SetupBBRoot".
    BasicBlock* block = _compiler->fgFirstBB;
    return !block->hasTryIndex() && (block->bbPreds == nullptr);
}

//------------------------------------------------------------------------
// AddVirtualUnwindFrame: Add "virtually unwindable" frame state.
//
// The first pass of exception handling needs to traverse the currently
// active stack of possible catch handlers without unwinding the native
// or shadow stack. We accomplish this by adding explicitly linked frames
// and maintaining "unwind index" representing the active protected region
// throughout execution of methods with catch handlers.
//
// To determine which blocks need the unwind index, we walk over the IR,
// recording where exceptions may be thrown. Then, when optimizing, we
// partition the graph into "unwind index groups" - areas where the unwind
// index will have the same value, and which have a well-defined set of
// entrypoints. Consider, for example:
//
//  BB01 (T0) -> BB02 (T0) --> BB03 (T1) -> BB05 (NO) --> BB06 (ZR)
//                         \-> BB04 (ZR) -/           \-> BB07 (ZR)
//
// We start with BB01. It has no predecessors and gets a new group (G0).
// This is an entry block to this group. We continue with BB02. It has only
// one predecessor - BB01, which has the same unwind index requirement so
// we put it into the same group. Next up are BB03 and BB04. Both cannot
// be placed into G0 as they need different unwind indices, and so will be
// assigned their own groups (G1 and G2). Next up is BB06. It has just one
// predecessor - BB05, which does not need an unwind index. We place both
// BB06 and BB05 into a new group (G3). We process BB05 itself and find
// it has predecessors with conflicting unwind index requirements, so it
// will be the entry block for G3. Finally, we process BB07, which by now
// has a predecessor in a group with the same unwind index as its own, so
// we place BB07 into G3 too. In the end, we will have with 4 block which
// end up defining the unwind index (BB01, BB03, BB04, BB05) - the optimal
// number for this flowgraph.
//
// This grouping algorithm is intended to take advantage of the clustery
// nature of protected regions while remaining fully general.
//
PhaseStatus Llvm::AddVirtualUnwindFrame()
{
    // Always compute the set of filter blocks, for simplicity.
    computeBlocksInFilters();

    // TODO-LLVM: make a distinct flag; using this alias avoids conflicts.
    static const BasicBlockFlags BBF_MAY_THROW = BBF_HAS_CALL;
    static const unsigned UNWIND_INDEX_GROUP_NONE = -1;

    // Build the mapping of EH table indices to unwind indices.
    ArrayStack<unsigned>* indexMap = computeUnwindIndexMap();
    if (indexMap == nullptr)
    {
        // No catch handlers; no need for virtual unwinding.
        return PhaseStatus::MODIFIED_NOTHING;
    }

    // Compute which blocks may throw and thus need an up-to-date unwind index.
    for (BasicBlock* block : _compiler->Blocks())
    {
        // BBF_MAY_THROW overlaps with BBF_HAS_CALL.
        block->RemoveFlags(BBF_MAY_THROW);

        for (GenTree* node : LIR::AsRange(block))
        {
            if (mayPhysicallyThrow(node))
            {
                block->SetFlags(BBF_MAY_THROW);
                break;
            }
        }
    }

    class Inserter
    {
        struct IndexDef
        {
            BasicBlock* Block;
            unsigned Value;
        };

        struct UnwindIndexGroup
        {
            unsigned UnwindIndex;
        };

        Llvm* m_llvm;
        Compiler* m_compiler;
        ArrayStack<unsigned>* m_indexMap;
        ArrayStack<UnwindIndexGroup> m_groups;
        ArrayStack<IndexDef> m_definedIndices;
        unsigned m_initialIndexValue = UNWIND_INDEX_NOT_IN_TRY;

    public:
        Inserter(Llvm* llvm, ArrayStack<unsigned>* indexMap)
            : m_llvm(llvm)
            , m_compiler(llvm->_compiler)
            , m_indexMap(indexMap)
            , m_groups(m_compiler->getAllocator(CMK_Codegen))
            , m_definedIndices(m_compiler->getAllocator(CMK_Codegen))
        {
        }

        static bool IsCatchUnwindIndex(unsigned index)
        {
            return index >= UNWIND_INDEX_BASE;
        }

        bool BlockUsesUnwindIndex(BasicBlock* block)
        {
            // Exceptions thrown in filters do not unwind to their enclosing protected region and are
            // instead always caught by the dispatcher. Thus, filter blocks do not need the unwind index.
            return block->HasFlag(BBF_MAY_THROW) && !m_llvm->isBlockInFilter(block);
        }

        unsigned GetUnwindIndexForBlock(BasicBlock* block)
        {
            if (!BlockUsesUnwindIndex(block))
            {
                return UNWIND_INDEX_NONE;
            }
            if (!block->hasTryIndex())
            {
                return UNWIND_INDEX_NOT_IN_TRY;
            }

            // Assert that we will only see the most nested index for mutually protecting regions.
            unsigned ehIndex = block->getTryIndex();
            assert((ehIndex == 0) || !m_compiler->ehGetDsc(ehIndex)->ebdIsSameTry(m_compiler, ehIndex - 1));

            return m_indexMap->Bottom(ehIndex);
        }

        void DefineIndex(BasicBlock* block, unsigned indexValue)
        {
            JITDUMP("Setting unwind index in " FMT_BB " to %u", block->bbNum, indexValue);
            JITDUMPEXEC(PrintUnwindIndex(indexValue));
            JITDUMP("\n");

            // As a size optimization, the first block's index will be initialized by the init helper.
            if (block == m_compiler->fgFirstBB)
            {
                m_initialIndexValue = indexValue;
                return;
            }

            m_definedIndices.Push({block, indexValue});
        }

        bool SerializeResultsIntoIR()
        {
            bool allIndicesAreNotInTryCatch = !IsCatchUnwindIndex(m_initialIndexValue);
            if (allIndicesAreNotInTryCatch)
            {
                for (int i = 0; i < m_definedIndices.Height(); i++)
                {
                    if (IsCatchUnwindIndex(m_definedIndices.BottomRef(i).Value))
                    {
                        allIndicesAreNotInTryCatch = false;
                        break;
                    }
                }
            }

            // This can happen if we have try regions without any throws. The compiler is not great at removing them.
            if (allIndicesAreNotInTryCatch)
            {
                JITDUMP("All unwind indices were NOT_IN_TRY[_CATCH], skipping inserting the unwind frame\n");
                return false;
            }

            ClassLayout* unwindFrameLayout = m_compiler->typGetBlkLayout(3 * TARGET_POINTER_SIZE);
            unsigned unwindFrameLclNum = m_compiler->lvaGrabTemp(false DEBUGARG("virtual unwind frame"));
            m_compiler->lvaSetStruct(unwindFrameLclNum, unwindFrameLayout, /* unsafeValueClsCheck */ false);
            m_compiler->lvaSetVarAddrExposed(unwindFrameLclNum DEBUGARG(AddressExposedReason::ESCAPE_ADDRESS));
            m_compiler->lvaGetDesc(unwindFrameLclNum)->lvHasExplicitInit = true;
            m_llvm->m_unwindFrameLclNum = unwindFrameLclNum;

            m_llvm->m_unwindIndexMap = m_indexMap;
            size_t unwindTableAddr = size_t(m_llvm->generateUnwindTable());
            GenTree* unwindTableAddrNode = m_compiler->gtNewIconHandleNode(unwindTableAddr, GTF_ICON_CONST_PTR);
            GenTree* unwindFrameLclAddr = m_compiler->gtNewLclVarAddrNode(unwindFrameLclNum);
            GenTreeIntCon* initialUnwindIndexNode = m_compiler->gtNewIconNode(m_initialIndexValue, TYP_I_IMPL);
            GenTreeCall* initializeCall =
                m_compiler->gtNewHelperCallNode(CORINFO_HELP_LLVM_EH_PUSH_VIRTUAL_UNWIND_FRAME, TYP_VOID,
                                                unwindFrameLclAddr, unwindTableAddrNode, initialUnwindIndexNode);
            LIR::Range initRange;
            initRange.InsertAtEnd(unwindFrameLclAddr);
            initRange.InsertAtEnd(unwindTableAddrNode);
            initRange.InsertAtEnd(initialUnwindIndexNode);
            initRange.InsertAtEnd(initializeCall);

            assert(m_llvm->isFirstBlockCanonical());
            m_llvm->lowerRange(m_compiler->fgFirstBB, initRange);
            LIR::AsRange(m_compiler->fgFirstBB).InsertAtBeginning(std::move(initRange));

            for (int i = 0; i < m_definedIndices.Height(); i++)
            {
                const IndexDef& def = m_definedIndices.BottomRef(i);
                GenTree* indexValueNode = m_compiler->gtNewIconNode(def.Value);
                GenTree* indexValueStore = m_compiler->gtNewStoreLclFldNode(unwindFrameLclNum, TYP_INT,
                                                                            2 * TARGET_POINTER_SIZE, indexValueNode);

                // No need to lower these nodes at this point in time.
                LIR::Range& blockRange = LIR::AsRange(def.Block);
                GenTree* insertionPoint = blockRange.FirstNonPhiOrCatchArgNode();
                blockRange.InsertBefore(insertionPoint, indexValueNode);
                blockRange.InsertBefore(insertionPoint, indexValueStore);
            }

            for (BasicBlock* block : m_compiler->Blocks())
            {
                // TODO-LLVM-EH: fold NOT_IN_TRY settings into pop calls when legal.
                if (block->KindIs(BBJ_RETURN))
                {
                    GenTree* lastNode = block->lastNode();
                    assert(lastNode->OperIs(GT_RETURN));

                    GenTreeCall* popCall =
                        m_compiler->gtNewHelperCallNode(CORINFO_HELP_LLVM_EH_POP_VIRTUAL_UNWIND_FRAME, TYP_VOID);
                    LIR::Range popCallRange;
                    popCallRange.InsertAtBeginning(popCall);
                    m_llvm->lowerRange(block, popCallRange);
                    LIR::AsRange(block).InsertBefore(lastNode, std::move(popCallRange));
                }
            }

            return true;
        }

        void InsertDefinitionsBasedOnUnwindIndexGroups()
        {
            ArrayStack<BasicBlock*> blockList(m_compiler->getAllocator(CMK_Codegen), m_compiler->fgBBcount);
            BitVecTraits blockListTraits(m_compiler->fgBBNumMax + 1, m_compiler);
            BitVec blockListSet = BitVecOps::MakeEmpty(&blockListTraits);

            for (BasicBlock* block = m_compiler->fgLastBB; block != nullptr; block = block->Prev())
            {
                if (BlockUsesUnwindIndex(block))
                {
                    blockList.Push(block);
                    BitVecOps::AddElemD(&blockListTraits, blockListSet, block->bbNum);
                }

                SetGroup(block, UNWIND_INDEX_GROUP_NONE);
            }

            while (!blockList.Empty())
            {
                BasicBlock* block = blockList.Pop();
                unsigned blockUnwindIndex = GetUnwindIndexForBlock(block);
                unsigned blockUnwindIndexGroup = GetGroup(block);
                assert(BitVecOps::IsMember(&blockListTraits, blockListSet, block->bbNum));

                JITDUMP("At " FMT_BB, block->bbNum);
                JITDUMPEXEC(PrintUnwindIndex(blockUnwindIndex));
                JITDUMP(": ");
                JITDUMPEXEC(PrintUnwindIndexGroup(blockUnwindIndexGroup));

                if (blockUnwindIndex == UNWIND_INDEX_NONE)
                {
                    assert(blockUnwindIndexGroup != UNWIND_INDEX_GROUP_NONE);
                    blockUnwindIndex = GetGroupUnwindIndex(blockUnwindIndexGroup);
                }

                // Due to this dependency on "BlockPredsWithEH" we run after SSA, which computes and caches it.
                FlowEdge* allPredEdges = m_compiler->BlockPredsWithEH(block);

                bool allPredsUseTheSameUnwindIndex = true;
                unsigned selectedUnwindIndexGroup = blockUnwindIndexGroup;
                for (BasicBlock* predBlock : PredBlockList(allPredEdges))
                {
                    unsigned predBlockUnwindIndex = GetUnwindIndexForBlock(predBlock);
                    unsigned predBlockUnwindIndexGroup = GetGroup(predBlock);
                    if (predBlockUnwindIndexGroup != UNWIND_INDEX_GROUP_NONE)
                    {
                        if (predBlockUnwindIndex == UNWIND_INDEX_NONE)
                        {
                            predBlockUnwindIndex = GetGroupUnwindIndex(predBlockUnwindIndexGroup);
                        }

                        assert(predBlockUnwindIndex == GetGroupUnwindIndex(predBlockUnwindIndexGroup));
                    }

                    if ((predBlockUnwindIndex != UNWIND_INDEX_NONE) && (predBlockUnwindIndex != blockUnwindIndex))
                    {
                        allPredsUseTheSameUnwindIndex = false;
                        break;
                    }

                    if (selectedUnwindIndexGroup == UNWIND_INDEX_GROUP_NONE)
                    {
                        selectedUnwindIndexGroup = predBlockUnwindIndexGroup;
                    }
                }

                const char* groupSelectionReason = nullptr;
                if (blockUnwindIndexGroup == UNWIND_INDEX_GROUP_NONE)
                {
                    if (!allPredsUseTheSameUnwindIndex || (selectedUnwindIndexGroup == UNWIND_INDEX_GROUP_NONE))
                    {
                        groupSelectionReason = "new";
                        blockUnwindIndexGroup = NewGroup(blockUnwindIndex);
                    }
                    else
                    {
                        groupSelectionReason = "selected";
                        blockUnwindIndexGroup = selectedUnwindIndexGroup;
                    }

                    SetGroup(block, blockUnwindIndexGroup);
                }

                JITDUMP(" -> ");
                JITDUMPEXEC(PrintUnwindIndexGroup(blockUnwindIndexGroup));
                JITDUMPEXEC(PrintUnwindIndex(blockUnwindIndex));
                if (groupSelectionReason != nullptr)
                {
                    JITDUMP(" - %s", groupSelectionReason);
                }
                assert(blockUnwindIndex == GetGroupUnwindIndex(blockUnwindIndexGroup));

                bool allPredsDefineTheSameUnwindIndex = allPredsUseTheSameUnwindIndex;
                if (allPredsUseTheSameUnwindIndex)
                {
                    for (BasicBlock* predBlock : PredBlockList(allPredEdges))
                    {
                        unsigned predBlockUnwindIndexGroup = GetGroup(predBlock);
                        if (predBlockUnwindIndexGroup != UNWIND_INDEX_GROUP_NONE)
                        {
                            continue;
                        }

                        JITDUMP(", pred " FMT_BB " -> ", predBlock->bbNum);
                        INDEBUG(const char* reasonWhyNot);
                        if (!ExpandGroup(predBlock DEBUGARG(&reasonWhyNot)))
                        {
                            JITDUMP("GZ (%s)", reasonWhyNot);
                            allPredsDefineTheSameUnwindIndex = false;
                            continue;
                        }

                        if (!BitVecOps::IsMember(&blockListTraits, blockListSet, predBlock->bbNum))
                        {
                            BitVecOps::AddElemD(&blockListTraits, blockListSet, predBlock->bbNum);
                            blockList.Push(predBlock);
                        }

                        JITDUMPEXEC(PrintUnwindIndexGroup(blockUnwindIndexGroup));
                        SetGroup(predBlock, blockUnwindIndexGroup);
                    }
                }

                if (!allPredsDefineTheSameUnwindIndex || (allPredEdges == nullptr))
                {
                    // This will be an entry block to this unwind index group.
                    block->SetFlags(BBF_MARKED);
                }

                JITDUMP("\n");
            }

            JITDUMPEXEC(PrintUnwindIndexGroupsForBlocks());

            for (BasicBlock* block : m_compiler->Blocks())
            {
                if (block->HasFlag(BBF_MARKED))
                {
                    DefineIndex(block, GetGroupUnwindIndex(GetGroup(block)));
                    block->RemoveFlags(BBF_MARKED);
                }
            }
        }

    private:
        unsigned GetGroup(BasicBlock* block)
        {
            return static_cast<unsigned>(reinterpret_cast<size_t>(block->bbEmitCookie));
        }

        void SetGroup(BasicBlock* block, unsigned groupIndex)
        {
            block->bbEmitCookie = reinterpret_cast<void*>(static_cast<size_t>(groupIndex));
        }

        unsigned GetGroupUnwindIndex(unsigned groupIndex)
        {
            assert(groupIndex != UNWIND_INDEX_GROUP_NONE);
            return m_groups.BottomRef(groupIndex).UnwindIndex;
        }

        unsigned NewGroup(unsigned unwindIndex)
        {
            assert(unwindIndex != UNWIND_INDEX_NONE);
            unsigned groupIndex = m_groups.Height();
            m_groups.Push({unwindIndex});

            return groupIndex;
        }

        bool ExpandGroup(BasicBlock* predBlock DEBUGARG(const char** pReasonWhyNot))
        {
            // The compiler models exceptional flow such that the catch handler associated with a given
            // filter is "invoked" by it (the handler's entry is a normal successor of BBJ_EHFILTERRET).
            // This transition, while atomic in the flowgraph, is not so in execution because of the
            // dispatch code that runs before the handler is reached. This dispatch code relies on the
            // stack of virtual unwind frames remaining consistent. Letting filters alter the unwind
            // index would risk "freeing" this frame too early. Therefore, we must not place any filter
            // blocks in any group.
            if (m_llvm->isBlockInFilter(predBlock))
            {
                INDEBUG(*pReasonWhyNot = "in filter");
                return false;
            }

            // The compiler requires that blocks representing targets of finally returns remain empty.
            if (predBlock->isBBCallFinallyPairTail())
            {
                INDEBUG("finret target");
                return false;
            }

            // TODO-LLVM-CQ: design CQ-driven heuristics for group expansion.
            return true;
        }

#ifdef DEBUG
        void PrintUnwindIndex(unsigned index)
        {
            m_llvm->printUnwindIndex(m_indexMap, index);
        }

        void PrintUnwindIndexGroup(unsigned groupIndex)
        {
            if (groupIndex == UNWIND_INDEX_GROUP_NONE)
            {
                printf("GZ");
                return;
            }

            printf("G%u", groupIndex);
        }

        void PrintUnwindIndexGroupsForBlocks()
        {
            printf("Final unwind index groups:\n");
            for (BasicBlock* block : m_compiler->Blocks())
            {
                unsigned groupIndex = GetGroup(block);

                printf(FMT_BB " %s : ", block->bbNum, BlockUsesUnwindIndex(block) ? "(U)" : "   ");
                PrintUnwindIndexGroup(groupIndex);
                if (groupIndex != UNWIND_INDEX_GROUP_NONE)
                {
                    PrintUnwindIndex(GetGroupUnwindIndex(groupIndex));
                    if (block->HasFlag(BBF_MARKED))
                    {
                        printf(" ENTRY");
                    }
                }
                printf("\n");
            }
        }
#endif // DEBUG
    };

    Inserter inserter(this, indexMap);

    // We will use the more precise algorithm when optimizing.
    if (_compiler->m_domTree != nullptr)
    {
        inserter.InsertDefinitionsBasedOnUnwindIndexGroups();
    }
    else
    {
        for (BasicBlock* block : _compiler->Blocks())
        {
            unsigned index = inserter.GetUnwindIndexForBlock(block);
            if (index != UNWIND_INDEX_NONE)
            {
                inserter.DefineIndex(block, index);
            }
        }
    }

    if (!inserter.SerializeResultsIntoIR())
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }

    return PhaseStatus::MODIFIED_EVERYTHING;
}

void Llvm::computeBlocksInFilters()
{
    for (EHblkDsc* ehDsc : EHClauses(_compiler))
    {
        if (ehDsc->HasFilter())
        {
            for (BasicBlock* block : _compiler->Blocks(ehDsc->ebdFilter, ehDsc->BBFilterLast()))
            {
                if (m_blocksInFilters == BlockSetOps::UninitVal())
                {
                    _compiler->EnsureBasicBlockEpoch();
                    m_blocksInFilters = BlockSetOps::MakeEmpty(_compiler);
                }

                BlockSetOps::AddElemD(_compiler, m_blocksInFilters, block->bbNum);
            }
        }
    }
}

ArrayStack<unsigned>* Llvm::computeUnwindIndexMap()
{
    unsigned lastUnwindIndex = UNWIND_INDEX_BASE;
    CompAllocator alloc = _compiler->getAllocator(CMK_Codegen);
    ArrayStack<unsigned>* indexMap = new (alloc) ArrayStack<unsigned>(alloc, _compiler->compHndBBtabCount);
    for (EHblkDsc* ehDsc : EHClauses(_compiler))
    {
        if (ehDsc->HasCatchHandler())
        {
            indexMap->Push(lastUnwindIndex++);
        }
        else
        {
            // Assume for a start that this fault will not use the unwind index.
            indexMap->Push(UNWIND_INDEX_NOT_IN_TRY);
        }
    }

    if (lastUnwindIndex == UNWIND_INDEX_BASE)
    {
        return nullptr;
    }

    // Now revisit our assumption about faults with this inner-to-outer loop.
    for (unsigned ehIndex = 0; ehIndex < _compiler->compHndBBtabCount; ehIndex++)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
        if ((indexMap->Bottom(ehIndex) != UNWIND_INDEX_NOT_IN_TRY) &&
            (ehDsc->ebdEnclosingHndIndex != EHblkDsc::NO_ENCLOSING_INDEX) &&
            (indexMap->Bottom(ehDsc->ebdEnclosingHndIndex) == UNWIND_INDEX_NOT_IN_TRY))
        {
            // The enclosing fault may use the unwind index. Make sure not to pop the frame too early.
            indexMap->BottomRef(ehDsc->ebdEnclosingHndIndex) = UNWIND_INDEX_NOT_IN_TRY_CATCH;
        }
    }

    // Now assign indices to potentially nested regions protected by fault/finally handlers.
    for (unsigned ehIndex = _compiler->compHndBBtabCount - 1; ehIndex != -1; ehIndex--)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
        if (ehDsc->HasCatchHandler())
        {
            continue;
        }

        // Since we're iterating outer-to-inner, all enclosing unwind indices are already correct.
        if (ehDsc->ebdEnclosingTryIndex != EHblkDsc::NO_ENCLOSING_INDEX)
        {
            unsigned enclosingUnwindIndex = indexMap->Bottom(ehDsc->ebdEnclosingTryIndex);
            if (enclosingUnwindIndex != UNWIND_INDEX_NOT_IN_TRY)
            {
                indexMap->BottomRef(ehIndex) = enclosingUnwindIndex;
            }
        }
    }

#ifdef DEBUG
    if (_compiler->verbose)
    {
        printf("Unwind index map:\n");
        for (unsigned ehIndex = 0; ehIndex < _compiler->compHndBBtabCount; ehIndex++)
        {
            printf("EH#%u:", ehIndex);
            printUnwindIndex(indexMap, indexMap->Bottom(ehIndex));
            printf("\n");
        }
    }
#endif // DEBUG

    return indexMap;
}

CORINFO_GENERIC_HANDLE Llvm::generateUnwindTable()
{
    JITDUMP("\nGenerating the unwind table:\n")
    ArrayStack<CORINFO_LLVM_EH_CLAUSE> clauses(_compiler->getAllocator(CMK_Codegen));
    for (unsigned ehIndex = 0; ehIndex < _compiler->compHndBBtabCount; ehIndex++)
    {
        EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
        if (ehDsc->HasCatchHandler())
        {
            CORINFO_LLVM_EH_CLAUSE clause{};
            if (ehDsc->HasFilter())
            {
                clause.Flags = CORINFO_EH_CLAUSE_FILTER;
                clause.FilterIndex = ehIndex;
            }
            else
            {
                clause.Flags = CORINFO_EH_CLAUSE_NONE;
                clause.ClauseTypeToken = ehDsc->ebdTyp;
            }

            if (ehDsc->ebdEnclosingTryIndex != EHblkDsc::NO_ENCLOSING_INDEX)
            {
                clause.EnclosingIndex = m_unwindIndexMap->Bottom(ehDsc->ebdEnclosingTryIndex);
            }
            else
            {
                clause.EnclosingIndex = UNWIND_INDEX_NOT_IN_TRY;
            }

            unsigned unwindIndex = m_unwindIndexMap->Bottom(ehIndex);
            JITDUMP("EH#%u: T%u, ", unwindIndex, ehIndex);
            if (clause.EnclosingIndex != UNWIND_INDEX_NOT_IN_TRY)
            {
                JITDUMP("enclosed by EH#%i ", clause.EnclosingIndex);
            }
            else
            {
                JITDUMP("top-level ");
            }
            if ((clause.Flags & CORINFO_EH_CLAUSE_FILTER) != 0)
            {
                JITDUMP("(filter: '%s')\n", GetMangledFilterFuncletName(clause.FilterIndex));
            }
            else
            {
                JITDUMP("(class: 0x%04X)\n", clause.ClauseTypeToken);
            }

            assert((unwindIndex - UNWIND_INDEX_BASE) == static_cast<unsigned>(clauses.Height()));
            clauses.Push(clause);
        }
    }

    CORINFO_GENERIC_HANDLE tableSymbolHandle = GetExceptionHandlingTable(&clauses.BottomRef(), clauses.Height());
    return tableSymbolHandle;
}

//------------------------------------------------------------------------
// mayPhysicallyThrow: Can this node cause unwinding?
//
// Certain nodes, such as allocator helpers, are marked no-throw in the Jit
// model, but we must still generate code that allows for the catching of
// exceptions they may produce.
//
// Arguments:
//    node - The node in question
//
// Return Value:
//    Whether "node" may physically throw.
//
bool Llvm::mayPhysicallyThrow(GenTree* node)
{
    if (node->IsCall())
    {
        if (node->IsHelperCall())
        {
            return helperCallMayPhysicallyThrow(node->AsCall()->GetHelperNum());
        }

        // We do not support exceptions propagating through native<->managed boundaries.
        if (node->AsCall()->IsUnmanaged())
        {
            return false;
        }
    }

    return node->OperMayThrow(_compiler);
}

//------------------------------------------------------------------------
// isBlockInFilter: Is this block part of a filter funclet?
//
// Only valid to call after "computeBlocksInFilters" has run.
//
// Arguments:
//    block - The block in question
//
// Return Value:
//    Whether "block" is part of a filter funclet.
//
bool Llvm::isBlockInFilter(BasicBlock* block) const
{
    if (m_blocksInFilters == BlockSetOps::UninitVal())
    {
        assert(!m_anyFilterFunclets);
        assert(!block->hasHndIndex() || !_compiler->ehGetBlockHndDsc(block)->InFilterRegionBBRange(block));
        return false;
    }

    // Ideally, this would be a flag (BBF_*), but we make do with a bitset for now to avoid modifying the frontend.
    assert(m_anyFilterFunclets);
    return BlockSetOps::IsMember(_compiler, m_blocksInFilters, block->bbNum);
}

#ifdef DEBUG
void Llvm::printUnwindIndex(ArrayStack<unsigned>* indexMap, unsigned index)
{
    printf(" (");
    switch (index)
    {
        case UNWIND_INDEX_NONE:
            printf("NO");
            break;
        case UNWIND_INDEX_NOT_IN_TRY:
            printf("ZR");
            break;
        case UNWIND_INDEX_NOT_IN_TRY_CATCH:
            printf("ZF");
            break;
        default:
            for (unsigned ehIndex = 0; ehIndex < _compiler->compHndBBtabCount; ehIndex++)
            {
                EHblkDsc* ehDsc = _compiler->ehGetDsc(ehIndex);
                if (ehDsc->HasCatchHandler() && (indexMap->Bottom(ehIndex) == index))
                {
                    printf("T%u", ehIndex);
                    break;
                }
            }
            break;
    }
    printf(")");
}
#endif // DEBUG
