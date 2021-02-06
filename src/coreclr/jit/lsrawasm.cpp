// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XX                                                                           XX
XX                    Register Requirements for AMD64                        XX
XX                                                                           XX
XX  This encapsulates all the logic for setting register requirements for    XX
XX  the AMD64 architecture.                                                  XX
XX                                                                           XX
XX                                                                           XX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
*/

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#if defined(TARGET_WASM32) || defined(TARGET_WASM64)

#include "jit.h"
#include "sideeffects.h"
#include "lower.h"

//------------------------------------------------------------------------
// BuildNode: Build the RefPositions for for a node
//
// Arguments:
//    treeNode - the node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
// Notes:
// Preconditions:
//    LSRA Has been initialized.
//
// Postconditions:
//    RefPositions have been built for all the register defs and uses required
//    for this node.
//
int LinearScan::BuildNode(GenTree* tree)
{
    assert(false);
    return 0;
}

//------------------------------------------------------------------------
// getTgtPrefOperands: Identify whether the operands of an Op should be preferenced to the target.
//
// Arguments:
//    tree    - the node of interest.
//    prefOp1 - a bool "out" parameter indicating, on return, whether op1 should be preferenced to the target.
//    prefOp2 - a bool "out" parameter indicating, on return, whether op2 should be preferenced to the target.
//
// Return Value:
//    This has two "out" parameters for returning the results (see above).
//
// Notes:
//    The caller is responsible for initializing the two "out" parameters to false.
//
void LinearScan::getTgtPrefOperands(GenTreeOp* tree, bool& prefOp1, bool& prefOp2)
{
    assert(false);
}

//------------------------------------------------------------------------------
// isRMWRegOper: Can this binary tree node be used in a Read-Modify-Write format
//
// Arguments:
//    tree      - a binary tree node
//
// Return Value:
//    Returns true if we can use the read-modify-write instruction form
//
// Notes:
//    This is used to determine whether to preference the source to the destination register.
//
bool LinearScan::isRMWRegOper(GenTree* tree)
{
    assert(false);
    return false;
}

// Support for building RefPositions for RMW nodes.
int LinearScan::BuildRMWUses(GenTreeOp* node, regMaskTP candidates)
{
    assert(false);
    return 0;
}

//------------------------------------------------------------------------
// BuildShiftRotate: Set the NodeInfo for a shift or rotate.
//
// Arguments:
//    tree      - The node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildShiftRotate(GenTree* tree)
{
    assert(false);
    return 0;
}

