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

struct JitStdStringKeyFuncs : JitKeyFuncsDefEquals<std::string>
{
    static unsigned GetHashCode(const std::string& val)
    {
        return static_cast<unsigned>(std::hash<std::string>()(val));
    }
};

struct DebugMetadata
{
    llvm::DIFile* fileMetadata;
    llvm::DICompileUnit* diCompileUnit;
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
};

// TODO: We should create a Static... class to manage the globals and their lifetimes.
// Note we declare all statics here, and define them in llvm.cpp, for documentation and
// visibility purposes even as some are only needed in other compilation units.
//
extern Module*                                                _module;
extern llvm::DIBuilder*                                       _diBuilder;
extern LLVMContext                                            _llvmContext;
extern Function*                                              _doNothingFunction;
extern std::unordered_map<CORINFO_CLASS_HANDLE, Type*>*       _llvmStructs;
extern std::unordered_map<CORINFO_CLASS_HANDLE, StructDesc*>* _structDescMap;

class Llvm
{
private:
    Compiler* _compiler;
    Compiler::Info _info;
    GCInfo* _gcInfo = nullptr;

    Function* _function;
    CORINFO_SIG_INFO _sigInfo; // sigInfo of function being compiled
    LIR::Range* _currentRange;
    BasicBlock* _currentBlock;
    DebugInfo _currentOffset;
    llvm::IRBuilder<> _builder;
    llvm::IRBuilder<> _prologBuilder;
    JitHashTable<BasicBlock*, JitPtrKeyFuncs<BasicBlock>, LlvmBlockRange> _blkToLlvmBlksMap;
    JitHashTable<GenTree*, JitPtrKeyFuncs<GenTree>, Value*> _sdsuMap;
    JitHashTable<SSAName, SSAName, Value*> _localsMap;
    std::vector<PhiPair> _phiPairs;
    std::vector<Value*> m_allocas;

    // DWARF
    llvm::DILocation* _currentOffsetDiLocation;
    llvm::DISubprogram* _debugFunction;
    DebugMetadata  _debugMetadata;
    JitHashTable<std::string, JitStdStringKeyFuncs, DebugMetadata> _debugMetadataMap;

    unsigned _shadowStackLocalsSize;
    unsigned _shadowStackLclNum;
    unsigned _retAddressLclNum;
    unsigned _llvmArgCount;

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
        return *_currentRange;
    }
    BasicBlock* CurrentBlock() const
    {
        return _currentBlock;
    }

    GCInfo* getGCInfo();

    CORINFO_CLASS_HANDLE tryGetStructClassHandle(LclVarDsc* varDsc);
    CorInfoType getCorInfoTypeForArg(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE& arg, CORINFO_CLASS_HANDLE* clsHnd);
    static CorInfoType toCorInfoType(var_types varType);

    static bool needsReturnStackSlot(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    bool callHasShadowStackArg(GenTreeCall* call);
    const HelperFuncInfo& getHelperFuncInfo(CorInfoHelpFunc helperFunc);

    bool canStoreLocalOnLlvmStack(LclVarDsc* varDsc);
    static bool canStoreArgOnLlvmStack(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    unsigned int padOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned int atOffset);
    unsigned int padNextOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned int atOffset);

    [[noreturn]] void failFunctionCompilation();

    // Raw Jit-EE interface functions.
    //
    const char* GetMangledMethodName(CORINFO_METHOD_HANDLE methodHandle);
    const char* GetMangledSymbolName(void* symbol);
    const char* GetTypeName(CORINFO_CLASS_HANDLE typeHandle);
    const char* AddCodeReloc(void* handle);
    bool IsRuntimeImport(CORINFO_METHOD_HANDLE methodHandle);
    const char* GetDocumentFileName();
    uint32_t FirstSequencePointLineNumber();
    uint32_t GetOffsetLineNumber(unsigned ilOffset);
    bool StructIsWrappedPrimitive(CORINFO_CLASS_HANDLE typeHandle, CorInfoType corInfoType);
    uint32_t PadOffset(CORINFO_CLASS_HANDLE typeHandle, unsigned atOffset);
    CorInfoTypeWithMod GetArgTypeIncludingParameterized(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE arg, CORINFO_CLASS_HANDLE* pTypeHandle);
    CorInfoTypeWithMod GetParameterType(CORINFO_CLASS_HANDLE typeHandle, CORINFO_CLASS_HANDLE* pInnerParameterTypeHandle);
    TypeDescriptor GetTypeDescriptor(CORINFO_CLASS_HANDLE typeHandle);
    CORINFO_METHOD_HANDLE GetCompilerHelpersMethodHandle(const char* helperClassTypeName, const char* helperMethodName);
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
    Type* getLlvmTypeForParameterType(CORINFO_CLASS_HANDLE classHnd);

    unsigned getElementSize(CORINFO_CLASS_HANDLE fieldClassHandle, CorInfoType corInfoType);
    void addPaddingFields(unsigned paddingSize, std::vector<Type*>& llvmFields);

    // ================================================================================================================
    // |                                                   Lowering                                                   |
    // ================================================================================================================

public:
    void Lower();

