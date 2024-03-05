// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                                        Linear Shadow Stack Allocator                                         |
// ================================================================================================================

#include "llvm.h"

class ShadowStackAllocator
{
    Compiler* const m_compiler;
    Llvm* const m_llvm;

    unsigned m_prologZeroingOffset = 0;
    unsigned m_prologZeroingSize = 0;
    GenTree* m_lastPrologNode = nullptr;

public:
    ShadowStackAllocator(Llvm* llvm) : m_compiler(llvm->_compiler), m_llvm(llvm)
    {
    }

    void Allocate()
    {
        IdentifyCandidatesAndInitializeLocals();
        SpillValuesLiveAcrossSafePoints();
        AllocateAndInitializeLocals();
        FinalizeProlog();
        RewriteShadowFrameReferences();
    }

private:
    void IdentifyCandidatesAndInitializeLocals()
    {
        // Initialize independently promoted parameter field locals.
        //
        for (unsigned lclNum = 0; lclNum < m_compiler->lvaCount; lclNum++)
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
            if (varDsc->lvPromoted)
            {
                assert(m_compiler->lvaGetPromotionType(varDsc) == Compiler::PROMOTION_TYPE_INDEPENDENT);
                assert(varDsc->lvRefCnt() == 0);

                if (varDsc->lvIsParam)
                {
                    for (unsigned index = 0; index < varDsc->lvFieldCnt; index++)
                    {
                        unsigned fieldLclNum = varDsc->lvFieldLclStart + index;
                        LclVarDsc* fieldVarDsc = m_compiler->lvaGetDesc(fieldLclNum);
                        if ((fieldVarDsc->lvRefCnt() != 0) &&
                            (m_llvm->getInitKindForLocal(fieldLclNum) == ValueInitKind::Param))
                        {
                            GenTree* fieldValue =
                                m_compiler->gtNewLclFldNode(lclNum, fieldVarDsc->TypeGet(), fieldVarDsc->lvFldOffset);
                            GenTreeLclVar* store = InitializeLocalInProlog(fieldLclNum, fieldValue);

                            // Update the SSA data for this now explicit definition.
                            if (m_compiler->lvaInSsa(fieldLclNum))
                            {
                                store->SetSsaNum(SsaConfig::FIRST_SSA_NUM);

                                LclSsaVarDsc* ssaDsc = fieldVarDsc->GetPerSsaData(store->GetSsaNum());
                                assert(ssaDsc->GetDefNode() == nullptr);
                                ssaDsc->SetDefNode(store);
                                ssaDsc->SetBlock(m_compiler->fgFirstBB);
                            }

                            // Notify codegen this local will need a home on the native stack.
                            varDsc->incLvRefCntSaturating(1);
                            fieldVarDsc->lvHasExplicitInit = true;
                        }
                    }
                }
            }
        }

