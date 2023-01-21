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

using SSAName = Compiler::SSAName;

#define IMAGE_FILE_MACHINE_WASM32             0xFFFF
#define IMAGE_FILE_MACHINE_WASM64             0xFFFE // TODO: appropriate values for this?  Used to check compilation is for intended target

struct OperandArgNum
{
    unsigned int argNum;
    GenTree* operand;
};

enum HelperFuncInfoFlags
{
    HFIF_NONE = 0,
    HFIF_SS_ARG = 1, // The helper has shadow stack arg.
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
    CorInfoType GetSigArgType(size_t index) const;
    CORINFO_CLASS_HANDLE GetSigArgClass(Compiler* compiler, size_t index) const;
    size_t GetSigArgCount() const;
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

// TODO: We should create a Static... class to manage the globals and their lifetimes.
// Note we declare all statics here, and define them in llvm.cpp, for documentation and
// visibility purposes even as some are only needed in other compilation units.
//
extern Module*                                                _module;
extern LLVMContext                                            _llvmContext;
extern Function*                                              _doNothingFunction;
extern std::unordered_map<CORINFO_CLASS_HANDLE, Type*>*       _llvmStructs;
extern std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>* _structDescMap;

class Llvm
{
private:
    Compiler* _compiler;
    Compiler::Info _info;
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

    static void llvmShutdown();
    static bool needsReturnStackSlot(Compiler* compiler, GenTreeCall* callee);

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

    static bool needsReturnStackSlot(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    bool callHasShadowStackArg(GenTreeCall* call);
    const HelperFuncInfo& getHelperFuncInfo(CorInfoHelpFunc helperFunc);

    static bool canStoreArgOnLlvmStack(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    unsigned padOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned atOffset);
    unsigned padNextOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned atOffset);

    [[noreturn]] void failFunctionCompilation();

    // Raw Jit-EE interface functions.
    //
    const char* GetMangledMethodName(CORINFO_METHOD_HANDLE methodHandle);
    const char* GetMangledSymbolName(void* symbol);
    const char* GetEHDispatchFunctionName(CORINFO_EH_CLAUSE_FLAGS handlerType);
    const char* GetTypeName(CORINFO_CLASS_HANDLE typeHandle);
    void AddCodeReloc(void* handle);
    bool IsRuntimeImport(CORINFO_METHOD_HANDLE methodHandle);
    const char* GetDocumentFileName();
    uint32_t FirstSequencePointLineNumber();
    uint32_t GetOffsetLineNumber(unsigned ilOffset);
    bool StructIsWrappedPrimitive(CORINFO_CLASS_HANDLE typeHandle, CorInfoType corInfoType);
    uint32_t PadOffset(CORINFO_CLASS_HANDLE typeHandle, unsigned atOffset);
    TypeDescriptor GetTypeDescriptor(CORINFO_CLASS_HANDLE typeHandle);
    uint32_t GetInstanceFieldAlignment(CORINFO_CLASS_HANDLE fieldTypeHandle);

    // ================================================================================================================
    // |                                                 Type system                                                  |
    // ================================================================================================================

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
    void lowerStoreLcl(GenTreeLclVarCommon* storeLclNode);
    void lowerFieldOfDependentlyPromotedStruct(GenTree* node);
    void ConvertShadowStackLocalNode(GenTreeLclVarCommon* node);
    void lowerCall(GenTreeCall* callNode);
    void lowerRethrow(GenTreeCall* callNode);
    void lowerCatchArg(GenTree* catchArgNode);
    void lowerIndir(GenTreeIndir* indirNode);
    void lowerStoreBlk(GenTreeBlk* storeBlkNode);
    void lowerStoreDynBlk(GenTreeStoreDynBlk* storeDynBlkNode);
    void lowerDivMod(GenTreeOp* divModNode);
    void lowerReturn(GenTreeUnOp* retNode);

    void lowerCallToShadowStack(GenTreeCall* callNode);
    void failUnsupportedCalls(GenTreeCall* callNode);
    GenTreeCall::Use* lowerCallReturn(GenTreeCall* callNode, GenTreeCall::Use* lastArg);

    void normalizeStructUse(GenTree* node, ClassLayout* layout);

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

    Value* getGenTreeValue(GenTree* node);
    Value* consumeValue(GenTree* node, Type* targetLlvmType = nullptr);
    void mapGenTreeToValue(GenTree* node, Value* nodeValue);

    void visitNode(GenTree* node);

    void buildLocalVar(GenTreeLclVar* lclVar);
    void buildStoreLocalVar(GenTreeLclVar* lclVar);
    void buildEmptyPhi(GenTreePhi* phi);
    void buildLocalField(GenTreeLclFld* lclFld);
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
    void buildReturn(GenTree* node);
    void buildJTrue(GenTree* node);
    void buildSwitch(GenTreeUnOp* switchNode);
    void buildNullCheck(GenTreeIndir* nullCheckNode);
    void buildBoundsCheck(GenTreeBoundsChk* boundsCheck);
    void buildCkFinite(GenTreeUnOp* ckNode);
    void buildILOffset(GenTreeILOffset* ilOffsetNode);

    void buildCallFinally(BasicBlock* block);

    void storeObjAtAddress(Value* baseAddress, Value* data, StructDesc* structDesc);
    unsigned buildMemCpy(Value* baseAddress, unsigned startOffset, unsigned endOffset, Value* srcAddress);

    void emitDoNothingCall();
    void emitJumpToThrowHelper(Value* jumpCondValue, SpecialCodeKind throwKind);
    void emitNullCheckForIndir(GenTreeIndir* indir, Value* addrValue);
    Value* emitCheckedArithmeticOperation(llvm::Intrinsic::ID intrinsicId, Value* op1Value, Value* op2Value);
    Value* emitHelperCall(CorInfoHelpFunc helperFunc, ArrayRef<Value*> sigArgs = { });
    llvm::CallBase* emitCallOrInvoke(llvm::FunctionCallee callee, ArrayRef<Value*> args);

    FunctionType* getFunctionType();
    Function* getOrCreateLlvmFunction(const char* symbolName, GenTreeCall* call);
    FunctionType* createFunctionTypeForCall(GenTreeCall* call);
    FunctionType* createFunctionTypeForHelper(CorInfoHelpFunc helperFunc);

    llvm::GlobalVariable* getOrCreateExternalSymbol(const char* symbolName);
    llvm::GlobalVariable* getOrCreateSymbol(CORINFO_GENERIC_HANDLE symbolHandle);
    CORINFO_GENERIC_HANDLE getSymbolHandleForClassToken(mdToken token);

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
    llvm::BasicBlock* getCurrentLlvmBlock() const;
    LlvmBlockRange* getLlvmBlocksForBlock(BasicBlock* block);
    llvm::BasicBlock* getFirstLlvmBlockForBlock(BasicBlock* block);
    llvm::BasicBlock* getLastLlvmBlockForBlock(BasicBlock* block);
    llvm::BasicBlock* getOrCreatePrologLlvmBlockForFunction(unsigned funcIdx);

    bool isReachable(BasicBlock* block) const;
    BasicBlock* getFirstBlockForFunction(unsigned funcIdx) const;

    Value* getLocalAddr(unsigned lclNum);
    Value* getOrCreateAllocaForLocalInFunclet(unsigned lclNum);
};
#endif /* End of _LLVM_H_ */