private:
    void lowerLocals();
    void lowerBlocks();

    void populateLlvmArgNums();
    void assignShadowStackOffsets(std::vector<LclVarDsc*>& shadowStackLocals, unsigned shadowStackParamCount);
    void initializeLocalInProlog(unsigned lclNum, GenTree* value);

    void lowerStoreLcl(GenTreeLclVarCommon* storeLclNode);
    void lowerFieldOfDependentlyPromotedStruct(GenTree* node);
    void ConvertShadowStackLocalNode(GenTreeLclVarCommon* node);
    void lowerCall(GenTreeCall* callNode);
    void lowerIndir(GenTreeIndir* indirNode);
    void lowerStoreBlk(GenTreeBlk* storeBlkNode);
    void lowerDivMod(GenTreeOp* divModNode);
    void lowerReturn(GenTreeUnOp* retNode);

    void lowerCallToShadowStack(GenTreeCall* callNode);
    void failUnsupportedCalls(GenTreeCall* callNode);
    GenTreeCall::Use* lowerCallReturn(GenTreeCall* callNode, GenTreeCall::Use* lastArg);

    void normalizeStructUse(GenTree* node, ClassLayout* layout);

    GenTree* createStoreNode(var_types nodeType, GenTree* addr, GenTree* data);
    GenTree* createShadowStackStoreNode(var_types storeType, GenTree* addr, GenTree* data);
    GenTree* insertShadowStackAddr(GenTree* insertBefore, ssize_t offset);

    // ================================================================================================================
    // |                                                   Codegen                                                    |
    // ================================================================================================================

public:
    void Compile();

private:
    void generateProlog();
    void initializeLocals();
    void generateBlock(BasicBlock* block);
    void generateThrowHelperBlock(BasicBlock* block);
    void fillPhis();

    Value* getGenTreeValue(GenTree* node);
    Value* consumeValue(GenTree* node, Type* targetLlvmType = nullptr);
    void mapGenTreeToValue(GenTree* node, Value* nodeValue);

    void startImportingNode();
    void visitNode(GenTree* node);

    void buildLocalVar(GenTreeLclVar* lclVar);
    void buildStoreLocalVar(GenTreeLclVar* lclVar);
    void buildEmptyPhi(GenTreePhi* phi);
    void buildLocalField(GenTreeLclFld* lclFld);
    void buildLocalVarAddr(GenTreeLclVarCommon* lclVar);
    void buildAdd(GenTreeOp* node);
    void buildDivMod(GenTree* node);
    void buildCast(GenTreeCast* cast);
    void buildLclHeap(GenTreeUnOp* lclHeap);
    void buildCmp(GenTreeOp* node);
    void buildCnsDouble(GenTreeDblCon* node);
    void buildCnsInt(GenTree* node);
    void buildCnsLng(GenTree* node);
    void buildCall(GenTreeCall* node);
    Value* buildFieldList(GenTreeFieldList* fieldList, Type* llvmType);
    void buildInd(GenTreeIndir* indNode);
    void buildBlk(GenTreeBlk* blkNode);
    void buildStoreInd(GenTreeStoreInd* storeIndOp);
    void buildStoreBlk(GenTreeBlk* blockOp);
    void buildUnaryOperation(GenTree* node);
    void buildBinaryOperation(GenTree* node);
    void buildShift(GenTreeOp* node);
    void buildReturn(GenTree* node);
    void buildJTrue(GenTree* node, Value* opValue);    
    void buildNullCheck(GenTreeIndir* nullCheckNode);
    void buildBoundsCheck(GenTreeBoundsChk* boundsCheck);

    void storeObjAtAddress(Value* baseAddress, Value* data, StructDesc* structDesc);
    unsigned buildMemCpy(Value* baseAddress, unsigned startOffset, unsigned endOffset, Value* srcAddress);

    void emitDoNothingCall();
    void emitJumpToThrowHelper(Value* jumpCondValue, SpecialCodeKind throwKind);
    void emitNullCheckForIndir(GenTreeIndir* indir, Value* addrValue);
    Value* emitHelperCall(CorInfoHelpFunc helperFunc, ArrayRef<Value*> sigArgs = { });
    Value* emitCallOrInvoke(llvm::FunctionCallee callee, ArrayRef<Value*> args);

    FunctionType* getFunctionType();
    Function* getOrCreateLlvmFunction(const char* symbolName, GenTreeCall* call);
    FunctionType* createFunctionTypeForCall(GenTreeCall* call);
    FunctionType* createFunctionTypeForHelper(CorInfoHelpFunc helperFunc);

    Value* getOrCreateExternalSymbol(const char* symbolName, Type* symbolType = nullptr);

    llvm::Instruction* getCast(llvm::Value* source, Type* targetType);
    Value* castIfNecessary(Value* source, Type* targetType, llvm::IRBuilder<>* builder = nullptr);
    Value* gepOrAddr(Value* addr, unsigned offset);
    Value* getShadowStackForCallee();

    DebugMetadata getOrCreateDebugMetadata(const char* documentFileName);
    llvm::DILocation* createDebugFunctionAndDiLocation(struct DebugMetadata debugMetadata, unsigned int lineNo);

    llvm::BasicBlock* createInlineLlvmBlock();
    llvm::BasicBlock* getFirstLlvmBlockForBlock(BasicBlock* block);
    llvm::BasicBlock* getLastLlvmBlockForBlock(BasicBlock* block);
    void setLastLlvmBlockForBlock(BasicBlock* block, llvm::BasicBlock* llvmBlock);

    bool isLlvmFrameLocal(LclVarDsc* varDsc);
    unsigned int getTotalLocalOffset();
};


#endif /* End of _LLVM_H_ */