        // Identify locals eligible for precise allocation and those that must (or must not) be on the shadow stack.
        for (unsigned lclNum = 0; lclNum < m_compiler->lvaCount; lclNum++)
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);

            if (varDsc->lvPromoted)
            {
                // As of the loop above, promoted locals are only live in the prolog. Simply dissolve them.
                m_llvm->dissolvePromotedLocal(lclNum);
                continue;
            }

            if ((varDsc->lvRefCnt() == 0) || m_llvm->isFuncletParameter(lclNum))
            {
                continue;
            }

            // The unwind frame MUST be allocated on the shadow stack. The runtime uses its value to invoke filters.
            if (lclNum == m_llvm->m_unwindFrameLclNum)
            {
                varDsc->SetRegNum(REG_STK_CANDIDATE_UNCONDITIONAL);
                continue;
            }

            // Locals live-in/out of funclets need to be accessible throughout the whole logical method and using the
            // shadow stack is a simple way to achieve this. Another would be to implement LLVM intrinsics that allow
            // accessing the root method's native frame (effectively) and use them in codegen. Note: we overapproximate
            // the set of locals live cross-funclet by using "lvLiveInOutOfHndlr" here as a CQ quirk. Currently, LLVM
            // is not able to enregister locals that are live across EH pads, and our codegen is not able to produce
            // the correct PHIs anyway.
            //
            if (m_compiler->ehHasCallableHandlers() && (!varDsc->lvTracked || varDsc->lvLiveInOutOfHndlr))
            {
                varDsc->SetRegNum(REG_STK_CANDIDATE_UNCONDITIONAL);
                continue;
            }

            // GC locals needs to go on the shadow stack for the scan to find them. With tracked locals, we may be
            // able to prove that they are not alive across any safe point and so designate them as "candidates".
            if (varDsc->HasGCPtr())
            {
                // TODO-LLVM-LSSA:
                // varDsc->SetRegNum(m_compiler->lvaInSsa(lclNum) ? REG_STK_CANDIDATE_TENTATIVE : REG_STK_CANDIDATE_UNCONDITIONAL);
                varDsc->SetRegNum(REG_STK_CANDIDATE_UNCONDITIONAL);
            }
        }
    }

    class LssaDomTreeVisitor : public DomTreeVisitor<LssaDomTreeVisitor>
    {
        // Cannot use raw node pointers as their values influence hash table iteration order.
        struct DeterministicNodeHashInfo : public HashTableInfo<DeterministicNodeHashInfo>
        {
            static bool Equals(GenTree* left, GenTree* right)
            {
                return left == right;
            }

            static unsigned GetHashCode(GenTree* node)
            {
                return node->TypeGet() ^ node->OperGet();
            }
        };

        ShadowStackAllocator* m_lssa;
        SmallHashTable<GenTree*, unsigned, 8, DeterministicNodeHashInfo> m_liveSdsuGcDefs;
        ArrayStack<unsigned> m_spillLclsRef;
        ArrayStack<unsigned> m_spillLclsByref;
        ArrayStack<GenTree*> m_containedOperands;

    public:
        LssaDomTreeVisitor(ShadowStackAllocator* lssa)
            : DomTreeVisitor(lssa->m_compiler)
            , m_lssa(lssa)
            , m_liveSdsuGcDefs(m_compiler->getAllocator(CMK_Codegen))
            , m_spillLclsRef(m_compiler->getAllocator(CMK_Codegen))
            , m_spillLclsByref(m_compiler->getAllocator(CMK_Codegen))
            , m_containedOperands(m_compiler->getAllocator(CMK_Codegen))
        {
        }

        void PreOrderVisit(BasicBlock* block)
        {
            ProcessBlock(block);
        }

        void ProcessBlock(BasicBlock* block)
        {
            assert(m_liveSdsuGcDefs.Count() == 0);
            LIR::Range& blockRange = LIR::AsRange(block);

            for (GenTree* node = blockRange.FirstNode(); node != nullptr; node = node->gtNext)
            {
                if (node->isContained())
                {
                    assert(!m_lssa->IsPotentialGcSafePoint(node));
                    continue;
                }

                // Handle a special case: calls with return buffer pointers need them pinned.
                if (node->IsCall() && node->AsCall()->gtArgs.HasRetBuffer())
                {
                    GenTree* retBufNode = node->AsCall()->gtArgs.GetRetBufferArg()->GetNode();
                    if ((retBufNode->gtLIRFlags & LIR::Flags::Mark) != 0)
                    {
                        // TODO-LLVM-LSSA: also need to pin locals used here.
                        unsigned spillLclNum;
                        m_liveSdsuGcDefs.TryGetValue(retBufNode, &spillLclNum);
                        SpillSdsuValue(blockRange, retBufNode, &spillLclNum);
                        m_liveSdsuGcDefs.AddOrUpdate(retBufNode, spillLclNum);
                    }
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
                            m_containedOperands.Push(operand);
                            continue;
                        }

                        if ((operand->gtLIRFlags & LIR::Flags::Mark) != 0)
                        {
                            unsigned spillLclNum = BAD_VAR_NUM;
                            bool operandWasRemoved = m_liveSdsuGcDefs.TryRemove(operand, &spillLclNum);
                            assert(operandWasRemoved);

                            if (spillLclNum != BAD_VAR_NUM)
                            {
                                GenTree* lclVarNode = m_compiler->gtNewLclVarNode(spillLclNum);

                                *use = lclVarNode;
                                blockRange.InsertBefore(user, lclVarNode);
                                ReleaseSpillLocal(spillLclNum);

                                JITDUMP("Spilled [%06u] used by [%06u] replaced with V%02u:\n",
                                    Compiler::dspTreeID(operand), Compiler::dspTreeID(user), spillLclNum);
                                DISPNODE(lclVarNode);
                            }

                            operand->gtLIRFlags &= ~LIR::Flags::Mark;
                        }
                    }

                    if (m_containedOperands.Empty())
                    {
                        break;
                    }

                    user = m_containedOperands.Pop();
                }

                // Find out if we need to spill anything.
                if (m_lssa->IsPotentialGcSafePoint(node))
                {
                    if (m_liveSdsuGcDefs.Count() != 0)
                    {
                        JITDUMP("\nFound a safe point with GC SDSUs live across it:\n", Compiler::dspTreeID(node));
                        DISPNODE(node);

                        for (auto def : m_liveSdsuGcDefs)
                        {
                            SpillSdsuValue(blockRange, def.Key(), &def.Value());
                        }
                    }
                }

                // Add the value defined by this node.
                if (node->IsValue() && !node->IsUnusedValue() && !node->OperIsLocal() && m_lssa->IsGcValue(node))
                {
                    node->gtLIRFlags |= LIR::Flags::Mark;
                    m_liveSdsuGcDefs.AddOrUpdate(node, BAD_VAR_NUM);
                }
            }
        }

    private:
        unsigned GetSpillLocal(GenTree* node)
        {
            var_types type = node->TypeGet();
            ClassLayout* layout = nullptr;
            unsigned lclNum = BAD_VAR_NUM;
            switch (type)
            {
            case TYP_REF:
                if (!m_spillLclsRef.Empty())
                {
                    lclNum = m_spillLclsRef.Pop();
                }
                break;
            case TYP_BYREF:
                if (!m_spillLclsByref.Empty())
                {
                    lclNum = m_spillLclsByref.Pop();
                }
                break;
            case TYP_STRUCT:
                // This case should be **very** rare if at all possible. Just use a new local.
                layout = node->GetLayout(m_compiler);
                break;
            default:
                unreached();
            }

            if (lclNum == BAD_VAR_NUM)
            {
                lclNum = m_compiler->lvaGrabTemp(true DEBUGARG("GC SDSU live across a safepoint"));
                LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
                varDsc->lvType = type;
                varDsc->SetRegNum(REG_STK_CANDIDATE_UNCONDITIONAL);
                if (type == TYP_STRUCT)
                {
                    m_compiler->lvaSetStruct(lclNum, layout, false);
                }
            }

            return lclNum;
        }

        void ReleaseSpillLocal(unsigned lclNum)
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
            if (varDsc->TypeGet() == TYP_REF)
            {
                m_spillLclsRef.Push(lclNum);
            }
            else if (varDsc->TypeGet() == TYP_BYREF)
            {
                m_spillLclsByref.Push(lclNum);
            }
        }

        void SpillSdsuValue(LIR::Range& blockRange, GenTree* defNode, unsigned* pSpillLclNum)
        {
            if (*pSpillLclNum != BAD_VAR_NUM)
            {
                // We may have already spilled this def live across multiple safe points.
                return;
            }

            unsigned spillLclNum = GetSpillLocal(defNode);
            JITDUMP("Spilling as V%02u:\n", spillLclNum);
            DISPNODE(defNode);

            GenTree* store = m_compiler->gtNewTempStore(spillLclNum, defNode);
            blockRange.InsertAfter(defNode, store);

            *pSpillLclNum = spillLclNum;
        }
    };

    //------------------------------------------------------------------------
    // SpillValuesLiveAcrossSafePoints: Spill GC values live across safe points.
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
    void SpillValuesLiveAcrossSafePoints()
    {
        LssaDomTreeVisitor visitor(this);

        for (BasicBlock* block : m_compiler->Blocks())
        {
            visitor.ProcessBlock(block);
        }
    }

    void AllocateAndInitializeLocals()
    {
        std::vector<unsigned> shadowFrameLocals;

        for (unsigned lclNum = 0; lclNum < m_compiler->lvaCount; lclNum++)
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);

            if (varDsc->GetRegNum() == REG_STK_CANDIDATE_TENTATIVE)
            {
                // There were no safe points across which this local was live.
                varDsc->SetRegNum(REG_LLVM);
            }

            if ((varDsc->GetRegNum() == REG_STK_CANDIDATE_UNCONDITIONAL) ||
                (varDsc->GetRegNum() == REG_STK_CANDIDATE_COMMITED))
            {
                // TODO-LLVM-LSSA: do not spill this implicit definition to the SS if it's not live across a safepoint
                // (same logic as for other definitions).
                ValueInitKind initValueKind = m_llvm->getInitKindForLocal(lclNum, ValueInitOpts::IncludeImplicitGcUse);
                if (initValueKind == ValueInitKind::Param)
                {
                    GenTreeLclVar* initValue = m_compiler->gtNewLclvNode(lclNum, varDsc->TypeGet());
                    initValue->SetRegNum(REG_LLVM);
                    if (m_compiler->lvaInSsa(lclNum))
                    {
                        initValue->SetSsaNum(SsaConfig::FIRST_SSA_NUM);
                    }
                    InitializeLocalInProlog(lclNum, initValue);
                }
                varDsc->lvMustInit = initValueKind == ValueInitKind::Zero;

                AssignLocalToShadowStack(varDsc);
                shadowFrameLocals.push_back(lclNum);
            }
            else
            {
                INDEBUG(varDsc->lvOnFrame = false); // For more accurate frame layout dumping.
            }
        }

        AssignShadowFrameOffsets(shadowFrameLocals);
    }

    void AssignLocalToShadowStack(LclVarDsc* varDsc)
    {
        if (varDsc->GetRegNum() == REG_STK_CANDIDATE_UNCONDITIONAL)
        {
            varDsc->lvInSsa = 0;
        }

        // All shadow locals must be referenced explicitly by this point. Assume for a start
        // that this local will be live only on the shadow stack. The loop replacing uses and
        // defs will increment the count back if there are any non-shadow references.
        varDsc->lvImplicitlyReferenced = 0;
        varDsc->setLvRefCnt(0);

        m_llvm->m_anyAddressExposedShadowLocals |= varDsc->IsAddressExposed();
    }

    void AssignShadowFrameOffsets(std::vector<unsigned>& shadowFrameLocals)
    {
        if (m_compiler->opts.OptimizationEnabled())
        {
            std::sort(shadowFrameLocals.begin(), shadowFrameLocals.end(),
                      [compiler = m_compiler](unsigned lhsLclNum, unsigned rhsLclNum)
            {
                LclVarDsc* lhsVarDsc = compiler->lvaGetDesc(lhsLclNum);
                LclVarDsc* rhsVarDsc = compiler->lvaGetDesc(rhsLclNum);
                return lhsVarDsc->lvRefCntWtd() > rhsVarDsc->lvRefCntWtd();
            });
        }

        unsigned offset = 0;
        auto assignOffset = [this, &offset](LclVarDsc* varDsc) {
            unsigned alignment = TARGET_POINTER_SIZE;
#ifndef TARGET_64BIT
            if (varDsc->lvStructDoubleAlign)
            {
                alignment = 8;
                m_llvm->m_shadowFrameAlignment = alignment;
            }
#endif // !TARGET_64BIT

            offset = AlignUp(offset, alignment);
            varDsc->SetStackOffset(offset);
            offset += m_compiler->lvaLclSize(m_compiler->lvaGetLclNum(varDsc));
            varDsc->SetRegNum(REG_STK);
        };

        // The shadow frame must be allocated at a zero offset; the runtime uses its value as the original
        // shadow frame parameter to filter funclets.
        if (m_llvm->m_unwindFrameLclNum != BAD_VAR_NUM)
        {
            assignOffset(m_compiler->lvaGetDesc(m_llvm->m_unwindFrameLclNum));
        }

        // Assigns offsets such that locals which need to be zeroed come first. This will allow us to zero them all
        // using a single memset in the prolog.
        m_prologZeroingOffset = offset;

        for (unsigned i = 0; i < shadowFrameLocals.size(); i++)
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(shadowFrameLocals.at(i));
            if (m_llvm->isShadowFrameLocal(varDsc) || !varDsc->lvMustInit)
            {
                continue;
            }

            assignOffset(varDsc);
        }

        m_prologZeroingSize = offset - m_prologZeroingOffset;

        for (unsigned i = 0; i < shadowFrameLocals.size(); i++)
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(shadowFrameLocals.at(i));
            if (m_llvm->isShadowFrameLocal(varDsc))
            {
                continue;
            }

            assignOffset(varDsc);
        }

        m_llvm->_shadowStackLocalsSize = AlignUp(offset, Llvm::DEFAULT_SHADOW_STACK_ALIGNMENT);

        m_compiler->compLclFrameSize = m_llvm->_shadowStackLocalsSize;
        m_compiler->lvaDoneFrameLayout = Compiler::TENTATIVE_FRAME_LAYOUT;

        JITDUMP("\nLocals after shadow stack layout:\n");
        JITDUMPEXEC(m_compiler->lvaTableDump());
        JITDUMP("\n");

        m_compiler->lvaDoneFrameLayout = Compiler::INITIAL_FRAME_LAYOUT;
    }

    void FinalizeProlog()
    {
        LIR::Range range;
        m_llvm->m_currentRange = &range;

        unsigned zeroingSize = m_prologZeroingSize;
        if (zeroingSize != 0)
        {
            unsigned offset = m_prologZeroingOffset;
            GenTree* addr = m_llvm->insertShadowStackAddr(nullptr, offset, m_llvm->_shadowStackLclNum);
            GenTree* zero = m_compiler->gtNewIconNode(0);
            ClassLayout* layout = m_compiler->typGetBlkLayout(zeroingSize);
            GenTree* store = m_compiler->gtNewStoreBlkNode(layout, addr, zero, GTF_IND_NONFAULTING);
            range.InsertAfter(addr, zero, store);

            JITDUMP("Added zero-initialization for shadow locals at: [%i, %i]:\n", offset, offset + zeroingSize);
            DISPTREERANGE(range, store);
        }

        // Insert a zero-offset ILOffset to notify codegen this is the start of user code.
        DebugInfo zeroILOffsetDi =
            DebugInfo(m_compiler->compInlineContext, ILLocation(0, /* isStackEmpty */ true, /* isCall */ false));
        GenTree* zeroILOffsetNode = new (m_compiler, GT_IL_OFFSET) GenTreeILOffset(zeroILOffsetDi);
        range.InsertAtEnd(zeroILOffsetNode);

        assert(m_llvm->isFirstBlockCanonical());
        m_llvm->lowerRange(m_compiler->fgFirstBB, range);
        LIR::AsRange(m_compiler->fgFirstBB).InsertAfter(m_lastPrologNode, std::move(range));
    }

    GenTreeLclVar* InitializeLocalInProlog(unsigned lclNum, GenTree* value)
    {
        LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
        JITDUMP("Adding initialization for V%02u, %s:\n", lclNum, varDsc->lvReason);

        GenTreeLclVar* store = m_compiler->gtNewStoreLclVarNode(lclNum, value);

        LIR::Range range;
        range.InsertAtEnd(value);
        range.InsertAtEnd(store);
        m_llvm->lowerRange(m_compiler->fgFirstBB, range);
        DISPTREERANGE(range, store);

        LIR::AsRange(m_compiler->fgFirstBB).InsertAfter(m_lastPrologNode, std::move(range));
        m_lastPrologNode = store;

        return store;
    }

    void RewriteShadowFrameReferences()
    {
        for (BasicBlock* block : m_compiler->Blocks())
        {
            m_llvm->m_currentBlock = block;
            m_llvm->m_currentRange = &LIR::AsRange(block);

            GenTree* node = m_llvm->CurrentRange().FirstNode();
            while (node != nullptr)
            {
                if (node->OperIsAnyLocal() && !node->OperIs(GT_PHI_ARG))
                {
                    node = RewriteLocal(node->AsLclVarCommon());
                    continue;
                }
                if (node->IsCall())
                {
                    RewriteCall(node->AsCall());
                }

                node = node->gtNext;
            }

            INDEBUG(m_llvm->CurrentRange().CheckLIR(m_compiler, /* checkUnusedValues */ true));
        }

        m_llvm->m_currentBlock = nullptr;
        m_llvm->m_currentRange = nullptr;
    }

    GenTree* RewriteLocal(GenTreeLclVarCommon* lclNode)
    {
        unsigned lclNum = lclNode->GetLclNum();
        LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);

        if (lclNode->HasSsaName() && !m_compiler->lvaInSsa(lclNum))
        {
            if (lclNode->IsPhiDefn())
            {
                return RemovePhiDef(lclNode->AsLclVar());
            }

            lclNode->SetSsaNum(SsaConfig::RESERVED_SSA_NUM);
        }

        if (m_llvm->isShadowFrameLocal(varDsc))
        {
            if (lclNode->GetRegNum() == REG_LLVM)
            {
                // We previously set reference counts of all shadow locals to zero, anticipating that all references
                // will be shadow ones. This one isn't - re-up the count.
                varDsc->incLvRefCntSaturating(1);
                return lclNode->gtNext;
            }

            // Filters will be called by the first pass while live state still exists on shadow frames above (in the
            // traditional sense, where stacks grow down) them. For this reason, filters will access state from the
            // original frame via a dedicated shadow stack pointer, and use the actual shadow stack for calls.
            unsigned shadowStackLclNum = m_llvm->isBlockInFilter(m_llvm->CurrentBlock())
                ? m_llvm->_originalShadowStackLclNum
                : m_llvm->_shadowStackLclNum;
            unsigned lclOffset = varDsc->GetStackOffset() + lclNode->GetLclOffs();
            GenTree* lclAddress = m_llvm->insertShadowStackAddr(lclNode, lclOffset, shadowStackLclNum);

            ClassLayout* layout = lclNode->TypeIs(TYP_STRUCT) ? lclNode->GetLayout(m_compiler) : nullptr;
            GenTree* storedValue = nullptr;
            genTreeOps indirOper;
            switch (lclNode->OperGet())
            {
                case GT_STORE_LCL_VAR:
                case GT_STORE_LCL_FLD:
                    indirOper = (layout != nullptr) ? GT_STORE_BLK : GT_STOREIND;
                    storedValue = lclNode->Data();
                    break;
                case GT_LCL_FLD:
                case GT_LCL_VAR:
                    indirOper = (layout != nullptr) ? GT_BLK : GT_IND;
                    break;
                case GT_LCL_ADDR:
                    // Local address nodes are directly replaced with the ADD.
                    m_llvm->CurrentRange().Remove(lclAddress);
                    lclNode->ReplaceWith(lclAddress, m_compiler);
                    return lclNode->gtNext;
                default:
                    unreached();
            }

            lclNode->ChangeOper(indirOper);
            lclNode->AsIndir()->SetAddr(lclAddress);
            lclNode->gtFlags |= GTF_IND_NONFAULTING;

            if (GenTree::OperIsStore(indirOper))
            {
                lclNode->gtFlags |= GTF_IND_TGT_NOT_HEAP;
                lclNode->AsIndir()->Data() = storedValue;
            }
            if (GenTree::OperIsBlk(indirOper))
            {
                lclNode->AsBlk()->SetLayout(layout);
                lclNode->AsBlk()->gtBlkOpKind = GenTreeBlk::BlkOpKindInvalid;
            }
        }

        return lclNode->gtNext;
    }

    GenTree* RemovePhiDef(GenTreeLclVar* phiDefn)
    {
        assert(phiDefn->IsPhiDefn());
        GenTreePhi* phi = phiDefn->Data()->AsPhi();
        for (GenTreePhi::Use& use : phi->Uses())
        {
            m_llvm->CurrentRange().Remove(use.GetNode());
        }

        GenTree* nextNode = phiDefn->gtNext;
        m_llvm->CurrentRange().Remove(phi);
        m_llvm->CurrentRange().Remove(phiDefn);
        return nextNode;
    }

    void RewriteCall(GenTreeCall* call)
    {
        // Add in the shadow stack argument now that we know the shadow frame size.
        if (m_llvm->callHasManagedCallingConvention(call))
        {
            unsigned funcIdx = m_llvm->getLlvmFunctionIndexForBlock(m_llvm->CurrentBlock());
            bool isTailCall = CanShadowTailCall(call);
            unsigned calleeShadowStackOffset = m_llvm->getCalleeShadowStackOffset(funcIdx, isTailCall);

            GenTree* calleeShadowStack =
                m_llvm->insertShadowStackAddr(call, calleeShadowStackOffset, m_llvm->_shadowStackLclNum);
            CallArg* calleeShadowStackArg =
                call->gtArgs.PushFront(m_compiler, NewCallArg::Primitive(calleeShadowStack, CORINFO_TYPE_PTR));

            calleeShadowStackArg->AbiInfo.IsPointer = true;
            calleeShadowStackArg->AbiInfo.ArgType = TYP_I_IMPL;
        }

        if (call->IsOptimizingRetBufAsLocal() && !call->gtArgs.GetRetBufferArg()->GetNode()->OperIs(GT_LCL_ADDR))
        {
            // We may have lost track of a shadow local defined by this call. Clear the flag if so.
            call->gtCallMoreFlags &= ~GTF_CALL_M_RETBUFFARG_LCLOPT;
        }
    }

    bool CanShadowTailCall(GenTreeCall* call)
    {
        BasicBlock* block = m_llvm->CurrentBlock();
        if (!m_llvm->canEmitCallAsShadowTailCall(block->hasTryIndex(), m_llvm->isBlockInFilter(block)))
        {
            return false;
        }

        // We support only the simplest cases for now.
        if (call->IsNoReturn() || ((call->gtNext != nullptr) && call->gtNext->OperIs(GT_RETURN)))
        {
            return true;
        }

        return false;
    }

    //------------------------------------------------------------------------
    // IsPotentialGcSafePoint: Can this node be a GC safe point?
    //
    // Arguments:
    //    node - The node
    //
    // Return Value:
    //    Whether "node" can trigger GC.
    //
    // Notes:
    //    Similar to "Compiler::IsGcSafePoint", with the difference being that
    //    the "conservative" return value for this method is "true". Does not
    //    consider nodes safe points only because they may throw.
    //
    bool IsPotentialGcSafePoint(GenTree* node)
    {
        if (node->IsCall())
        {
            if (node->AsCall()->IsUnmanaged() && node->AsCall()->IsSuppressGCTransition())
            {
                return false;
            }
            if (node->IsHelperCall())
            {
                const HelperFuncInfo& info = m_llvm->getHelperFuncInfo(node->AsCall()->GetHelperNum());
                if (info.HasFlag(HFIF_NO_RPI_OR_GC) || info.HasFlag(HFIF_THROW_OR_NO_RPI_OR_GC))
                {
                    return false;
                }
            }

            // All other calls are assumed to be possible safe points.
            return true;
        }

        return false;
    }

    bool IsGcValue(const GenTree* node) const
    {
        assert(node->IsValue());

        if (varTypeIsGC(node) || node->TypeIs(TYP_STRUCT))
        {
            if (node->TypeIs(TYP_STRUCT))
            {
                if (node->OperIs(GT_IND, GT_PHI))
                {
                    return false;
                }
                if (!node->GetLayout(m_compiler)->HasGCPtr())
                {
                    return false;
                }
            }

            // Local address nodes always point to the stack (native or shadow). Constant handles will
            // only point to immortal and immovable (frozen) objects.
            return !node->OperIs(GT_LCL_ADDR) && !node->IsIconHandle() && !node->IsIntegralConst(0);
        }

        return false;
    }

    bool IsGcConservative() const
    {
        return true;
    }
};

