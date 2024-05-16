// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*****************************************************************************/
#ifndef _LLVM_H_
#define _LLVM_H_
#undef __PLACEMENT_NEW_INLINE

#include "alloc.h"
#include "jitpch.h"
#include "sideeffects.h"
#include "jitgcinfo.h"
#include "llvmtypes.h"
#include <new>

// this breaks StringMap.h
#undef NumItems

// TODO-LLVM-Upstream: figure out how to fix these warnings in LLVM headers.
#pragma warning(push)
#pragma warning(disable : 4146)
#pragma warning(disable : 4242)
#pragma warning(disable : 4244)
#pragma warning(disable : 4267)
#pragma warning(disable : 4459)
#pragma warning(disable : 4702)
#include "llvm/IR/IRBuilder.h"
#include "llvm/IR/DIBuilder.h"
#include "llvm/IR/Function.h"
#include "llvm/IR/Verifier.h"
#include "llvm/IR/IntrinsicsWebAssembly.h"
#pragma warning(pop)

using llvm::LLVMContext;
using llvm::Module;
using llvm::Function;
using llvm::FunctionType;
using llvm::Value;
using llvm::Type;
using llvm::Instruction;
using llvm::AllocaInst;
using llvm::ArrayRef;
using llvm::StringRef;
using llvm::Twine;

using SSAName = Compiler::SSAName;
using structPassingKind = Compiler::structPassingKind;

#define IMAGE_FILE_MACHINE_WASM32             0xFFFF
#define IMAGE_FILE_MACHINE_WASM64             0xFFFE // TODO: appropriate values for this?  Used to check compilation is for intended target

const int TARGET_POINTER_BITS = TARGET_POINTER_SIZE * BITS_PER_BYTE;

// Part of the Jit/EE interface, must be kept in sync with the managed versions in "CorInfoImpl.Llvm.cs".
//
enum class TargetAbiType : uint8_t
{
    Void,
    Int32,
    Int64,
    Float,
    Double
};

enum class CorInfoLlvmEHModel
{
    Cpp, // Landingpad-based LLVM IR; compatible with Itanium ABI.
    Wasm, // WinEH-based LLVM IR; custom WASM EH-based ABI.
    Emulated, // Invoke-free LLVM IR; "unwinding" performed via explicit checks and returns
};

struct CORINFO_LLVM_EH_CLAUSE
{
    CORINFO_EH_CLAUSE_FLAGS Flags;
    unsigned EnclosingIndex;
    mdToken ClauseTypeToken;
    unsigned FilterIndex;
};

enum CorInfoLlvmJitTestKind
{
    CORINFO_JIT_TEST_LSSA = 1
};

struct CORINFO_LLVM_JIT_TEST_INFO
{
    const char* ExpectedLssaAllocation;
};

typedef unsigned CORINFO_LLVM_DEBUG_TYPE_HANDLE;

const CORINFO_LLVM_DEBUG_TYPE_HANDLE NO_DEBUG_TYPE = 0;

struct CORINFO_LLVM_FORWARD_REF_TYPE_DEBUG_INFO;
struct CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO;
struct CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO;
struct CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO;
struct CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO;
struct CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO;
struct CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO;
struct CORINFO_LLVM_METHOD_DEBUG_INFO;
struct CORINFO_LLVM_TYPE_DEBUG_INFO;

struct MallocAllocator
{
    template <typename T>
    T* allocate(size_t count)
    {
        void* p = malloc(count * sizeof(T));
        if (p == nullptr)
        {
            NOMEM();
        }
        return static_cast<T*>(p);
    }

    void deallocate(void* p)
    {
        free(p);
    }
};

template <typename T>
inline ArrayRef<T> AsRef(ArrayStack<T>& stack)
{
    return stack.Empty() ? ArrayRef<T>() : ArrayRef<T>(&stack.BottomRef(0), static_cast<size_t>(stack.Height()));
}

