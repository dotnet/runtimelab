// ================================================================================================================
// |                                       Lowering for the LLVM backend                                          |
// ================================================================================================================

#include "llvm.h"

//------------------------------------------------------------------------
// Convert GT_STORE_LCL_VAR and GT_LCL_VAR to use the shadow stack when the local needs to be GC tracked,
// rewrite calls that returns GC types to do so via a store to a passed in address on the shadow stack.
// Likewise, store the returned value there if required.
//
void Llvm::Lower()
{
    lowerLocals();
    lowerBlocks();
}

//------------------------------------------------------------------------
// lowerLocals: "Lower" locals: strip annotations and insert initializations.
//
// We decouple promoted structs from their field locals: for independently
// promoted ones, we treat the fields as regular temporaries; parameters are
// initialized explicitly via "STORE_LCL_VAR<field>(LCL_FLD<parent>)". For
// dependently promoted cases, we will later rewrite all fields to reference
// the parent instead, and so here strip some annotations ("lvIsParam"). We
// also determine the set of locals which will need to go on the shadow stack,
// zero-initialize them if required, and assign stack offsets.
//
void Llvm::lowerLocals()
{
    populateLlvmArgNums();

    std::vector<LclVarDsc*> shadowStackLocals;
    unsigned shadowStackParamCount = 0;

    _shadowStackLocalsSize = 0;

    for (unsigned lclNum = 0; lclNum < _compiler->lvaCount; lclNum++)
    {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        if (varDsc->lvIsParam)
        {
            if (_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_INDEPENDENT)
            {
                for (unsigned index = 0; index < varDsc->lvFieldCnt; index++)
                {
                    unsigned   fieldLclNum = varDsc->lvFieldLclStart + index;
                    LclVarDsc* fieldVarDsc = _compiler->lvaGetDesc(fieldLclNum);
                    if (fieldVarDsc->lvRefCnt(RCS_NORMAL) != 0)
                    {
                        GenTree* fieldValue =
                            _compiler->gtNewLclFldNode(lclNum, fieldVarDsc->TypeGet(), fieldVarDsc->lvFldOffset);
                        initializeLocalInProlog(fieldLclNum, fieldValue);
                    }

                    fieldVarDsc->lvIsStructField   = false;
                    fieldVarDsc->lvParentLcl       = BAD_VAR_NUM;
                    fieldVarDsc->lvIsParam         = false;
                    fieldVarDsc->lvHasExplicitInit = true;
                }

                varDsc->lvPromoted      = false;
                varDsc->lvFieldLclStart = BAD_VAR_NUM;
                varDsc->lvFieldCnt      = 0;
            }
            else if (_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_DEPENDENT)
            {
                // Dependent promotion, just mark fields as not lvIsParam.
                for (unsigned index = 0; index < varDsc->lvFieldCnt; index++)
                {
                    unsigned   fieldLclNum = varDsc->lvFieldLclStart + index;
                    LclVarDsc* fieldVarDsc = _compiler->lvaGetDesc(fieldLclNum);
                    fieldVarDsc->lvIsParam = false;
                }
            }
        }

        // We don't know if untracked locals are live-in/out of handlers and have to assume the worst.
        if (!varDsc->lvTracked && _compiler->ehAnyFunclets())
        {
            varDsc->lvLiveInOutOfHndlr = 1;
        }

        // GC locals needs to go on the shadow stack for the scan to find them. Locals live-in/out of handlers
        // need to be preserved after the native unwind for the funclets to be callable, thus, they too need to
        // go on the shadow stack (except for parameters to funclets, naturally).
        //
        if (!isFuncletParameter(lclNum) && (varDsc->HasGCPtr() || varDsc->lvLiveInOutOfHndlr))
        {
            if (_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_INDEPENDENT)
            {
                // The individual fields will placed on the shadow stack.
                continue;
            }
            if (_compiler->lvaIsFieldOfDependentlyPromotedStruct(varDsc))
            {
                // The fields will be referenced through the parent.
                continue;
            }

            // We will always need to assign offsets to shadow stack parameters.
            const bool isLlvmParam = varDsc->lvLlvmArgNum != BAD_LLVM_ARG_NUM;
            if (varDsc->lvIsParam && !isLlvmParam)
            {
                shadowStackParamCount++;
                shadowStackLocals.push_back(varDsc);
                continue;
            }

            if (varDsc->lvRefCnt() == 0)
            {
                // No need to place unreferenced temps on the shadow stack.
                continue;
            }

            // We may need to insert initialization:
            //
            //  1) Zero-init if this is a non-parameter GC local, to fullfill frontend's expectations.
            //  2) Copy the initial value if this a parameter not passed on the shadow stack, but
            //     still assigned a home on it.
            //
            // TODO-LLVM: in both cases we should avoid redundant initializations using liveness
            // info (for tracked locals), sharing code with "initializeLocals" in codegen. However,
            // that is currently not possible because late liveness runs after lowering.
            //
            if (!varDsc->lvHasExplicitInit)
            {
                if (isLlvmParam)
                {
                    GenTree* initVal = _compiler->gtNewLclvNode(lclNum, varDsc->TypeGet());
                    initVal->SetRegNum(REG_LLVM);

                    initializeLocalInProlog(lclNum, initVal);
                }
                else if (!_compiler->fgVarNeedsExplicitZeroInit(lclNum, /* bbInALoop */ false, /* bbIsReturn*/ false) ||
                         varDsc->HasGCPtr())
                {
                    var_types zeroType =
                        ((varDsc->TypeGet() == TYP_STRUCT) || (varDsc->TypeGet() == TYP_BLK)) ? TYP_INT
                                                                                              : genActualType(varDsc);
                    initializeLocalInProlog(lclNum, _compiler->gtNewZeroConNode(zeroType));
                }
            }

            shadowStackLocals.push_back(varDsc);
        }
        else
        {
            INDEBUG(varDsc->lvOnFrame = false); // For more accurate frame layout dumping.
        }
    }

    assignShadowStackOffsets(shadowStackLocals, shadowStackParamCount);
}