void Llvm::Allocate()
{
    ShadowStackAllocator(this).Allocate();
}

ValueInitKind Llvm::getInitKindForLocal(unsigned lclNum, ValueInitOpts opts) const
{
    LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
    assert(varDsc->lvRefCnt() != 0); // The caller is expected to check this.

    // Is the value live on entry?
    if (varDsc->lvHasExplicitInit)
    {
        // No - no need to initialize it, even in the GC case.
        return ValueInitKind::None;
    }

    // We choose to always initialize GC values to reduce the number of "random" pointers on the shadow stack.
    if (varDsc->HasGCPtr() && (opts == ValueInitOpts::IncludeImplicitGcUse))
    {
        // This value may or may not be live.
        return varDsc->lvIsParam ? ValueInitKind::Param : ValueInitKind::Zero;
    }

    if (varDsc->lvTracked && !VarSetOps::IsMember(_compiler, _compiler->fgFirstBB->bbLiveIn, varDsc->lvVarIndex))
    {
        return ValueInitKind::None;
    }

    if (varDsc->lvIsParam)
    {
        return ValueInitKind::Param;
    }

    if (!_compiler->fgVarNeedsExplicitZeroInit(lclNum, /* bbInALoop */ false, /* bbIsReturn */ false))
    {
        return ValueInitKind::Zero;
    }

    return ValueInitKind::Uninit;
}

