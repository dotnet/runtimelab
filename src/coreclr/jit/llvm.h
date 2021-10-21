// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*****************************************************************************/
#ifndef _LLVM_H_
#define _LLVM_H_
#undef __PLACEMENT_NEW_INLINE

#include "alloc.h"
#include "jitpch.h"
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
    int m_argIx; // -1 indicates not in the LLVM arg list, but on the shadow stack
    unsigned int m_shadowStackOffset;
};

struct DebugMetadata
{
    llvm::DIFile* fileMetadata;
    llvm::DICompileUnit* diCompileUnit;
};

// TODO: might need the LLVM Value* in here for exception funclets.
struct SpilledExpressionEntry
{
    CorInfoType m_CorInfoType;
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
                                      const char* (*_getMangledSymbolNamePtr)(void*, void*),
                                      const char* (*addCodeReloc)(void*, void*),
                                      const uint32_t (*isRuntimeImport)(void*, CORINFO_METHOD_STRUCT_*),
                                      const char* (*getDocumentFileName)(void*),
                                      const uint32_t (*firstSequencePointLineNumber)(void*),
                                      const uint32_t (*getOffsetLineNumber)(void*, unsigned int),
                                      const uint32_t (*structIsWrappedPrimitive)(void*, CORINFO_CLASS_STRUCT_*, CorInfoType));


enum class ValueLocation
{
    LlvmStack,
    ShadowStack,
    ForwardReference
};

// Helper for unifying vars that do and don't need to be on the shadow stack.
struct LocatedLlvmValue
{
    Value* llvmValue;
    ValueLocation location;

    LocatedLlvmValue(ValueLocation location, Value* llvmValue) : llvmValue(llvmValue), location(location) {}
    Value* getValue(llvm::IRBuilder<>& builder)
    {
        return valueRequiresLoad() ? builder.CreateLoad(llvmValue) : llvmValue;
    }

    Value* getRawValue()
    {
        return llvmValue;
    }