//------------------------------------------------------------------------
// BuildCall: Set the NodeInfo for a call.
//
// Arguments:
//    call      - The call node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildCall(GenTreeCall* call)
{
    bool                  hasMultiRegRetVal = false;
    const ReturnTypeDesc* retTypeDesc       = nullptr;
    int                   srcCount          = 0;
    int                   dstCount          = 0;
    regMaskTP             dstCandidates     = RBM_NONE;

    assert(!call->isContained());
    if (call->TypeGet() != TYP_VOID)
    {
        hasMultiRegRetVal = call->HasMultiRegRetVal();
        if (hasMultiRegRetVal)
        {
            // dst count = number of registers in which the value is returned by call
            retTypeDesc = call->GetReturnTypeDesc();
            dstCount    = retTypeDesc->GetReturnRegCount();
        }
        else
        {
            dstCount = 1;
        }
    }

    GenTree* ctrlExpr = call->gtControlExpr;
    if (call->gtCallType == CT_INDIRECT)
    {
        ctrlExpr = call->gtCallAddr;
    }

    RegisterType registerType = regType(call);

    // Set destination candidates for return value of the call.
    CLANG_FORMAT_COMMENT_ANCHOR;

#ifdef TARGET_X86
    if (call->IsHelperCall(compiler, CORINFO_HELP_INIT_PINVOKE_FRAME))
    {
        // The x86 CORINFO_HELP_INIT_PINVOKE_FRAME helper uses a custom calling convention that returns with
        // TCB in REG_PINVOKE_TCB. AMD64/ARM64 use the standard calling convention. fgMorphCall() sets the
        // correct argument registers.
        dstCandidates = RBM_PINVOKE_TCB;
    }
    else
#endif // TARGET_X86
        if (hasMultiRegRetVal)
    {
        assert(retTypeDesc != nullptr);
        dstCandidates = retTypeDesc->GetABIReturnRegs();
        assert((int)genCountBits(dstCandidates) == dstCount);
    }
    else if (varTypeUsesFloatReg(registerType))
    {
#ifdef TARGET_X86
        // The return value will be on the X87 stack, and we will need to move it.
        dstCandidates = allRegs(registerType);
#else  // !TARGET_X86
        dstCandidates = RBM_FLOATRET;
#endif // !TARGET_X86
    }
    else if (registerType == TYP_LONG)
    {
        dstCandidates = RBM_LNGRET;
    }
    else
    {
        dstCandidates = RBM_INTRET;
    }

    // number of args to a call =
    // callRegArgs + (callargs - placeholders, setup, etc)
    // there is an explicit thisPtr but it is redundant

    bool callHasFloatRegArgs = false;
    bool isVarArgs           = call->IsVarargs();

    // First, determine internal registers.
    // We will need one for any float arguments to a varArgs call.
    for (GenTreeCall::Use& use : call->LateArgs())
    {
        GenTree* argNode = use.GetNode();
        if (argNode->OperIsPutArgReg())
        {
            HandleFloatVarArgs(call, argNode, &callHasFloatRegArgs);
        }
        else if (argNode->OperGet() == GT_FIELD_LIST)
        {
            for (GenTreeFieldList::Use& use : argNode->AsFieldList()->Uses())
            {
                assert(use.GetNode()->OperIsPutArgReg());
                HandleFloatVarArgs(call, use.GetNode(), &callHasFloatRegArgs);
            }
        }
    }

    // Now, count reg args
    for (GenTreeCall::Use& use : call->LateArgs())
    {
        // By this point, lowering has ensured that all call arguments are one of the following:
        // - an arg setup store
        // - an arg placeholder
        // - a nop
        // - a copy blk
        // - a field list
        // - a put arg
        //
        // Note that this property is statically checked by LinearScan::CheckBlock.
        GenTree* argNode = use.GetNode();

        // Each register argument corresponds to one source.
        if (argNode->OperIsPutArgReg())
        {
            srcCount++;
            BuildUse(argNode, genRegMask(argNode->GetRegNum()));
        }
#ifdef UNIX_AMD64_ABI
        else if (argNode->OperGet() == GT_FIELD_LIST)
        {
            for (GenTreeFieldList::Use& use : argNode->AsFieldList()->Uses())
            {
                assert(use.GetNode()->OperIsPutArgReg());
                srcCount++;
                BuildUse(use.GetNode(), genRegMask(use.GetNode()->GetRegNum()));
            }
        }
#endif // UNIX_AMD64_ABI

#ifdef DEBUG
        // In DEBUG only, check validity with respect to the arg table entry.

        fgArgTabEntry* curArgTabEntry = compiler->gtArgEntryByNode(call, argNode);
        assert(curArgTabEntry);

        if (curArgTabEntry->GetRegNum() == REG_STK)
        {
            // late arg that is not passed in a register
            assert(argNode->gtOper == GT_PUTARG_STK);

#ifdef FEATURE_PUT_STRUCT_ARG_STK
            // If the node is TYP_STRUCT and it is put on stack with
            // putarg_stk operation, we consume and produce no registers.
            // In this case the embedded Obj node should not produce
            // registers too since it is contained.
            // Note that if it is a SIMD type the argument will be in a register.
            if (argNode->TypeGet() == TYP_STRUCT)
            {
                assert(argNode->gtGetOp1() != nullptr && argNode->gtGetOp1()->OperGet() == GT_OBJ);
                assert(argNode->gtGetOp1()->isContained());
            }
#endif // FEATURE_PUT_STRUCT_ARG_STK
            continue;
        }
#ifdef UNIX_AMD64_ABI
        if (argNode->OperGet() == GT_FIELD_LIST)
        {
            assert(argNode->isContained());
            assert(varTypeIsStruct(argNode) || curArgTabEntry->isStruct);

            unsigned regIndex = 0;
            for (GenTreeFieldList::Use& use : argNode->AsFieldList()->Uses())
            {
                const regNumber argReg = curArgTabEntry->GetRegNum(regIndex);
                assert(use.GetNode()->GetRegNum() == argReg);
                regIndex++;
            }
        }
        else
#endif // UNIX_AMD64_ABI
        {
            const regNumber argReg = curArgTabEntry->GetRegNum();
            assert(argNode->GetRegNum() == argReg);
        }
#endif // DEBUG
    }

#ifdef DEBUG
    // Now, count stack args
    // Note that these need to be computed into a register, but then
    // they're just stored to the stack - so the reg doesn't
    // need to remain live until the call.  In fact, it must not
    // because the code generator doesn't actually consider it live,
    // so it can't be spilled.

    for (GenTreeCall::Use& use : call->Args())
    {
        GenTree* arg = use.GetNode();
        if (!(arg->gtFlags & GTF_LATE_ARG) && !arg)
        {
            if (arg->IsValue() && !arg->isContained())
            {
                assert(arg->IsUnusedValue());
            }
        }
    }
#endif // DEBUG

    // set reg requirements on call target represented as control sequence.
    if (ctrlExpr != nullptr)
    {
        regMaskTP ctrlExprCandidates = RBM_NONE;

        // In case of fast tail implemented as jmp, make sure that gtControlExpr is
        // computed into a register.
        if (call->IsFastTailCall())
        {
            assert(!ctrlExpr->isContained());
            // Fast tail call - make sure that call target is always computed in RAX
            // so that epilog sequence can generate "jmp rax" to achieve fast tail call.
            ctrlExprCandidates = RBM_RAX;
        }
#ifdef TARGET_X86
        else if (call->IsVirtualStub() && (call->gtCallType == CT_INDIRECT))
        {
            // On x86, we need to generate a very specific pattern for indirect VSD calls:
            //
            //    3-byte nop
            //    call dword ptr [eax]
            //
            // Where EAX is also used as an argument to the stub dispatch helper. Make
            // sure that the call target address is computed into EAX in this case.
            assert(ctrlExpr->isIndir() && ctrlExpr->isContained());
            ctrlExprCandidates = RBM_VIRTUAL_STUB_TARGET;
        }
#endif // TARGET_X86

#if FEATURE_VARARG
        // If it is a fast tail call, it is already preferenced to use RAX.
        // Therefore, no need set src candidates on call tgt again.
        if (call->IsVarargs() && callHasFloatRegArgs && !call->IsFastTailCall())
        {
            // Don't assign the call target to any of the argument registers because
            // we will use them to also pass floating point arguments as required
            // by Amd64 ABI.
            ctrlExprCandidates = allRegs(TYP_INT) & ~(RBM_ARG_REGS);
        }
#endif // !FEATURE_VARARG
        srcCount += BuildOperandUses(ctrlExpr, ctrlExprCandidates);
    }

    buildInternalRegisterUses();

    // Now generate defs and kills.
    regMaskTP killMask = getKillSetForCall(call);
    BuildDefsWithKills(call, dstCount, dstCandidates, killMask);
    return srcCount;
}

//------------------------------------------------------------------------
// BuildBlockStore: Build the RefPositions for a block store node.
//
// Arguments:
//    blkNode - The block store node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildBlockStore(GenTreeBlk* blkNode)
{
    assert(false);
    return 0;
}

#ifdef FEATURE_PUT_STRUCT_ARG_STK
//------------------------------------------------------------------------
// BuildPutArgStk: Set the NodeInfo for a GT_PUTARG_STK.
//
// Arguments:
//    tree      - The node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildPutArgStk(GenTreePutArgStk* putArgStk)
{
    assert(false);
    return 0;
}
#endif // FEATURE_PUT_STRUCT_ARG_STK