template <typename T>
inline ArrayRef<T> AsRef(const jitstd::vector<T>& vec)
{
    return vec.empty() ? ArrayRef<T>() : ArrayRef<T>(&vec[0], vec.size());
}

enum HelperFuncInfoFlags
{
    HFIF_NONE = 0,
    HFIF_SS_ARG = 1, // The helper has shadow stack arg.
    HFIF_VAR_ARG = 1 << 1, // The helper has a variable number of args and must be treated specially.
    HFIF_NO_RPI_OR_GC = 1 << 2, // The helper will not call (back) into managed code or trigger GC.
    HFIF_THROW_OR_NO_RPI_OR_GC = 1 << 3, // The helper either throws or does not call into managed code / trigger GC.
};

struct HelperFuncInfo
{
    static const int MAX_SIG_ARG_COUNT = 4;

    INDEBUG(unsigned char Func);
    unsigned char SigReturnType;
    unsigned char SigArgTypes[MAX_SIG_ARG_COUNT + 1];
    unsigned char Flags;

    bool IsInitialized() const
    {
        return SigReturnType != CORINFO_TYPE_UNDEF;
    }

    bool HasFlag(HelperFuncInfoFlags flags) const
    {
        return (Flags & flags) == flags;
    }

    CorInfoType GetSigReturnType() const;
    CORINFO_CLASS_HANDLE GetSigReturnClass(Compiler* compiler) const;
    CorInfoType GetSigArgType(size_t index) const;
    CORINFO_CLASS_HANDLE GetSigArgClass(Compiler* compiler, size_t index) const;
    size_t GetSigArgCount(unsigned* callArgCount = nullptr) const;
};

enum class ValueInitOpts
{
    None,
    IncludeImplicitGcUse
};

enum class ValueInitKind
{
    None,
    Param,
    Zero,
    Uninit
};

struct ThrowHelperKey
{
    unsigned ThrowIndex;
    CorInfoHelpFunc HelperFunc;

    static unsigned GetHashCode(ThrowHelperKey key)
    {
        return key.ThrowIndex + static_cast<unsigned>(key.HelperFunc);
    }

    static bool Equals(ThrowHelperKey keyOne, ThrowHelperKey keyTwo)
    {
        return (keyOne.ThrowIndex == keyTwo.ThrowIndex) && (keyOne.HelperFunc == keyTwo.HelperFunc);
    }
};

struct PhiPair
{
    GenTreeLclVar* StoreNode;
    llvm::PHINode* LlvmPhiNode;
};

struct LlvmBlockRange
{
    llvm::BasicBlock* FirstBlock;
    llvm::BasicBlock* LastBlock;
    INDEBUG(unsigned Count = 1);

    LlvmBlockRange(llvm::BasicBlock* llvmBlock) : FirstBlock(llvmBlock), LastBlock(llvmBlock)
    {
    }
};

typedef JitHashTable<unsigned, JitSmallPrimitiveKeyFuncs<unsigned>, llvm::AllocaInst*> AllocaMap;

struct FunctionInfo
{
    Function* LlvmFunction;
    union {
        llvm::AllocaInst** Allocas; // Dense "lclNum -> Alloca*" mapping used for the main function.
        AllocaMap* AllocaMap; // Sparse "lclNum -> Alloca*" mapping used for funclets.
    };
    llvm::BasicBlock* ResumeLlvmBlock;
    llvm::BasicBlock* ExceptionThrownReturnLlvmBlock;
};

struct EHRegionInfo
{
    llvm::BasicBlock* UnwindBlock;
    Value* CatchArgValue;
};

