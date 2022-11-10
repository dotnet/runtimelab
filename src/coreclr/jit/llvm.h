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
#ifdef TARGET_WASM

#include "llvm/IR/IRBuilder.h"
#include "llvm/IR/Function.h"

#include <unordered_map>

using llvm::Value;
using llvm::Type;

#define IMAGE_FILE_MACHINE_WASM32             0xFFFF
#define IMAGE_FILE_MACHINE_WASM64             0xFFFE // TODO: appropriate values for this?  Used to check compilation is for intended target

struct OperandArgNum
{
    unsigned int argNum;
    GenTree* operand;
};

struct LlvmArgInfo
{
    int          m_argIx; // -1 indicates not in the LLVM arg list, but on the shadow stack
    unsigned int m_shadowStackOffset;

    bool IsLlvmArg()
    {
        return m_argIx >= 0;
    }
};

struct DebugMetadata
{
    llvm::DIFile* fileMetadata;
    llvm::DICompileUnit* diCompileUnit;
};

struct IncomingPhi
{
    llvm::PHINode* phiNode;
    llvm::BasicBlock* llvmBasicBlock;
};

typedef std::pair<unsigned, unsigned> SsaPair;

struct SsaPairHash
{
    template <class T1, class T2>
    std::size_t operator()(const std::pair<T1, T2>& pair) const
    {
        return std::hash<T1>()(pair.first) ^ std::hash<T2>()(pair.second);
    }
};

extern "C" void registerLlvmCallbacks(void*       thisPtr,
                                      const char* outputFileName,
                                      const char* triple,
                                      const char* dataLayout,
                                      const char* (*getMangledMethodNamePtr)(void*, CORINFO_METHOD_STRUCT_*),
                                      const char* (*getMangledSymbolNamePtr)(void*, void*),
                                      const char* (*getMangledSymbolNameFromHelperTargetPtr)(void*, void*),
                                      const char* (*getTypeName)(void*, CORINFO_CLASS_HANDLE),
                                      const char* (*addCodeReloc)(void*, void*),
                                      const uint32_t (*isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*),
                                      const char* (*getDocumentFileName)(void*),
                                      const uint32_t (*firstSequencePointLineNumber)(void*),
                                      const uint32_t (*getOffsetLineNumber)(void*, unsigned int),
                                      const uint32_t(*structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType),
                                      const uint32_t(*padOffset)(void*, CORINFO_CLASS_STRUCT_*, unsigned),
                                      const CorInfoTypeWithMod(*_getArgTypeIncludingParameterized)(void*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*),
                                      const CorInfoTypeWithMod(*_getParameterType)(void*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*),
                                      const TypeDescriptor(*getTypeDescriptor)(void*, CORINFO_CLASS_HANDLE),
                                      CORINFO_METHOD_HANDLE (*_getCompilerHelpersMethodHandle)(void*, const char*, const char*),
                                      const uint32_t (*getInstanceFieldAlignment)(void*, CORINFO_CLASS_HANDLE));

struct PhiPair
{
    GenTreePhi* irPhiNode;
    llvm::PHINode* llvmPhiNode;
};

typedef JitHashTable<BasicBlock*, JitPtrKeyFuncs<BasicBlock>, llvm::BasicBlock*> BlkToLlvmBlkVectorMap;

class Llvm
{
private:
    Compiler* _compiler;
    Compiler::Info _info;
    GCInfo* _gcInfo = nullptr;

    llvm::Function* _function;
    CORINFO_SIG_INFO _sigInfo; // sigInfo of function being compiled
    LIR::Range* _currentRange;
    BasicBlock* _currentBlock;
    IL_OFFSETX _currentOffset;
    BlkToLlvmBlkVectorMap* _blkToLlvmBlkVectorMap;
    llvm::IRBuilder<> _builder;
    llvm::IRBuilder<> _prologBuilder;
    std::unordered_map<GenTree*, Value*>* _sdsuMap;
    std::unordered_map<SsaPair, Value*, SsaPairHash>* _localsMap;
    std::vector<PhiPair> _phiPairs;
    std::vector<Value*> m_allocas;

    // DWARF
    llvm::DILocation* _currentOffsetDiLocation;
    llvm::DISubprogram* _debugFunction;
    DebugMetadata  _debugMetadata;
    std::unordered_map<std::string, struct DebugMetadata> _debugMetadataMap;

    unsigned _shadowStackLocalsSize;
    unsigned _shadowStackLclNum;
    unsigned _retAddressLclNum;
    unsigned _llvmArgCount;

    // ================================================================================================================
    // |                                                   General                                                    |
    // ================================================================================================================

    LIR::Range& CurrentRange()
    {
        return *_currentRange;
    }

    GCInfo* getGCInfo();

    CORINFO_CLASS_HANDLE tryGetStructClassHandle(LclVarDsc* varDsc);
    CorInfoType getCorInfoTypeForArg(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE& arg, CORINFO_CLASS_HANDLE* clsHnd);
    CorInfoType toCorInfoType(var_types varType);

    static bool needsReturnStackSlot(Compiler* compiler, CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);
    bool needsReturnStackSlot(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);

