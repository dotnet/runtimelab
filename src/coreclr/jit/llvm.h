// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*****************************************************************************/
#ifndef _LLVM_H_
#define _LLVM_H_
#undef __PLACEMENT_NEW_INLINE

#include "alloc.h"
#include "jitpch.h"
#include "jitgcinfo.h"
#include "llvmtypes.h"
#include <new>

// these break std::min/max in LLVM's headers
#undef min
#undef max
// this breaks StringMap.h
#undef NumItems

#pragma warning (disable: 4702)
#include "llvm/IR/IRBuilder.h"
#include "llvm/IR/DIBuilder.h"
#include "llvm/IR/Function.h"
#include "llvm/IR/Verifier.h"
#pragma warning (error: 4702)

#include <unordered_map>

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

// Part of the Jit/EE interface, must be kept in sync with the managed version in "CorInfoImpl.Llvm.cs".
//
enum class TargetAbiType : uint8_t
{
    Void,
    Int32,
    Int64,
    Float,
    Double
};

// LLVM/WASM-specific helper functions. Reside in the same "namespace" as the regular Jit helpers.
//
enum CorInfoHelpLlvmFunc
{
    CORINFO_HELP_LLVM_UNDEF = CORINFO_HELP_COUNT,
    CORINFO_HELP_LLVM_GET_OR_INIT_SHADOW_STACK_TOP,
    CORINFO_HELP_LLVM_SET_SHADOW_STACK_TOP,
    CORINFO_HELP_ANY_COUNT
};

typedef unsigned CorInfoHelpAnyFunc; // Allow us to use both flavors of helpers.

enum HelperFuncInfoFlags
{
    HFIF_NONE = 0,
    HFIF_SS_ARG = 1, // The helper has shadow stack arg.
    HFIF_VAR_ARG = 1 << 1, // The helper has a variable number of args and must be treated specially.
    HFIF_NO_RPI_OR_GC = 1 << 2, // The helper will not call (back) into managed code or trigger GC.
};

struct HelperFuncInfo
{
    static const int MAX_SIG_ARG_COUNT = 3;

    INDEBUG(unsigned char Func);
    unsigned char SigReturnType;
    unsigned char SigArgTypes[MAX_SIG_ARG_COUNT];
    unsigned char Flags;

    bool IsInitialized() const
    {
        return SigReturnType != CORINFO_TYPE_UNDEF;
    }

    bool HasFlags(HelperFuncInfoFlags flags) const
    {
        return (Flags & flags) == flags;
    }

    CorInfoType GetSigReturnType() const;
    CORINFO_CLASS_HANDLE GetSigReturnClass(Compiler* compiler) const;
    CorInfoType GetSigArgType(size_t index) const;
    CORINFO_CLASS_HANDLE GetSigArgClass(Compiler* compiler, size_t index) const;
    size_t GetSigArgCount(unsigned* callArgCount = nullptr) const;
};

struct PhiPair
{
    GenTreePhi* irPhiNode;
    llvm::PHINode* llvmPhiNode;
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
};

// TODO: The module/context pair must be bound to a thread context. We should investigate removing the type maps.
// Note we declare all statics here, and define them in llvm.cpp, for documentation and visibility purposes even as
// some are only needed in other compilation units.
//
extern Module* _module;
extern LLVMContext _llvmContext;
extern std::unordered_map<CORINFO_CLASS_HANDLE, Type*>* _llvmStructs;
extern std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>* _structDescMap;

class Llvm
{
private:
    Compiler* const _compiler;
    Compiler::Info* const m_info;
    void* const m_pEECorInfo; // TODO-LLVM: workaround for not changing the JIT/EE interface.
    CORINFO_SIG_INFO _sigInfo; // sigInfo of function being compiled
    GCInfo* _gcInfo = nullptr;

    // Used by both lowering and codegen.
    BasicBlock* m_currentBlock = nullptr;

    // Lowering members.
    LIR::Range m_prologRange = LIR::Range();
    LIR::Range* m_currentRange = nullptr;

    // Codegen members.
    llvm::IRBuilder<> _builder;
    JitHashTable<BasicBlock*, JitPtrKeyFuncs<BasicBlock>, LlvmBlockRange> _blkToLlvmBlksMap;
    JitHashTable<GenTree*, JitPtrKeyFuncs<GenTree>, Value*> _sdsuMap;
    JitHashTable<SSAName, SSAName, Value*> _localsMap;
    std::vector<PhiPair> _phiPairs;
    std::vector<FunctionInfo> m_functions;
    std::vector<llvm::BasicBlock*> m_EHDispatchLlvmBlocks;

