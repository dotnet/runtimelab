// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                                        Linear Shadow Stack Allocator                                         |
// ================================================================================================================

#include "llvm.h"
#include "ssarenamestate.h"

// TODO-LLVM-LSSA: only enable this in Debug - test using a Checked compiler in CI.
#define FEATURE_LSSA_ALLOCATION_RESULT

//
// LSSA - the shadow stack allocator.
//
// To make objects visible to the GC scan, we allocate them on the shadow stack. This is the primary component
// that decides **how** that is done. Since we don't want to store on every def and reload on every use, when
// optimizing, we utilize liveness as well as SSA to only allocate those locals that have their lifetimes
// crossed by a safe point (a call which may trigger GC). This is a three-step process:
//
// 1) We determine which locals are may be allocated on the shadow stack. Broadly, there are two cases:
//    - "Tentative", where we know the precise lifetimes. These locals may not end up on the shadow stack at
//      all, as we may determine that they don't cross any safe point, or only some of their defs may end up
//      there.
//    - "Unconditional" cases, where the local will be fully commited to the shadow stack and stored on each def,
//      and reloaded on each use. This may be because the local was address-exposed, and we don't know its exact
//      live range, or due to implementation constraints.
//
// 2) We walk over the totality of IR, in dominator pre-order, maintaining the stack of currently active SSA
//    definitions as well as liveness, and "spill" definitions live across a safe point to the shadow stack.
//    Here, we rely on the conservative nature of our target GC, and don't need to worry about reloading, due
//    to implicit pinning. This phase also processes SDSUs, spilling them in a similar manner to candidate
//    locals.
//
// 3) Finally, we determine the set of locals that will need a home on the shadow stack, assign offsets, and
//    walk the IR once more to replace "shadow" local references with loads and stores.
//
class ShadowStackAllocator
{
    Compiler* const m_compiler;
    Llvm* const m_llvm;

    // TODO-LLVM-LSSA-TP: we could use a denser indexing for the candidates to save memory...
    unsigned m_largestCandidateVarIndexPlusOne = 0;

    unsigned m_prologZeroingOffset = 0;
    unsigned m_prologZeroingSize = 0;
    GenTree* m_lastPrologNode = nullptr;

#ifdef FEATURE_LSSA_ALLOCATION_RESULT
    class LssaAllocationResult;

    LssaAllocationResult* m_allocationResult = nullptr;
    const char* m_expectedAllocation = nullptr;
#endif // FEATURE_LSSA_ALLOCATION_RESULT

public:
    ShadowStackAllocator(Llvm* llvm) : m_compiler(llvm->_compiler), m_llvm(llvm)
    {
    }

    void Allocate()
    {
        IdentifyCandidatesAndInitializeLocals();
        SpillValuesLiveAcrossSafePoints();
        AllocateAndInitializeLocals();
        InitializeAllocationResult();
        FinalizeProlog();
        RewriteShadowFrameReferences();
        ReportAllocationResult();
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

                            // Update the SSA and liveness data for this now explicit definition.
                            BasicBlock* initBlock = m_compiler->fgFirstBB;
                            if (m_compiler->lvaInSsa(fieldLclNum))
                            {
                                store->SetSsaNum(SsaConfig::FIRST_SSA_NUM);

                                LclSsaVarDsc* ssaDsc = fieldVarDsc->GetPerSsaData(store->GetSsaNum());
                                assert(ssaDsc->GetDefNode() == nullptr);
                                ssaDsc->SetDefNode(store);
                                ssaDsc->SetBlock(initBlock);
                            }
                            if (fieldVarDsc->lvTracked)
                            {
                                VarSetOps::RemoveElemD(m_compiler, initBlock->bbLiveIn, fieldVarDsc->lvVarIndex);
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

            // Assume for a start this won't be a shadow local.
            regNumber allocLocation = REG_NA;
            INDEBUG(const char* reason = nullptr);

            // The unwind frame MUST be allocated on the shadow stack. The runtime uses its value to invoke filters.
            if (lclNum == m_llvm->m_unwindFrameLclNum)
            {
                allocLocation = REG_STK_CANDIDATE_UNCONDITIONAL;
                INDEBUG(reason = "virtual unwind frame");
            }
            // Locals live-in/out of funclets need to be accessible throughout the whole logical method and using the
            // shadow stack is a simple way to achieve this. Another would be to implement LLVM intrinsics that allow
            // accessing the root method's native frame (effectively) and use them in codegen. Note: we overapproximate
            // the set of locals live cross-funclet by using "lvLiveInOutOfHndlr" here as a CQ quirk. Currently, LLVM
            // is not able to enregister locals that are live across EH pads, and our codegen is not able to produce
            // the correct PHIs anyway.
            //
            else if (m_compiler->ehHasCallableHandlers() && (!varDsc->lvTracked || varDsc->lvLiveInOutOfHndlr))
            {
                allocLocation = REG_STK_CANDIDATE_UNCONDITIONAL;
                INDEBUG(reason = "eh");
            }
            // GC locals needs to go on the shadow stack for the scan to find them. With tracked locals, we may be
            // able to prove that they are not alive across any safe point and so designate them as "candidates".
            else if (varDsc->HasGCPtr())
            {
                if (m_compiler->lvaInSsa(lclNum))
                {
                    if (varDsc->lvVarIndex >= m_largestCandidateVarIndexPlusOne)
                    {
                        m_largestCandidateVarIndexPlusOne = varDsc->lvVarIndex + 1;
                    }

                    allocLocation = REG_STK_CANDIDATE_TENTATIVE;
                    INDEBUG(reason = "gc");
                }
                else
                {
                    allocLocation = REG_STK_CANDIDATE_UNCONDITIONAL;
                    INDEBUG(reason = "gc, not in SSA");
                }
            }

            if (allocLocation != REG_NA)
            {
                JITDUMP("V%02u: %s (%s)\n", lclNum, getRegName(allocLocation), reason);
                varDsc->SetRegNum(allocLocation);
            }
        }
    }

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
        JITDUMP("\nIn Lssa::SpillValuesLiveAcrossSafePoints\n");
        LssaDomTreeVisitor visitor(this);
        visitor.ProcessBlocks();
    }

    class LssaDomTreeVisitor : public NewDomTreeVisitor<LssaDomTreeVisitor>
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

        static const unsigned GC_EXPOSED_NO = 0;
        static const unsigned GC_EXPOSED_YES = 1;
        static const unsigned GC_EXPOSED_UNKNOWN = ValueNumStore::NoVN;
        static const unsigned LAST_ACTIVE_USE_IS_LAST_USE_BIT = 1 << 31;

        ShadowStackAllocator* m_lssa;
        SmallHashTable<GenTree*, unsigned, 8, DeterministicNodeHashInfo> m_liveSdsuGcDefs;
        ArrayStack<unsigned> m_spillLclsRef;
        ArrayStack<unsigned> m_spillLclsByref;
        ArrayStack<GenTree*> m_containedOperands;

        bool m_anyCandidates;
        SsaRenameState m_activeDefs;
        VARSET_TP m_liveDefs;