    bool valueRequiresLoad()
    {
        return location != ValueLocation::LlvmStack;
    }
};

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
    llvm::Function* _function;
    CORINFO_SIG_INFO _sigInfo; // sigInfo of function being compiled
    bool _functionSigHasReturnAddress;
    LIR::Range* _currentRange;
    BasicBlock* _currentBlock;
    IL_OFFSETX _currentOffset;
    BlkToLlvmBlkVectorMap* _blkToLlvmBlkVectorMap;
    llvm::IRBuilder<> _builder;
    llvm::IRBuilder<> _prologBuilder;
    std::unordered_map<GenTree*, LocatedLlvmValue>* _sdsuMap;
    std::unordered_map<SsaPair, LocatedLlvmValue, SsaPairHash>* _localsMap;
    std::unordered_map<SsaPair, IncomingPhi, SsaPairHash>* _forwardReferencingPhis;
    std::vector<PhiPair> _phiPairs;

    // DWARF
    llvm::DILocation* _currentOffsetDiLocation;
    llvm::DISubprogram* _debugFunction;
    DebugMetadata  _debugMetadata;
    std::unordered_map<std::string, struct DebugMetadata> _debugMetadataMap;

    std::vector<SpilledExpressionEntry> _spilledExpressions;
    unsigned _shadowStackLocalsSize;
    unsigned _shadowStackLclNum;
    unsigned _retAddressLclNum;

    inline LIR::Range& CurrentRange()
    {
        return *_currentRange;
    }

    void addForwardPhiArg(SsaPair ssaPair, llvm::Value* phiArg);
    void buildAdd(GenTree* node, Value* op1, Value* op2);
    void buildCall(GenTree* node);
    void buildCast(GenTreeCast* cast);
    void buildCmp(genTreeOps op, GenTree* node, Value* op1, Value* op2);
    void buildCnsDouble(GenTreeDblCon* node);
    void buildCnsInt(GenTree* node);
    void buildInd(GenTree* node, Value* ptr);
    Value* buildJTrue(GenTree* node, Value* opValue);
    void buildEmptyPhi(GenTreePhi* phi);
    void buildReturn(GenTree* node);
    void buildReturnRef(GenTreeOp* node);
    Value* buildUserFuncCall(GenTreeCall* call);
    bool canStoreArgOnLlvmStack(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);
    Value* castIfNecessary(Value* source, Type* targetType);
    void castingStore(Value* toStore, Value* address, llvm::Type* llvmType);
    void castingStore(Value* toStore, Value* address, var_types type);
    Value* castToPointerToLlvmType(Value* address, llvm::Type* llvmType);
    llvm::DILocation* createDebugFunctionAndDiLocation(struct DebugMetadata debugMetadata, unsigned int lineNo);
    void CreateShadowStackLocalAddress(GenTree* node);
    void emitDoNothingCall();
    void endImportingBasicBlock(BasicBlock* block);
    void failFunctionCompilation();
    void fillPhis();
    llvm::Instruction* getCast(llvm::Value* source, Type* targetType);
    void generateProlog();
    CorInfoType getCorInfoTypeForArg(CORINFO_SIG_INFO& sigInfo, CORINFO_ARG_LIST_HANDLE& arg, CORINFO_CLASS_HANDLE* clsHnd);
    llvm::FunctionType* getFunctionTypeForSigInfo(CORINFO_SIG_INFO& sigInfo);
    LocatedLlvmValue getGenTreeValue(GenTree* node);
    LlvmArgInfo getLlvmArgInfoForArgIx(CORINFO_SIG_INFO& sigInfo, unsigned int lclNum);
    llvm::BasicBlock* getLLVMBasicBlockForBlock(BasicBlock* block);
    Type* getLlvmTypeForCorInfoType(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);
    Type* getLlvmTypeForStruct(CORINFO_CLASS_HANDLE structHandle);
    Type* getLLVMTypeForVarType(var_types type);
    int getLocalOffsetAtIndex(GenTreeLclVar* lclVar);
    Value* getLocalVarAddress(GenTreeLclVar* lclVar);
    struct DebugMetadata getOrCreateDebugMetadata(const char* documentFileName);
    Value* getShadowStackForCallee();
    unsigned int getSpillOffsetAtIndex(unsigned int index, unsigned int offset);
    LocatedLlvmValue getSsaLocalForPhi(unsigned lclNum, unsigned ssaNum);
    Value* getShadowStackOffest(Value* shadowStack, unsigned int offset);
    unsigned int getTotalRealLocalOffset();
    Value* genTreeAsLlvmType(GenTree* tree, Type* type);
    unsigned getElementSize(CORINFO_CLASS_HANDLE fieldClassHandle, CorInfoType corInfoType);
    unsigned int getTotalLocalOffset();
    void importStoreInd(GenTreeStoreInd* storeIndOp);
    bool isThisArg(GenTreeCall* call, GenTree* operand);
    Value* localVar(GenTreeLclVar* lclVar);
    Value* mapGenTreeToValue(GenTree* genTree, ValueLocation valueLocation, Value* valueRef);
    void mapGenTreeToValue(GenTree* genTree, Value* valueRef);
    bool needsReturnStackSlot(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd);
    unsigned int padNextOffset(CorInfoType corInfoType, unsigned int atOffset);
    unsigned int padOffset(CorInfoType corInfoType, unsigned int atOffset);
    void startImportingBasicBlock(BasicBlock* block);
    void startImportingNode();
    void storeOnShadowStack(GenTree* operand, Value* shadowStackForCallee, unsigned int offset);
    void storeLocalVar(GenTreeLclVar* lclVar);
    CorInfoType toCorInfoType(var_types varType);
    void visitNode(GenTree* node);
    Value* Llvm::zextIntIfNecessary(Value* intValue);

public:
    Llvm(Compiler* pCompiler);

    static void llvmShutdown();


    void ConvertShadowStackLocals();
    void PlaceAndConvertShadowStackLocals();
    void Compile();
};

#endif

#endif /* End of _LLVM_H_ */