    Value* m_rootFunctionShadowStackValue = nullptr;

    // Codegen emit context.
    unsigned m_currentLlvmFunctionIndex = ROOT_FUNC_IDX;
    unsigned m_currentProtectedRegionIndex = EHblkDsc::NO_ENCLOSING_INDEX;
    LlvmBlockRange* m_currentLlvmBlocks = nullptr;

    // DWARF debug info.
    llvm::DIBuilder* m_diBuilder = nullptr;
    llvm::DISubprogram* m_diFunction = nullptr;

    unsigned _shadowStackLocalsSize = 0;
    unsigned _originalShadowStackLclNum = BAD_VAR_NUM;
    unsigned _shadowStackLclNum = BAD_VAR_NUM;
    unsigned _retAddressLclNum = BAD_VAR_NUM;
    unsigned _llvmArgCount = 0;

    // ================================================================================================================
    // |                                                   General                                                    |
    // ================================================================================================================

public:
    Llvm(Compiler* compiler);

    bool needsReturnStackSlot(const GenTreeCall* callee);
    var_types GetArgTypeForStructWasm(CORINFO_CLASS_HANDLE structHnd, structPassingKind* pPassKind, unsigned size);
    var_types GetReturnTypeForStructWasm(CORINFO_CLASS_HANDLE structHnd, structPassingKind* pPassKind, unsigned size);

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

    static CorInfoType toCorInfoType(var_types varType);

    bool needsReturnStackSlot(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    bool callRequiresShadowStackSave(const GenTreeCall* call) const;
    bool helperCallRequiresShadowStackSave(CorInfoHelpAnyFunc helperFunc) const;
    bool callHasShadowStackArg(const GenTreeCall* call) const;
    bool helperCallHasShadowStackArg(CorInfoHelpAnyFunc helperFunc) const;
    bool callHasManagedCallingConvention(const GenTreeCall* call) const;
    bool helperCallHasManagedCallingConvention(CorInfoHelpAnyFunc helperFunc) const;

    static const HelperFuncInfo& getHelperFuncInfo(CorInfoHelpAnyFunc helperFunc);

    bool canStoreArgOnLlvmStack(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    unsigned padOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned atOffset);
    unsigned padNextOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned atOffset);

    TargetAbiType getAbiTypeForType(var_types type);

    CORINFO_GENERIC_HANDLE getSymbolHandleForHelperFunc(CorInfoHelpAnyFunc helperFunc);
    CORINFO_GENERIC_HANDLE getSymbolHandleForClassToken(mdToken token);

    // Raw Jit-EE interface functions.
    //
    const char* GetMangledMethodName(CORINFO_METHOD_HANDLE methodHandle);
    const char* GetMangledSymbolName(void* symbol);
    bool GetSignatureForMethodSymbol(CORINFO_GENERIC_HANDLE symbolHandle, CORINFO_SIG_INFO* pSig);
    const char* GetEHDispatchFunctionName(CORINFO_EH_CLAUSE_FLAGS handlerType);
    void AddCodeReloc(void* handle);
    bool IsRuntimeImport(CORINFO_METHOD_HANDLE methodHandle) const;
    const char* GetDocumentFileName();
    uint32_t GetOffsetLineNumber(unsigned ilOffset);
    CorInfoType GetPrimitiveTypeForTrivialWasmStruct(CORINFO_CLASS_HANDLE structHandle);
    uint32_t PadOffset(CORINFO_CLASS_HANDLE typeHandle, unsigned atOffset);
    TypeDescriptor GetTypeDescriptor(CORINFO_CLASS_HANDLE typeHandle);
    const char* GetAlternativeFunctionName();
    CORINFO_GENERIC_HANDLE GetExternalMethodAccessor(
        CORINFO_METHOD_HANDLE methodHandle, const TargetAbiType* callSiteSig, int sigLength);
    CORINFO_GENERIC_HANDLE GetLlvmHelperFuncEntrypoint(CorInfoHelpLlvmFunc helperFunc);

public:
    static void StartThreadContextBoundCompilation(const char* path, const char* triple, const char* dataLayout);
    static void FinishThreadContextBoundCompilation();

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
    void addPaddingFields(unsigned paddingSize, std::vector<Type*>& llvmFields);

    Type* getPtrLlvmType();
    Type* getIntPtrLlvmType();