void Llvm::populateLlvmArgNums()
{
    if (_sigInfo.hasTypeArg())
    {
        failFunctionCompilation();
    }

    if (_compiler->ehAnyFunclets())
    {
        _originalShadowStackLclNum = _compiler->lvaGrabTemp(true DEBUGARG("original shadowstack"));
        LclVarDsc* originalShadowStackVarDsc = _compiler->lvaGetDesc(_originalShadowStackLclNum);
        originalShadowStackVarDsc->lvType = TYP_I_IMPL;
        originalShadowStackVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
    }

    _shadowStackLclNum = _compiler->lvaGrabTemp(true DEBUGARG("shadowstack"));
    LclVarDsc* shadowStackVarDsc = _compiler->lvaGetDesc(_shadowStackLclNum);
    unsigned   nextLlvmArgNum    = 0;

    shadowStackVarDsc->lvLlvmArgNum = nextLlvmArgNum++;
    shadowStackVarDsc->lvType = TYP_I_IMPL;
    shadowStackVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
    shadowStackVarDsc->lvIsParam = true;

    if (needsReturnStackSlot(_compiler, _sigInfo.retType, _sigInfo.retTypeClass))
    {
        _retAddressLclNum = _compiler->lvaGrabTemp(true DEBUGARG("returnslot"));
        LclVarDsc* retAddressVarDsc  = _compiler->lvaGetDesc(_retAddressLclNum);
        retAddressVarDsc->lvLlvmArgNum = nextLlvmArgNum++;
        retAddressVarDsc->lvType       = TYP_I_IMPL;
        retAddressVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
        retAddressVarDsc->lvIsParam    = true;
    }

    CORINFO_ARG_LIST_HANDLE sigArgs = _sigInfo.args;
    unsigned firstCorInfoArgLocalNum = 0;
    if (_sigInfo.hasThis())
    {
        firstCorInfoArgLocalNum++;
    }

    for (unsigned int i = 0; i < _sigInfo.numArgs; i++, sigArgs = _info.compCompHnd->getArgNext(sigArgs))
    {
        CORINFO_CLASS_HANDLE classHnd;
        CorInfoType          corInfoType = strip(_info.compCompHnd->getArgType(&_sigInfo, sigArgs, &classHnd));
        if (canStoreArgOnLlvmStack(_compiler, corInfoType, classHnd))
        {
            LclVarDsc* varDsc = _compiler->lvaGetDesc(i + firstCorInfoArgLocalNum);

            varDsc->lvLlvmArgNum = nextLlvmArgNum++;
            varDsc->lvCorInfoType = corInfoType;
            varDsc->lvClassHnd = classHnd;
        }
    }

    _llvmArgCount = nextLlvmArgNum;
}

void Llvm::assignShadowStackOffsets(std::vector<LclVarDsc*>& shadowStackLocals, unsigned shadowStackParamCount)
{
    if (_compiler->opts.OptimizationEnabled())
    {
        std::sort(shadowStackLocals.begin() + shadowStackParamCount, shadowStackLocals.end(),
                  [](const LclVarDsc* lhs, const LclVarDsc* rhs) { return lhs->lvRefCntWtd() > rhs->lvRefCntWtd(); });
    }

    unsigned offset = 0;
    auto assignOffset = [this, &offset](LclVarDsc* varDsc) {
        if (varDsc->TypeGet() == TYP_BLK)
        {
            assert((varDsc->lvSize() % TARGET_POINTER_SIZE) == 0);

            offset = roundUp(offset, TARGET_POINTER_SIZE);
            varDsc->SetStackOffset(offset);
            offset += varDsc->lvSize();
        }
        else
        {
            CorInfoType corInfoType = toCorInfoType(varDsc->TypeGet());
            CORINFO_CLASS_HANDLE classHandle = varTypeIsStruct(varDsc) ? varDsc->GetStructHnd() : NO_CLASS_HANDLE;

            offset = padOffset(corInfoType, classHandle, offset);
            varDsc->SetStackOffset(offset);
            offset = padNextOffset(corInfoType, classHandle, offset);
        }

        // We will use this as the indication that the local has a home on the shadow stack.
        varDsc->SetRegNum(REG_STK);
    };

    // First, we process the parameters, since their offsets are fixed by the ABI. Then, we process the rest.
    // Doing this ensures we don't count LLVM parameters live on the shadow stack as shadow parameters.
    //
    unsigned assignedShadowStackParamCount = 0;
    for (unsigned i = 0; i < shadowStackLocals.size(); i++)
    {
        LclVarDsc* varDsc = shadowStackLocals.at(i);

        if (varDsc->lvIsParam && (varDsc->lvLlvmArgNum == BAD_LLVM_ARG_NUM))
        {
            assignOffset(varDsc);
            assignedShadowStackParamCount++;
            varDsc->lvIsParam = false; // After lowering, "lvIsParam" <=> "is LLVM parameter".

            if (assignedShadowStackParamCount == shadowStackParamCount)
            {
                break;
            }
        }
    }

    for (unsigned i = 0; i < shadowStackLocals.size(); i++)
    {
        LclVarDsc* varDsc = shadowStackLocals.at(i);

        if (!isShadowFrameLocal(varDsc))
        {
            assignOffset(varDsc);
        }
    }

    _shadowStackLocalsSize = AlignUp(offset, TARGET_POINTER_SIZE);

    _compiler->compLclFrameSize = _shadowStackLocalsSize;
    _compiler->lvaDoneFrameLayout = Compiler::TENTATIVE_FRAME_LAYOUT;

    JITDUMP("\nLocals after shadow stack layout:\n");
    JITDUMPEXEC(_compiler->lvaTableDump());
    JITDUMP("\n");

    _compiler->lvaDoneFrameLayout = Compiler::INITIAL_FRAME_LAYOUT;
}