//------------------------------------------------------------------------
// BuildLclHeap: Set the NodeInfo for a GT_LCLHEAP.
//
// Arguments:
//    tree      - The node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildLclHeap(GenTree* tree)
{
    int srcCount = 1;

    // Need a variable number of temp regs (see genLclHeap() in codegenamd64.cpp):
    // Here '-' means don't care.
    //
    //     Size?                    Init Memory?         # temp regs
    //      0                            -                  0 (returns 0)
    //      const and <=6 reg words      -                  0 (pushes '0')
    //      const and >6 reg words       Yes                0 (pushes '0')
    //      const and <PageSize          No                 0 (amd64) 1 (x86)
    //                                                        (x86:tmpReg for sutracting from esp)
    //      const and >=PageSize         No                 2 (regCnt and tmpReg for subtracing from sp)
    //      Non-const                    Yes                0 (regCnt=targetReg and pushes '0')
    //      Non-const                    No                 2 (regCnt and tmpReg for subtracting from sp)
    //
    // Note: Here we don't need internal register to be different from targetReg.
    // Rather, require it to be different from operand's reg.

    GenTree* size = tree->gtGetOp1();
    if (size->IsCnsIntOrI())
    {
        assert(size->isContained());
        srcCount       = 0;
        size_t sizeVal = size->AsIntCon()->gtIconVal;

        if (sizeVal == 0)
        {
            buildInternalIntRegisterDefForNode(tree);
        }
        else
        {
            // Compute the amount of memory to properly STACK_ALIGN.
            // Note: The Gentree node is not updated here as it is cheap to recompute stack aligned size.
            // This should also help in debugging as we can examine the original size specified with localloc.
            sizeVal = AlignUp(sizeVal, STACK_ALIGN);

            // For small allocations up to 6 pointer sized words (i.e. 48 bytes of localloc)
            // we will generate 'push 0'.
            assert((sizeVal % REGSIZE_BYTES) == 0);
            size_t cntRegSizedWords = sizeVal / REGSIZE_BYTES;
            if (cntRegSizedWords > 6)
            {
                if (!compiler->info.compInitMem)
                {
                    // No need to initialize allocated stack space.
                    if (sizeVal < compiler->eeGetPageSize())
                    {
#ifdef TARGET_X86
                        // x86 needs a register here to avoid generating "sub" on ESP.
                        buildInternalIntRegisterDefForNode(tree);
#endif
                    }
                    else
                    {
                        // We need two registers: regCnt and RegTmp
                        buildInternalIntRegisterDefForNode(tree);
                        buildInternalIntRegisterDefForNode(tree);
                    }
                }
            }
        }
    }
    else
    {
        if (!compiler->info.compInitMem)
        {
            buildInternalIntRegisterDefForNode(tree);
            buildInternalIntRegisterDefForNode(tree);
        }
        BuildUse(size);
    }
    buildInternalRegisterUses();
    BuildDef(tree);
    return srcCount;
}

//------------------------------------------------------------------------
// BuildModDiv: Set the NodeInfo for GT_MOD/GT_DIV/GT_UMOD/GT_UDIV.
//
// Arguments:
//    tree      - The node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildModDiv(GenTree* tree)
{
    GenTree*     op1           = tree->gtGetOp1();
    GenTree*     op2           = tree->gtGetOp2();
    regMaskTP    dstCandidates = RBM_NONE;
    RefPosition* internalDef   = nullptr;
    int          srcCount      = 0;

    if (varTypeIsFloating(tree->TypeGet()))
    {
        return BuildSimple(tree);
    }

    // Amd64 Div/Idiv instruction:
    //    Dividend in RAX:RDX  and computes
    //    Quotient in RAX, Remainder in RDX

    if (tree->OperGet() == GT_MOD || tree->OperGet() == GT_UMOD)
    {
        // We are interested in just the remainder.
        // RAX is used as a trashable register during computation of remainder.
        dstCandidates = RBM_RDX;
    }
    else
    {
        // We are interested in just the quotient.
        // RDX gets used as trashable register during computation of quotient
        dstCandidates = RBM_RAX;
    }

#ifdef TARGET_X86
    if (op1->OperGet() == GT_LONG)
    {
        assert(op1->isContained());

        // To avoid reg move would like to have op1's low part in RAX and high part in RDX.
        GenTree* loVal = op1->gtGetOp1();
        GenTree* hiVal = op1->gtGetOp2();
        assert(!loVal->isContained() && !hiVal->isContained());

        assert(op2->IsCnsIntOrI());
        assert(tree->OperGet() == GT_UMOD);

        // This situation also requires an internal register.
        buildInternalIntRegisterDefForNode(tree);

        BuildUse(loVal, RBM_EAX);
        BuildUse(hiVal, RBM_EDX);
        srcCount = 2;
    }
    else
#endif
    {
        // If possible would like to have op1 in RAX to avoid a register move.
        RefPosition* op1Use = BuildUse(op1, RBM_EAX);
        tgtPrefUse          = op1Use;
        srcCount            = 1;
    }

    srcCount += BuildDelayFreeUses(op2, op1, allRegs(TYP_INT) & ~(RBM_RAX | RBM_RDX));

    buildInternalRegisterUses();

    regMaskTP killMask = getKillSetForModDiv(tree->AsOp());
    BuildDefsWithKills(tree, 1, dstCandidates, killMask);
    return srcCount;
}