class SingleThreadedCompilationContext
{
public:
    LLVMContext Context;
    Module Module;
    JitHashTable<CORINFO_CLASS_HANDLE, JitPtrKeyFuncs<CORINFO_CLASS_STRUCT_>, Type*, MallocAllocator> LlvmStructTypesMap;
    JitHashTable<CORINFO_CLASS_HANDLE, JitPtrKeyFuncs<CORINFO_CLASS_STRUCT_>, StructDesc*, MallocAllocator> StructDescMap;
    JitHashTable<CORINFO_LLVM_DEBUG_TYPE_HANDLE, JitSmallPrimitiveKeyFuncs<CORINFO_LLVM_DEBUG_TYPE_HANDLE>, llvm::DIType*, MallocAllocator> DebugTypesMap;
    JitHashTable<llvm::DIFile*, JitPtrKeyFuncs<llvm::DIFile>, llvm::DICompileUnit*, MallocAllocator> DebugCompileUnitsMap;

    SingleThreadedCompilationContext(StringRef name)
        : Module(name, Context)
        , LlvmStructTypesMap({})
        , StructDescMap({})
        , DebugTypesMap({})
        , DebugCompileUnitsMap({})
    {
    }
};

class Llvm
{
private:
    static const unsigned SHADOW_STACK_ARG_INDEX = 0;
    static const unsigned DEFAULT_SHADOW_STACK_ALIGNMENT = TARGET_POINTER_SIZE;
    static const unsigned MIN_HEAP_OBJ_SIZE = TARGET_POINTER_SIZE * 2;

    void* const m_pEECorInfo; // TODO-LLVM: workaround for not changing the JIT/EE interface.
    SingleThreadedCompilationContext* const m_context;
    Compiler* const _compiler;
    Compiler::Info* const m_info;
    GCInfo* _gcInfo = nullptr;

    // Used by all of lowering, allocator and codegen.
    BasicBlock* m_currentBlock = nullptr;

    // Lowering members.
    LIR::Range* m_currentRange = nullptr;
    SideEffectSet m_scratchSideEffects; // Used for IsInvariantInRange.
    bool m_anyFilterFunclets = false;

    // Shared between lowering and codegen.
    bool m_anyReturns = false;

    // Shared between unwind index insertion and EH codegen.
    ArrayStack<unsigned>* m_unwindIndexMap = nullptr;
    BlockSet m_blocksInFilters = BlockSetOps::UninitVal();

    // Shared between LSSA and codegen.
    bool m_anyAddressExposedOrPinnedShadowLocals = false;

    // Codegen members.
    llvm::IRBuilder<> _builder;
    JitHashTable<GenTree*, JitPtrKeyFuncs<GenTree>, Value*> _sdsuMap;
    JitHashTable<SSAName, SSAName, Value*> _localsMap;
    JitHashTable<ThrowHelperKey, ThrowHelperKey, llvm::BasicBlock*> m_throwHelperBlocksMap;
    jitstd::vector<PhiPair> m_phiPairs;
    FunctionInfo* m_functions;

    CorInfoLlvmEHModel m_ehModel;
    EHRegionInfo* m_EHRegionsInfo;
    Value* m_exceptionThrownAddressValue = nullptr;

    Value* m_rootFunctionShadowStackValue = nullptr;

    // Codegen emit context.
    unsigned m_currentLlvmFunctionIndex = ROOT_FUNC_IDX;
    unsigned m_currentProtectedRegionIndex = EHblkDsc::NO_ENCLOSING_INDEX;
    unsigned m_currentHandlerIndex = EHblkDsc::NO_ENCLOSING_INDEX;
    LlvmBlockRange* m_currentLlvmBlocks = nullptr;

    // DWARF debug info.
    llvm::DIBuilder* m_diBuilder = nullptr;
    llvm::DISubprogram* m_diFunction = nullptr;
    JitHashTable<unsigned, JitSmallPrimitiveKeyFuncs<unsigned>, llvm::DILocalVariable*> m_debugVariablesMap;
    unsigned m_lineNumberCount;
    CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO* m_lineNumbers;

    unsigned m_shadowFrameAlignment = DEFAULT_SHADOW_STACK_ALIGNMENT;
    unsigned _shadowStackLocalsSize = 0;
    unsigned _originalShadowStackLclNum = BAD_VAR_NUM;
    unsigned _shadowStackLclNum = BAD_VAR_NUM;
    unsigned m_unwindFrameLclNum = BAD_VAR_NUM;
    unsigned _llvmArgCount = 0;