void Llvm::initializeLocalInProlog(unsigned lclNum, GenTree* value)
{
    JITDUMP("Adding initialization for V%02u, %s:\n", lclNum, _compiler->lvaGetDesc(lclNum)->lvReason);

    _compiler->fgEnsureFirstBBisScratch();
    LIR::Range& firstBlockRange = LIR::AsRange(_compiler->fgFirstBB);

    firstBlockRange.InsertAtBeginning(value);

    // TYP_BLK locals have to be handled specially as they can only be referenced indirectly.
    // TODO-LLVM: use STORE_LCL_FLD<struct> here once enough of upstream is merged.
    GenTree* store;
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    if (varDsc->TypeGet() == TYP_BLK)
    {
        GenTree* lclAddr = _compiler->gtNewLclVarAddrNode(lclNum);
        lclAddr->gtFlags |= GTF_VAR_DEF;
        firstBlockRange.InsertAfter(value, lclAddr);

        ClassLayout* layout = _compiler->typGetBlkLayout(varDsc->lvExactSize);
        store = new (_compiler, GT_STORE_BLK) GenTreeBlk(GT_STORE_BLK, TYP_STRUCT, lclAddr, value, layout);
        store->gtFlags |= (GTF_ASG | GTF_IND_NONFAULTING);
        firstBlockRange.InsertAfter(lclAddr, store);
    }
    else
    {
        store = _compiler->gtNewStoreLclVar(lclNum, value);
        firstBlockRange.InsertAfter(value, store);
    }

    DISPTREERANGE(firstBlockRange, store);
}

void Llvm::lowerBlocks()
{
    for (BasicBlock* block : _compiler->Blocks())
    {
        lowerBlock(block);
        block->bbFlags |= BBF_MARKED;
    }

    // Lowering may insert out-of-line throw helper blocks that must themselves be lowered. We do not
    // need a more complex "to a fixed point" loop here because lowering these throw helpers will not
    // create new blocks.
    //
    for (BasicBlock* block : _compiler->Blocks())
    {
        if ((block->bbFlags & BBF_MARKED) == 0)
        {
            lowerBlock(block);
        }

        block->bbFlags &= ~BBF_MARKED;
    }

    m_currentBlock = nullptr;
}

void Llvm::lowerBlock(BasicBlock* block)
{
    m_currentBlock = block;
    _currentRange = &LIR::AsRange(block);

    for (GenTree* node : CurrentRange())
    {
        switch (node->OperGet())
        {
            case GT_LCL_VAR:
            case GT_LCL_FLD:
            case GT_LCL_VAR_ADDR:
            case GT_LCL_FLD_ADDR:
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
                lowerFieldOfDependentlyPromotedStruct(node);

                if (node->OperIs(GT_STORE_LCL_VAR))
                {
                    lowerStoreLcl(node->AsLclVarCommon());
                }

                if (node->OperIsLocal() || node->OperIsLocalAddr())
                {
                    ConvertShadowStackLocalNode(node->AsLclVarCommon());
                }

                if (node->OperIsLocalAddr() || node->OperIsLocalField())
                {
                    // Indicates that this local is to live on the LLVM frame, and will not participate in SSA.
                    _compiler->lvaGetDesc(node->AsLclVarCommon())->lvHasLocalAddr = 1;
                }
                break;

            case GT_CALL:
                lowerCall(node->AsCall());
                break;

            case GT_CATCH_ARG:
                lowerCatchArg(node);
                break;

            case GT_IND:
            case GT_OBJ:
            case GT_BLK:
            case GT_NULLCHECK:
            case GT_STOREIND:
                lowerIndir(node->AsIndir());
                break;

            case GT_STORE_BLK:
            case GT_STORE_OBJ:
                lowerStoreBlk(node->AsBlk());
                break;

            case GT_STORE_DYN_BLK:
                lowerStoreDynBlk(node->AsStoreDynBlk());
                break;

            case GT_DIV:
            case GT_MOD:
            case GT_UDIV:
            case GT_UMOD:
                lowerDivMod(node->AsOp());
                break;

            case GT_RETURN:
                lowerReturn(node->AsUnOp());
                break;

            default:
                break;
        }
    }

    INDEBUG(CurrentRange().CheckLIR(_compiler, /* checkUnusedValues */ true));

}

void Llvm::lowerStoreLcl(GenTreeLclVarCommon* storeLclNode)
{
    LclVarDsc* addrVarDsc = _compiler->lvaGetDesc(storeLclNode->GetLclNum());
    GenTree* data = storeLclNode->gtGetOp1();

    if (addrVarDsc->CanBeReplacedWithItsField(_compiler))
    {
        ClassLayout* layout      = addrVarDsc->GetLayout();
        var_types    addrVarType = addrVarDsc->TypeGet();

        storeLclNode->SetOper(GT_LCL_VAR_ADDR);
        storeLclNode->ChangeType(TYP_I_IMPL);
        storeLclNode->SetLclNum(addrVarDsc->lvFieldLclStart);

        GenTree* storeObjNode = new (_compiler, GT_STORE_OBJ) GenTreeObj(addrVarType, storeLclNode, data, layout);
        storeObjNode->gtFlags |= (GTF_ASG | GTF_IND_NONFAULTING);

        CurrentRange().InsertAfter(storeLclNode, storeObjNode);
    }

    if (storeLclNode->TypeIs(TYP_STRUCT) && data->TypeIs(TYP_STRUCT))
    {
        normalizeStructUse(data, addrVarDsc->GetLayout());
    }
}

void Llvm::lowerFieldOfDependentlyPromotedStruct(GenTree* node)
{
    if (node->OperIsLocal() || node->OperIsLocalAddr())
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
                    break;

                case GT_LCL_VAR_ADDR:
                    lclVar->SetOper(GT_LCL_FLD_ADDR);
                    break;
            }

            lclVar->SetLclNum(varDsc->lvParentLcl);
            lclVar->AsLclFld()->SetLclOffs(varDsc->lvFldOffset + offset);

            if ((node->gtFlags & GTF_VAR_DEF) != 0)
            {
                // Conservatively assume these become partial.
                // TODO-ADDR: only apply to stores be precise.
                node->gtFlags |= GTF_VAR_USEASG;
            }
        }
    }
}