//------------------------------------------------------------------------
// BuildIntrinsic: Set the NodeInfo for a GT_INTRINSIC.
//
// Arguments:
//    tree      - The node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildIntrinsic(GenTree* tree)
{
    // Both operand and its result must be of floating point type.
    GenTree* op1 = tree->gtGetOp1();
    assert(varTypeIsFloating(op1));
    assert(op1->TypeGet() == tree->TypeGet());
    RefPosition* internalFloatDef = nullptr;

    switch (tree->AsIntrinsic()->gtIntrinsicName)
    {
        case NI_System_Math_Abs:
            // Abs(float x) = x & 0x7fffffff
            // Abs(double x) = x & 0x7ffffff ffffffff

            // In case of Abs we need an internal register to hold mask.

            // TODO-XArch-CQ: avoid using an internal register for the mask.
            // Andps or andpd both will operate on 128-bit operands.
            // The data section constant to hold the mask is a 64-bit size.
            // Therefore, we need both the operand and mask to be in
            // xmm register. When we add support in emitter to emit 128-bit
            // data constants and instructions that operate on 128-bit
            // memory operands we can avoid the need for an internal register.
            internalFloatDef = buildInternalFloatRegisterDefForNode(tree, internalFloatRegCandidates());
            break;

#ifdef TARGET_X86
        case NI_System_Math_Cos:
        case NI_System_Math_Sin:
            NYI_X86("Math intrinsics Cos and Sin");
            break;
#endif // TARGET_X86

        case NI_System_Math_Sqrt:
        case NI_System_Math_Round:
        case NI_System_Math_Ceiling:
        case NI_System_Math_Floor:
            break;

        default:
            // Right now only Sqrt/Abs are treated as math intrinsics
            noway_assert(!"Unsupported math intrinsic");
            unreached();
            break;
    }
    assert(tree->gtGetOp2IfPresent() == nullptr);
    int srcCount;
    if (op1->isContained())
    {
        srcCount = BuildOperandUses(op1);
    }
    else
    {
        tgtPrefUse = BuildUse(op1);
        srcCount   = 1;
    }
    if (internalFloatDef != nullptr)
    {
        buildInternalRegisterUses();
    }
    BuildDef(tree);
    return srcCount;
}