#ifdef DEBUG
        JitHashTable<LclSsaVarDsc*, JitPtrKeyFuncs<LclSsaVarDsc>, const char*> m_gcExposedStatusReasonMap;
#endif // DEBUG

    public:
        LssaDomTreeVisitor(ShadowStackAllocator* lssa)
            : NewDomTreeVisitor(lssa->m_compiler)
            , m_lssa(lssa)
            , m_liveSdsuGcDefs(m_compiler->getAllocator(CMK_LSRA))
            , m_spillLclsRef(m_compiler->getAllocator(CMK_LSRA))
            , m_spillLclsByref(m_compiler->getAllocator(CMK_LSRA))
            , m_containedOperands(m_compiler->getAllocator(CMK_LSRA))
            , m_anyCandidates(lssa->m_largestCandidateVarIndexPlusOne != 0)
            , m_activeDefs(m_compiler->getAllocator(CMK_LSRA), lssa->m_largestCandidateVarIndexPlusOne)
            , m_liveDefs(VarSetOps::MakeEmpty(m_compiler))
#ifdef DEBUG
            , m_gcExposedStatusReasonMap(m_compiler->getAllocator(CMK_DebugOnly))
#endif // DEBUG
        {
        }

        void PreOrderVisit(BasicBlock* block)
        {
            ProcessBlock(block);
        }

        void PostOrderVisit(BasicBlock* block)
        {
            m_activeDefs.PopBlockStacks(block);
        }

        void ProcessBlocks()
        {
            if (m_anyCandidates)
            {
                // Push the live-in definitions on the stack.
                unsigned lclVarIndex;
                BasicBlock* initBlock = m_compiler->fgFirstBB;
                VarSetOps::Iter iter(m_compiler, initBlock->bbLiveIn);
                while (iter.NextElem(&lclVarIndex))
                {
                    LclVarDsc* varDsc = m_compiler->lvaGetDescByTrackedIndex(lclVarIndex);
                    if (IsCandidateLocal(varDsc))
                    {
                        assert(varDsc->GetPerSsaData(SsaConfig::FIRST_SSA_NUM)->GetDefNode() == nullptr);
                        PushActiveLocalDef(initBlock, varDsc, SsaConfig::FIRST_SSA_NUM);
                    }
                }

                // When optimizing, we need to keep track of the most recent SSA defininions for locals,
                // and so process blocks in dominator pre-order.
                WalkTree(m_compiler->m_domTree);

                INDEBUG(VerifyActiveUseCounts());
            }
            else
            {
                for (BasicBlock* block : m_compiler->Blocks())
                {
                    ProcessBlock(block);
                }
            }
        }

    private:
        void ProcessBlock(BasicBlock* block)
        {
            assert(m_liveSdsuGcDefs.Count() == 0);
            LIR::Range& blockRange = LIR::AsRange(block);

            if (m_anyCandidates)
            {
                VarSetOps::Assign(m_compiler, m_liveDefs, block->bbLiveIn);
                JITDUMPEXEC(PrintCurrentLiveCandidates());
            }

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
                    assert(m_lssa->IsGcConservative());

                    GenTree* retBufNode = node->AsCall()->gtArgs.GetRetBufferArg()->GetNode();
                    if ((retBufNode->gtLIRFlags & LIR::Flags::Mark) != 0)
                    {
                        unsigned spillLclNum;
                        m_liveSdsuGcDefs.TryGetValue(retBufNode, &spillLclNum);
                        SpillSdsuValue(blockRange, retBufNode, &spillLclNum);
                        m_liveSdsuGcDefs.AddOrUpdate(retBufNode, spillLclNum);
                    }
                    else if (retBufNode->OperIsLocalRead())
                    {
                        unsigned lclNum = retBufNode->AsLclVarCommon()->GetLclNum();
                        LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
                        if (IsCandidateLocal(varDsc))
                        {
                            SpillLocalValue(lclNum, retBufNode->AsLclVarCommon()->GetSsaNum()
                                            DEBUGARG("is used as a return buffer"));
                        }
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

                        ProcessUse(block, user, use);
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
                    ProcessSafePoint(block, node);
                }

                ProcessDef(block, node);
            }
        }

        void ProcessUse(BasicBlock* block, GenTree* user, GenTree** use)
        {
            GenTree* node = *use;
            assert(!node->isContained());

            if ((node->gtLIRFlags & LIR::Flags::Mark) != 0)
            {
                unsigned spillLclNum = BAD_VAR_NUM;
                bool operandWasRemoved = m_liveSdsuGcDefs.TryRemove(node, &spillLclNum);
                assert(operandWasRemoved);

                if (spillLclNum != BAD_VAR_NUM)
                {
                    GenTree* lclVarNode = m_compiler->gtNewLclVarNode(spillLclNum);

                    *use = lclVarNode;
                    LIR::AsRange(block).InsertBefore(user, lclVarNode);
                    ReleaseSdsuSpillLocal(spillLclNum);

                    JITDUMP("Spilled [%06u] used by [%06u] replaced with V%02u:\n",
                        Compiler::dspTreeID(node), Compiler::dspTreeID(user), spillLclNum);
                    DISPNODE(lclVarNode);
                }

                node->gtLIRFlags &= ~LIR::Flags::Mark;
            }
            else if (node->OperIsLocalRead() && !node->OperIs(GT_PHI_ARG))
            {
                LclVarDsc* varDsc = m_compiler->lvaGetDesc(node->AsLclVarCommon());
                if (IsCandidateLocal(varDsc))
                {
                    // IR does not record correct last uses because liveness considers positions of LCL_VAR nodes
                    // in the linear order as their use positions. However, this may not not be the case, e. g.:
                    //
                    //  t1 = LCL_VAR V01 ; not tagged <last use>, but is the actual true last use
                    //  t2 = LCL_VAR V01 <last use>; according to liveness
                    //      USE(t2)
                    //      USE(t1)
                    //
                    // This all stems from the fact LCL_VARs are used at their user. We need to track last uses
                    // precisely ("killing" a local too early would be a correctness problem, as it may yet be
                    // live across some safe point) and therefore employ a workaround, utilizing the fact we know
                    // such "out of order" last uses can only occur within a single block.
                    //
                    LclSsaVarDsc* ssaDsc = varDsc->GetPerSsaData(node->AsLclVarCommon()->GetSsaNum());
                    if (node->AsLclVarCommon()->HasLastUse())
                    {
                        // This may be a case like the above (we're processing "Use(t2)"). Since liveness always
                        // tags the first local encountered in a backwards traversal as "last use", there will be
                        // no more LCL_VAR nodes that refer to this def (in this block). The only ones remaining
                        // are those in the "activeUseCount", thus, the true last use will be the one to set it to
                        // zero. Record this fact.
                        SetLastActiveUseIsLastUse(ssaDsc);
                    }

                    if ((GetActiveUseCount(ssaDsc) == 1) && IsLastActiveUseLastUse(ssaDsc))
                    {
                        UpdateLiveLocalDefs(varDsc, /* isDead */ true, /* isBorn */ false);
                        ResetActiveUseCount(ssaDsc); // There may be multiple last uses; clear the bit and the count.
                    }
                    else
                    {
                        DecrementActiveUseCount(ssaDsc);
                    }
                }
            }
        }

        void ProcessSafePoint(BasicBlock* block, GenTree* node)
        {
            JITDUMP(" -- Processing a safe point:\n");
            DISPNODE(node);

            if (m_liveSdsuGcDefs.Count() != 0)
            {
                JITDUMP("Found GC SDSUs live across it:\n");
                for (auto def : m_liveSdsuGcDefs)
                {
                    SpillSdsuValue(LIR::AsRange(block), def.Key(), &def.Value());
                }
            }
            if (m_anyCandidates)
            {
                unsigned lclVarIndex;
                VarSetOps::Iter iter(m_compiler, m_liveDefs);
                while (iter.NextElem(&lclVarIndex))
                {
                    unsigned lclNum = m_compiler->lvaTrackedIndexToLclNum(lclVarIndex);
                    LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
                    if (IsCandidateLocal(varDsc))
                    {
                        unsigned ssaNum = m_activeDefs.Top(lclVarIndex);
                        SpillLocalValue(lclNum, ssaNum DEBUGARG("is live"));
                    }
                }
            }
        }

        unsigned GetSdsuSpillLocal(GenTree* node)
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
                varDsc->SetRegNum(REG_STK_CANDIDATE_UNCONDITIONAL); // TODO-LLVM-LSSA-CQ: improve, no need to reload.
                if (type == TYP_STRUCT)
                {
                    m_compiler->lvaSetStruct(lclNum, layout, false);
                }
            }

            return lclNum;
        }

        void ReleaseSdsuSpillLocal(unsigned lclNum)
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

            unsigned spillLclNum = GetSdsuSpillLocal(defNode);
            JITDUMP("Spilling as V%02u:\n", spillLclNum);
            DISPNODE(defNode);

            GenTree* store = m_compiler->gtNewTempStore(spillLclNum, defNode);
            blockRange.InsertAfter(defNode, store);

            *pSpillLclNum = spillLclNum;
        }

        void SpillLocalValue(unsigned lclNum, unsigned ssaNum DEBUGARG(const char* spillReason))
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
            LclSsaVarDsc* ssaDsc = varDsc->GetPerSsaData(ssaNum);
            GenTreeLclVarCommon* defNode = ssaDsc->GetDefNode();

            INDEBUG(const char* reason);
            if (!IsGcExposedLocalValue(ssaDsc DEBUGARG(&reason)))
            {
                JITDUMP("V%02u/%d %s, but did not insert a spill: %s\n", lclNum, ssaNum, spillReason, reason);
                return;
            }

            if (defNode == nullptr)
            {
                // The implicit definitions is always "spilled", see "AllocateAndInitializeLocals". We could be more
                // precise by:
                // 1. Only spilling the implicit definition when it is explicitly live (same as for other definitions).
                // 2. At each safe point, mark all candidates that do not have a dominating spill as "implicitly live",
                //    so that they are zero-initialized in the prolog.
                // TODO-LLVM-LSSA-CQ: implement the above optimization.
                JITDUMP("V%02u/%d (implicit def) %s, anticipating initialization in prolog\n", lclNum, ssaNum);
                MarkLocalSpilled(varDsc, ssaDsc);
                return;
            }

            // TODO-LLVM-LSSA-CQ: support more optimal spilling for local fields. Currently this does not matter because
            // we exclude all local fields from SSA.
            GenTreeLclVar* value = m_compiler->gtNewLclVarNode(lclNum);
            value->SetSsaNum(ssaNum);
            value->SetRegNum(REG_LLVM);
            GenTree* store = m_compiler->gtNewStoreLclVarNode(lclNum, value);

            LIR::Range& defBlockRange = LIR::AsRange(ssaDsc->GetBlock());
            if (defNode->IsPhiDefn())
            {
                defBlockRange.InsertBefore(
                    defBlockRange.FirstNonPhiOrCatchArgNode(), value, store);
            }
            else
            {
                defBlockRange.InsertAfter(defNode, value, store);
            }

            JITDUMP("V%02u/%d %s, inserted a spill:\n", lclNum, ssaNum, spillReason);
            DISPTREERANGE(defBlockRange, store);
            MarkLocalSpilled(varDsc, ssaDsc);
        }

        bool IsGcExposedValue(GenTree* node)
        {
            // TODO-LLVM-LSSA-CQ: add handling for PHIs here. Take note to handle cycles properly.
            assert(node->IsValue());
            if (node->OperIsLocalRead())
            {
                LclVarDsc* varDsc = m_compiler->lvaGetDesc(node->AsLclVarCommon());
                if (IsCandidateLocal(varDsc))
                {
                    LclSsaVarDsc* ssaDsc = varDsc->GetPerSsaData(node->AsLclVarCommon()->GetSsaNum());
                    return IsGcExposedLocalValue(ssaDsc);
                }

                // Non-candidate locals are always on the shadow stack and so not exposed.
                return false;
            }

            return IsGcExposedSdsuValue(node);
        }

        bool IsGcExposedSdsuValue(GenTree* node)
        {
            assert(node->IsValue() && !node->OperIsLocal());

            // Addition and subtraction of byrefs follows convenient rules that allow us to consider only
            // whether the "base" is exposed or not. Namely, byref arithmetic must stay within the bounds
            // of the underlying object, or be performed on pinned values only.
            if (node->OperIs(GT_ADD, GT_SUB) && node->gtGetOp2()->TypeIs(TYP_I_IMPL))
            {
                return IsGcExposedValue(node->gtGetOp1());
            }

            // Local address nodes always point to the stack (native or shadow). Constant handles will
            // only point to immortal and immovable (frozen) objects.
            return !node->OperIs(GT_LCL_ADDR) && !node->IsIconHandle() && !node->IsIntegralConst(0);
        }

        bool IsGcExposedLocalValue(LclSsaVarDsc* ssaDsc DEBUGARG(const char** pReason = nullptr))
        {
            unsigned status = GetGcExposedStatus(ssaDsc DEBUGARG(pReason));
            if (status == GC_EXPOSED_UNKNOWN)
            {
                INDEBUG(const char* reason = nullptr);
                GenTreeLclVarCommon* defNode = ssaDsc->GetDefNode();
                if ((defNode != nullptr) && defNode->OperIs(GT_STORE_LCL_VAR) && !IsGcExposedValue(defNode->Data()))
                {
                    INDEBUG(reason = "value not exposed");
                    status = GC_EXPOSED_NO;
                }
                else
                {
                    status = GC_EXPOSED_YES;
                }

                // This caching is designed to prevent quadratic behavior.
                SetGcExposedStatus(ssaDsc, status DEBUGARG(reason));
                DBEXEC(pReason != nullptr, *pReason = reason);
            }

            assert(status != GC_EXPOSED_UNKNOWN);
            return status == GC_EXPOSED_YES;
        }

        void SetGcExposedStatus(LclSsaVarDsc* ssaDsc, unsigned status DEBUGARG(const char* reason))
        {
            unsigned* pStatus = GetRawGcExposedStatusRef(ssaDsc);
            assert((*pStatus == GC_EXPOSED_UNKNOWN) || ((*pStatus == GC_EXPOSED_YES) && (status == GC_EXPOSED_NO)));
            assert(status != GC_EXPOSED_UNKNOWN);

            DBEXEC(reason != nullptr, m_gcExposedStatusReasonMap.Set(ssaDsc, reason));
            *pStatus = status;
        }

        unsigned GetGcExposedStatus(LclSsaVarDsc* ssaDsc DEBUGARG(const char** pReason))
        {
            INDEBUG(m_gcExposedStatusReasonMap.Lookup(ssaDsc, pReason));
            return *GetRawGcExposedStatusRef(ssaDsc);
        }

        unsigned* GetRawGcExposedStatusRef(LclSsaVarDsc* ssaDsc)
        {
            // We do a little hack here to avoid modifying "LclSsaVarDsc". VNs are not used at this point.
            return ssaDsc->m_vnPair.GetLiberalAddr();
        }

        void MarkLocalSpilled(LclVarDsc* varDsc, LclSsaVarDsc* ssaDsc)
        {
            varDsc->SetRegNum(REG_STK_CANDIDATE_COMMITED);
            SetGcExposedStatus(ssaDsc, GC_EXPOSED_NO DEBUGARG("already spilled"));
        }

        void ProcessDef(BasicBlock* block, GenTree* node)
        {
            if (node->OperIsLocal())
            {
                LclVarDsc* varDsc = m_compiler->lvaGetDesc(node->AsLclVarCommon());
                if (IsCandidateLocal(varDsc))
                {
                    // We depend here on the conservativeness of GC in not reloading after a safepoint.
                    assert(m_lssa->IsGcConservative());
                    node->SetRegNum(REG_LLVM);

                    // If this is a definition, add it to the stack of currently active ones and update liveness.
                    unsigned ssaNum = node->AsLclVarCommon()->GetSsaNum();
                    if (node->OperIsLocalStore())
                    {
                        PushActiveLocalDef(block, varDsc, ssaNum);
                        UpdateLiveLocalDefs(varDsc, node->AsLclVarCommon()->HasLastUse(), /* isBorn */ true);
                    }
                    // Increment the "active" use count used for accurate last use detection. Note that skipping
                    // unused locals here means that we could technically extend their live ranges unnecessarily
                    // (if this was the last 'use'), but all such cases should have been DCEd by this point, and
                    // it's not a correctness problem to skip them.
                    else if (!node->OperIs(GT_PHI_ARG) && !node->IsUnusedValue())
                    {
                        IncrementActiveUseCount(varDsc->GetPerSsaData(ssaNum));
                    }
                }
            }
            else if (node->IsValue() && !node->IsUnusedValue() && IsGcExposedType(node) && IsGcExposedSdsuValue(node))
            {
                node->gtLIRFlags |= LIR::Flags::Mark;
                m_liveSdsuGcDefs.AddOrUpdate(node, BAD_VAR_NUM);
            }
        }

        bool IsGcExposedType(GenTree* node) const
        {
            if (varTypeIsGC(node))
            {
                return true;
            }
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

                return true;
            }

            return false;
        }

        void PushActiveLocalDef(BasicBlock* block, LclVarDsc* varDsc, unsigned ssaNum)
        {
            LclSsaVarDsc* ssaDsc = varDsc->GetPerSsaData(ssaNum);
            m_activeDefs.Push(block, varDsc->lvVarIndex, ssaNum);
            ResetActiveUseCount(ssaDsc);
        }

        void UpdateLiveLocalDefs(LclVarDsc* varDsc, bool isDead, bool isBorn)
        {
            if (isDead)
            {
                VarSetOps::RemoveElemD(m_compiler, m_liveDefs, varDsc->lvVarIndex);
            }
            else if (isBorn)
            {
                VarSetOps::AddElemD(m_compiler, m_liveDefs, varDsc->lvVarIndex);
            }

            JITDUMPEXEC(PrintCurrentLiveCandidates());
        }

        void SetLastActiveUseIsLastUse(LclSsaVarDsc* ssaDsc)
        {
            assert(!IsLastActiveUseLastUse(ssaDsc));
            *GetRawActiveUseCountRef(ssaDsc) |= LAST_ACTIVE_USE_IS_LAST_USE_BIT;
        }

        bool IsLastActiveUseLastUse(LclSsaVarDsc* ssaDsc)
        {
            return (*GetRawActiveUseCountRef(ssaDsc) & LAST_ACTIVE_USE_IS_LAST_USE_BIT) != 0;
        }

        void ResetActiveUseCount(LclSsaVarDsc* ssaDsc)
        {
            unsigned* pCount = GetRawActiveUseCountRef(ssaDsc);
            assert((*pCount == ValueNumStore::NoVN) || (GetActiveUseCount(ssaDsc) <= 1));
            *pCount = 0;
        }

        void IncrementActiveUseCount(LclSsaVarDsc* ssaDsc)
        {
            assert(GetActiveUseCount(ssaDsc) < ssaDsc->GetNumUses());
            (*GetRawActiveUseCountRef(ssaDsc))++;
        }

        void DecrementActiveUseCount(LclSsaVarDsc* ssaDsc)
        {
            assert(GetActiveUseCount(ssaDsc) > 0);
            (*GetRawActiveUseCountRef(ssaDsc))--;
        }

        unsigned GetActiveUseCount(LclSsaVarDsc* ssaDsc)
        {
            return *GetRawActiveUseCountRef(ssaDsc) & ~LAST_ACTIVE_USE_IS_LAST_USE_BIT;
        }

        unsigned* GetRawActiveUseCountRef(LclSsaVarDsc* ssaDsc)
        {
            // Another little hack for storing per-def information, needed for last-use detection.
            return ssaDsc->m_vnPair.GetConservativeAddr();
        }

        bool IsCandidateLocal(LclVarDsc* varDsc)
        {
            return (varDsc->GetRegNum() == REG_STK_CANDIDATE_TENTATIVE) ||
                   (varDsc->GetRegNum() == REG_STK_CANDIDATE_COMMITED);
        }

