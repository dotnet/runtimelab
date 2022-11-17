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

        if (!canStoreLocalOnLlvmStack(varDsc))
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
            if (!varDsc->lvIsParam && (varDsc->lvRefCnt() == 0))
            {
                // No need to place unreferenced temps on the shadow stack.
                continue;
            }

            // Insert zero-initialization for this local if required (which it almost always will be).
            if (!varDsc->lvIsParam && !varDsc->lvHasExplicitInit)
            {
                var_types zeroType = (varDsc->TypeGet() == TYP_STRUCT) ? TYP_INT : genActualType(varDsc);
                initializeLocalInProlog(lclNum, _compiler->gtNewZeroConNode(zeroType));
            }

            shadowStackLocals.push_back(varDsc);
            if (varDsc->lvIsParam)
            {
                shadowStackParamCount++;
                varDsc->lvIsParam = false;
            }
        }
    }

    assignShadowStackOffsets(shadowStackLocals, shadowStackParamCount);

    JITDUMP("\nLocals after shadow stack layout:\n");
    JITDUMPEXEC(_compiler->lvaDoneFrameLayout = Compiler::TENTATIVE_FRAME_LAYOUT);
    JITDUMPEXEC(_compiler->lvaTableDump());
}

void Llvm::populateLlvmArgNums()
{
    if (_sigInfo.hasTypeArg())
    {
        failFunctionCompilation();
    }

    _shadowStackLclNum = _compiler->lvaGrabTemp(true DEBUGARG("shadowstack"));
    LclVarDsc* shadowStackVarDsc = _compiler->lvaGetDesc(_shadowStackLclNum);
    unsigned   nextLlvmArgNum    = 0;

    shadowStackVarDsc->lvLlvmArgNum = nextLlvmArgNum++;
    shadowStackVarDsc->lvType    = TYP_I_IMPL;
    shadowStackVarDsc->lvCorInfoType = CORINFO_TYPE_PTR;
    shadowStackVarDsc->lvIsParam = true;

    if (needsReturnStackSlot(_sigInfo.retType, _sigInfo.retTypeClass))
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
        CorInfoType          corInfoType = getCorInfoTypeForArg(&_sigInfo, sigArgs, &classHnd);
        LclVarDsc*           varDsc      = _compiler->lvaGetDesc(i + firstCorInfoArgLocalNum);
        if (canStoreLocalOnLlvmStack(varDsc))
        {
            varDsc->lvLlvmArgNum  = nextLlvmArgNum++;
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

    unsigned int offset = 0;
    for (unsigned i = 0; i < shadowStackLocals.size(); i++)
    {
        LclVarDsc* varDsc = shadowStackLocals.at(i);
        CorInfoType corInfoType = toCorInfoType(varDsc->TypeGet());
        CORINFO_CLASS_HANDLE classHandle = tryGetStructClassHandle(varDsc);

        offset = padOffset(corInfoType, classHandle, offset);
        varDsc->SetStackOffset(offset);
        offset = padNextOffset(corInfoType, classHandle, offset);
    }
    _shadowStackLocalsSize = offset;
}

void Llvm::initializeLocalInProlog(unsigned lclNum, GenTree* value)
{
    JITDUMP("Adding initialization for V%02u, %s:\n", lclNum, _compiler->lvaGetDesc(lclNum)->lvReason);

    _compiler->fgEnsureFirstBBisScratch();
    LIR::Range& firstBlockRange = LIR::AsRange(_compiler->fgFirstBB);

    GenTree* store = _compiler->gtNewStoreLclVar(lclNum, value);
    firstBlockRange.InsertAtEnd(value);
    firstBlockRange.InsertAtEnd(store);

    DISPTREERANGE(firstBlockRange, store);
}

void Llvm::lowerBlocks()
{
    for (BasicBlock* _currentBlock : _compiler->Blocks())
    {
        _currentRange = &LIR::AsRange(_currentBlock);
        for (GenTree* node : CurrentRange())
        {
            lowerFieldOfDependentlyPromotedStruct(node);

            if (node->OperIs(GT_STORE_LCL_VAR))
            {
                lowerStoreLcl(node->AsLclVarCommon());
            }

            if (node->OperIsLocal() || node->OperIsLocalAddr())
            {
                ConvertShadowStackLocalNode(node->AsLclVarCommon());
            }
            else if (node->IsCall())
            {
                GenTreeCall* callNode = node->AsCall();

                if (callNode->IsHelperCall())
                {
                    // helper calls are built differently
                    continue;
                }

                failUnsupportedCalls(callNode);

                lowerCallToShadowStack(callNode);

                // If there is a no return, or always throw call, delete the dead code so we can add unreachable statment immediately, and not after any dead RET.
                if (_compiler->fgIsThrow(callNode) || callNode->IsNoReturn())
                {
                    while (CurrentRange().LastNode() != callNode)
                    {
                        CurrentRange().Remove(CurrentRange().LastNode(), /* markOperandsUnused */ true);
                    }
                }
            }
            else if (node->OperIs(GT_STORE_BLK, GT_STORE_OBJ))
            {
                lowerStoreBlk(node->AsBlk());
            }
            else if (node->OperIs(GT_RETURN))
            {
                lowerReturn(node->AsUnOp());
            }

            if (node->OperIsLocalAddr() || node->OperIsLocalField())
            {
                // Indicates that this local is to live on the LLVM frame, and will not participate in SSA.
                _compiler->lvaGetDesc(node->AsLclVarCommon())->lvHasLocalAddr = 1;
            }
        }

        INDEBUG(CurrentRange().CheckLIR(_compiler, /* checkUnusedValues */ true));
    }
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
        storeObjNode->gtFlags |= GTF_ASG;

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
    genTreeOps oper = node->OperGet();

    if (!canStoreLocalOnLlvmStack(varDsc))
    {
        // TODO-LLVM: if the offset == 0, just GT_STOREIND at the shadowStack
        unsigned offsetVal = varDsc->GetStackOffset() + node->GetLclOffs();
        GenTreeIntCon* offset = _compiler->gtNewIconNode(offsetVal, TYP_I_IMPL);

        GenTreeLclVar* shadowStackLocal = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
        GenTree* lclAddress = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackLocal, offset);

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

        CurrentRange().InsertBefore(node, shadowStackLocal, offset);
        if (indirOper == GT_NONE)
        {
            // Local address nodes are directly replaced with the ADD.
            node->ReplaceWith(lclAddress, _compiler);
        }
        else
        {
            CurrentRange().InsertBefore(node, lclAddress);
        }
    }
}