#ifdef DEBUG
void Llvm::displayInitKindForLocal(unsigned lclNum, ValueInitKind initKind)
{
    printf("Setting V%02u's initial value to ", lclNum);
    switch (initKind)
    {
        case ValueInitKind::None:
            printf("nothing\n");
            break;
        case ValueInitKind::Param:
            printf("param %%%u\n", _compiler->lvaGetDesc(lclNum)->lvLlvmArgNum);
            break;
        case ValueInitKind::Zero:
            printf("zero\n");
            break;
        case ValueInitKind::Uninit:
            printf("uninit\n");
            break;
        default:
            unreached();
    }
}
#endif // DEBUG

//------------------------------------------------------------------------
// getShadowFrameSize: What is the size of a function's shadow frame?
//
// Arguments:
//    funcIdx - Index representing the function
//
// Return Value:
//    The size of the shadow frame for the given function. We term this
//    the value by which the shadow stack pointer must be offset before
//    calling managed code such that the caller will not clobber anything
//    live on the frame. Note that filters do not have any shadow state
//    of their own and use the "original" frame from the parent function.
//
unsigned Llvm::getShadowFrameSize(unsigned funcIdx) const
{
    if (_compiler->funGetFunc(funcIdx)->funKind == FUNC_FILTER)
    {
        return 0;
    }

    assert((_shadowStackLocalsSize % TARGET_POINTER_SIZE) == 0);
    return _shadowStackLocalsSize;
}