    // ================================================================================================================
    // |                                                   Lowering                                                   |
    // ================================================================================================================

public:
    void Lower();

private:
    void lowerLocals();
    void populateLlvmArgNums();
    void assignShadowStackOffsets(std::vector<LclVarDsc*>& shadowStackLocals, unsigned shadowStackParamCount);
    void initializeLocalInProlog(unsigned lclNum, GenTree* value);

    void insertProlog();

    void lowerBlocks();
    void lowerBlock(BasicBlock* block);
    void lowerNode(GenTree* node);
    void lowerLocal(GenTreeLclVarCommon* node);
    void lowerStoreLcl(GenTreeLclVarCommon* storeLclNode);
    void lowerFieldOfDependentlyPromotedStruct(GenTree* node);
    bool ConvertShadowStackLocalNode(GenTreeLclVarCommon* node);
    void lowerCall(GenTreeCall* callNode);
    void lowerRethrow(GenTreeCall* callNode);
    void lowerCatchArg(GenTree* catchArgNode);
    void lowerIndir(GenTreeIndir* indirNode);
    void lowerStoreBlk(GenTreeBlk* storeBlkNode);
    void lowerStoreDynBlk(GenTreeStoreDynBlk* storeDynBlkNode);
    void lowerDivMod(GenTreeOp* divModNode);
    void lowerReturn(GenTreeUnOp* retNode);

    void lowerVirtualStubCallBeforeArgs(GenTreeCall* callNode, unsigned* pThisLclNum, GenTree** pCellArgNode);
    void lowerVirtualStubCallAfterArgs(
        GenTreeCall* callNode, unsigned thisArgLclNum, GenTree* cellArgNode, unsigned shadowArgsSize);
    void insertNullCheckForCall(GenTreeCall* callNode);
    void lowerUnmanagedCall(GenTreeCall* callNode);
    unsigned lowerCallToShadowStack(GenTreeCall* callNode);
    CallArg* lowerCallReturn(GenTreeCall* callNode);

    GenTree* normalizeStructUse(LIR::Use& use, ClassLayout* layout);

    unsigned representAsLclVar(LIR::Use& use);
    GenTree* createStoreNode(var_types nodeType, GenTree* addr, GenTree* data);
    GenTree* createShadowStackStoreNode(var_types storeType, GenTree* addr, GenTree* data);
    GenTree* insertShadowStackAddr(GenTree* insertBefore, ssize_t offset, unsigned shadowStackLclNum);

    bool isShadowFrameLocal(LclVarDsc* varDsc) const;
    bool isFuncletParameter(unsigned lclNum) const;

    unsigned getCurrentShadowFrameSize() const;
    unsigned getShadowFrameSize(unsigned hndIndex) const;
    unsigned getOriginalShadowFrameSize() const;
    unsigned getCatchArgOffset() const;

    // ================================================================================================================
    // |                                                   Codegen                                                    |
    // ================================================================================================================

public:
    void Compile();

private:
    const unsigned ROOT_FUNC_IDX = 0;

    bool initializeFunctions();
    void initializeDebugInfo();
    void generateProlog();
    void initializeLocals();
    void generateBlock(BasicBlock* block);
    void generateEHDispatch();
    Value* generateEHDispatchTable(Function* llvmFunc, unsigned innerEHIndex, unsigned outerEHIndex);
    void fillPhis();
    void generateAuxiliaryArtifacts();

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
    void buildDivMod(GenTree* node);
    void buildRotate(GenTreeOp* node);
    void buildCast(GenTreeCast* cast);
    void buildLclHeap(GenTreeUnOp* lclHeap);
    void buildCmp(GenTreeOp* node);
    void buildCnsDouble(GenTreeDblCon* node);
    void buildIntegralConst(GenTreeIntConCommon* node);
    void buildCall(GenTreeCall* node);
    Value* buildFieldList(GenTreeFieldList* fieldList, Type* llvmType);
    void buildInd(GenTreeIndir* indNode);
    void buildBlk(GenTreeBlk* blkNode);
    void buildStoreInd(GenTreeStoreInd* storeIndOp);
    void buildStoreBlk(GenTreeBlk* blockOp);
    void buildStoreDynBlk(GenTreeStoreDynBlk* blockOp);
    void buildUnaryOperation(GenTree* node);
    void buildBinaryOperation(GenTree* node);
    void buildShift(GenTreeOp* node);
    void buildIntrinsic(GenTreeIntrinsic* intrinsicNode);
    void buildMemoryBarrier(GenTree* node);
    void buildReturn(GenTree* node);
    void buildJTrue(GenTree* node);
    void buildSwitch(GenTreeUnOp* switchNode);
    void buildNullCheck(GenTreeIndir* nullCheckNode);
    void buildBoundsCheck(GenTreeBoundsChk* boundsCheck);
    void buildCkFinite(GenTreeUnOp* ckNode);
    void buildKeepAlive(GenTreeUnOp* keepAliveNode);
    void buildILOffset(GenTreeILOffset* ilOffsetNode);