void Llvm::lowerStoreBlk(GenTreeBlk* storeBlkNode)
{
    assert(storeBlkNode->OperIs(GT_STORE_BLK, GT_STORE_OBJ));

    // Fix up type mismatches on copies for codegen.
    if (storeBlkNode->OperIsCopyBlkOp())
    {
        GenTree* src = storeBlkNode->Data();
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

    // A zero-sized block store is a no-op. Lower it away.
    if (storeBlkNode->Size() == 0)
    {
        assert(storeBlkNode->OperIsInitBlkOp() || storeBlkNode->Data()->OperIs(GT_BLK));

        storeBlkNode->Addr()->SetUnusedValue();
        CurrentRange().Remove(storeBlkNode->Data(), /* markOperandsUnused */ true);
        CurrentRange().Remove(storeBlkNode);
    }
}

void Llvm::lowerReturn(GenTreeUnOp* retNode)
{
    GenTree* retVal = retNode->gtGetOp1();
    if (retNode->TypeIs(TYP_STRUCT) && retVal->TypeIs(TYP_STRUCT))
    {
        normalizeStructUse(retVal, _compiler->typGetObjLayout(_sigInfo.retTypeClass));
    }

    if (_retAddressLclNum != BAD_VAR_NUM)
    {
        LclVarDsc* retAddressVarDsc = _compiler->lvaGetDesc(_retAddressLclNum);
        GenTreeLclVar* retAddressLocal = _compiler->gtNewLclvNode(_retAddressLclNum, TYP_I_IMPL);
        GenTree* storeNode = createShadowStackStoreNode(retNode->TypeGet(), retAddressLocal, retVal);

        retNode->gtOp1 = nullptr;
        retNode->ChangeType(TYP_VOID);

        CurrentRange().InsertBefore(retNode, retAddressLocal, storeNode);
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

    fgArgInfo*                 argInfo    = callNode->fgArgInfo;
    unsigned int               argCount   = argInfo->ArgCount();
    fgArgTabEntry**            argTable   = argInfo->ArgTable();
    std::vector<OperandArgNum> sortedArgs = std::vector<OperandArgNum>(argCount);
    OperandArgNum*             sortedData = sortedArgs.data();

    GenTreeCall::Use* lastArg;
    GenTreeCall::Use* insertReturnAfter;
    GenTreeCall::Use* callThisArg = callNode->gtCallThisArg;

    callNode->ResetArgInfo();
    callNode->gtCallThisArg = nullptr;

    // set up the callee shadowstack, creating a temp and the PUTARG
    GenTreeLclVar* shadowStackVar = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
    GenTreeIntCon* offset         = _compiler->gtNewIconNode(_shadowStackLocalsSize, TYP_I_IMPL);
    // TODO-LLVM: possible performance benefit: when _shadowStackLocalsSize == 0, then omit the GT_ADD.
    GenTree* calleeShadowStack = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackVar, offset);

    GenTreePutArgType* calleeShadowStackPutArg =
        _compiler->gtNewPutArgType(calleeShadowStack, CORINFO_TYPE_PTR, NO_CLASS_HANDLE);
#ifdef DEBUG
    calleeShadowStackPutArg->SetArgNum(-1); // -1 will represent the shadowstack  arg for LLVM
#endif

    callNode->gtCallArgs     = _compiler->gtNewCallArgs(calleeShadowStackPutArg);
    lastArg                  = callNode->gtCallArgs;
    insertReturnAfter        = lastArg; // add the return slot after the shadow stack arg
    callNode->gtCallLateArgs = nullptr;

    CurrentRange().InsertBefore(callNode, shadowStackVar, offset, calleeShadowStack, calleeShadowStackPutArg);

    lastArg = lowerCallReturn(callNode, insertReturnAfter);

    for (unsigned i = 0; i < argCount; i++)
    {
        fgArgTabEntry* curArgTabEntry = argTable[i];
        unsigned int   argNum         = curArgTabEntry->argNum;
        OperandArgNum  opAndArg       = {argNum, curArgTabEntry->GetNode()};
        sortedData[argNum]            = opAndArg;
    }

    CORINFO_SIG_INFO* calleeSigInfo = callNode->callSig;
    // Relies on the fact all arguments not in the signature come before those that are.
    unsigned firstSigArgIx = argCount - calleeSigInfo->numArgs;

    CORINFO_ARG_LIST_HANDLE sigArgs = calleeSigInfo->args;
    unsigned                argIx   = 0;

    for (OperandArgNum opAndArg : sortedArgs)
    {
        CORINFO_CLASS_HANDLE clsHnd      = NO_CLASS_HANDLE;
        CorInfoType          corInfoType = CORINFO_TYPE_UNDEF;

        // "this" not in sigInfo arg list
        bool isThis = callThisArg != nullptr && opAndArg.argNum == 0 && calleeSigInfo->hasThis();
        bool isSigArg = argIx >= firstSigArgIx;
        if (isSigArg)
        {
            corInfoType = getCorInfoTypeForArg(calleeSigInfo, sigArgs, &clsHnd);
        }
        else if (!isThis)
        {
            corInfoType = toCorInfoType(opAndArg.operand->TypeGet());
        }

        bool argOnShadowStack = isThis || (isSigArg && !canStoreArgOnLlvmStack(_compiler, corInfoType, clsHnd));
        if (argOnShadowStack)
        {
            if (corInfoType == CORINFO_TYPE_VALUECLASS)
            {
                shadowStackUseOffest = padOffset(corInfoType, clsHnd, shadowStackUseOffest);
            }

            if (opAndArg.operand->OperIs(GT_FIELD_LIST))
            {
                for (GenTreeFieldList::Use& use : opAndArg.operand->AsFieldList()->Uses())
                {
                    assert(use.GetType() != TYP_STRUCT);

                    GenTree*       lclShadowStack = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
                    GenTreeIntCon* fieldOffset =
                        _compiler->gtNewIconNode(_shadowStackLocalsSize + shadowStackUseOffest + use.GetOffset(),
                                                 TYP_I_IMPL);
                    GenTree* fieldSlotAddr =
                        _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, lclShadowStack, fieldOffset);
                    GenTree* fieldStoreNode = createShadowStackStoreNode(use.GetType(), fieldSlotAddr, use.GetNode());

                    CurrentRange().InsertBefore(callNode, lclShadowStack, fieldOffset, fieldSlotAddr,
                                                fieldStoreNode);
                }

                CurrentRange().Remove(opAndArg.operand);
            }
            else
            {
                GenTree*       lclShadowStack = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
                GenTreeIntCon* offset =
                    _compiler->gtNewIconNode(_shadowStackLocalsSize + shadowStackUseOffest, TYP_I_IMPL);
                GenTree* slotAddr  = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, lclShadowStack, offset);

                GenTree* storeNode = createShadowStackStoreNode(opAndArg.operand->TypeGet(), slotAddr, opAndArg.operand);
                CurrentRange().InsertBefore(callNode, lclShadowStack, offset, slotAddr, storeNode);
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
            // arg on LLVM stack
            GenTreePutArgType* putArg = _compiler->gtNewPutArgType(opAndArg.operand, corInfoType, clsHnd);
#if DEBUG
            putArg->SetArgNum(opAndArg.argNum);
#endif
            lastArg = _compiler->gtInsertNewCallArgAfter(putArg, lastArg);

            CurrentRange().InsertBefore(callNode, putArg);
        }
        if (isSigArg)
        {
            sigArgs = _info.compCompHnd->getArgNext(sigArgs);
        }

        argIx++;
    }
}