#ifdef FEATURE_SIMD
//------------------------------------------------------------------------
// BuildSIMD: Set the NodeInfo for a GT_SIMD tree.
//
// Arguments:
//    tree       - The GT_SIMD node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildSIMD(GenTreeSIMD* simdTree)
{
    // All intrinsics have a dstCount of 1
    assert(simdTree->IsValue());

    bool      buildUses     = true;
    regMaskTP dstCandidates = RBM_NONE;

    if (simdTree->isContained())
    {
        // Only SIMDIntrinsicInit can be contained
        assert(simdTree->gtSIMDIntrinsicID == SIMDIntrinsicInit);
    }
    SetContainsAVXFlags(simdTree->gtSIMDSize);
    GenTree* op1      = simdTree->gtGetOp1();
    GenTree* op2      = simdTree->gtGetOp2();
    int      srcCount = 0;

    switch (simdTree->gtSIMDIntrinsicID)
    {
        case SIMDIntrinsicInit:
        {
            // This sets all fields of a SIMD struct to the given value.
            // Mark op1 as contained if it is either zero or int constant of all 1's,
            // or a float constant with 16 or 32 byte simdType (AVX case)
            //
            // Note that for small int base types, the initVal has been constructed so that
            // we can use the full int value.
            CLANG_FORMAT_COMMENT_ANCHOR;

#if !defined(TARGET_64BIT)
            if (op1->OperGet() == GT_LONG)
            {
                assert(op1->isContained());
                GenTree* op1lo = op1->gtGetOp1();
                GenTree* op1hi = op1->gtGetOp2();

                if (op1lo->isContained())
                {
                    srcCount = 0;
                    assert(op1hi->isContained());
                    assert((op1lo->IsIntegralConst(0) && op1hi->IsIntegralConst(0)) ||
                           (op1lo->IsIntegralConst(-1) && op1hi->IsIntegralConst(-1)));
                }
                else
                {
                    srcCount = 2;
                    buildInternalFloatRegisterDefForNode(simdTree);
                    setInternalRegsDelayFree = true;
                }

                if (srcCount == 2)
                {
                    BuildUse(op1lo, RBM_EAX);
                    BuildUse(op1hi, RBM_EDX);
                }
                buildUses = false;
            }
#endif // !defined(TARGET_64BIT)
        }
        break;

        case SIMDIntrinsicInitN:
        {
            var_types baseType = simdTree->gtSIMDBaseType;
            srcCount           = (short)(simdTree->gtSIMDSize / genTypeSize(baseType));
            // Need an internal register to stitch together all the values into a single vector in a SIMD reg.
            buildInternalFloatRegisterDefForNode(simdTree);
            int initCount = 0;
            for (GenTree* list = op1; list != nullptr; list = list->gtGetOp2())
            {
                assert(list->OperGet() == GT_LIST);
                GenTree* listItem = list->gtGetOp1();
                assert(listItem->TypeGet() == baseType);
                assert(!listItem->isContained());
                BuildUse(listItem);
                initCount++;
            }
            assert(initCount == srcCount);
            buildUses = false;
        }
        break;

        case SIMDIntrinsicInitArray:
            // We have an array and an index, which may be contained.
            break;

        case SIMDIntrinsicSub:
        case SIMDIntrinsicBitwiseAnd:
        case SIMDIntrinsicBitwiseOr:
            break;

        case SIMDIntrinsicEqual:
            break;

        case SIMDIntrinsicGetItem:
        {
            // This implements get_Item method. The sources are:
            //  - the source SIMD struct
            //  - index (which element to get)
            // The result is baseType of SIMD struct.
            // op1 may be a contained memory op, but if so we will consume its address.
            // op2 may be a contained constant.
            op1 = simdTree->gtGetOp1();
            op2 = simdTree->gtGetOp2();

            if (!op1->isContained())
            {
                // If the index is not a constant, we will use the SIMD temp location to store the vector.
                // Otherwise, if the baseType is floating point, the targetReg will be a xmm reg and we
                // can use that in the process of extracting the element.
                //
                // If the index is a constant and base type is a small int we can use pextrw, but on AVX
                // we will need a temp if are indexing into the upper half of the AVX register.
                // In all other cases with constant index, we need a temp xmm register to extract the
                // element if index is other than zero.

                if (!op2->IsCnsIntOrI())
                {
                    (void)compiler->getSIMDInitTempVarNum();
                }
                else if (!varTypeIsFloating(simdTree->gtSIMDBaseType))
                {
                    bool needFloatTemp;
                    if (varTypeIsSmallInt(simdTree->gtSIMDBaseType) &&
                        (compiler->getSIMDSupportLevel() == SIMD_AVX2_Supported))
                    {
                        int byteShiftCnt = (int)op2->AsIntCon()->gtIconVal * genTypeSize(simdTree->gtSIMDBaseType);
                        needFloatTemp    = (byteShiftCnt >= 16);
                    }
                    else
                    {
                        needFloatTemp = !op2->IsIntegralConst(0);
                    }

                    if (needFloatTemp)
                    {
                        buildInternalFloatRegisterDefForNode(simdTree);
                    }
                }
#ifdef TARGET_X86
                // This logic is duplicated from genSIMDIntrinsicGetItem().
                // When we generate code for a SIMDIntrinsicGetItem, under certain circumstances we need to
                // generate a movzx/movsx. On x86, these require byteable registers. So figure out which
                // cases will require this, so the non-byteable registers can be excluded.

                var_types baseType = simdTree->gtSIMDBaseType;
                if (op2->IsCnsIntOrI() && varTypeIsSmallInt(baseType))
                {
                    bool     ZeroOrSignExtnReqd = true;
                    unsigned baseSize           = genTypeSize(baseType);
                    if (baseSize == 1)
                    {
                        if ((op2->AsIntCon()->gtIconVal % 2) == 1)
                        {
                            ZeroOrSignExtnReqd = (baseType == TYP_BYTE);
                        }
                    }
                    else
                    {
                        assert(baseSize == 2);
                        ZeroOrSignExtnReqd = (baseType == TYP_SHORT);
                    }
                    if (ZeroOrSignExtnReqd)
                    {
                        dstCandidates = allByteRegs();
                    }
                }
#endif // TARGET_X86
            }
        }
        break;

        case SIMDIntrinsicSetX:
        case SIMDIntrinsicSetY:
        case SIMDIntrinsicSetZ:
        case SIMDIntrinsicSetW:
            // We need an internal integer register for SSE2 codegen
            if (compiler->getSIMDSupportLevel() == SIMD_SSE2_Supported)
            {
                buildInternalIntRegisterDefForNode(simdTree);
            }

            break;

        case SIMDIntrinsicCast:
            break;

        case SIMDIntrinsicConvertToSingle:
            if (simdTree->gtSIMDBaseType == TYP_UINT)
            {
                // We need an internal register different from targetReg.
                setInternalRegsDelayFree = true;
                buildInternalFloatRegisterDefForNode(simdTree);
                buildInternalFloatRegisterDefForNode(simdTree);
                // We also need an integer register.
                buildInternalIntRegisterDefForNode(simdTree);
            }
            break;

        case SIMDIntrinsicConvertToInt32:
            break;

        case SIMDIntrinsicWidenLo:
        case SIMDIntrinsicWidenHi:
            if (varTypeIsIntegral(simdTree->gtSIMDBaseType))
            {
                // We need an internal register different from targetReg.
                setInternalRegsDelayFree = true;
                buildInternalFloatRegisterDefForNode(simdTree);
            }
            break;

        case SIMDIntrinsicConvertToInt64:
            // We need an internal register different from targetReg.
            setInternalRegsDelayFree = true;
            buildInternalFloatRegisterDefForNode(simdTree);
            if (compiler->getSIMDSupportLevel() == SIMD_AVX2_Supported)
            {
                buildInternalFloatRegisterDefForNode(simdTree);
            }
            // We also need an integer register.
            buildInternalIntRegisterDefForNode(simdTree);
            break;

        case SIMDIntrinsicConvertToDouble:
            // We need an internal register different from targetReg.
            setInternalRegsDelayFree = true;
            buildInternalFloatRegisterDefForNode(simdTree);
#ifdef TARGET_X86
            if (simdTree->gtSIMDBaseType == TYP_LONG)
            {
                buildInternalFloatRegisterDefForNode(simdTree);
                buildInternalFloatRegisterDefForNode(simdTree);
            }
            else
#endif
                if ((compiler->getSIMDSupportLevel() == SIMD_AVX2_Supported) || (simdTree->gtSIMDBaseType == TYP_ULONG))
            {
                buildInternalFloatRegisterDefForNode(simdTree);
            }
            // We also need an integer register.
            buildInternalIntRegisterDefForNode(simdTree);
            break;

        case SIMDIntrinsicNarrow:
            // We need an internal register different from targetReg.
            setInternalRegsDelayFree = true;
            buildInternalFloatRegisterDefForNode(simdTree);
            if ((compiler->getSIMDSupportLevel() == SIMD_AVX2_Supported) && (simdTree->gtSIMDBaseType != TYP_DOUBLE))
            {
                buildInternalFloatRegisterDefForNode(simdTree);
            }
            break;

        case SIMDIntrinsicShuffleSSE2:
            // Second operand is an integer constant and marked as contained.
            assert(simdTree->gtGetOp2()->isContainedIntOrIImmed());
            break;

        case SIMDIntrinsicGetX:
        case SIMDIntrinsicGetY:
        case SIMDIntrinsicGetZ:
        case SIMDIntrinsicGetW:
            assert(!"Get intrinsics should not be seen during Lowering.");
            unreached();

        default:
            noway_assert(!"Unimplemented SIMD node type.");
            unreached();
    }
    if (buildUses)
    {
        assert(!op1->OperIs(GT_LIST));
        assert(srcCount == 0);
        // This is overly conservative, but is here for zero diffs.
        srcCount = BuildRMWUses(simdTree);
    }
    buildInternalRegisterUses();
    BuildDef(simdTree, dstCandidates);
    return srcCount;
}
#endif // FEATURE_SIMD