    // ================================================================================================================
    // |                                                   General                                                    |
    // ================================================================================================================

public:
    Llvm(Compiler* compiler);

    static void ConfigureDiagnosticOutput();

    var_types GetArgTypeForStructWasm(CORINFO_CLASS_HANDLE structHnd, structPassingKind* pPassKind);
    var_types GetReturnTypeForStructWasm(CORINFO_CLASS_HANDLE structHnd, structPassingKind* pPassKind);

private:
    LIR::Range& CurrentRange()
    {
        return *m_currentRange;
    }
    BasicBlock* CurrentBlock() const
    {
        return m_currentBlock;
    }

    GCInfo* getGCInfo();

    bool callHasShadowStackArg(const GenTreeCall* call) const;
    bool helperCallHasShadowStackArg(CorInfoHelpFunc helperFunc) const;
    bool callHasManagedCallingConvention(const GenTreeCall* call) const;
    bool helperCallHasManagedCallingConvention(CorInfoHelpFunc helperFunc) const;
    bool helperCallMayPhysicallyThrow(CorInfoHelpFunc helperFunc) const;

    static const HelperFuncInfo& getHelperFuncInfo(CorInfoHelpFunc helperFunc);

    CorInfoType getLlvmArgTypeForArg(CorInfoType argSigType, CORINFO_CLASS_HANDLE argSigClass, bool* pIsByRef = nullptr);
    CorInfoType getLlvmReturnType(CorInfoType sigRetType, CORINFO_CLASS_HANDLE sigRetClass, bool* pIsByRef = nullptr);

    static CorInfoType toCorInfoType(var_types varType);
    static CorInfoType getLlvmArgTypeForCallArg(CallArg* arg);
    TargetAbiType getAbiTypeForType(var_types type);

    CORINFO_GENERIC_HANDLE getSymbolHandleForHelperFunc(CorInfoHelpFunc helperFunc);
    CORINFO_GENERIC_HANDLE getSymbolHandleForClassToken(mdToken token);

    // Raw Jit-EE interface functions.
    //
    const char* GetMangledMethodName(CORINFO_METHOD_HANDLE methodHandle);
    const char* GetMangledSymbolName(void* symbol);
    const char* GetMangledFilterFuncletName(unsigned index);
    bool GetSignatureForMethodSymbol(CORINFO_GENERIC_HANDLE symbolHandle, CORINFO_SIG_INFO* pSig);
    void AddCodeReloc(void* handle);
    CorInfoType GetPrimitiveTypeForTrivialWasmStruct(CORINFO_CLASS_HANDLE structHandle);
    void GetTypeDescriptor(CORINFO_CLASS_HANDLE typeHandle, TypeDescriptor* pTypeDescriptor);
    const char* GetAlternativeFunctionName();
    CORINFO_GENERIC_HANDLE GetExternalMethodAccessor(
        CORINFO_METHOD_HANDLE methodHandle, const TargetAbiType* callSiteSig, int sigLength);
    CORINFO_LLVM_DEBUG_TYPE_HANDLE GetDebugTypeForType(CORINFO_CLASS_HANDLE typeHandle);
    void GetDebugInfoForDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle, CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo);
    void GetDebugInfoForCurrentMethod(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo);
    SingleThreadedCompilationContext* GetSingleThreadedCompilationContext();
    CorInfoLlvmEHModel GetExceptionHandlingModel();
    CORINFO_GENERIC_HANDLE GetExceptionThrownVariable();
    CORINFO_GENERIC_HANDLE GetExceptionHandlingTable(CORINFO_LLVM_EH_CLAUSE* pClauses, int count);
    void GetJitTestInfo(CorInfoLlvmJitTestKind kind, CORINFO_LLVM_JIT_TEST_INFO* pInfo);