void Llvm::ConvertShadowStackLocalNode(GenTreeLclVarCommon* node)
{
    GenTreeLclVarCommon* lclVar = node->AsLclVarCommon();
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclVar->GetLclNum());

    if (isShadowFrameLocal(varDsc) && (lclVar->GetRegNum() == REG_NA))
    {
        // Funclets (especially filters) will be called by the dispatcher while live state still exists
        // on shadow frames below (in the tradional sense, where stacks grow down) them. For this reason,
        // funclets will access state from the original frame via a dedicated shadow stack pointer, and
        // use the actual shadow stack for calls.
        unsigned shadowStackLclNum = CurrentBlock()->hasHndIndex() ? _originalShadowStackLclNum : _shadowStackLclNum;
        GenTree* lclAddress =
            insertShadowStackAddr(node, varDsc->GetStackOffset() + node->GetLclOffs(), shadowStackLclNum);

        genTreeOps indirOper;
        GenTree* storedValue = nullptr;
        switch (node->OperGet())
        {
            case GT_STORE_LCL_VAR:
                indirOper = lclVar->TypeIs(TYP_STRUCT) ? GT_STORE_OBJ : GT_STOREIND;
                storedValue = node->AsOp()->gtGetOp1();
                break;
            case GT_LCL_VAR:
                indirOper = lclVar->TypeIs(TYP_STRUCT) ? GT_OBJ : GT_IND;
                break;
            case GT_LCL_FLD:
                if (lclVar->TypeIs(TYP_STRUCT))
                {
                    // TODO-LLVM: handle once we merge enough of upstream to have "GenTreeLclFld::GetLayout".
                    failFunctionCompilation();
                }
                indirOper = GT_IND;
                break;
            case GT_LCL_VAR_ADDR:
            case GT_LCL_FLD_ADDR:
                indirOper = GT_NONE;
                break;
            case GT_STORE_LCL_FLD:
                indirOper   = lclVar->TypeIs(TYP_STRUCT) ? GT_STORE_OBJ : GT_STOREIND;
                storedValue = node->AsOp()->gtGetOp1();
                break;
            default:
                unreached();
        }
        if (GenTree::OperIsIndir(indirOper))
        {
            node->ChangeOper(indirOper);
            node->AsIndir()->SetAddr(lclAddress);
            node->gtFlags |= GTF_IND_NONFAULTING;
        }
        if (GenTree::OperIsStore(indirOper))
        {
            node->gtFlags |= GTF_IND_TGT_NOT_HEAP;
            node->AsOp()->gtOp2 = storedValue;
        }
        if (GenTree::OperIsBlk(indirOper))
        {
            node->AsBlk()->SetLayout(varDsc->GetLayout());
            node->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
        }

        if (indirOper == GT_NONE)
        {
            // Local address nodes are directly replaced with the ADD.
            CurrentRange().Remove(lclAddress);
            node->ReplaceWith(lclAddress, _compiler);
        }
    }
}

void Llvm::lowerCall(GenTreeCall* callNode)
{
    failUnsupportedCalls(callNode);

    if (callNode->IsHelperCall(_compiler, CORINFO_HELP_RETHROW))
    {
        lowerRethrow(callNode);
    }

    lowerCallToShadowStack(callNode);

    // If there is a no return, or always throw call, delete the dead code so we can add unreachable
    // statement immediately, and not after any dead RET.
    if (_compiler->fgIsThrow(callNode) || callNode->IsNoReturn())
    {
        while (CurrentRange().LastNode() != callNode)
        {
            CurrentRange().Remove(CurrentRange().LastNode(), /* markOperandsUnused */ true);
        }
    }
}

void Llvm::lowerRethrow(GenTreeCall* callNode)
{
    assert(callNode->IsHelperCall(_compiler, CORINFO_HELP_RETHROW));

    // Language in ECMA 335 I.12.4.2.8.2.2 clearly states that rethrows nested inside finallys are
    // legal, however, neither C# nor the old verification system allow this. CoreCLR behavior was
    // not tested. Implementing this would imply saving the exception object to the "original" shadow
    // frame shared between funclets. For now we punt.
    if (!_compiler->ehGetDsc(CurrentBlock()->getHndIndex())->HasCatchHandler())
    {
        IMPL_LIMITATION("Nested rethrow");
    }

    // A rethrow is a special throw that preserves the stack trace. Our helper we use for rethrow has
    // the equivalent of a managed signature "void (object*)", i. e. takes the exception object address
    // explicitly. Add it here, before the general call lowering.
    assert(callNode->gtCallArgs == nullptr);
    callNode->ResetArgInfo();

    GenTree* excObjAddr = insertShadowStackAddr(callNode, getCatchArgOffset(), _shadowStackLclNum);
    callNode->gtCallArgs = _compiler->gtNewCallArgs(excObjAddr);

    _compiler->fgInitArgInfo(callNode);
}

void Llvm::lowerCatchArg(GenTree* catchArgNode)
{
    GenTree* excObjAddr = insertShadowStackAddr(catchArgNode, getCatchArgOffset(), _shadowStackLclNum);

    catchArgNode->ChangeOper(GT_IND);
    catchArgNode->gtFlags |= GTF_IND_NONFAULTING;
    catchArgNode->AsIndir()->SetAddr(excObjAddr);
}

void Llvm::lowerIndir(GenTreeIndir* indirNode)
{
    if ((indirNode->gtFlags & GTF_IND_NONFAULTING) == 0)
    {
        _compiler->fgAddCodeRef(CurrentBlock(), _compiler->bbThrowIndex(CurrentBlock()), SCK_NULL_REF_EXCPN);
    }
}

void Llvm::lowerStoreBlk(GenTreeBlk* storeBlkNode)
{
    assert(storeBlkNode->OperIs(GT_STORE_BLK, GT_STORE_OBJ));

    GenTree* src = storeBlkNode->Data();

    // Fix up type mismatches on copies for codegen.
    if (storeBlkNode->OperIsCopyBlkOp())
    {
        ClassLayout* dstLayout = storeBlkNode->GetLayout();
        if (src->OperIs(GT_IND))
        {
            src->SetOper(GT_BLK);
            src->AsBlk()->SetLayout(dstLayout);
            src->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
        }
        else
        {
            CORINFO_CLASS_HANDLE srcHandle = _compiler->gtGetStructHandleIfPresent(src);

            if (dstLayout->GetClassHandle() != srcHandle)
            {
                ClassLayout* dataLayout;
                if (srcHandle != NO_CLASS_HANDLE)
                {
                    dataLayout = _compiler->typGetObjLayout(srcHandle);
                }
                else
                {
                    assert(src->OperIs(GT_BLK));
                    dataLayout = src->AsBlk()->GetLayout();
                }

                storeBlkNode->SetLayout(dataLayout);
            }
        }
    }
    else
    {
        src->SetContained();
    }

    // A zero-sized block store is a no-op. Lower it away.
    if (storeBlkNode->Size() == 0)
    {
        assert(storeBlkNode->OperIsInitBlkOp() || storeBlkNode->Data()->OperIs(GT_BLK));

        storeBlkNode->Addr()->SetUnusedValue();
        CurrentRange().Remove(storeBlkNode->Data(), /* markOperandsUnused */ true);
        CurrentRange().Remove(storeBlkNode);
    }
    else
    {
        lowerIndir(storeBlkNode);
    }
}

