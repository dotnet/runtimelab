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
    lowerSpillTempsLiveAcrossSafePoints();
    lowerLocals();
    insertProlog();
    lowerBlocks();
}

//------------------------------------------------------------------------
// lowerSpillTempsLiveAcrossSafePoints: Spill GC SDSUs live across safe points.
//
// Rewrites:
//   gcTmp = IND<ref>(...)
//           CALL ; May trigger GC
//           USE(gcTmp)
// Into:
//   gcTmp = IND<ref>(...)
//           STORE_LCL_VAR<V00>(gcTmp)
//           CALL ; May trigger GC
//           USE(LCL_VAR<V00>)
//
// Done as a full IR walk pre-pass before the general lowering since we need
// to know about all GC locals to lay out the shadow stack.
//
void Llvm::lowerSpillTempsLiveAcrossSafePoints()
{
    // Set of SDSUs live at (after) the current node.
    SmallHashTable<GenTree*, unsigned> liveGcDefs(_compiler->getAllocator(CMK_Codegen));
    ArrayStack<unsigned> spillLclsRef(_compiler->getAllocator(CMK_Codegen));
    ArrayStack<unsigned> spillLclsByref(_compiler->getAllocator(CMK_Codegen));
    ArrayStack<GenTree*> containedOperands(_compiler->getAllocator(CMK_Codegen));

    auto getSpillLcl = [&](GenTree* node) {
        var_types type = node->TypeGet();
        CORINFO_CLASS_HANDLE structHandle = NO_CLASS_HANDLE;
        unsigned lclNum = BAD_VAR_NUM;
        switch (type)
        {
            case TYP_REF:
                if (!spillLclsRef.Empty())
                {
                    lclNum = spillLclsRef.Pop();
                }
                break;
            case TYP_BYREF:
                if (!spillLclsByref.Empty())
                {
                    lclNum = spillLclsByref.Pop();
                }
                break;
            case TYP_STRUCT:
                // This case should be **very** rare if at all possible. Just use a new local.
                structHandle = _compiler->gtGetStructHandle(node);
                break;
            default:
                unreached();
        }

        if (lclNum == BAD_VAR_NUM)
        {
            lclNum = _compiler->lvaGrabTemp(true DEBUGARG("GC SDSU live across a safepoint"));
            _compiler->lvaGetDesc(lclNum)->lvType = type;
            if (type == TYP_STRUCT)
            {
                noway_assert(structHandle != NO_CLASS_HANDLE);
                _compiler->lvaSetStruct(lclNum, structHandle, false);
            }
        }

        return lclNum;
    };

    auto releaseSpillLcl = [&](unsigned lclNum) {
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
        if (varDsc->TypeGet() == TYP_REF)
        {
            spillLclsRef.Push(lclNum);
        }
        else if (varDsc->TypeGet() == TYP_BYREF)
        {
            spillLclsByref.Push(lclNum);
        }
    };

    auto isGcTemp = [compiler = _compiler](GenTree* node) {
        if (varTypeIsGC(node) || node->TypeIs(TYP_STRUCT))
        {
            if (node->TypeIs(TYP_STRUCT))
            {
                // TODO-LLVM: replace with "!node->GetLayout()->HasGCPtr()" once enough of upstream is merged.
                CORINFO_CLASS_HANDLE structHandle = compiler->gtGetStructHandleIfPresent(node);
                if ((structHandle == NO_CLASS_HANDLE) || !compiler->typGetObjLayout(structHandle)->HasGCPtr())
                {
                    return false;
                }
            }

            return !node->OperIsLocal() && !node->OperIsLocalAddr();
        }

        return false;
    };

    for (BasicBlock* block : _compiler->Blocks())
    {
        assert(liveGcDefs.Count() == 0);
        LIR::Range& blockRange = LIR::AsRange(block);

        for (GenTree* node : blockRange)
        {
            if (node->isContained())
            {
                assert(!isPotentialGcSafePoint(node));
                continue;
            }

            GenTree* user = node;
            while (true)
            {
                for (GenTree** use : user->UseEdges())
                {
                    GenTree* operand = *use;
                    if (operand->isContained())
                    {
                        // Operands of contained nodes are used by the containing nodes. Note this algorithm will
                        // process contained operands in an out-of-order fashion; that is ok.
                        assert(operand->OperIs(GT_FIELD_LIST));
                        containedOperands.Push(operand);
                        continue;
                    }

                    if (isGcTemp(operand))
                    {
                        unsigned spillLclNum = BAD_VAR_NUM;
                        bool operandWasRemoved = liveGcDefs.TryRemove(operand, &spillLclNum);
                        assert(operandWasRemoved);

                        if (spillLclNum != BAD_VAR_NUM)
                        {
                            LclVarDsc* spillVarDsc = _compiler->lvaGetDesc(spillLclNum);
                            GenTree* lclVarNode = _compiler->gtNewLclvNode(spillLclNum, spillVarDsc->TypeGet());

                            *use = lclVarNode;
                            blockRange.InsertBefore(user, lclVarNode);
                            releaseSpillLcl(spillLclNum);

                            JITDUMP("Spilled [%06u] used by [%06u] replaced with V%02u:\n",
                                    Compiler::dspTreeID(operand), Compiler::dspTreeID(user), spillLclNum);
                            DISPNODE(lclVarNode);
                        }
                    }
                }

                if (containedOperands.Empty())
                {
                    break;
                }

                user = containedOperands.Pop();
            }

            // Find out if we need to spill anything.
            if (isPotentialGcSafePoint(node) && (liveGcDefs.Count() != 0))
            {
                JITDUMP("\nFound a safe point with GC SDSUs live across it:\n", Compiler::dspTreeID(node));
                DISPNODE(node);

                for (auto def : liveGcDefs)
                {
                    if (def.Value() != BAD_VAR_NUM)
                    {
                        // We may have already spilled this def live across multiple safe points.
                        continue;
                    }

                    GenTree* defNode = def.Key();
                    unsigned spillLclNum = getSpillLcl(defNode);
                    JITDUMP("Spilling as V%02u:\n", spillLclNum);
                    DISPNODE(defNode);

                    GenTree* store = _compiler->gtNewTempAssign(spillLclNum, defNode);
                    blockRange.InsertAfter(defNode, store);

                    def.Value() = spillLclNum;
                }
            }

            // Add the value defined by this node.
            if (node->IsValue() && !node->IsUnusedValue() && isGcTemp(node))
            {
                liveGcDefs.AddOrUpdate(node, BAD_VAR_NUM);
            }
        }
    }
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
    if (_compiler->ehAnyFunclets())
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

    if (needsReturnStackSlot(_sigInfo.retType, _sigInfo.retTypeClass))
    {
        _retAddressLclNum = _compiler->lvaGrabTemp(true DEBUGARG("returnslot"));
        LclVarDsc* retAddressVarDsc = _compiler->lvaGetDesc(_retAddressLclNum);
        retAddressVarDsc->lvType = TYP_I_IMPL;
        retAddressVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
        retAddressVarDsc->lvLlvmArgNum = nextLlvmArgNum++;
        retAddressVarDsc->lvIsParam = true;
    }

    if (m_info->compThisArg != BAD_VAR_NUM)
    {
        LclVarDsc* thisVarDsc = _compiler->lvaGetDesc(m_info->compThisArg);
        thisVarDsc->lvCorInfoType = toCorInfoType(thisVarDsc->TypeGet());
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

        CorInfoType argSigType = varDsc->lvCorInfoType;
        CORINFO_CLASS_HANDLE argSigClass = (varDsc->TypeGet() == TYP_STRUCT) ? varDsc->GetStructHnd() : NO_CLASS_HANDLE;
        if (getLlvmArgTypeForArg(isManagedAbi, argSigType, argSigClass))
        {
            varDsc->lvLlvmArgNum = nextLlvmArgNum++;
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
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    JITDUMP("Adding initialization for V%02u, %s:\n", lclNum, varDsc->lvReason);

    // TYP_BLK locals have to be handled specially as they can only be referenced indirectly.
    GenTreeUnOp* store;
    if (varDsc->TypeGet() == TYP_BLK)
    {
        ClassLayout* layout = _compiler->typGetBlkLayout(varDsc->lvExactSize);
        store = new (_compiler, GT_STORE_LCL_FLD) GenTreeLclFld(GT_STORE_LCL_FLD, TYP_STRUCT, lclNum, 0, layout);
        store->gtOp1 = value;
        store->gtFlags |= (GTF_ASG | GTF_VAR_DEF);
    }
    else
    {
        store = _compiler->gtNewStoreLclVar(lclNum, value);
    }

    m_prologRange.InsertAtEnd(value);
    m_prologRange.InsertAtEnd(store);

    DISPTREERANGE(m_prologRange, store);
}

void Llvm::insertProlog()
{
    if (!m_prologRange.IsEmpty())
    {
        _compiler->fgEnsureFirstBBisScratch();
    }

    LIR::Range& firstBlockRange = LIR::AsRange(_compiler->fgFirstBB);
    if (firstBlockRange.IsEmpty() || !firstBlockRange.FirstNode()->OperIs(GT_IL_OFFSET) ||
        !firstBlockRange.FirstNode()->AsILOffset()->gtStmtDI.GetRoot().IsValid())
    {
        // Insert a zero-offset ILOffset to notify codegen this is the start of user code.
        DebugInfo zeroILOffsetDi =
            DebugInfo(_compiler->compInlineContext, ILLocation(0, /* isStackEmpty */ true, /* isCall */ false));
        GenTree* zeroILOffsetNode = new (_compiler, GT_IL_OFFSET) GenTreeILOffset(zeroILOffsetDi);
        firstBlockRange.InsertAtBeginning(zeroILOffsetNode);
    }

    if (!m_prologRange.IsEmpty())
    {
        firstBlockRange.InsertAtBeginning(std::move(m_prologRange));
    }
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
    m_currentRange = &LIR::AsRange(block);

    for (GenTree* node : CurrentRange())
    {
        lowerNode(node);
    }

    INDEBUG(CurrentRange().CheckLIR(_compiler, /* checkUnusedValues */ true));

}

void Llvm::lowerNode(GenTree* node)
{
    switch (node->OperGet())
    {
        case GT_LCL_VAR:
        case GT_LCL_FLD:
        case GT_LCL_VAR_ADDR:
        case GT_LCL_FLD_ADDR:
        case GT_STORE_LCL_VAR:
        case GT_STORE_LCL_FLD:
            lowerLocal(node->AsLclVarCommon());
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

void Llvm::lowerLocal(GenTreeLclVarCommon* node)
{
    lowerFieldOfDependentlyPromotedStruct(node);

    if (node->OperIs(GT_STORE_LCL_VAR))
    {
        lowerStoreLcl(node->AsLclVarCommon());
    }

    if ((node->OperIsLocal() || node->OperIsLocalAddr()) && ConvertShadowStackLocalNode(node->AsLclVarCommon()))
    {
        return;
    }

    if (node->OperIsLocalStore() && node->TypeIs(TYP_STRUCT) && genActualTypeIsInt(node->gtGetOp1()))
    {
        node->gtGetOp1()->SetContained();
    }

    if (node->OperIsLocalAddr() || node->OperIsLocalField())
    {
        // Indicates that this local is to live on the LLVM frame, and will not participate in SSA.
        _compiler->lvaGetDesc(node->AsLclVarCommon())->lvHasLocalAddr = 1;
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

bool Llvm::ConvertShadowStackLocalNode(GenTreeLclVarCommon* lclNode)
{
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNode->GetLclNum());

    if (isShadowFrameLocal(varDsc) && (lclNode->GetRegNum() != REG_LLVM))
    {
        // Funclets (especially filters) will be called by the dispatcher while live state still exists
        // on shadow frames below (in the tradional sense, where stacks grow down) them. For this reason,
        // funclets will access state from the original frame via a dedicated shadow stack pointer, and
        // use the actual shadow stack for calls.
        unsigned shadowStackLclNum = CurrentBlock()->hasHndIndex() ? _originalShadowStackLclNum : _shadowStackLclNum;
        GenTree* lclAddress =
            insertShadowStackAddr(lclNode, varDsc->GetStackOffset() + lclNode->GetLclOffs(), shadowStackLclNum);

        ClassLayout* layout = lclNode->TypeIs(TYP_STRUCT) ? lclNode->GetLayout(_compiler) : nullptr;
        GenTree* storedValue = nullptr;
        genTreeOps indirOper;
        switch (lclNode->OperGet())
        {
            case GT_STORE_LCL_VAR:
            case GT_STORE_LCL_FLD:
                indirOper = (layout != nullptr) ? GT_STORE_OBJ : GT_STOREIND;
                storedValue = lclNode->Data();
                break;
            case GT_LCL_FLD:
            case GT_LCL_VAR:
                indirOper = (layout != nullptr) ? GT_OBJ : GT_IND;
                break;
            case GT_LCL_VAR_ADDR:
            case GT_LCL_FLD_ADDR:
                // Local address nodes are directly replaced with the ADD.
                CurrentRange().Remove(lclAddress);
                lclNode->ReplaceWith(lclAddress, _compiler);
                return true;
            default:
                unreached();
        }

        lclNode->ChangeOper(indirOper);
        lclNode->AsIndir()->SetAddr(lclAddress);
        lclNode->gtFlags |= GTF_IND_NONFAULTING;

        if (GenTree::OperIsStore(indirOper))
        {
            lclNode->gtFlags |= GTF_IND_TGT_NOT_HEAP;
            lclNode->AsOp()->gtOp2 = storedValue;
        }
        if (GenTree::OperIsBlk(indirOper))
        {
            lclNode->AsBlk()->SetLayout(layout);
            lclNode->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
        }

        lowerNode(lclNode);
        return true;
    }

    return false;
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
        CurrentRange().Remove(callNode->gtArgs.GetArgByIndex(0)->GetNode());
        callNode->gtArgs.RemoveAfter(nullptr);
    }

    // Doing this early simplifies code below.
    callNode->gtArgs.MoveLateToEarly();

    unsigned thisArgLclNum = BAD_VAR_NUM;
    GenTree* cellArgNode = nullptr;
    if (callNode->IsVirtualStub())
    {
        lowerVirtualStubCallBeforeArgs(callNode, &thisArgLclNum, &cellArgNode);
    }

    if (callNode->NeedsNullCheck())
    {
        insertNullCheckForCall(callNode);
    }

    unsigned shadowArgsSize = lowerCallToShadowStack(callNode);

    if (callNode->IsVirtualStub())
    {
        lowerVirtualStubCallAfterArgs(callNode, thisArgLclNum, cellArgNode, shadowArgsSize);
    }
    else if (callNode->IsUnmanaged())
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
    assert(callNode->gtArgs.IsEmpty());

    GenTree* excObjAddr = insertShadowStackAddr(callNode, getCatchArgOffset(), _shadowStackLclNum);
    callNode->gtArgs.PushFront(_compiler, NewCallArg::Primitive(excObjAddr, CORINFO_TYPE_PTR));
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
    LIR::Use retValUse(CurrentRange(), &retNode->gtOp1, retNode);
    ClassLayout* layout = retNode->TypeIs(TYP_STRUCT) ? _compiler->typGetObjLayout(_sigInfo.retTypeClass) : nullptr;
    if (retNode->TypeIs(TYP_STRUCT) && retVal->TypeIs(TYP_STRUCT))
    {
        normalizeStructUse(retValUse, layout);
    }

    bool isStructZero = retNode->TypeIs(TYP_STRUCT) && retVal->IsIntegralConst(0);
    if (_retAddressLclNum != BAD_VAR_NUM)
    {
        GenTreeLclVar* retAddrNode = _compiler->gtNewLclvNode(_retAddressLclNum, TYP_I_IMPL);
        GenTree* storeNode;
        if (isStructZero)
        {
            storeNode = new (_compiler, GT_STORE_BLK) GenTreeBlk(GT_STORE_BLK, TYP_STRUCT, retAddrNode, retVal, layout);
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

void Llvm::lowerVirtualStubCallBeforeArgs(GenTreeCall* callNode, unsigned* pThisLclNum, GenTree** pCellArgNode)
{
    assert(callNode->IsVirtualStub());

    // Make "this" available for reuse. Note we pass the raw pointer value to the stub, this is ok as the stub runs in
    // cooperative mode and makes sure to spill the value to the shadow stack in case it needs to call managed code.
    LIR::Use thisArgUse(CurrentRange(), &callNode->gtArgs.GetThisArg()->EarlyNodeRef(), callNode);
    unsigned thisArgLclNum = representAsLclVar(thisArgUse);

    // Flag the call as needing a null check. Our stubs don't handle null "this", as we presume doing the check here is
    // better as it will likely be eliminated as redundant (by LLVM).
    callNode->gtFlags |= GTF_CALL_NULLCHECK;

    // Remove the cell arg from the arg list before lowering args (it will be reused for the stub later).
    CallArg* cellArg = callNode->gtArgs.FindWellKnownArg(WellKnownArg::VirtualStubCell);
    callNode->gtArgs.Remove(cellArg);

    *pThisLclNum = thisArgLclNum;
    *pCellArgNode = cellArg->GetNode();
}

void Llvm::lowerVirtualStubCallAfterArgs(
    GenTreeCall* callNode, unsigned thisArgLclNum, GenTree* cellArgNode, unsigned shadowArgsSize)
{
    assert(callNode->IsVirtualStub() && (callNode->gtControlExpr == nullptr));
    assert((shadowArgsSize % TARGET_POINTER_SIZE) == 0);
    //
    // We transform:
    //  Call(pCell, [@this], args...)
    // Into:
    //  delegate* pStub = *pCell;
    //  delegate* pTarget = pStub(SS, @this, pCell)
    //  pTarget([@this], args...)
    //
    // We "lower" this call manually as it is rather special, inserted **after** the arguments for the main call have
    // been set up and thus needing a larger shadow stack offset. This is done to not create a new safe point across
    // which GC arguments to the main call would be live; the stub itself may call into managed code and trigger a GC.
    //
    unsigned shadowStackOffsetForStub = getCurrentShadowFrameSize() + shadowArgsSize;
    GenTree* shadowStackForStub = insertShadowStackAddr(callNode, shadowStackOffsetForStub, _shadowStackLclNum);
    GenTree* thisForStub = _compiler->gtNewLclvNode(thisArgLclNum, TYP_REF);
    CurrentRange().InsertBefore(callNode, thisForStub);

    // This call could be indirect (in case this is shared code and the cell address needed
    // to be resolved dynamically). Use the available address node directly in that case.
    GenTree* stubAddr;
    if (callNode->gtCallType == CT_INDIRECT)
    {
        stubAddr = callNode->gtCallAddr;
    }
    else
    {
        // Frontend makes this into an FTN_ADDR, but it is actually a data address in our case.
        assert(cellArgNode->IsIconHandle(GTF_ICON_FTN_ADDR));
        cellArgNode->gtFlags = GTF_ICON_GLOBAL_PTR;

        stubAddr = _compiler->gtNewIconHandleNode(cellArgNode->AsIntCon()->IconValue(), GTF_ICON_GLOBAL_PTR);
        CurrentRange().InsertBefore(callNode, stubAddr);
    }
    // This is the cell's address, stub itself is its first field - get it.
    stubAddr = _compiler->gtNewIndir(TYP_I_IMPL, stubAddr);
    stubAddr->SetAllEffectsFlags(GTF_EMPTY);
    stubAddr->gtFlags |= GTF_IND_NONFAULTING;
    CurrentRange().InsertBefore(callNode, stubAddr);

    GenTreeCall* stubCall = _compiler->gtNewIndCallNode(stubAddr, TYP_I_IMPL);
    stubCall->gtArgs.PushFront(_compiler, NewCallArg::Primitive(shadowStackForStub, CORINFO_TYPE_PTR),
                               NewCallArg::Primitive(thisForStub, CORINFO_TYPE_CLASS),
                               NewCallArg::Primitive(cellArgNode, CORINFO_TYPE_PTR));
    for (CallArg& arg : stubCall->gtArgs.Args())
    {
        arg.AbiInfo.IsPointer = arg.GetSignatureCorInfoType() == CORINFO_TYPE_PTR;
        arg.AbiInfo.ArgType = arg.GetSignatureType();
    }
    stubCall->gtCorInfoType = CORINFO_TYPE_PTR;
    stubCall->gtFlags |= GTF_CALL_UNMANAGED;
    stubCall->gtCallMoreFlags |= GTF_CALL_M_SUPPRESS_GC_TRANSITION;
    CurrentRange().InsertBefore(callNode, stubCall);

    // Finally, retarget our call. It is no longer VSD.
    callNode->gtCallType = CT_INDIRECT;
    callNode->gtCallAddr = stubCall;
    callNode->gtStubCallStubAddr = nullptr;
    callNode->gtCallCookie = nullptr;
    callNode->gtFlags &= ~GTF_CALL_VIRT_STUB;
    callNode->gtCallMoreFlags &= ~GTF_CALL_M_VIRTSTUB_REL_INDIRECT;
}

void Llvm::insertNullCheckForCall(GenTreeCall* callNode)
{
    assert(callNode->NeedsNullCheck() && callNode->gtArgs.HasThisPointer());

    LIR::Use thisArgUse(CurrentRange(), &callNode->gtArgs.GetThisArg()->EarlyNodeRef(), callNode);
    unsigned thisArgLclNum = representAsLclVar(thisArgUse);

    GenTree* thisArgNode = _compiler->gtNewLclvNode(thisArgLclNum, _compiler->lvaGetDesc(thisArgLclNum)->TypeGet());
    GenTree* thisArgNullCheck = _compiler->gtNewNullCheck(thisArgNode, CurrentBlock());
    CurrentRange().InsertBefore(callNode, thisArgNode, thisArgNullCheck);

    lowerIndir(thisArgNullCheck->AsIndir());
}

void Llvm::lowerUnmanagedCall(GenTreeCall* callNode)
{
    assert(callNode->IsUnmanaged());

    if (callNode->gtCallType != CT_INDIRECT)
    {
        // We cannot easily handle varargs as we do not know which args are the fixed ones.
        assert((callNode->gtCallType == CT_USER_FUNC) && !callNode->IsVarargs());

        ArrayStack<TargetAbiType> sig(_compiler->getAllocator(CMK_Codegen));
        var_types returnType = callNode->TypeGet();
        if (returnType == TYP_STRUCT)
        {
            CorInfoType simpleReturnType = GetPrimitiveTypeForTrivialWasmStruct(callNode->gtRetClsHnd);

            if (returnType == CORINFO_TYPE_UNDEF)
            {
                // TODO-LLVM-ABI: implement by-ref passing of structs.
                NYI("Unmanaged ABI for structs");
            }

            returnType = JITtype2varType(simpleReturnType);
        }
        sig.Push(getAbiTypeForType(returnType));

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
        //
        // TODO-LLVM: ideally, we would use a helper function here, however, adding new LLVM-specific helpers is not
        // currently possible and so we make do with special handling in codegen.
        callNode->gtEntryPoint.handle =
            GetExternalMethodAccessor(callNode->gtCallMethHnd, &sig.BottomRef(), sig.Height());
    }

    // Insert the GC transitions if required. TODO-LLVM-CQ: batch these if there are no safe points between
    // two or more consecutive PI calls.
    if (!callNode->IsSuppressGCTransition())
    {
        assert(_compiler->opts.ShouldUsePInvokeHelpers()); // No inline transition support yet.
        assert(_compiler->lvaInlinedPInvokeFrameVar != BAD_VAR_NUM);

        // Insert CORINFO_HELP_JIT_PINVOKE_BEGIN.
        GenTreeLclVar* frameAddr = _compiler->gtNewLclVarAddrNode(_compiler->lvaInlinedPInvokeFrameVar);
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
}

//------------------------------------------------------------------------
// lowerCallToShadowStack: Lower the call, rewriting its arguments.
//
// This method has two primary objectives:
//  1) Transfer the information about the arguments from gtArgs to explicit
//     PutArgType nodes, to make it easy for codegen to consume it. Also, all
//     of the late argument nodes are moved (back) to the early list.
//  2) Rewrite arguments and the return to be stored on the shadow stack. We take
//     the arguments which need to be on the shadow stack, remove them from the call
//     arguments list, store their values on the shadow stack, at offsets calculated
//     in a simple increasing order, matching the signature. We also rewrite returns
//     that must be on the shadow stack, see "lowerCallReturn".
//
// LLVM Arg layout:
//    - Shadow stack (if required)
//    - Return slot (if required)
//    - Generic context (if required)
//    - Args passed as LLVM parameters (not on the shadow stack)
//
unsigned Llvm::lowerCallToShadowStack(GenTreeCall* callNode)
{
    // Rewrite the args, adding shadow stack, and moving gc tracked args to the shadow stack.
    // This transformation only applies to calls that have a managed calling convention (e. g.
    // it doesn't apply to runtime imports, or helpers implemented as FCalls, etc).
    const bool isManagedCall = callHasManagedCallingConvention(callNode);
    unsigned shadowFrameSize = getCurrentShadowFrameSize();
    int sigArgIdx = 0;

    // Insert the shadow stack at the front
    if (callHasShadowStackArg(callNode))
    {
        GenTree* calleeShadowStack = insertShadowStackAddr(callNode, shadowFrameSize, _shadowStackLclNum);
        callNode->gtArgs.PushFront(_compiler, NewCallArg::Primitive(calleeShadowStack, CORINFO_TYPE_PTR));
        sigArgIdx--;
    }

    if (lowerCallReturn(callNode) != nullptr)
    {
        sigArgIdx--;
    }

    const HelperFuncInfo* helperInfo = nullptr;
    if (callNode->IsHelperCall())
    {
        helperInfo = &getHelperFuncInfo(_compiler->eeGetHelperNum(callNode->gtCallMethHnd));
    }

    unsigned shadowStackUseOffset = 0;
    CallArg* lastLlvmStackArg = nullptr;
    CallArg* callArg = callNode->gtArgs.Args().begin().GetArg();
    while (callArg != nullptr)
    {
        GenTree* argNode = callArg->GetNode();
        CorInfoType argSigType;
        CORINFO_CLASS_HANDLE argSigClass;
        if ((helperInfo == nullptr) || (sigArgIdx < 0))
        {
            if (callArg->GetWellKnownArg() == WellKnownArg::ThisPointer)
            {
                argSigType = argNode->TypeIs(TYP_REF) ? CORINFO_TYPE_CLASS : CORINFO_TYPE_BYREF;
            }
            else if (callArg->GetWellKnownArg() == WellKnownArg::InstParam)
            {
                argSigType = CORINFO_TYPE_PTR;
            }
            else if (callArg->GetSignatureCorInfoType() != CORINFO_TYPE_UNDEF)
            {
                argSigType = callArg->GetSignatureCorInfoType();
            }
            else
            {
                assert(callArg->GetSignatureType() != TYP_I_IMPL);
                argSigType = toCorInfoType(callArg->GetSignatureType());
            }

            argSigClass = callArg->GetSignatureClassHandle();
        }
        else
        {
            argSigType = helperInfo->GetSigArgType(sigArgIdx);
            argSigClass = helperInfo->GetSigArgClass(_compiler, sigArgIdx);
        }

        CorInfoType argType;
        if (!getLlvmArgTypeForArg(isManagedCall, argSigType, argSigClass, &argType))
        {
            assert(!callArg->AbiInfo.PassedByRef);
            if (argType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffset = padOffset(argType, argSigClass, shadowStackUseOffset);
            }

            if (argNode->OperIs(GT_FIELD_LIST))
            {
                for (GenTreeFieldList::Use& use : argNode->AsFieldList()->Uses())
                {
                    assert(use.GetType() != TYP_STRUCT);

                    unsigned fieldOffsetValue = shadowFrameSize + shadowStackUseOffset + use.GetOffset();
                    GenTree* fieldSlotAddr = insertShadowStackAddr(callNode, fieldOffsetValue, _shadowStackLclNum);
                    GenTree* fieldStoreNode = createShadowStackStoreNode(use.GetType(), fieldSlotAddr, use.GetNode());

                    CurrentRange().InsertBefore(callNode, fieldStoreNode);
                }

                CurrentRange().Remove(argNode);
            }
            else
            {
                unsigned offsetValue = shadowFrameSize + shadowStackUseOffset;
                GenTree* slotAddr  = insertShadowStackAddr(callNode, offsetValue, _shadowStackLclNum);
                GenTree* storeNode = createShadowStackStoreNode(argNode->TypeGet(), slotAddr, argNode);

                CurrentRange().InsertBefore(callNode, storeNode);
            }

            if (argType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffset = padNextOffset(argType, argSigClass, shadowStackUseOffset);
            }
            else
            {
                shadowStackUseOffset += TARGET_POINTER_SIZE;
            }

            callNode->gtArgs.RemoveAfter(lastLlvmStackArg);
        }
        else // Arg on LLVM stack.
        {
            if (argNode->TypeIs(TYP_STRUCT) && !argNode->OperIs(GT_FIELD_LIST))
            {
                LIR::Use argNodeUse(CurrentRange(), &callArg->EarlyNodeRef(), callNode);
                argNode = normalizeStructUse(argNodeUse, _compiler->typGetObjLayout(argSigClass));
            }

            callArg->AbiInfo.IsPointer = argType == CORINFO_TYPE_PTR;
            callArg->AbiInfo.ArgType = JITtype2varType(argType);
            lastLlvmStackArg = callArg;
        }

        sigArgIdx++;
        callArg = callArg->GetNext();
    }

    return roundUp(shadowStackUseOffset, TARGET_POINTER_SIZE);
}

// If the return type must be GC tracked, removes the return type
// and converts to a return slot arg, modifying the call args, and building the necessary IR
//
// Returns:
//   The "CallArg*" for the created call return slot, if created, otherwise "nullptr"
CallArg* Llvm::lowerCallReturn(GenTreeCall* callNode)
{
    CallArg* returnSlot = nullptr;

    if (needsReturnStackSlot(callNode))
    {
        // replace the "CALL ref" with a "CALL void" that takes a return address as the first argument
        GenTree* returnValueAddress = insertShadowStackAddr(callNode, getCurrentShadowFrameSize(), _shadowStackLclNum);

        // create temp for the return address
        unsigned returnTempNum = _compiler->lvaGrabTemp(false DEBUGARG("return value address"));
        LclVarDsc* returnAddrVarDsc = _compiler->lvaGetDesc(returnTempNum);
        returnAddrVarDsc->lvType = TYP_I_IMPL;

        GenTree* addrStore = _compiler->gtNewStoreLclVar(returnTempNum, returnValueAddress);
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

        // if we are lowering a return, then we will at least have a shadow stack CallArg
        returnSlot = callNode->gtArgs.InsertAfter(_compiler, callNode->gtArgs.GetArgByIndex(0),
                                                  NewCallArg::Primitive(returnAddrLcl, CORINFO_TYPE_PTR));

        callNode->gtReturnType = TYP_VOID;
        callNode->gtCorInfoType = CORINFO_TYPE_VOID;
        callNode->ChangeType(TYP_VOID);

        CurrentRange().InsertBefore(callNode, addrStore, returnAddrLcl);
        CurrentRange().InsertAfter(callNode, returnAddrLclAfterCall, indirNode);
    }
    else
    {
        if (callNode->IsHelperCall())
        {
            CorInfoHelpFunc helperFunc = _compiler->eeGetHelperNum(callNode->gtCallMethHnd);
            callNode->gtCorInfoType = getHelperFuncInfo(helperFunc).GetSigReturnType();
        }
        else if (callNode->gtCorInfoType == CORINFO_TYPE_UNDEF)
        {
            assert(callNode->TypeGet() != TYP_I_IMPL);
            callNode->gtCorInfoType = toCorInfoType(callNode->TypeGet());
        }
    }

    return returnSlot;
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

    // "IND<struct>" nodes always need to be normalized.
    if (node->OperIs(GT_IND))
    {
        node->SetOper(GT_BLK);
        node->AsBlk()->SetLayout(layout);
        node->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
    }
    else
    {
        // TODO-LLVM: use GenTree::GetLayout here once enough of upstream is merged and delete the
        // "useHandle == NO_CLASS_HANDLE" check below.
        CORINFO_CLASS_HANDLE useHandle = _compiler->gtGetStructHandleIfPresent(node);

        // Note both can be blocks ("NO_CLASS_HANDLE"), in which case we don't need to do anything.
        if ((useHandle != layout->GetClassHandle()) &&
            ((useHandle == NO_CLASS_HANDLE) || (getLlvmTypeForStruct(useHandle) != getLlvmTypeForStruct(layout))))
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
// isPotentialGcSafePoint: Can this node be a GC safe point?
//
// Arguments:
//    node - The node
//
// Return Value:
//    Whether "node" can trigger GC.
//
// Notes:
//    Similar to "Compiler::IsGcSafePoint", with the difference being that
//    the "conservative" return value for this method is "true".
//
bool Llvm::isPotentialGcSafePoint(GenTree* node)
{
    if (node->IsCall())
    {
        if (node->AsCall()->IsUnmanaged() && node->AsCall()->IsSuppressGCTransition())
        {
            return false;
        }
        if (node->IsHelperCall())
        {
            CorInfoHelpFunc helperFunc = _compiler->eeGetHelperNum(node->AsCall()->gtCallMethHnd);
            if (getHelperFuncInfo(helperFunc).HasFlags(HFIF_NO_RPI_OR_GC))
            {
                return false;
            }
        }

        // All other calls are assumed to be possible safe points.
        return true;
    }

    return false;
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