#ifdef FEATURE_HW_INTRINSICS
//------------------------------------------------------------------------
// BuildHWIntrinsic: Set the NodeInfo for a GT_HWINTRINSIC tree.
//
// Arguments:
//    tree       - The GT_HWINTRINSIC node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildHWIntrinsic(GenTreeHWIntrinsic* intrinsicTree)
{
    NamedIntrinsic         intrinsicId = intrinsicTree->gtHWIntrinsicId;
    var_types              baseType    = intrinsicTree->gtSIMDBaseType;
    CORINFO_InstructionSet isa         = HWIntrinsicInfo::lookupIsa(intrinsicId);
    HWIntrinsicCategory    category    = HWIntrinsicInfo::lookupCategory(intrinsicId);
    int                    numArgs     = HWIntrinsicInfo::lookupNumArgs(intrinsicTree);

    // Set the AVX Flags if this instruction may use VEX encoding for SIMD operations.
    // Note that this may be true even if the ISA is not AVX (e.g. for platform-agnostic intrinsics
    // or non-AVX intrinsics that will use VEX encoding if it is available on the target).
    if (intrinsicTree->isSIMD())
    {
        SetContainsAVXFlags(intrinsicTree->gtSIMDSize);
    }

    GenTree* op1    = intrinsicTree->gtGetOp1();
    GenTree* op2    = intrinsicTree->gtGetOp2();
    GenTree* op3    = nullptr;
    GenTree* lastOp = nullptr;

    int srcCount = 0;
    int dstCount = intrinsicTree->IsValue() ? 1 : 0;

    regMaskTP dstCandidates = RBM_NONE;

    if (op1 == nullptr)
    {
        assert(op2 == nullptr);
        assert(numArgs == 0);
    }
    else
    {
        if (op1->OperIsList())
        {
            assert(op2 == nullptr);
            assert(numArgs >= 3);

            GenTreeArgList* argList = op1->AsArgList();

            op1     = argList->Current();
            argList = argList->Rest();

            op2     = argList->Current();
            argList = argList->Rest();

            op3 = argList->Current();

            while (argList->Rest() != nullptr)
            {
                argList = argList->Rest();
            }

            lastOp  = argList->Current();
            argList = argList->Rest();

            assert(argList == nullptr);
        }
        else if (op2 != nullptr)
        {
            assert(numArgs == 2);
            lastOp = op2;
        }
        else
        {
            assert(numArgs == 1);
            lastOp = op1;
        }

        assert(lastOp != nullptr);

        bool buildUses = true;

        if ((category == HW_Category_IMM) && !HWIntrinsicInfo::NoJmpTableImm(intrinsicId))
        {
            if (HWIntrinsicInfo::isImmOp(intrinsicId, lastOp) && !lastOp->isContainedIntOrIImmed())
            {
                assert(!lastOp->IsCnsIntOrI());

                // We need two extra reg when lastOp isn't a constant so
                // the offset into the jump table for the fallback path
                // can be computed.
                buildInternalIntRegisterDefForNode(intrinsicTree);
                buildInternalIntRegisterDefForNode(intrinsicTree);
            }
        }

        // Determine whether this is an RMW operation where op2+ must be marked delayFree so that it
        // is not allocated the same register as the target.
        bool isRMW = intrinsicTree->isRMWHWIntrinsic(compiler);

        // Create internal temps, and handle any other special requirements.
        // Note that the default case for building uses will handle the RMW flag, but if the uses
        // are built in the individual cases, buildUses is set to false, and any RMW handling (delayFree)
        // must be handled within the case.
        switch (intrinsicId)
        {
            case NI_Vector128_CreateScalarUnsafe:
            case NI_Vector128_ToScalar:
            case NI_Vector256_CreateScalarUnsafe:
            case NI_Vector256_ToScalar:
            {
                assert(numArgs == 1);

                if (varTypeIsFloating(baseType))
                {
                    if (op1->isContained())
                    {
                        srcCount += BuildOperandUses(op1);
                    }
                    else
                    {
                        // We will either be in memory and need to be moved
                        // into a register of the appropriate size or we
                        // are already in an XMM/YMM register and can stay
                        // where we are.

                        tgtPrefUse = BuildUse(op1);
                        srcCount += 1;
                    }

                    buildUses = false;
                }
                break;
            }

            case NI_Vector128_ToVector256:
            case NI_Vector128_ToVector256Unsafe:
            case NI_Vector256_GetLower:
            {
                assert(numArgs == 1);

                if (op1->isContained())
                {
                    srcCount += BuildOperandUses(op1);
                }
                else
                {
                    // We will either be in memory and need to be moved
                    // into a register of the appropriate size or we
                    // are already in an XMM/YMM register and can stay
                    // where we are.

                    tgtPrefUse = BuildUse(op1);
                    srcCount += 1;
                }

                buildUses = false;
                break;
            }

            case NI_SSE2_MaskMove:
            {
                assert(numArgs == 3);
                assert(!isRMW);

                // MaskMove hardcodes the destination (op3) in DI/EDI/RDI
                srcCount += BuildOperandUses(op1);
                srcCount += BuildOperandUses(op2);
                srcCount += BuildOperandUses(op3, RBM_EDI);

                buildUses = false;
                break;
            }

            case NI_SSE41_BlendVariable:
            {
                assert(numArgs == 3);

                if (!compiler->canUseVexEncoding())
                {
                    assert(isRMW);

                    // SSE4.1 blendv* hardcode the mask vector (op3) in XMM0
                    tgtPrefUse = BuildUse(op1);

                    srcCount += 1;
                    srcCount += op2->isContained() ? BuildOperandUses(op2) : BuildDelayFreeUses(op2, op1);
                    srcCount += BuildDelayFreeUses(op3, op1, RBM_XMM0);

                    buildUses = false;
                }
                break;
            }

            case NI_SSE41_Extract:
            {
                if (baseType == TYP_FLOAT)
                {
                    buildInternalIntRegisterDefForNode(intrinsicTree);
                }
#ifdef TARGET_X86
                else if (varTypeIsByte(baseType))
                {
                    dstCandidates = allByteRegs();
                }
#endif
                break;
            }

#ifdef TARGET_X86
            case NI_SSE42_Crc32:
            case NI_SSE42_X64_Crc32:
            {
                // TODO-XArch-Cleanup: Currently we use the BaseType to bring the type of the second argument
                // to the code generator. We may want to encode the overload info in another way.

                assert(numArgs == 2);
                assert(isRMW);

                // CRC32 may operate over "byte" but on x86 only RBM_BYTE_REGS can be used as byte registers.
                tgtPrefUse = BuildUse(op1);

                srcCount += 1;
                srcCount += BuildDelayFreeUses(op2, op1, varTypeIsByte(baseType) ? allByteRegs() : RBM_NONE);

                buildUses = false;
                break;
            }
#endif // TARGET_X86

            case NI_BMI2_MultiplyNoFlags:
            case NI_BMI2_X64_MultiplyNoFlags:
            {
                assert(numArgs == 2 || numArgs == 3);
                srcCount += BuildOperandUses(op1, RBM_EDX);
                srcCount += BuildOperandUses(op2);
                if (numArgs == 3)
                {
                    // op3 reg should be different from target reg to
                    // store the lower half result after executing the instruction
                    srcCount += BuildDelayFreeUses(op3, op1);
                    // Need a internal register different from the dst to take the lower half result
                    buildInternalIntRegisterDefForNode(intrinsicTree);
                    setInternalRegsDelayFree = true;
                }
                buildUses = false;
                break;
            }

            case NI_FMA_MultiplyAdd:
            case NI_FMA_MultiplyAddNegated:
            case NI_FMA_MultiplyAddNegatedScalar:
            case NI_FMA_MultiplyAddScalar:
            case NI_FMA_MultiplyAddSubtract:
            case NI_FMA_MultiplySubtract:
            case NI_FMA_MultiplySubtractAdd:
            case NI_FMA_MultiplySubtractNegated:
            case NI_FMA_MultiplySubtractNegatedScalar:
            case NI_FMA_MultiplySubtractScalar:
            {
                assert(numArgs == 3);
                assert(isRMW);

                const bool copiesUpperBits = HWIntrinsicInfo::CopiesUpperBits(intrinsicId);

                // Intrinsics with CopyUpperBits semantics cannot have op1 be contained
                assert(!copiesUpperBits || !op1->isContained());

                if (op2->isContained())
                {
                    // 132 form: op1 = (op1 * op3) + [op2]

                    tgtPrefUse = BuildUse(op1);

                    srcCount += 1;
                    srcCount += BuildOperandUses(op2);
                    srcCount += BuildDelayFreeUses(op3, op1);
                }
                else if (op1->isContained())
                {
                    // 231 form: op3 = (op2 * op3) + [op1]

                    tgtPrefUse = BuildUse(op3);

                    srcCount += BuildOperandUses(op1);
                    srcCount += BuildDelayFreeUses(op2, op1);
                    srcCount += 1;
                }
                else
                {
                    // 213 form: op1 = (op2 * op1) + [op3]

                    tgtPrefUse = BuildUse(op1);
                    srcCount += 1;

                    if (copiesUpperBits)
                    {
                        srcCount += BuildDelayFreeUses(op2, op1);
                    }
                    else
                    {
                        tgtPrefUse2 = BuildUse(op2);
                        srcCount += 1;
                    }

                    srcCount += op3->isContained() ? BuildOperandUses(op3) : BuildDelayFreeUses(op3, op1);
                }

                buildUses = false;
                break;
            }

            case NI_AVX2_GatherVector128:
            case NI_AVX2_GatherVector256:
            {
                assert(numArgs == 3);
                assert(!isRMW);

                // Any pair of the index, mask, or destination registers should be different
                srcCount += BuildOperandUses(op1);
                srcCount += BuildDelayFreeUses(op2, op1);

                // op3 should always be contained
                assert(op3->isContained());

                // get a tmp register for mask that will be cleared by gather instructions
                buildInternalFloatRegisterDefForNode(intrinsicTree, allSIMDRegs());
                setInternalRegsDelayFree = true;

                buildUses = false;
                break;
            }

            case NI_AVX2_GatherMaskVector128:
            case NI_AVX2_GatherMaskVector256:
            {
                assert(numArgs == 5);
                assert(!isRMW);
                assert(intrinsicTree->gtGetOp1()->OperIsList());

                GenTreeArgList* argList = intrinsicTree->gtGetOp1()->AsArgList()->Rest()->Rest()->Rest();
                GenTree*        op4     = argList->Current();

                // Any pair of the index, mask, or destination registers should be different
                srcCount += BuildOperandUses(op1);
                srcCount += BuildDelayFreeUses(op2);
                srcCount += BuildDelayFreeUses(op3);
                srcCount += BuildDelayFreeUses(op4);

                // op5 should always be contained
                assert(argList->Rest()->Current()->isContained());

                // get a tmp register for mask that will be cleared by gather instructions
                buildInternalFloatRegisterDefForNode(intrinsicTree, allSIMDRegs());
                setInternalRegsDelayFree = true;

                buildUses = false;
                break;
            }

            default:
            {
                assert((intrinsicId > NI_HW_INTRINSIC_START) && (intrinsicId < NI_HW_INTRINSIC_END));
                break;
            }
        }

        if (buildUses)
        {
            assert((numArgs > 0) && (numArgs < 4));

            if (intrinsicTree->OperIsMemoryLoadOrStore())
            {
                srcCount += BuildAddrUses(op1);
            }
            else if (isRMW && !op1->isContained())
            {
                tgtPrefUse = BuildUse(op1);
                srcCount += 1;
            }
            else
            {
                srcCount += BuildOperandUses(op1);
            }

            if (op2 != nullptr)
            {
                if (op2->OperIs(GT_HWINTRINSIC) && op2->AsHWIntrinsic()->OperIsMemoryLoad() && op2->isContained())
                {
                    srcCount += BuildAddrUses(op2->gtGetOp1());
                }
                else if (isRMW)
                {
                    if (!op2->isContained() && HWIntrinsicInfo::IsCommutative(intrinsicId))
                    {
                        // When op2 is not contained and we are commutative, we can set op2
                        // to also be a tgtPrefUse. Codegen will then swap the operands.

                        tgtPrefUse2 = BuildUse(op2);
                        srcCount += 1;
                    }
                    else if (!op2->isContained() || varTypeIsArithmetic(intrinsicTree->TypeGet()))
                    {
                        // When op2 is not contained or if we are producing a scalar value
                        // we need to mark it as delay free because the operand and target
                        // exist in the same register set.

                        srcCount += BuildDelayFreeUses(op2);
                    }
                    else
                    {
                        // When op2 is contained and we are not producing a scalar value we
                        // have no concerns of overwriting op2 because they exist in different
                        // register sets.

                        srcCount += BuildOperandUses(op2);
                    }
                }
                else
                {
                    srcCount += BuildOperandUses(op2);
                }

                if (op3 != nullptr)
                {
                    srcCount += isRMW ? BuildDelayFreeUses(op3) : BuildOperandUses(op3);
                }
            }
        }

        buildInternalRegisterUses();
    }

    if (dstCount == 1)
    {
        BuildDef(intrinsicTree, dstCandidates);
    }
    else
    {
        assert(dstCount == 0);
    }

    return srcCount;
}
#endif