void Llvm::lowerStoreDynBlk(GenTreeStoreDynBlk* storeDynBlkNode)
{
    storeDynBlkNode->Data()->SetContained();
    lowerIndir(storeDynBlkNode);
}

void Llvm::lowerDivMod(GenTreeOp* divModNode)
{
    assert(divModNode->OperIs(GT_DIV, GT_MOD, GT_UDIV, GT_UMOD));

    // TODO-LLVM: use OperExceptions here when enough of upstream is merged.
    if (divModNode->OperMayThrow(_compiler))
    {
        _compiler->fgAddCodeRef(CurrentBlock(), _compiler->bbThrowIndex(CurrentBlock()), SCK_DIV_BY_ZERO);

        if (divModNode->OperIs(GT_DIV, GT_MOD))
        {
            _compiler->fgAddCodeRef(CurrentBlock(), _compiler->bbThrowIndex(CurrentBlock()), SCK_OVERFLOW);
        }
    }
}

void Llvm::lowerReturn(GenTreeUnOp* retNode)
{
    if (retNode->TypeIs(TYP_VOID))
    {
        // Nothing to do.
        return;
    }

    GenTree* retVal = retNode->gtGetOp1();
    ClassLayout* retLayout = retNode->TypeIs(TYP_STRUCT) ? _compiler->typGetObjLayout(_sigInfo.retTypeClass) : nullptr;
    if (retNode->TypeIs(TYP_STRUCT) && retVal->TypeIs(TYP_STRUCT))
    {
        normalizeStructUse(retVal, retLayout);
    }

    bool isStructZero = retNode->TypeIs(TYP_STRUCT) && retVal->IsIntegralConst(0);

    if (_retAddressLclNum != BAD_VAR_NUM)
    {
        GenTreeLclVar* retAddrNode = _compiler->gtNewLclvNode(_retAddressLclNum, TYP_I_IMPL);
        GenTree* storeNode;
        if (isStructZero)
        {
            storeNode = new (_compiler, GT_STORE_BLK) GenTreeBlk(GT_STORE_BLK, TYP_STRUCT, retAddrNode, retVal,
                                                                 retLayout);
            storeNode->gtFlags |= (GTF_ASG | GTF_IND_NONFAULTING);
        }
        else
        {
            // Morph will not create size mismatches beyond the "zero" case handled above,
            // so here we can store the value (of whichever "actual" type) directly.
            storeNode = createShadowStackStoreNode(genActualType(retVal), retAddrNode, retVal);
        }

        retNode->gtOp1 = nullptr;
        retNode->ChangeType(TYP_VOID);

        CurrentRange().InsertBefore(retNode, retAddrNode, storeNode);
    }
    // Morph can create pretty much any type mismatch here (struct <-> primitive, primitive <-> struct, etc).
    // Fix these by spilling to a temporary (we could do better but it is not worth it, upstream will get rid
    // of the important cases). Exclude zero-init-ed structs (codegen supports them directly).
    else if ((retNode->TypeGet() != genActualType(retVal)) && !isStructZero)
    {
        LIR::Use retValUse(CurrentRange(), &retNode->gtOp1, retNode);
        retValUse.ReplaceWithLclVar(_compiler);

        GenTreeLclVar* lclVarNode = retValUse.Def()->AsLclVar();
        _compiler->lvaGetDesc(lclVarNode)->lvHasLocalAddr = true;

        if (retNode->TypeIs(TYP_STRUCT))
        {
            // TODO-LLVM: replace this with TYP_STRUCT LCL_FLD once it is available.
            lclVarNode->SetOper(GT_LCL_VAR_ADDR);
            GenTree* objNode = _compiler->gtNewObjNode(_sigInfo.retTypeClass, lclVarNode);
            objNode->gtFlags |= GTF_IND_NONFAULTING;

            retValUse.ReplaceWith(objNode);
            CurrentRange().InsertBefore(retNode, objNode);
        }
        else
        {
            // TODO-LLVM: change to "SetOper" once enough of upstream is merged.
            lclVarNode->ChangeOper(GT_LCL_FLD);
            lclVarNode->ChangeType(_info.compRetType);
        }
    }
}