unsigned Llvm::getCalleeShadowStackOffset(unsigned funcIdx, bool isTailCall) const
{
    if (isTailCall)
    {
        return 0;
    }

    return getShadowFrameSize(funcIdx);
}

//------------------------------------------------------------------------
// canEmitCallAsShadowTailCall: Can a call be made into a shadow tail call?
//
// Arguments:
//    callIsInTry    - Is the call in a protected region
//    callIsInFilter - Is the call in a filter
//
// Return Value:
//    Whether a call can be made without preserving the current shadow frame.
//
bool Llvm::canEmitCallAsShadowTailCall(bool callIsInTry, bool callIsInFilter) const
{
    // We don't want to tail call anything in debug code, as it leads to a confusing debugging experience where
    // calls down the stack may modify (corrupt) shadow variables from their callers.
    if (_compiler->opts.compDbgCode)
    {
        return false;
    }

    // Address-exposed shadow state may be observed by the callee or filters that run in the first pass of EH.
    if (m_anyAddressExposedShadowLocals)
    {
        return false;
    }

    // Both protected regions and filters induce exceptional flow that may return back to this method.
    return !callIsInTry && !callIsInFilter;
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
    // Other backends use "lvOnFrame" for this value, but for us it is
    // not a great fit because of defaulting to "true" for new locals.
    return varDsc->GetRegNum() == REG_STK;
}

bool Llvm::isShadowStackLocal(unsigned lclNum) const
{
    return (lclNum == _shadowStackLclNum) || (lclNum == _originalShadowStackLclNum);
}

bool Llvm::isFuncletParameter(unsigned lclNum) const
{
    return isShadowStackLocal(lclNum);
}