public:
    static SingleThreadedCompilationContext* StartSingleThreadedCompilation(
        const char* path, const char* triple, const char* dataLayout);
    static void FinishSingleThreadedCompilation(SingleThreadedCompilationContext* context);

    // ================================================================================================================
    // |                                                 Type system                                                  |
    // ================================================================================================================

private:
    StructDesc* getStructDesc(CORINFO_CLASS_HANDLE structHandle);

    Type* getLlvmTypeForStruct(ClassLayout* classLayout);
    Type* getLlvmTypeForStruct(CORINFO_CLASS_HANDLE structHandle);
    Type* getLlvmTypeForVarType(var_types type);
    Type* getLlvmTypeForLclVar(LclVarDsc* varDsc);
    Type* getLlvmTypeForCorInfoType(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    unsigned getElementSize(CORINFO_CLASS_HANDLE fieldClassHandle, CorInfoType corInfoType);
    void addPaddingFields(unsigned paddingSize, ArrayStack<Type*>& llvmFields);

    Type* getPtrLlvmType();
    Type* getIntPtrLlvmType();

    // ================================================================================================================
    // |                                                   Lowering                                                   |
    // ================================================================================================================

public:
    void AddUnhandledExceptionHandler();
    void Lower();

private:
    void initializeFunclets();
    void initializeLlvmArgInfo();

    void lowerBlocks();
    void lowerBlock(BasicBlock* block);
    void lowerRange(BasicBlock* block, LIR::Range& range);
    void lowerNode(GenTree* node);
    void lowerLocal(GenTreeLclVarCommon* node);
    void lowerStoreLcl(GenTreeLclVarCommon* storeLclNode);
    void lowerFieldOfDependentlyPromotedStruct(GenTree* node);
    void lowerCall(GenTreeCall* callNode);
    void lowerRethrow(GenTreeCall* callNode);
    void lowerIndir(GenTreeIndir* indirNode);
    void lowerStoreBlk(GenTreeBlk* storeBlkNode);
    void lowerArrLength(GenTreeArrCommon* node);
    void lowerReturn(GenTreeUnOp* retNode);

    void lowerVirtualStubCall(GenTreeCall* callNode);
    void insertNullCheckForCall(GenTreeCall* callNode);
    void lowerDelegateInvoke(GenTreeCall* callNode);
    void lowerUnmanagedCall(GenTreeCall* callNode);
    void lowerCallToShadowStack(GenTreeCall* callNode);
    void lowerCallReturn(GenTreeCall* callNode);

    void lowerAddressToAddressMode(GenTreeIndir* indir);
    bool isAddressInBounds(GenTree* addr, FieldSeq* fieldSeq, target_size_t offset);
    unsigned getObjectSizeBound(GenTree* obj);

    GenTree* normalizeStructUse(LIR::Use& use, ClassLayout* layout);

    unsigned representAsLclVar(LIR::Use& use);
    GenTree* insertShadowStackAddr(GenTree* insertBefore, unsigned offset, unsigned shadowStackLclNum);
    GenTreeAddrMode* createAddrModeNode(GenTree* base, unsigned offset);

    bool isInvariantInRange(GenTree* node, GenTree* endExclusive);

    void lowerDissolveDependentlyPromotedLocals();
    void dissolvePromotedLocal(unsigned lclNum);

    void lowerCanonicalizeFirstBlock();
    bool isFirstBlockCanonical();

public:
    PhaseStatus AddVirtualUnwindFrame();

private:
    static const unsigned UNWIND_INDEX_NONE = -1;
    static const unsigned UNWIND_INDEX_NOT_IN_TRY = 0;
    static const unsigned UNWIND_INDEX_NOT_IN_TRY_CATCH = 1;
    static const unsigned UNWIND_INDEX_BASE = 2;

    void computeBlocksInFilters();
    ArrayStack<unsigned>* computeUnwindIndexMap();
    CORINFO_GENERIC_HANDLE generateUnwindTable();

    bool mayPhysicallyThrow(GenTree* node);
    bool isBlockInFilter(BasicBlock* block) const;

#ifdef DEBUG
    void printUnwindIndex(ArrayStack<unsigned>* indexMap, unsigned index);
#endif // DEBUG

    // ================================================================================================================
    // |                                           Shadow stack allocation                                            |
    // ================================================================================================================
public:
    void Allocate();

private:
    friend class ShadowStackAllocator;

    ValueInitKind getInitKindForLocal(unsigned lclNum, ValueInitOpts opts = ValueInitOpts::None) const;
#ifdef DEBUG
    void displayInitKindForLocal(unsigned lclNum, ValueInitKind initKind);
#endif // DEBUG

    unsigned getShadowFrameSize(unsigned funcIdx) const;
    unsigned getCalleeShadowStackOffset(unsigned funcIdx, bool isTailCall) const;
    bool canEmitCallAsShadowTailCall(bool callIsInTry, bool callIsInFilter) const;
    bool isPotentialGcSafePoint(GenTree* node) const;
    bool isShadowFrameLocal(LclVarDsc* varDsc) const;
    bool isShadowStackLocal(unsigned lclNum) const;
    bool isFuncletParameter(unsigned lclNum) const;

    // ================================================================================================================
    // |                                                   Codegen                                                    |
    // ================================================================================================================

public:
    void Compile();

private:
    static const unsigned ROOT_FUNC_IDX = 0;

    void initializeFunctions();
    void annotateFunctions();
    void generateProlog();
    void initializeShadowStack();
    void initializeLocals();
    void initializeBlocks();
    void generateUnwindBlocks();
    void generateBlocks();
    void generateBlock(BasicBlock* block);
    void fillPhis();
    void generateAuxiliaryArtifacts();
    void verifyGeneratedCode();
    void displayGeneratedCode();

    Value* getGenTreeValue(GenTree* node);
    Value* consumeValue(GenTree* node, Type* targetLlvmType = nullptr);
    void mapGenTreeToValue(GenTree* node, Value* nodeValue);

    void visitNode(GenTree* node);

    void buildLocalVar(GenTreeLclVar* lclVar);
    void buildStoreLocalVar(GenTreeLclVar* lclVar);
    void buildEmptyPhi(GenTreePhi* phi);
    void buildLocalField(GenTreeLclFld* lclFld);
    void buildStoreLocalField(GenTreeLclFld* lclFld);
    void buildLocalVarAddr(GenTreeLclVarCommon* lclVar);
    void buildAdd(GenTreeOp* node);
    void buildSub(GenTreeOp* node);
    void buildAddrMode(GenTreeAddrMode* addrMode);
    void buildDivMod(GenTree* node);
    void buildRotate(GenTreeOp* node);
    void buildCast(GenTreeCast* cast);
    void buildLclHeap(GenTreeUnOp* lclHeap);
    void buildCmp(GenTreeOp* node);
    void buildCnsDouble(GenTreeDblCon* node);
    void buildIntegralConst(GenTreeIntConCommon* node);
    void buildCall(GenTreeCall* node);
    void buildInd(GenTreeIndir* indNode);
    void buildBlk(GenTreeBlk* blkNode);
    void buildStoreInd(GenTreeStoreInd* storeIndOp);
    void buildStoreBlk(GenTreeBlk* blockOp);
    void buildUnaryOperation(GenTree* node);
    void buildBinaryOperation(GenTree* node);
    void buildShift(GenTreeOp* node);
    void buildIntrinsic(GenTreeIntrinsic* intrinsicNode);
    void buildAtomicOp(GenTreeIndir* atomicOpNode);
    void buildCmpXchg(GenTreeCmpXchg* cmpXchgNode);
    void buildMemoryBarrier(GenTree* node);
    void buildCatchArg(GenTree* catchArg);
    void buildReturn(GenTree* node);
    void buildJTrue(GenTree* node);
    void buildSwitch(GenTreeUnOp* switchNode);
    void buildNullCheck(GenTreeIndir* nullCheckNode);
    void buildBoundsCheck(GenTreeBoundsChk* boundsCheck);
    void buildCkFinite(GenTreeUnOp* ckNode);
    void buildKeepAlive(GenTreeUnOp* keepAliveNode);
    void buildILOffset(GenTreeILOffset* ilOffsetNode);

    void buildCatchRet(BasicBlock* block);
    void buildCallFinally(BasicBlock* block);

    Value* consumeAddressAndEmitNullCheck(GenTreeIndir* indir);
    void emitNullCheckForAddress(GenTree* addr, Value* addrValue);
    void emitAlignmentCheckForAddress(GenTree* addr, Value* addrValue, unsigned alignment);
    bool isAddressAligned(GenTree* addr, unsigned alignment);

    Value* consumeInitVal(GenTree* initVal);
    void storeObjAtAddress(Value* baseAddress, Value* data, StructDesc* structDesc);
    unsigned buildMemCpy(Value* baseAddress, unsigned startOffset, unsigned endOffset, Value* srcAddress);

    void emitJumpToThrowHelper(Value* jumpCondValue, CorInfoHelpFunc helperFunc);
    Value* emitCheckedArithmeticOperation(llvm::Intrinsic::ID intrinsicId, Value* op1Value, Value* op2Value);

    llvm::CallBase* emitGcStressCall(GenTreeCall* call, llvm::CallBase* callValue);
    llvm::CallBase* emitHelperCall(CorInfoHelpFunc helperFunc, ArrayRef<Value*> sigArgs = {});
    bool canEmitHelperCallAsShadowTailCall(CorInfoHelpFunc helperFunc);
    llvm::CallBase* emitCallOrInvoke(llvm::Function* callee, ArrayRef<Value*> args = {});
    llvm::CallBase* emitCallOrInvoke(llvm::FunctionCallee callee, ArrayRef<Value*> args, bool isThrowingCall);

    FunctionType* createFunctionType();
    llvm::FunctionCallee consumeCallTarget(GenTreeCall* call);
    FunctionType* createFunctionTypeForSignature(CORINFO_SIG_INFO* pSig);
    FunctionType* createFunctionTypeForCall(GenTreeCall* call);
    FunctionType* createFunctionTypeForHelper(CorInfoHelpFunc helperFunc);
    void annotateHelperFunction(CorInfoHelpFunc helperFunc, Function* llvmFunc);
    Function* getOrCreateKnownLlvmFunction(StringRef name,
                                           std::function<FunctionType*()> createFunctionType,
                                           std::function<void(Function*)> annotateFunction = [](Function*) { });

    EHRegionInfo& getEHRegionInfo(unsigned ehIndex);
    llvm::BasicBlock* getUnwindLlvmBlockForCurrentInvoke();
    void emitUnwindToOuterHandler();
    void emitUnwindToOuterHandlerFromFault();
    Function* getOrCreatePersonalityLlvmFunction(CorInfoLlvmEHModel ehModel);
    llvm::CatchPadInst* getCatchPadForHandler(unsigned hndIndex);
    llvm::BasicBlock* getOrCreateExceptionThrownReturnBlock();
    Value* getOrCreateExceptionThrownAddressValue();

    llvm::GlobalVariable* getOrCreateDataSymbol(StringRef symbolName, bool isThreadLocal = false);
    llvm::GlobalValue* getOrCreateSymbol(CORINFO_GENERIC_HANDLE symbolHandle, bool isThreadLocal = false);

    Value* gepOrAddr(Value* addr, unsigned offset);
    Value* gepOrAddrInBounds(Value* addr, unsigned offset);
    llvm::Constant* getIntPtrConst(target_size_t value, Type* llvmType = nullptr);
    Value* getShadowStack();
    Value* getShadowStackForCallee(bool isTailCall = false);
    Value* getOriginalShadowStack();

    void setCurrentEmitContextForBlock(BasicBlock* block);
    void setCurrentEmitContextBlocks(LlvmBlockRange* llvmBlocks);
    void setCurrentEmitContext(unsigned funcIdx, unsigned tryIndex, unsigned hndIndex, LlvmBlockRange* llvmBlock);
    unsigned getCurrentLlvmFunctionIndex() const;
    unsigned getCurrentProtectedRegionIndex() const;
    unsigned getCurrentHandlerIndex() const;
    LlvmBlockRange* getCurrentLlvmBlocks() const;

    bool isCurrentContextInFilter() const;

    Function* getRootLlvmFunction();
    Function* getCurrentLlvmFunction();
    Function* getLlvmFunctionForIndex(unsigned funcIdx);
    FunctionInfo& getLlvmFunctionInfoForIndex(unsigned funcIdx);
    unsigned getLlvmFunctionIndexForBlock(BasicBlock* block) const;
    unsigned getLlvmFunctionIndexForProtectedRegion(unsigned tryIndex) const;
    bool isLlvmFunctionReachable(unsigned funcIdx) const;

    llvm::BasicBlock* createInlineLlvmBlock();
    LlvmBlockRange* getLlvmBlocksForBlock(BasicBlock* block);
    llvm::BasicBlock* getFirstLlvmBlockForBlock(BasicBlock* block);
    llvm::BasicBlock* getLastLlvmBlockForBlock(BasicBlock* block);
    llvm::BasicBlock* getOrCreatePrologLlvmBlockForFunction(unsigned funcIdx);

    bool isReachable(BasicBlock* block) const;
    BasicBlock* getFirstBlockForFunction(unsigned funcIdx) const;

    Value* getLocalAddr(unsigned lclNum);
    Value* getOrCreateAllocaForLocalInFunclet(unsigned lclNum);

    void displayValue(Value* value);

public:
    bool IsLlvmIntrinsic(NamedIntrinsic intrinsicName) const;

private:
    llvm::Intrinsic::ID getLlvmIntrinsic(NamedIntrinsic intrinsicName) const;

    // ================================================================================================================
    // |                                    DWARF debug info (part of codegen)                                        |
    // ================================================================================================================

    void initializeDebugInfo();
    llvm::DIFile* initializeDebugInfoBuilder(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo);
    void initializeDebugVariables(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo);

    void declareDebugVariables();
    void assignDebugVariable(unsigned lclNum, Value* value);

    unsigned getLineNumberForILOffset(unsigned ilOffset);
    llvm::DILocation* getDebugLocation(unsigned lineNo);
    llvm::DILocation* getArtificialDebugLocation();
    llvm::DILocation* getCurrentOrArtificialDebugLocation();
    llvm::DIFile* getUnknownDebugFile();

    llvm::DIType* getOrCreateDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle);
    llvm::DIType* createDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle);
    llvm::DIType* createDebugTypeForPrimitive(CorInfoType type);
    llvm::DIType* createDebugTypeForCompositeType(
        CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle, CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO* pInfo);
    llvm::DIType* createDebugTypeForEnumType(CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO* pInfo);
    llvm::DIType* createDebugTypeForArrayType(CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO* pInfo);
    llvm::DIType* createDebugTypeForPointerType(CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO* pInfo);
    llvm::DIType* createFixedArrayDebugType(llvm::DIType* elementDebugType, unsigned size);
    llvm::DISubroutineType* createDebugTypeForFunctionType(CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO* pInfo);
    llvm::DIType* createClassDebugType(StringRef name, unsigned size, ArrayRef<llvm::Metadata*> elements);
    llvm::DIDerivedType* createDebugMember(StringRef name, llvm::DIType* debugType, unsigned offset);
    llvm::DIDerivedType* createPointerDebugType(llvm::DIType* pointeeDebugType);
};
#endif /* End of _LLVM_H_ */
