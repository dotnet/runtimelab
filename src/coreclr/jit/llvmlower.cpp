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
    populateLlvmArgNums();

    _shadowStackLocalsSize = 0;

    std::vector<LclVarDsc*> locals;
    unsigned localsParamCount = 0;

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
                        _compiler->fgEnsureFirstBBisScratch();
                        LIR::Range& firstBlockRange = LIR::AsRange(_compiler->fgFirstBB);

                        GenTree* fieldValue =
                            _compiler->gtNewLclFldNode(lclNum, fieldVarDsc->TypeGet(), fieldVarDsc->lvFldOffset);

                        GenTree* fieldStore = _compiler->gtNewStoreLclVar(fieldLclNum, fieldValue);
                        firstBlockRange.InsertAtBeginning(fieldStore);
                        firstBlockRange.InsertAtBeginning(fieldValue);
                    }

                    fieldVarDsc->lvIsStructField = false;
                    fieldVarDsc->lvParentLcl     = BAD_VAR_NUM;
                    fieldVarDsc->lvIsParam       = false;
                }

                varDsc->lvPromoted      = false;
                varDsc->lvFieldLclStart = BAD_VAR_NUM;
                varDsc->lvFieldCnt      = 0;
            }
            else if (_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_DEPENDENT)
            {
                /* dependent promotion, just mark fields as not lvIsParam */
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

            locals.push_back(varDsc);
            if (varDsc->lvIsParam)
            {
                localsParamCount++;
                varDsc->lvIsParam = false;
            }
        }
    }

    if (_compiler->opts.OptimizationEnabled())
    {
        std::sort(locals.begin() + localsParamCount, locals.end(), [](const LclVarDsc* lhs, const LclVarDsc* rhs) { return lhs->lvRefCntWtd() > rhs->lvRefCntWtd(); });
    }

    unsigned int offset = 0;
    for (unsigned i = 0; i < locals.size(); i++)
    {
        LclVarDsc* varDsc = locals.at(i);
        CorInfoType corInfoType = toCorInfoType(varDsc->TypeGet());
        CORINFO_CLASS_HANDLE classHandle = tryGetStructClassHandle(varDsc);

        offset = padOffset(corInfoType, classHandle, offset);
        varDsc->SetStackOffset(offset);
        offset = padNextOffset(corInfoType, classHandle, offset);
    }
    _shadowStackLocalsSize = offset;

    lowerToShadowStack();
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

    // TODO-LLVM: other non-standard args, generic context, outs

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

void Llvm::lowerToShadowStack()
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

                if ((_compiler->fgIsThrow(callNode) || callNode->IsNoReturn()) && (callNode->gtNext != nullptr))
                {
                    // If there is a no return, or always throw call, delete the dead code so we can add the unreachable statment immediately, and not after any dead RET
                    CurrentRange().Remove(callNode->gtNext, _currentBlock->lastNode());
                }
            }
            else if (node->OperIs(GT_RETURN) && (_retAddressLclNum != BAD_VAR_NUM))
            {
                var_types originalReturnType = node->TypeGet();
                LclVarDsc* retAddressVarDsc = _compiler->lvaGetDesc(_retAddressLclNum);
                retAddressVarDsc->lvIsParam = 1;
                retAddressVarDsc->lvType = TYP_I_IMPL;

                GenTreeLclVar* retAddressLocal = _compiler->gtNewLclvNode(_retAddressLclNum, TYP_I_IMPL);
                GenTree* storeNode = createShadowStackStoreNode(originalReturnType, retAddressLocal, node->AsOp()->gtGetOp1(),
                                                                originalReturnType == TYP_STRUCT ? _compiler->typGetObjLayout(_sigInfo.retTypeClass) : nullptr);

                GenTreeOp* retNode = node->AsOp();
                retNode->gtOp1 = nullptr;
                node->ChangeType(TYP_VOID);

                CurrentRange().InsertBefore(node, retAddressLocal, storeNode);
            }

            if (node->OperIsLocalAddr() || node->OperIsLocalField())
            {
                // Indicates that this local is to live on the LLVM frame, and will not participate in SSA.
                _compiler->lvaGetDesc(node->AsLclVarCommon())->lvHasLocalAddr = 1;
            }
        }
    }
}