void Llvm::failUnsupportedCalls(GenTreeCall* callNode)
{
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
GenTreeCall::Use* Llvm::lowerCallReturn(GenTreeCall*      callNode,
                                        GenTreeCall::Use* insertAfterArg)
{
    GenTreeCall::Use* lastArg = insertAfterArg;
    var_types callReturnType = callNode->TypeGet();
    CORINFO_SIG_INFO* calleeSigInfo = callNode->callSig;

    // Some ctors, e.g. strings (and maybe only strings), have a return type in IR so
    // pass the call return type instead of the CORINFO_SIG_INFO return type, which is void in these cases
    if (needsReturnStackSlot(toCorInfoType(callReturnType), calleeSigInfo->retTypeClass))
    {
        // replace the "CALL ref" with a "CALL void" that takes a return address as the first argument
        GenTreeLclVar* shadowStackVar     = _compiler->gtNewLclvNode(_shadowStackLclNum, TYP_I_IMPL);
        GenTreeIntCon* offset             = _compiler->gtNewIconNode(_shadowStackLocalsSize, TYP_I_IMPL);
        GenTree*       returnValueAddress = _compiler->gtNewOperNode(GT_ADD, TYP_I_IMPL, shadowStackVar, offset);

        // create temp for the return address
        unsigned   returnTempNum    = _compiler->lvaGrabTemp(false DEBUGARG("return value address"));
        LclVarDsc* returnAddrVarDsc = _compiler->lvaGetDesc(returnTempNum);
        returnAddrVarDsc->lvType    = TYP_I_IMPL;

        GenTree*          addrStore     = _compiler->gtNewStoreLclVar(returnTempNum, returnValueAddress);
        GenTree*          returnAddrLcl = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);

        GenTree* returnAddrLclAfterCall = _compiler->gtNewLclvNode(returnTempNum, TYP_I_IMPL);
        GenTree* indirNode;
        if (callReturnType == TYP_STRUCT)
        {
            indirNode = _compiler->gtNewObjNode(calleeSigInfo->retTypeClass, returnAddrLclAfterCall);
        }
        else
        {
            indirNode = _compiler->gtNewIndir(callReturnType, returnAddrLclAfterCall);
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

        GenTreePutArgType* putArg = _compiler->gtNewPutArgType(returnAddrLcl, CORINFO_TYPE_PTR, nullptr);
#if DEBUG
        putArg->SetArgNum(-2);  // -2 will represent the return arg for LLVM
#endif
        lastArg = _compiler->gtInsertNewCallArgAfter(putArg, insertAfterArg);

        callNode->gtReturnType = TYP_VOID;
        callNode->gtCorInfoType = CORINFO_TYPE_VOID;
        callNode->ChangeType(TYP_VOID);

        CurrentRange().InsertBefore(callNode, shadowStackVar, offset, returnValueAddress, addrStore);
        CurrentRange().InsertAfter(addrStore, returnAddrLcl, putArg);
        CurrentRange().InsertAfter(callNode, returnAddrLclAfterCall, indirNode);
    }
    else
    {
        callNode->gtCorInfoType = calleeSigInfo->retType;
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