//------------------------------------------------------------------------
// BuildCast: Set the NodeInfo for a GT_CAST.
//
// Arguments:
//    cast - The GT_CAST node
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildCast(GenTreeCast* cast)
{
    GenTree* src = cast->gtGetOp1();

    const var_types srcType  = genActualType(src->TypeGet());
    const var_types castType = cast->gtCastType;

    regMaskTP candidates = RBM_NONE;
#ifdef TARGET_X86
    if (varTypeIsByte(castType))
    {
        candidates = allByteRegs();
    }

    assert(!varTypeIsLong(srcType) || (src->OperIs(GT_LONG) && src->isContained()));
#else
    // Overflow checking cast from TYP_(U)LONG to TYP_UINT requires a temporary
    // register to extract the upper 32 bits of the 64 bit source register.
    if (cast->gtOverflow() && varTypeIsLong(srcType) && (castType == TYP_UINT))
    {
        // Here we don't need internal register to be different from targetReg,
        // rather require it to be different from operand's reg.
        buildInternalIntRegisterDefForNode(cast);
    }
#endif

    int srcCount = BuildOperandUses(src, candidates);
    buildInternalRegisterUses();
    BuildDef(cast, candidates);
    return srcCount;
}

//-----------------------------------------------------------------------------------------
// BuildIndir: Specify register requirements for address expression of an indirection operation.
//
// Arguments:
//    indirTree    -   GT_IND or GT_STOREIND gentree node
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildIndir(GenTreeIndir* indirTree)
{
    assert(false);
    return 0;
}