    unsigned int padNextOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned int atOffset);
    unsigned int padOffset(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHandle, unsigned int atOffset);

    [[noreturn]] void failFunctionCompilation();

    // ================================================================================================================
    // |                                                 Type system                                                  |
    // ================================================================================================================

    StructDesc* getStructDesc(CORINFO_CLASS_HANDLE structHandle);

    Type* getLlvmTypeForStruct(ClassLayout* classLayout);
    Type* getLlvmTypeForStruct(CORINFO_CLASS_HANDLE structHandle);
    Type* getLlvmTypeForVarType(var_types type);
    Type* getLlvmTypeForLclVar(GenTreeLclVar* lclVar);
    Type* getLlvmTypeForCorInfoType(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);
    Type* getLlvmTypeForParameterType(CORINFO_CLASS_HANDLE classHnd);

    unsigned getElementSize(CORINFO_CLASS_HANDLE fieldClassHandle, CorInfoType corInfoType);

    // ================================================================================================================
    // |                                                   Lowering                                                   |
    // ================================================================================================================

    void populateLlvmArgNums();
    void lowerToShadowStack();

    void lowerStoreLcl(GenTreeLclVarCommon* storeLclNode);
    void lowerFieldOfDependentlyPromotedStruct(GenTree* node);
    void ConvertShadowStackLocalNode(GenTreeLclVarCommon* node);

    void lowerCallToShadowStack(GenTreeCall* callNode);
    void failUnsupportedCalls(GenTreeCall* callNode);
    GenTreeCall::Use* lowerCallReturn(GenTreeCall* callNode, GenTreeCall::Use* lastArg);

    GenTree* createStoreNode(var_types nodeType, GenTree* addr, GenTree* data, ClassLayout* structClassLayout = nullptr);
    GenTree* createShadowStackStoreNode(var_types nodeType, GenTree* addr, GenTree* data, ClassLayout* structClassLayout);

    // ================================================================================================================
    // |                                                   Codegen                                                    |
    // ================================================================================================================

    void generateProlog();
    void createAllocasForLocalsWithAddrOp();
    void startImportingBasicBlock(BasicBlock* block);
    void endImportingBasicBlock(BasicBlock* block);
    void fillPhis();

    Value* getGenTreeValue(GenTree* node);
    Value* consumeValue(GenTree* node, Type* targetLlvmType);
    Value* mapGenTreeToValue(GenTree* genTree, Value* valueRef);

    void startImportingNode();
    void visitNode(GenTree* node);

    Value* localVar(GenTreeLclVar* lclVar);
    void storeLocalVar(GenTreeLclVar* lclVar);
    void buildEmptyPhi(GenTreePhi* phi);
    void buildLocalField(GenTreeLclFld* lclFld);
    void buildLocalVarAddr(GenTreeLclVarCommon* lclVar);
    void buildAdd(GenTree* node, Value* op1, Value* op2);
    void buildDiv(GenTree* node);
    void buildCast(GenTreeCast* cast);
    void buildCmp(GenTree* node, Value* op1, Value* op2);
    void buildCnsDouble(GenTreeDblCon* node);
    void buildCnsInt(GenTree* node);
    void buildCnsLng(GenTree* node);
    void buildCall(GenTree* node);
    void buildInd(GenTree* node, Value* ptr);
    void buildObj(GenTreeObj* node);
    void buildUnaryOperation(GenTree* node);
    void buildBinaryOperation(GenTree* node);
    void buildShift(GenTreeOp* node);
    void buildReturn(GenTree* node);
    Value* buildJTrue(GenTree* node, Value* opValue);
    void buildStoreInd(GenTreeStoreInd* storeIndOp);
    void buildStoreBlk(GenTreeBlk* blockOp);
    void buildNullCheck(GenTreeUnOp* nullCheckNode);

    void buildHelperFuncCall(GenTreeCall* call);
    Value* buildUserFuncCall(GenTreeCall* call);
    Value* buildFieldList(GenTreeFieldList* fieldList, Type* llvmType);
    void storeObjAtAddress(Value* baseAddress, Value* data, StructDesc* structDesc);
    unsigned buildMemCpy(Value* baseAddress, unsigned startOffset, unsigned endOffset, Value* srcAddress);
    void emitDoNothingCall();
    void buildThrowException(llvm::IRBuilder<>& builder, const char* helperClass, const char* helperMethodName, Value* shadowStack);
    void buildLlvmCallOrInvoke(llvm::Function* callee, llvm::ArrayRef<Value*> args);

    llvm::Instruction* getCast(llvm::Value* source, Type* targetType);
    Value* castIfNecessary(Value* source, Type* targetType, llvm::IRBuilder<>* builder = nullptr);
    Value* gepOrAddr(Value* addr, unsigned offset);
    Value* getShadowStackForCallee();

    llvm::FunctionType* getFunctionType();
    llvm::Function* getOrCreateLlvmFunction(const char* symbolName, GenTreeCall* call);
    llvm::FunctionType* createFunctionTypeForCall(GenTreeCall* call);
    llvm::FunctionType* buildHelperLlvmFunctionType(GenTreeCall* call, bool withShadowStack);
    bool helperRequiresShadowStack(CORINFO_METHOD_HANDLE corinfoMethodHnd);

    llvm::BasicBlock* getLLVMBasicBlockForBlock(BasicBlock* block);

    DebugMetadata getOrCreateDebugMetadata(const char* documentFileName);
    llvm::DILocation* createDebugFunctionAndDiLocation(struct DebugMetadata debugMetadata, unsigned int lineNo);

    bool isLlvmFrameLocal(LclVarDsc* varDsc);
    unsigned int getTotalRealLocalOffset();
    unsigned int getTotalLocalOffset();
    LlvmArgInfo getLlvmArgInfoForArgIx(unsigned int lclNum);

public:
    Llvm(Compiler* pCompiler);

    void Lower();
    void Compile();

    static void llvmShutdown();
    static bool needsReturnStackSlot(Compiler* compiler, GenTreeCall* callee);
};

#endif

#endif /* End of _LLVM_H_ */