//------------------------------------------------------------------------
// lowerCallToShadowStack: Lower the call, rewriting its arguments.
//
// This method has two primary objectives:
//  1) Transfer the information about the arguments from arg info to explicit
//     PutArgType nodes, to make it easy for codegen to consume it. Also, get
//     rid of the late/non-late argument distinction, by sorting the inserted nodes
//     in the original evaluation order, matching that of them in the signature.
//  2) Rewrite arguments and the return to be stored on the shadow stack. We take
//     the arguments which need to be on the shadow stack, remove them from the call
//     arguments list, store their values on the shadow stack, at offsets calculated
//     in a simple increasing order, matching the signature. We also rewrite returns
//     that must be on the shadow stack, see "lowerCallReturn".
//
void Llvm::lowerCallToShadowStack(GenTreeCall* callNode)
{
    // rewrite the args, adding shadow stack, and moving gc tracked args to the shadow stack
    unsigned shadowStackUseOffest = 0;

    fgArgInfo*                 argInfo     = callNode->fgArgInfo;
    unsigned int               argCount    = argInfo->ArgCount();
    fgArgTabEntry**            argTable    = argInfo->ArgTable();
    std::vector<OperandArgNum> sortedArgs  = std::vector<OperandArgNum>(argCount);
    OperandArgNum*             sortedData  = sortedArgs.data();

    callNode->ResetArgInfo();
    callNode->gtCallThisArg = nullptr;
    callNode->gtCallArgs = nullptr;
    callNode->gtCallLateArgs = nullptr;

    GenTreeCall::Use* lastArg = nullptr;
    if (callHasShadowStackArg(callNode))
    {
        GenTree* calleeShadowStack = insertShadowStackAddr(callNode, getCurrentShadowFrameSize(), _shadowStackLclNum);

        GenTreePutArgType* calleeShadowStackPutArg =
            _compiler->gtNewPutArgType(calleeShadowStack, CORINFO_TYPE_PTR, NO_CLASS_HANDLE);
#ifdef DEBUG
        calleeShadowStackPutArg->SetArgNum(-1); // -1 will represent the shadowstack arg for LLVM
#endif
        CurrentRange().InsertBefore(callNode, calleeShadowStackPutArg);

        lastArg = _compiler->gtNewCallArgs(calleeShadowStackPutArg);
        callNode->gtCallArgs = lastArg;
    }

    // Add the return slot after the shadow stack arg.
    lastArg = lowerCallReturn(callNode, lastArg);

    for (unsigned i = 0; i < argCount; i++)
    {
        fgArgTabEntry* curArgTabEntry = argTable[i];
        unsigned int   argNum         = curArgTabEntry->argNum;
        OperandArgNum  opAndArg       = {argNum, curArgTabEntry->GetNode()};
        sortedData[argNum]            = opAndArg;
    }

    CORINFO_SIG_INFO* sigInfo = nullptr;
    CORINFO_ARG_LIST_HANDLE sigArgs = nullptr;
    const HelperFuncInfo* helperInfo = nullptr;
    unsigned sigArgCount = 0;
    if (callNode->IsHelperCall())
    {
        helperInfo = &getHelperFuncInfo(_compiler->eeGetHelperNum(callNode->gtCallMethHnd));
        sigArgCount = helperInfo->GetSigArgCount();
    }
    else
    {
        sigInfo = callNode->callSig;
        sigArgs = sigInfo->args;
        sigArgCount = sigInfo->numArgs;
    }

    // Relies on the fact all arguments not in the signature come before those that are.
    unsigned firstSigArgIx = argCount - sigArgCount;
    unsigned argIx = 0;

    for (OperandArgNum opAndArg : sortedArgs)
    {
        GenTree*             argNode     = opAndArg.operand;
        CORINFO_CLASS_HANDLE clsHnd      = NO_CLASS_HANDLE;
        CorInfoType          corInfoType = CORINFO_TYPE_UNDEF;
        bool                 isSigArg    = argIx >= firstSigArgIx;

        // We currently do not place any args for helpers on the shadow stack. This is a potential GC
        // hole and not correct ABI-wise for managed helpers. TODO-LLVM: investigate and fix issues.
        bool argOnShadowStack = false;
        if (sigInfo != nullptr)
        {
            // Is this an in-signature argument?
            if (isSigArg)
            {
                corInfoType = strip(_info.compCompHnd->getArgType(sigInfo, sigArgs, &clsHnd));
            }
            else // Not-in-sig arguments. We need to handle these specially.
            {
                if (sigInfo->hasThis() && (opAndArg.argNum == 0))
                {
                    corInfoType = argNode->TypeIs(TYP_REF) ? CORINFO_TYPE_CLASS : CORINFO_TYPE_BYREF;
                }
                else
                {
                    // TODO-LLVM: this is not fully correct (e. g. we may think pointer an integer),
                    // but sufficient for now. Handle precisely once we merge the call args refactor.
                    corInfoType = toCorInfoType(genActualType(argNode));
                }
            }

            argOnShadowStack = !canStoreArgOnLlvmStack(_compiler, corInfoType, clsHnd);
        }
        else
        {
            assert(helperInfo != nullptr);
            if (!isSigArg)
            {
                // There are helpers that do not have a specified signature (have a variable number of args).
                // We'll have to wait for upstream call args changes to get merged to handle those properly.
                failFunctionCompilation();
            }

            corInfoType = helperInfo->GetSigArgType(argIx);
            clsHnd = helperInfo->GetSigArgClass(_compiler, argIx);
        }

        if (argOnShadowStack)
        {
            if (corInfoType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffest = padOffset(corInfoType, clsHnd, shadowStackUseOffest);
            }

            unsigned shadowFrameSize = getCurrentShadowFrameSize();
            if (argNode->OperIs(GT_FIELD_LIST))
            {
                for (GenTreeFieldList::Use& use : argNode->AsFieldList()->Uses())
                {
                    assert(use.GetType() != TYP_STRUCT);

                    unsigned fieldOffsetValue = shadowFrameSize + shadowStackUseOffest + use.GetOffset();
                    GenTree* fieldSlotAddr = insertShadowStackAddr(callNode, fieldOffsetValue, _shadowStackLclNum);
                    GenTree* fieldStoreNode = createShadowStackStoreNode(use.GetType(), fieldSlotAddr, use.GetNode());

                    CurrentRange().InsertBefore(callNode, fieldStoreNode);
                }

                CurrentRange().Remove(argNode);
            }
            else
            {
                unsigned offsetValue = shadowFrameSize + shadowStackUseOffest;
                GenTree* slotAddr  = insertShadowStackAddr(callNode, offsetValue, _shadowStackLclNum);
                GenTree* storeNode = createShadowStackStoreNode(argNode->TypeGet(), slotAddr, argNode);

                CurrentRange().InsertBefore(callNode, storeNode);
            }

            if (corInfoType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffest = padNextOffset(corInfoType, clsHnd, shadowStackUseOffest);
            }
            else
            {
                shadowStackUseOffest += TARGET_POINTER_SIZE;
            }
        }
        else
        {
            // Arg on LLVM stack.
            GenTreePutArgType* putArg = _compiler->gtNewPutArgType(argNode, corInfoType, clsHnd);
#if DEBUG
            putArg->SetArgNum(opAndArg.argNum);
#endif
            if (lastArg == nullptr)
            {
                lastArg = _compiler->gtNewCallArgs(putArg);
                callNode->gtCallArgs = lastArg;
            }
            else
            {
                lastArg = _compiler->gtInsertNewCallArgAfter(putArg, lastArg);
            }

            CurrentRange().InsertBefore(callNode, putArg);
        }

        if (isSigArg && (sigInfo != nullptr))
        {
            sigArgs = _info.compCompHnd->getArgNext(sigArgs);
        }

        argIx++;
    }
}