#ifdef DEBUG
        void VerifyActiveUseCounts()
        {
            for (unsigned lclNum = 0; lclNum < m_compiler->lvaCount; lclNum++)
            {
                LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
                if (IsCandidateLocal(varDsc))
                {
                    unsigned defCount = varDsc->lvPerSsaData.GetCount();
                    for (unsigned defIndex = 0; defIndex < defCount; defIndex++)
                    {
                        // We should have seen the exact totality of all uses in the IR walk. Make an exemption
                        // for an unused implicit definition, for which we do not initialize the use counts.
                        LclSsaVarDsc* ssaDsc = varDsc->lvPerSsaData.GetSsaDefByIndex(defIndex);
                        if ((ssaDsc->GetDefNode() != nullptr) || (ssaDsc->GetNumUses() != 0))
                        {
                            assert(GetActiveUseCount(ssaDsc) == 0);
                        }
                    }
                }
            }
        }

        void PrintCurrentLiveCandidates()
        {
            if (!m_anyCandidates)
            {
                return;
            }

            printf("Liveness: { ");
            unsigned lclVarIndex;
            VarSetOps::Iter iter(m_compiler, m_liveDefs);
            while (iter.NextElem(&lclVarIndex))
            {
                unsigned lclNum = m_compiler->lvaTrackedIndexToLclNum(lclVarIndex);
                LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
                if (IsCandidateLocal(varDsc))
                {
                    printf("V%02u/%d ", lclNum, m_activeDefs.Top(lclVarIndex));
                }
            }
            printf("}\n");
        }