void Llvm::lowerStoreLcl(GenTreeLclVarCommon* storeLclNode)
{
    LclVarDsc* addrVarDsc = _compiler->lvaGetDesc(storeLclNode->GetLclNum());

    if (addrVarDsc->CanBeReplacedWithItsField(_compiler))
    {
        ClassLayout* layout      = addrVarDsc->GetLayout();
        GenTree*     data        = storeLclNode->gtGetOp1();
        var_types    addrVarType = addrVarDsc->TypeGet();

        storeLclNode->SetOper(GT_LCL_VAR_ADDR);
        storeLclNode->ChangeType(TYP_I_IMPL);
        storeLclNode->SetLclNum(addrVarDsc->lvFieldLclStart);

        GenTree* storeObjNode = new (_compiler, GT_STORE_OBJ) GenTreeObj(addrVarType, storeLclNode, data, layout);
        storeObjNode->gtFlags |= GTF_ASG;

        CurrentRange().InsertAfter(storeLclNode, storeObjNode);
    }

    if (storeLclNode->TypeIs(TYP_STRUCT))
    {
        GenTree* dataOp = storeLclNode->gtGetOp1();
        CORINFO_CLASS_HANDLE dataHandle = _compiler->gtGetStructHandleIfPresent(dataOp);
        if (dataOp->OperIs(GT_IND))
        {
            // Special case: "gtGetStructHandleIfPresent" sometimes guesses the handle from
            // field sequences, but we will always need to transform TYP_STRUCT INDs into OBJs.
            dataHandle = NO_CLASS_HANDLE;
        }

        if (addrVarDsc->GetStructHnd() != dataHandle)
        {
            if (dataOp->OperIsIndir())
            {
                dataOp->SetOper(GT_OBJ);
                dataOp->AsObj()->SetLayout(addrVarDsc->GetLayout());
            }
            else if (dataOp->OperIs(GT_LCL_VAR)) // can get icon 0 here
            {
                GenTreeLclVarCommon* dataLcl = dataOp->AsLclVarCommon();
                LclVarDsc* dataVarDsc = _compiler->lvaGetDesc(dataLcl->GetLclNum());

                dataVarDsc->lvHasLocalAddr = 1;

                GenTree* dataAddrNode = _compiler->gtNewLclVarAddrNode(dataLcl->GetLclNum());

                dataLcl->ChangeOper(GT_OBJ);
                dataLcl->AsObj()->SetAddr(dataAddrNode);
                dataLcl->AsObj()->SetLayout(addrVarDsc->GetLayout());

                CurrentRange().InsertBefore(dataLcl, dataAddrNode);
            }
        }
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
                    GenTree* fieldStoreNode = createShadowStackStoreNode(use.GetType(), fieldSlotAddr, use.GetNode(), nullptr);

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

                GenTree* storeNode = createShadowStackStoreNode(opAndArg.operand->TypeGet(), slotAddr, opAndArg.operand,
                                                                corInfoType == CORINFO_TYPE_VALUECLASS
                                                                ? _compiler->typGetObjLayout(clsHnd)
                                                                : nullptr);
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
            indirNode    = _compiler->gtNewObjNode(calleeSigInfo->retTypeClass, returnAddrLclAfterCall);
        }
        else
        {
            indirNode = _compiler->gtNewOperNode(GT_IND, callReturnType, returnAddrLclAfterCall);
        }
        indirNode->gtFlags |= GTF_IND_TGT_NOT_HEAP; // No RhpAssignRef required
        LIR::Use callUse;
        if (CurrentRange().TryGetUse(callNode, &callUse))
        {
            callUse.ReplaceWith(_compiler, indirNode);
        }
        else
        {
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

GenTree* Llvm::createStoreNode(var_types nodeType, GenTree* addr, GenTree* data, ClassLayout* structClassLayout)
{
    GenTree* storeNode;
    if (nodeType == TYP_STRUCT)
    {
        storeNode = new (_compiler, GT_STORE_OBJ) GenTreeObj(nodeType, addr, data, structClassLayout);
    }
    else
    {
        storeNode = new (_compiler, GT_STOREIND) GenTreeStoreInd(nodeType, addr, data);
    }
    return storeNode;
}

GenTree* Llvm::createShadowStackStoreNode(var_types nodeType, GenTree* addr, GenTree* data, ClassLayout* structClassLayout)
{
    GenTree* storeNode = createStoreNode(nodeType, addr, data, structClassLayout);
    storeNode->gtFlags |= GTF_IND_TGT_NOT_HEAP;

    return storeNode;
}