void Llvm::failUnsupportedCalls(GenTreeCall* callNode)
{
    if (callNode->IsHelperCall())
    {
        return;
    }

    if (callNode->NeedsNullCheck())
    {
        // We need to insert the null check when lowering args.
        failFunctionCompilation();
    }

    if (callNode->IsUnmanaged())
    {
        failFunctionCompilation();
    }

    // we can't do these yet
    if ((callNode->gtCallType != CT_INDIRECT && IsRuntimeImport(callNode->gtCallMethHnd)) || callNode->IsTailCall())
    {
        failFunctionCompilation();
    }

    CORINFO_SIG_INFO* calleeSigInfo = callNode->callSig;
    // TODO-LLVM: not attempting to compile generic signatures with context arg via clrjit yet
    // Investigate which methods do not get callSig set - happens currently with the Generics test
    if (calleeSigInfo == nullptr || calleeSigInfo->hasTypeArg())
    {
        failFunctionCompilation();
    }

    if (callNode->gtCallArgs != nullptr)
    {
        for (GenTree* operand : callNode->Operands())
        {
            if (operand->IsArgPlaceHolderNode() || !operand->IsValue())
            {
                // Either of these situations may happen with calls.
                continue;
            }
            if (operand == callNode->gtControlExpr || operand == callNode->gtCallAddr)
            {
                // vtable target or indirect target
                continue;
            }

            fgArgTabEntry* curArgTabEntry = _compiler->gtArgEntryByNode(callNode, operand);
            if (curArgTabEntry->nonStandardArgKind == NonStandardArgKind::VirtualStubCell)
            {
                failFunctionCompilation();
            }
        }
    }
}

// If the return type must be GC tracked, removes the return type
// and converts to a return slot arg, modifying the call args, and building the necessary IR
GenTreeCall::Use* Llvm::lowerCallReturn(GenTreeCall* callNode, GenTreeCall::Use* insertAfterArg)
{
    GenTreeCall::Use* lastArg = insertAfterArg;

    if (needsReturnStackSlot(_compiler, callNode))
    {
        // replace the "CALL ref" with a "CALL void" that takes a return address as the first argument
        GenTree* returnValueAddress = insertShadowStackAddr(callNode, getCurrentShadowFrameSize(), _shadowStackLclNum);

        // create temp for the return address
        unsigned   returnTempNum    = _compiler->lvaGrabTemp(false DEBUGARG("return value address"));
        LclVarDsc* returnAddrVarDsc = _compiler->lvaGetDesc(returnTempNum);
        returnAddrVarDsc->lvType    = TYP_I_IMPL;

        GenTree* addrStore     = _compiler->gtNewStoreLclVar(returnTempNum, returnValueAddress);
        GenTree* returnAddrLcl = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);

        GenTree* returnAddrLclAfterCall = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);
        GenTree* indirNode;
        if (callNode->TypeIs(TYP_STRUCT))
        {
            indirNode = _compiler->gtNewObjNode(callNode->gtRetClsHnd, returnAddrLclAfterCall);
        }
        else
        {
            indirNode = _compiler->gtNewIndir(callNode->TypeGet(), returnAddrLclAfterCall);
        }
        indirNode->gtFlags |= GTF_IND_NONFAULTING;
        indirNode->SetAllEffectsFlags(GTF_EMPTY);

        LIR::Use callUse;
        if (CurrentRange().TryGetUse(callNode, &callUse))
        {
            callUse.ReplaceWith(indirNode);
        }
        else
        {
            indirNode->SetUnusedValue();
            callNode->ClearUnusedValue();
        }

        GenTreePutArgType* putArg = _compiler->gtNewPutArgType(returnAddrLcl, CORINFO_TYPE_PTR, NO_CLASS_HANDLE);
#if DEBUG
        putArg->SetArgNum(-2);  // -2 will represent the return arg for LLVM
#endif
        lastArg = _compiler->gtInsertNewCallArgAfter(putArg, insertAfterArg);

        callNode->gtReturnType = TYP_VOID;
        callNode->gtCorInfoType = CORINFO_TYPE_VOID;
        callNode->ChangeType(TYP_VOID);

        CurrentRange().InsertBefore(callNode, addrStore, returnAddrLcl, putArg);
        CurrentRange().InsertAfter(callNode, returnAddrLclAfterCall, indirNode);
    }
    else
    {
        if (callNode->IsHelperCall())
        {
            CorInfoHelpFunc helperFunc = _compiler->eeGetHelperNum(callNode->gtCallMethHnd);
            callNode->gtCorInfoType = getHelperFuncInfo(helperFunc).GetSigReturnType();
        }
        else
        {
            callNode->gtCorInfoType = callNode->callSig->retType;
        }
    }

    return lastArg;
}