//------------------------------------------------------------------------
// BuildMul: Set the NodeInfo for a multiply.
//
// Arguments:
//    tree      - The node of interest
//
// Return Value:
//    The number of sources consumed by this node.
//
int LinearScan::BuildMul(GenTree* tree)
{
    assert(tree->OperIsMul());
    GenTree* op1 = tree->gtGetOp1();
    GenTree* op2 = tree->gtGetOp2();

    // Only non-floating point mul has special requirements
    if (varTypeIsFloating(tree->TypeGet()))
    {
        return BuildSimple(tree);
    }

    int       srcCount      = BuildBinaryUses(tree->AsOp());
    int       dstCount      = 1;
    regMaskTP dstCandidates = RBM_NONE;

    bool isUnsignedMultiply    = ((tree->gtFlags & GTF_UNSIGNED) != 0);
    bool requiresOverflowCheck = tree->gtOverflowEx();

    // There are three forms of x86 multiply:
    // one-op form:     RDX:RAX = RAX * r/m
    // two-op form:     reg *= r/m
    // three-op form:   reg = r/m * imm

    // This special widening 32x32->64 MUL is not used on x64
    CLANG_FORMAT_COMMENT_ANCHOR;
#if defined(TARGET_X86)
    if (tree->OperGet() != GT_MUL_LONG)
#endif
    {
        assert((tree->gtFlags & GTF_MUL_64RSLT) == 0);
    }

    // We do use the widening multiply to implement
    // the overflow checking for unsigned multiply
    //
    if (isUnsignedMultiply && requiresOverflowCheck)
    {
        // The only encoding provided is RDX:RAX = RAX * rm
        //
        // Here we set RAX as the only destination candidate
        // In LSRA we set the kill set for this operation to RBM_RAX|RBM_RDX
        //
        dstCandidates = RBM_RAX;
    }
    else if (tree->OperGet() == GT_MULHI)
    {
        // Have to use the encoding:RDX:RAX = RAX * rm. Since we only care about the
        // upper 32 bits of the result set the destination candidate to REG_RDX.
        dstCandidates = RBM_RDX;
    }
#if defined(TARGET_X86)
    else if (tree->OperGet() == GT_MUL_LONG)
    {
        // have to use the encoding:RDX:RAX = RAX * rm
        dstCandidates = RBM_RAX | RBM_RDX;
        dstCount      = 2;
    }
#endif
    GenTree* containedMemOp = nullptr;
    if (op1->isContained() && !op1->IsCnsIntOrI())
    {
        assert(!op2->isContained() || op2->IsCnsIntOrI());
        containedMemOp = op1;
    }
    else if (op2->isContained() && !op2->IsCnsIntOrI())
    {
        containedMemOp = op2;
    }
    regMaskTP killMask = getKillSetForMul(tree->AsOp());
    BuildDefsWithKills(tree, dstCount, dstCandidates, killMask);
    return srcCount;
}

//------------------------------------------------------------------------------
// SetContainsAVXFlags: Set ContainsAVX flag when it is floating type, set
// Contains256bitAVX flag when SIMD vector size is 32 bytes
//
// Arguments:
//    isFloatingPointType   - true if it is floating point type
//    sizeOfSIMDVector      - SIMD Vector size
//
void LinearScan::SetContainsAVXFlags(unsigned sizeOfSIMDVector /* = 0*/)
{
    assert(false);
}

#endif // defined(TARGET_WASM32) || defined(TARGET_WASM64)