    void buildCallFinally(BasicBlock* block);

    void storeObjAtAddress(Value* baseAddress, Value* data, StructDesc* structDesc);
    unsigned buildMemCpy(Value* baseAddress, unsigned startOffset, unsigned endOffset, Value* srcAddress);

    void emitJumpToThrowHelper(Value* jumpCondValue, SpecialCodeKind throwKind);
    void emitNullCheckForIndir(GenTreeIndir* indir, Value* addrValue);
    Value* emitCheckedArithmeticOperation(llvm::Intrinsic::ID intrinsicId, Value* op1Value, Value* op2Value);
    Value* emitHelperCall(CorInfoHelpAnyFunc helperFunc, ArrayRef<Value*> sigArgs = { });
    llvm::CallBase* emitCallOrInvoke(llvm::FunctionCallee callee, ArrayRef<Value*> args);

    FunctionType* getFunctionType();
    llvm::FunctionCallee consumeCallTarget(GenTreeCall* call);
    FunctionType* createFunctionTypeForSignature(CORINFO_SIG_INFO* pSig);
    FunctionType* createFunctionTypeForCall(GenTreeCall* call);
    FunctionType* createFunctionTypeForHelper(CorInfoHelpAnyFunc helperFunc);
    void annotateHelperFunction(CorInfoHelpAnyFunc helperFunc, Function* llvmFunc);
    Function* getOrCreateKnownLlvmFunction(StringRef name,
                                           std::function<FunctionType*()> createFunctionType,
                                           std::function<void(Function*)> annotateFunction = [](Function*) { });
    Function* getOrCreateExternalLlvmFunctionAccessor(StringRef name);

    llvm::GlobalVariable* getOrCreateDataSymbol(StringRef symbolName);
    llvm::GlobalValue* getOrCreateSymbol(CORINFO_GENERIC_HANDLE symbolHandle);

    Instruction* getCast(Value* source, Type* targetType);
    Value* castIfNecessary(Value* source, Type* targetType, llvm::IRBuilder<>* builder = nullptr);
    Value* gepOrAddr(Value* addr, unsigned offset);
    Value* getShadowStack();
    Value* getShadowStackForCallee();
    Value* getOriginalShadowStack();

    llvm::DILocation* createDebugLocation(unsigned lineNo);
    llvm::DILocation* getArtificialDebugLocation();

    void setCurrentEmitContextForBlock(BasicBlock* block);
    void setCurrentEmitContext(unsigned funcIdx, unsigned tryIndex, LlvmBlockRange* llvmBlock);
    unsigned getCurrentLlvmFunctionIndex() const;
    unsigned getCurrentProtectedRegionIndex() const;
    LlvmBlockRange* getCurrentLlvmBlocks() const;

    Function* getRootLlvmFunction();
    Function* getCurrentLlvmFunction();
    Function* getLlvmFunctionForIndex(unsigned funcIdx);
    FunctionInfo& getLlvmFunctionInfoForIndex(unsigned funcIdx);
    unsigned getLlvmFunctionIndexForBlock(BasicBlock* block) const;
    unsigned getLlvmFunctionIndexForProtectedRegion(unsigned tryIndex) const;

    llvm::BasicBlock* createInlineLlvmBlock();
    LlvmBlockRange* getLlvmBlocksForBlock(BasicBlock* block);
    llvm::BasicBlock* getFirstLlvmBlockForBlock(BasicBlock* block);
    llvm::BasicBlock* getLastLlvmBlockForBlock(BasicBlock* block);
    llvm::BasicBlock* getOrCreatePrologLlvmBlockForFunction(unsigned funcIdx);

    bool isReachable(BasicBlock* block) const;
    BasicBlock* getFirstBlockForFunction(unsigned funcIdx) const;

    Value* getLocalAddr(unsigned lclNum);
    Value* getOrCreateAllocaForLocalInFunclet(unsigned lclNum);

public:
    bool IsLlvmIntrinsic(NamedIntrinsic intrinsicName) const;

private:
    llvm::Intrinsic::ID getLlvmIntrinsic(NamedIntrinsic intrinsicName) const;
};
#endif /* End of _LLVM_H_ */