//------------------------------------------------------------------------
// normalizeStructUse: Retype "node" to have the exact type of "layout".
//
// LLVM has a strict constraint on uses and users of structs: they must
// have the exact same type, while IR only requires "layout compatibility".
// So in lowering we retype uses (and users) to match LLVM's expectations.
//
// Arguments:
//    node   - The struct node to retype
//    layout - The target layout
//
void Llvm::normalizeStructUse(GenTree* node, ClassLayout* layout)
{
    // Note on SIMD: we will support it in codegen via bitcasts.
    assert(node->TypeIs(TYP_STRUCT));

    // "IND<struct>" nodes always need to be normalized.
    if (node->OperIs(GT_IND))
    {
        node->SetOper(GT_BLK);
        node->AsBlk()->SetLayout(layout);
        node->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
    }
    else
    {
        CORINFO_CLASS_HANDLE useHandle = _compiler->gtGetStructHandleIfPresent(node);

        // Note both can be blocks ("NO_CLASS_HANDLE"), in which case we don't need to do anything.
        if (useHandle != layout->GetClassHandle())
        {
            switch (node->OperGet())
            {
                case GT_BLK:
                case GT_OBJ:
                    node->AsBlk()->SetLayout(layout);
                    if (layout->IsBlockLayout() && node->OperIs(GT_OBJ))
                    {
                        // OBJ nodes cannot have block layouts.
                        node->SetOper(GT_BLK);
                    }
                    break;

                case GT_LCL_VAR:
                {
                    unsigned lclNum = node->AsLclVarCommon()->GetLclNum();
                    GenTree* lclAddrNode = _compiler->gtNewLclVarAddrNode(lclNum);
                    _compiler->lvaGetDesc(lclNum)->lvHasLocalAddr = true;

                    node->ChangeOper(GT_OBJ);
                    node->AsObj()->SetAddr(lclAddrNode);
                    node->AsObj()->SetLayout(layout);
                    node->AsObj()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
                    node->gtFlags |= GTF_IND_NONFAULTING;

                    CurrentRange().InsertBefore(node, lclAddrNode);
                }
                break;

                case GT_CALL:
                    // TODO-LLVM: implement by spilling to a local.
                    failFunctionCompilation();

                case GT_LCL_FLD:
                    // TODO-LLVM: handle by altering the layout once enough of upstream is merged.
                    failFunctionCompilation();

                default:
                    unreached();
            }
        }
    }
}

GenTree* Llvm::createStoreNode(var_types storeType, GenTree* addr, GenTree* data)
{
    assert(data->TypeIs(TYP_STRUCT) == (storeType == TYP_STRUCT));

    GenTree* storeNode;
    if (storeType == TYP_STRUCT)
    {
        ClassLayout* layout;
        // TODO-LLVM: use "GenTree::GetLayout" once enough of upstream is merged.
        if (data->OperIsBlk())
        {
            layout = data->AsBlk()->GetLayout();
        }
        else
        {
            layout = _compiler->typGetObjLayout(_compiler->gtGetStructHandle(data));
        }

        storeNode = new (_compiler, GT_STORE_BLK) GenTreeBlk(GT_STORE_BLK, storeType, addr, data, layout);
    }
    else
    {
        storeNode = new (_compiler, GT_STOREIND) GenTreeStoreInd(storeType, addr, data);
    }
    storeNode->gtFlags |= GTF_ASG;

    return storeNode;
}

GenTree* Llvm::createShadowStackStoreNode(var_types storeType, GenTree* addr, GenTree* data)
{
    GenTree* storeNode = createStoreNode(storeType, addr, data);
    storeNode->gtFlags |= (GTF_IND_TGT_NOT_HEAP | GTF_IND_NONFAULTING);

    return storeNode;
}

GenTree* Llvm::insertShadowStackAddr(GenTree* insertBefore, ssize_t offset, unsigned shadowStackLclNum)
{
    assert((shadowStackLclNum == _shadowStackLclNum) || (shadowStackLclNum == _originalShadowStackLclNum));

    GenTree* shadowStackLcl = _compiler->gtNewLclvNode(shadowStackLclNum, TYP_I_IMPL);
    CurrentRange().InsertBefore(insertBefore, shadowStackLcl);

    if (offset == 0)
    {
        return shadowStackLcl;
    }

    GenTree* offsetNode = _compiler->gtNewIconNode(offset, TYP_I_IMPL);
    CurrentRange().InsertBefore(insertBefore, offsetNode);
    GenTree* addNode = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackLcl, offsetNode);
    CurrentRange().InsertBefore(insertBefore, addNode);

    return addNode;
}

//------------------------------------------------------------------------
// isShadowFrameLocal: Does the given local have a home on the shadow frame?
//
// Arguments:
//    varDsc - Local's descriptor
//
// Return Value:
//    Whether the given local has a location assigned to it on the shadow
//    frame. Note the fact it does is not an implication that it is live
//    on it at all times: the local can be live on the LLVM frame, or the
//    shadow one, or both.
//
bool Llvm::isShadowFrameLocal(LclVarDsc* varDsc) const
{
    // Other backends use "lvOnFrame" for this value, but for us it is not
    // a great fit because we add new locals after shadow frame layout.
    return varDsc->GetRegNum() == REG_STK;
}

bool Llvm::isFuncletParameter(unsigned lclNum) const
{
    return (lclNum == _shadowStackLclNum) || (lclNum == _originalShadowStackLclNum);
}

unsigned Llvm::getCurrentShadowFrameSize() const
{
    assert(CurrentBlock() != nullptr);
    unsigned hndIndex = CurrentBlock()->hasHndIndex() ? CurrentBlock()->getHndIndex() : EHblkDsc::NO_ENCLOSING_INDEX;
    return getShadowFrameSize(hndIndex);
}

//------------------------------------------------------------------------
// getShadowFrameSize: What is the size of a function's shadow frame?
//
// Arguments:
//    hndIndex - Handler index representing the function, NO_ENCLOSING_INDEX
//               is used for the root
//
// Return Value:
//    The size of the shadow frame for the given function. We term this
//    the value by which the shadow stack pointer must be offset before
//    calling managed code such that the caller will not clobber anything
//    live on the frame. Note that funclets do not have any shadow state
//    of their own and use the "original" frame from the parent function,
//    with one exception: catch handlers and filters have one readonly
//    pointer-sized argument representing the exception.
//
unsigned Llvm::getShadowFrameSize(unsigned hndIndex) const
{
    if (hndIndex == EHblkDsc::NO_ENCLOSING_INDEX)
    {
        return getOriginalShadowFrameSize();
    }
    if (_compiler->ehGetDsc(hndIndex)->HasCatchHandler())
    {
        // For the implicit (readonly) exception object argument.
        return TARGET_POINTER_SIZE;
    }

    return 0;
}

unsigned Llvm::getOriginalShadowFrameSize() const
{
    assert((_shadowStackLocalsSize % TARGET_POINTER_SIZE) == 0);
    return _shadowStackLocalsSize;
}

unsigned Llvm::getCatchArgOffset() const
{
    return 0;
}