#endif // DEBUG
    };

    void AllocateAndInitializeLocals()
    {
        JITDUMP("\nIn Lssa::AllocateAndInitializeLocals\n");

        std::vector<unsigned> shadowFrameLocals;
        for (unsigned lclNum = 0; lclNum < m_compiler->lvaCount; lclNum++)
        {
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);

            if (varDsc->GetRegNum() == REG_STK_CANDIDATE_TENTATIVE)
            {
                // There were no safe points across which this local was live.
                JITDUMP("V%02u: %s -> %s\n", lclNum, getRegName(REG_STK_CANDIDATE_TENTATIVE), getRegName(REG_LLVM));
                varDsc->SetRegNum(REG_LLVM);
            }

            if ((varDsc->GetRegNum() == REG_STK_CANDIDATE_COMMITED) ||
                (varDsc->GetRegNum() == REG_STK_CANDIDATE_UNCONDITIONAL))
            {
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
            RecordAllocationActionZeroInit(m_compiler->fgFirstBB, offset, zeroingSize);
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

            unsigned lclBaseOffset = varDsc->GetStackOffset();
            RecordAllocationActionLoadStore(m_llvm->CurrentBlock(), lclBaseOffset, lclNode);

            // Filters will be called by the first pass while live state still exists on shadow frames above (in the
            // traditional sense, where stacks grow down) them. For this reason, filters will access state from the
            // original frame via a dedicated shadow stack pointer, and use the actual shadow stack for calls.
            unsigned shadowStackLclNum = m_llvm->isBlockInFilter(m_llvm->CurrentBlock())
                ? m_llvm->_originalShadowStackLclNum
                : m_llvm->_shadowStackLclNum;
            unsigned lclOffset = lclBaseOffset + lclNode->GetLclOffs();
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

    bool IsGcConservative() const
    {
        return true;
    }

#ifdef FEATURE_LSSA_ALLOCATION_RESULT
    void InitializeAllocationResult()
    {
        const char* expectedAllocation = nullptr;
        if (JitConfig.JitRunLssaTests())
        {
            CORINFO_LLVM_JIT_TEST_INFO info;
            m_llvm->GetJitTestInfo(CORINFO_JIT_TEST_LSSA, &info);
            expectedAllocation = info.ExpectedLssaAllocation;
        }

        if ((expectedAllocation == nullptr) && !VERBOSE)
        {
            return;
        }

        CompAllocator alloc = m_compiler->getAllocator(CMK_DebugOnly);
        m_allocationResult = new (alloc) LssaAllocationResult(m_compiler, alloc);
        m_expectedAllocation = expectedAllocation;
    }

    void ReportAllocationResult()
    {
        if (!RecordAllocationResult())
        {
            return;
        }

        if (VERBOSE)
        {
            printf("\nFinal allocation:\n");
            m_allocationResult->Print();
            printf("\n");
        }

        if (m_expectedAllocation != nullptr)
        {
            if (!m_allocationResult->Compare(m_expectedAllocation))
            {
                printf("LSSA test '%s' failed.\n", m_compiler->eeGetMethodFullName(m_llvm->m_info->compMethodHnd));
                printf("Expected allocation:\n%s\n", m_expectedAllocation);
                printf("Actual allocation:\n");
                m_allocationResult->Print();
                printf("\n");

                assert(!"LSSA test failed");
                BADCODE("LSSA test failed");
            }

            JITDUMP("Ran [LSSATest(...)] - succeeded!\n");
        }
    }

    bool RecordAllocationResult() const
    {
        return m_allocationResult != nullptr;
    }

    void RecordAllocationActionZeroInit(BasicBlock* initialBlock, unsigned offset, unsigned size)
    {
        if (RecordAllocationResult())
        {
            m_allocationResult->SelectBlock(initialBlock);
            m_allocationResult->RecordZeroInit(offset, size);
        }
    }

    void RecordAllocationActionLoadStore(BasicBlock* block, unsigned offset, GenTreeLclVarCommon* lclNode)
    {
        if (RecordAllocationResult())
        {
            if (lclNode->OperIs(GT_LCL_ADDR))
            {
                // We don't record LCL_ADDR transformations.
                return;
            }

            m_allocationResult->SelectBlock(block);
            m_allocationResult->RecordLoadStore(offset, lclNode);
        }
    }

    class LssaAllocationResult
    {
        enum class AllocationActionKind : unsigned
        {
            Block,      // BB<index>:
            ZeroInit,   // ZEROINIT <slots...>
            Store,      // STORE <slot> <local> <value>
            Load,       // LOAD  <slot> <local>
            StoreField, // STORE_FIELD[<start>..<end>] <slot> <local> <value>
            LoadField,  // LOAD_FIELD[<start>..<end>]  <slot> <local> <value>
            Count
        };

        enum class IRValueKind : unsigned
        {
            Sdsu,
            ArgLocal,
            UserLocal,
            TempLocal,
            Count
        };

        struct IRValue
        {
            IRValueKind Kind : 2;
            unsigned Num : 30;
            unsigned SsaNum;
        };

        struct AllocationAction
        {
            AllocationActionKind Kind;
            union
            {
                unsigned BlockIndex;
                struct
                {
                    unsigned ZeroInitOffset;
                    unsigned ZeroInitOffsetEnd;
                };
                struct
                {
                    unsigned Slot;
                    unsigned short FieldOffset;
                    unsigned short FieldEndOffset;
                    IRValue Local;
                    IRValue Value;
                };
            };

            // This part of the struct is for comments only.
            GenTree* CommentNode = nullptr;
        };

        Compiler* m_compiler;
        CompAllocator m_alloc;
        ArrayStack<AllocationAction> m_actions;

        BasicBlock* m_currentBlock = nullptr;
        unsigned m_currentBlockIndex = 1;

        // Shadow stack offsets are relabeled via "slots" to avoid offset allocation differences affecting the output.
        JitHashTable<unsigned, JitSmallPrimitiveKeyFuncs<unsigned>, unsigned> m_slotMap;
        unsigned m_currentSlot = 0;

        // Compiler temporaries are relabled to avoid small differences in numbering affecting the output.
        JitHashTable<unsigned, JitSmallPrimitiveKeyFuncs<unsigned>, unsigned> m_tempLocalsMap;
        unsigned m_currentTempLocalNum = 0;

    public:
        LssaAllocationResult(Compiler* compiler, CompAllocator alloc)
            : m_compiler(compiler)
            , m_alloc(alloc)
            , m_actions(alloc)
            , m_slotMap(alloc)
            , m_tempLocalsMap(alloc)
        {
        }

        void SelectBlock(BasicBlock* block)
        {
            if (block != m_currentBlock)
            {
                m_actions.Push({AllocationActionKind::Block, m_currentBlockIndex++});
                m_currentBlock = block;
            }
        }

        void RecordZeroInit(unsigned offset, unsigned size)
        {
            AllocationAction action{AllocationActionKind::ZeroInit};
            action.ZeroInitOffset = offset;
            action.ZeroInitOffsetEnd = offset + size;
            m_actions.Push(action);
        }

        void RecordLoadStore(unsigned offset, GenTreeLclVarCommon* lclNode)
        {
            AllocationActionKind kind;
            switch (lclNode->OperGet())
            {
                case GT_STORE_LCL_VAR:
                    kind = AllocationActionKind::Store;
                    break;
                case GT_LCL_VAR:
                    kind = AllocationActionKind::Load;
                    break;
                case GT_STORE_LCL_FLD:
                    kind = AllocationActionKind::StoreField;
                    break;
                case GT_LCL_FLD:
                    kind = AllocationActionKind::LoadField;
                    break;
                default:
                    unreached();
            }

            AllocationAction action{kind};
            action.Slot = GetSlot(offset);
            if (lclNode->OperIsLocalField())
            {
                unsigned storeSize =
                    lclNode->TypeIs(TYP_STRUCT) ? lclNode->GetLayout(m_compiler)->GetSize() : genTypeSize(lclNode);
                action.FieldOffset = lclNode->GetLclOffs();
                action.FieldEndOffset = action.FieldOffset + storeSize;
            }
            action.Local = GetLocalValue(lclNode->GetLclNum(), lclNode->GetSsaNum());

            if (lclNode->OperIsLocalStore())
            {
                GenTree* value = lclNode->Data();
                if (value->OperIsLocalRead())
                {
                    action.Value =
                        GetLocalValue(value->AsLclVarCommon()->GetLclNum(), value->AsLclVarCommon()->GetSsaNum());
                }
                else
                {
                    action.Value = {IRValueKind::Sdsu};
                }
            }

            action.CommentNode = lclNode;
            m_actions.Push(action);
        }

        void Print()
        {
            for (int i = 0; i < m_actions.Height(); i++)
            {
                AllocationAction& action = m_actions.BottomRef(i);
                if (action.Kind != AllocationActionKind::Block)
                {
                    printf("  ");
                }
                PrintAction(action);
                printf("\n");
            }
        }

        bool Compare(const char* expected)
        {
            // Make sure we don't need to escape 'expected' for "sscanf" below.
            assert(strstr(expected, "%") == nullptr);

            char actualActionBuffer[512];
            for (int i = 0; i < m_actions.Height(); i++)
            {
                char* actualActionBufferEnd = actualActionBuffer;
                PrintFormatted(&actualActionBufferEnd, " ");
                PrintAction(m_actions.BottomRef(i), &actualActionBufferEnd);
                PrintFormatted(&actualActionBufferEnd, " %%n");

                // Use "sscanf" to do what is effectively a whitespace-ignoring comparison.
                int consumed = 0;
                sscanf(expected, actualActionBuffer, &consumed);
                if (consumed == 0)
                {
                    return false;
                }

                expected += consumed;
            }

            // The loop above must consume the input exactly.
            if (strlen(expected) != 0)
            {
                return false;
            }

            return true;
        }

    private:
        unsigned GetSlot(unsigned offset)
        {
            unsigned slot;
            if (!m_slotMap.Lookup(offset, &slot))
            {
                slot = m_currentSlot++;
                m_slotMap.Set(offset, slot);
            }

            return slot;
        }

        IRValue GetLocalValue(unsigned lclNum, unsigned ssaNum)
        {
            IRValue value;
            LclVarDsc* varDsc = m_compiler->lvaGetDesc(lclNum);
            if (lclNum < m_compiler->info.compLocalsCount)
            {
                value.Kind = varDsc->lvIsParam ? IRValueKind::ArgLocal : IRValueKind::UserLocal;
                value.Num = lclNum;
            }
            else
            {
                unsigned num;
                if (!m_tempLocalsMap.Lookup(lclNum, &num))
                {
                    num = m_currentTempLocalNum++;
                    m_tempLocalsMap.Set(lclNum, num);
                }

                value.Kind = IRValueKind::TempLocal;
                value.Num = num;
            }

            value.SsaNum = ssaNum;
            return value;
        }

#define FMT_SS_SLOT "SS%02u"

        template <typename... TArgs>
        void PrintFormatted(char** pBuffer, const char* format, TArgs... args)
        {
            if (pBuffer == nullptr)
            {
                printf(format, args...);
            }
            else
            {
                *pBuffer += sprintf(*pBuffer, format, args...);
            }
        }

        void PrintAction(const AllocationAction& action, char** pBuffer = nullptr)
        {
            const char* format = GetActionKindFormat(action.Kind);
            switch (action.Kind)
            {
                case AllocationActionKind::Block:
                    PrintFormatted(pBuffer, format, action.BlockIndex);
                    break;

                case AllocationActionKind::ZeroInit:
                    PrintFormatted(pBuffer, format);
                    for (unsigned offset = action.ZeroInitOffset; offset < action.ZeroInitOffsetEnd;
                         offset += TARGET_POINTER_SIZE)
                    {
                        unsigned slot;
                        if (m_slotMap.Lookup(offset, &slot))
                        {
                            PrintFormatted(pBuffer, " " FMT_SS_SLOT, slot);
                        }
                    }
                    break;

                case AllocationActionKind::Store:
                case AllocationActionKind::Load:
                case AllocationActionKind::StoreField:
                case AllocationActionKind::LoadField:
                    if ((action.Kind == AllocationActionKind::StoreField) ||
                        (action.Kind == AllocationActionKind::LoadField))
                    {
                        PrintFormatted(pBuffer, format, action.FieldOffset, action.FieldEndOffset, action.Slot);
                    }
                    else
                    {
                        PrintFormatted(pBuffer, format, action.Slot);
                    }
                    PrintFormatted(pBuffer, " ");
                    PrintIRValue(action.Local, pBuffer);

                    if ((action.Kind == AllocationActionKind::Store) ||
                        (action.Kind == AllocationActionKind::StoreField))
                    {
                        PrintFormatted(pBuffer, " ");
                        PrintIRValue(action.Value, pBuffer);
                    }

                    if ((pBuffer == nullptr) && (action.CommentNode != nullptr))
                    {
                        JITDUMP(" # ");
                        JITDUMPEXEC(m_compiler->printTreeID(action.CommentNode));
                    }
                    break;

                default:
                    unreached();
            }
        }

        void PrintIRValue(const IRValue& value, char** pBuffer)
        {
            const char* format = GetIRValueKindFormat(value.Kind);
            PrintFormatted(pBuffer, format, value.Num);
            if ((value.Kind != IRValueKind::Sdsu) && (value.SsaNum != SsaConfig::RESERVED_SSA_NUM))
            {
                PrintFormatted(pBuffer, "/%u", value.SsaNum);
            }
        }

        static const char* GetActionKindFormat(AllocationActionKind kind)
        {
            switch (kind)
            {
                case AllocationActionKind::Block:
                    return "BB%02u:";
                case AllocationActionKind::ZeroInit:
                    return "ZEROINIT";
                case AllocationActionKind::Store:
                    return "STORE " FMT_SS_SLOT;
                case AllocationActionKind::Load:
                    return "LOAD  " FMT_SS_SLOT;
                case AllocationActionKind::StoreField:
                    return "STORE_FIELD[%u..%u] " FMT_SS_SLOT;
                case AllocationActionKind::LoadField:
                    return "LOAD_FIELD[%u..%u]  " FMT_SS_SLOT;
                default:
                    unreached();
            }
        }

        static const char* GetIRValueKindFormat(IRValueKind kind)
        {
            switch (kind)
            {
                case IRValueKind::Sdsu:
                    return "SDSU";
                case IRValueKind::ArgLocal:
                    return "ARG%02u";
                case IRValueKind::UserLocal:
                    return "USR%02u";
                case IRValueKind::TempLocal:
                    return "TMP%02u";
                default:
                    unreached();
            }
        }
    };
#else // !FEATURE_LSSA_ALLOCATION_RESULT
    void InitializeAllocationResult() { }
    void ReportAllocationResult() { }
    bool RecordAllocationResult() const { return false; }
    void RecordAllocationActionZeroInit(BasicBlock* initialBlock, unsigned offset, unsigned size) { }
    void RecordAllocationActionLoadStore(BasicBlock* block, unsigned offset, GenTreeLclVarCommon* lclNode) { }
#endif // !FEATURE_LSSA_ALLOCATION_RESULT
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

    // We can choose to always initialize GC values to reduce the number of "random" pointers on the shadow stack.
    bool isGcLocal = varDsc->HasGCPtr();
    if (isGcLocal && (opts == ValueInitOpts::IncludeImplicitGcUse))
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

    // GC values should never contain indeterminate bits. Note how we're a bit more conservative than the other
    // targets here, which may only zero-initialize the GC fields in a small struct, leaving the rest indeterminate.
    if (isGcLocal || !_compiler->fgVarNeedsExplicitZeroInit(lclNum, /* bbInALoop */ false, /* bbIsReturn */ false))
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
