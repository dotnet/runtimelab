// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                            DWARF debug info generation for the LLVM backend                                  |
// ================================================================================================================

#include "llvm.h"

// TODO-LLVM-Upstream: figure out how to fix these warnings in LLVM headers.
#pragma warning(push)
#pragma warning(disable : 4242)
#pragma warning(disable : 4244)
#pragma warning(disable : 4267)
#include "llvm/IR/DIBuilder.h"
#pragma warning(pop)

using namespace llvm::dwarf;

using llvm::DIBuilder;
using llvm::Metadata;
using llvm::DINode;
using llvm::DINodeArray;
using llvm::DICompileUnit;
using llvm::DIFile;
using llvm::DIType;
using llvm::DICompositeType;
using llvm::DIDerivedType;
using llvm::DISubroutineType;
using llvm::DISubprogram;
using llvm::DILocation;
using llvm::DIExpression;

struct CORINFO_LLVM_STRING
{
    const char* Data;
    size_t Length;
};

struct CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO
{
    const char* Name;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
    unsigned Offset;
};

struct CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO
{
    const char* Name;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
    const char* BaseSymbolName;
    unsigned StaticOffset;
    int IsStaticDataInObject;
};

struct CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO
{
    const char* Name;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE BaseClass;
    unsigned Size;

    unsigned InstanceFieldCount;
    CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO* InstanceFields;

    unsigned StaticFieldCount;
    CORINFO_LLVM_STATIC_FIELD_DEBUG_INFO* StaticFields;
};

struct CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO
{
    const char* Name;
    unsigned long long Value;
};

struct CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO
{
    const char* Name;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
    unsigned long long ElementCount;
    CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO* Elements;
};

struct CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO
{
    const char* Name;
    unsigned Rank;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
    int IsMultiDimensional;
};

struct CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO
{
    CORINFO_LLVM_DEBUG_TYPE_HANDLE ElementType;
    int IsReference;
};

struct CORINFO_LLVM_FORWARD_TYPE_DEBUG_INFO
{
    const char* Name;
    CorInfoLlvmDebugTypeForwardKind Kind;
};

struct CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO
{
    CORINFO_LLVM_DEBUG_TYPE_HANDLE TypeOfThisPointer;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE ReturnType;
    unsigned NumberOfArguments;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE* ArgumentTypes;
};

struct CORINFO_LLVM_TYPE_DEBUG_INFO
{
    CorInfoLlvmDebugTypeKind Kind;

    union
    {
        CorInfoType PrimitiveType;
        CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO CompositeInfo;
        CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO EnumInfo;
        CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO ArrayInfo;
        CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO PointerInfo;
        CORINFO_LLVM_FORWARD_TYPE_DEBUG_INFO ForwardInfo;
        CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO FunctionInfo;
    };
};

struct CORINFO_LLVM_VARIABLE_DEBUG_INFO
{
    const char* Name;
    unsigned VarNumber;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
};

struct CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO
{
    unsigned ILOffset;
    unsigned LineNumber;
};

struct CORINFO_LLVM_METHOD_DECL_DEBUG_INFO
{
    const char* Name;
    CORINFO_LLVM_STRING LinkageName;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE OwnerType;
};

struct CORINFO_LLVM_METHOD_DEBUG_INFO
{
    CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE Decl;
    const char* Directory;
    const char* FileName;
    unsigned LineNumberCount;
    CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO* SortedLineNumbers;
    unsigned VariableCount;
    CORINFO_LLVM_VARIABLE_DEBUG_INFO* Variables;
};

static StringRef AsRef(CORINFO_LLVM_STRING string)
{
    return StringRef(string.Data, string.Length);
}

class TypeDebugInfoModule
{
    SingleThreadedCompilationContext* m_context;
    DIBuilder m_diBuilder;
    jitstd::vector<DIType*, MallocAllocator> m_diTypes;
    DISubprogram* m_lastEmittedMethodDecl = nullptr;
    unsigned m_lastEmittedDeclIndex = 0;

public:
    TypeDebugInfoModule(SingleThreadedCompilationContext* context)
        : m_context(context)
        , m_diBuilder(context->Module, /* AllowUnresolved */ true)
        , m_diTypes({})
    {
        m_diTypes.push_back(nullptr);
        InitializeCompileUnit();
    }

    DIBuilder* GetDIBuilder()
    {
        return &m_diBuilder;
    }

    void Finish()
    {
        EmitDebugInfoRootFunction();
        m_diBuilder.finalize();
    }

    CORINFO_LLVM_DEBUG_TYPE_HANDLE EmitType(CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo)
    {
        DIType* debugType;
        switch (pInfo->Kind)
        {
            case CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE:
                debugType = EmitPrimitiveType(pInfo->PrimitiveType);
                break;
            case CORINFO_LLVM_DEBUG_TYPE_COMPOSITE:
                debugType = EmitCompositeType(&pInfo->CompositeInfo);
                break;
            case CORINFO_LLVM_DEBUG_TYPE_ENUM:
                debugType = EmitEnumType(&pInfo->EnumInfo);
                break;
            case CORINFO_LLVM_DEBUG_TYPE_ARRAY:
                debugType = EmitArrayType(&pInfo->ArrayInfo);
                break;
            case CORINFO_LLVM_DEBUG_TYPE_POINTER:
                debugType = EmitPointerType(&pInfo->PointerInfo);
                break;
            case CORINFO_LLVM_DEBUG_TYPE_FORWARD:
                debugType = EmitForwardType(&pInfo->ForwardInfo);
                break;
            case CORINFO_LLVM_DEBUG_TYPE_FUNCTION:
                debugType = EmitFunctionType(&pInfo->FunctionInfo);
                break;
            default:
                unreached();
        }

        unsigned index = static_cast<unsigned>(m_diTypes.size());
        m_diTypes.push_back(debugType);
        return index;
    }

    CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE EmitMethodDecl(CORINFO_LLVM_METHOD_DECL_DEBUG_INFO* pInfo)
    {
        DICompositeType* debugOwnerType = llvm::cast<DICompositeType>(GetEmittedType(pInfo->OwnerType));
        DISubroutineType* debugFuncType = llvm::cast<DISubroutineType>(GetEmittedType(pInfo->Type));
        llvm::DITypeRefArray debugFuncParamTypes = debugFuncType->getTypeArray();

        DINode::DIFlags diFlags = DINode::FlagPrototyped;
        if (debugFuncParamTypes.size() < 2 || !debugFuncParamTypes[1]->isObjectPointer())
        {
            diFlags |= DINode::FlagStaticMember;
        }
        DISubprogram* debugDecl = m_diBuilder.createMethod(debugOwnerType, pInfo->Name, AsRef(pInfo->LinkageName),
            nullptr, 0, debugFuncType, 0, 0, nullptr, diFlags);

        if (!debugOwnerType->isForwardDecl())
        {
            // We've previously made the elements distinct, hence we can add to them.
            debugOwnerType->getElements()->push_back(debugDecl);
        }
        m_lastEmittedMethodDecl = debugDecl;
        return ++m_lastEmittedDeclIndex;
    }

    DIType* GetEmittedType(CORINFO_LLVM_DEBUG_TYPE_HANDLE handle)
    {
        unsigned index = handle;
        assert(index < m_diTypes.size());
        return m_diTypes[index];
    }

    DISubprogram* GetEmittedMethodDecl(CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE handle)
    {
        // Our current users do not need decls other than the last one. Once/if
        // that changes, we can store them in an array like we do with types.
        assert(handle == m_lastEmittedDeclIndex);
        assert(m_lastEmittedMethodDecl != nullptr);
        return m_lastEmittedMethodDecl;
    }

private:
    DICompileUnit* InitializeCompileUnit()
    {
        StringRef path = m_context->Module.getName();
        StringRef name;
        size_t dirEnd = path.find_last_of("\\/");
        if (dirEnd != StringRef::npos)
        {
            name = path.substr(dirEnd + 1);
        }
        else
        {
            name = path;
        }

        DIFile* debugFile = m_diBuilder.createFile(name, "");
        return m_diBuilder.createCompileUnit(DW_LANG_C_plus_plus, debugFile, "ILC", false, "", 1, "",
            DICompileUnit::FullDebug, 0, false);
    }

    DIType* EmitPrimitiveType(CorInfoType type)
    {
        switch (type)
        {
            case CORINFO_TYPE_VOID:
                return nullptr;
            case CORINFO_TYPE_BOOL:
                return m_diBuilder.createBasicType("bool", 8, DW_ATE_boolean);
            case CORINFO_TYPE_CHAR:
                return m_diBuilder.createBasicType("char16_t", 16, DW_ATE_UTF);
            case CORINFO_TYPE_BYTE:
                return m_diBuilder.createBasicType("signed char", 8, DW_ATE_signed);
            case CORINFO_TYPE_UBYTE:
                return m_diBuilder.createBasicType("unsigned char", 8, DW_ATE_unsigned);
            case CORINFO_TYPE_SHORT:
                return m_diBuilder.createBasicType("short", 16, DW_ATE_signed);
            case CORINFO_TYPE_USHORT:
                return m_diBuilder.createBasicType("unsigned short", 16, DW_ATE_unsigned);
            case CORINFO_TYPE_INT:
                return m_diBuilder.createBasicType("int", 32, DW_ATE_signed);
            case CORINFO_TYPE_UINT:
                return m_diBuilder.createBasicType("unsigned int", 32, DW_ATE_unsigned);
            case CORINFO_TYPE_LONG:
                return m_diBuilder.createBasicType("long long", 64, DW_ATE_signed);
            case CORINFO_TYPE_ULONG:
                return m_diBuilder.createBasicType("unsigned long long", 64, DW_ATE_unsigned);
            case CORINFO_TYPE_NATIVEINT:
                return m_diBuilder.createBasicType("long", TARGET_POINTER_BITS, DW_ATE_signed);
            case CORINFO_TYPE_NATIVEUINT:
                return m_diBuilder.createBasicType("unsigned long", TARGET_POINTER_BITS, DW_ATE_unsigned);
            case CORINFO_TYPE_FLOAT:
                return m_diBuilder.createBasicType("float", 32, DW_ATE_float);
            case CORINFO_TYPE_DOUBLE:
                return m_diBuilder.createBasicType("double", 64, DW_ATE_float);
            default:
                unreached();
        }
    }

    DIType* EmitCompositeType(CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO* pInfo)
    {
        // Forward-declare our structure to handle inheritance.
        llvm::TempDIType declType = llvm::TempDIType(
            m_diBuilder.createReplaceableCompositeType(DW_TAG_structure_type, "", nullptr, nullptr, 0));
        unsigned debugElementsCount = (pInfo->BaseClass != NO_DEBUG_TYPE) + pInfo->InstanceFieldCount;
        llvm::SmallVector<Metadata*> debugElements(debugElementsCount);
        if (pInfo->BaseClass != NO_DEBUG_TYPE)
        {
            DIType* baseDebugType = GetEmittedType(pInfo->BaseClass);
            DIDerivedType* inheritance =
                m_diBuilder.createInheritance(declType.get(), baseDebugType, 0, 0, DINode::FlagZero);
            debugElements.push_back(inheritance);
        }

        for (size_t i = 0; i < pInfo->InstanceFieldCount; i++)
        {
            CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO* pFieldInfo = &pInfo->InstanceFields[i];
            DIType* fieldDebugType = GetEmittedType(pFieldInfo->Type);
            DIDerivedType* debugField = CreateMember(pFieldInfo->Name, fieldDebugType, pFieldInfo->Offset);
            debugElements.push_back(debugField);
        }

        DIType* debugType = EmitClassTypeWithMembers(pInfo->Name, pInfo->Size, debugElements);
        m_diBuilder.replaceTemporary(std::move(declType), debugType);

        // TODO-LLVM-DI: static fields.
        return debugType;
    }

    DIType* EmitEnumType(CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO* pInfo)
    {
        llvm::SmallVector<Metadata*, 24> elements(static_cast<size_t>(pInfo->ElementCount));
        for (size_t i = 0; i < pInfo->ElementCount; i++)
        {
            CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO* pElementInfo = &pInfo->Elements[i];
            llvm::DIEnumerator* element = m_diBuilder.createEnumerator(pElementInfo->Name, pElementInfo->Value);
            elements.push_back(element);
        }

        DINodeArray elementsArray = m_diBuilder.getOrCreateArray(elements);
        DIType* underlyingDebugType = GetEmittedType(pInfo->ElementType);
        DIType* enumDebugType =
            m_diBuilder.createEnumerationType(nullptr, pInfo->Name, nullptr, 0,
                underlyingDebugType->getSizeInBits(), underlyingDebugType->getAlignInBits(),
                elementsArray, underlyingDebugType);
        return enumDebugType;
    }

    DIType* EmitArrayType(CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO* pInfo)
    {
        // Array layout: [void* m_pEEType, int32 Length, [int32 padding on 64 bit], <bounds>, Data].
        // Where <bounds> (for an MD array) is an array of [LowerBound..., Length...].
        unsigned rank = pInfo->Rank;
        bool isMDArray = pInfo->IsMultiDimensional != 0;
        llvm::SmallVector<Metadata*> members;

        DIType* lengthDebugType = EmitPrimitiveType(CORINFO_TYPE_INT);
        DIDerivedType* lengthDebugField = CreateMember("Length", lengthDebugType, OFFSETOF__CORINFO_Array__length);
        members.push_back(lengthDebugField);

        if (isMDArray)
        {
            unsigned lowerBoundsOffset = Compiler::eeGetMDArrayLowerBoundOffset(rank, 0);
            DIType* boundsDebugType = CreateFixedArrayType(lengthDebugType, rank);
            DIDerivedType* lowerBoundsDebugField = CreateMember("LowerBounds", boundsDebugType, lowerBoundsOffset);
            members.push_back(lowerBoundsDebugField);

            unsigned lengthsOffset = Compiler::eeGetMDArrayLengthOffset(rank, 0);
            DIDerivedType* lengthsDebugField = CreateMember("Lengths", boundsDebugType, lengthsOffset);
            members.push_back(lengthsDebugField);
        }

        unsigned dataOffset = isMDArray ? Compiler::eeGetMDArrayDataOffset(rank) : Compiler::eeGetArrayDataOffset();
        DIType* elementDebugType = GetEmittedType(pInfo->ElementType);
        DIType* dataDebugType = CreateFixedArrayType(elementDebugType, 0);
        DIDerivedType* dataDebugField = CreateMember("Data", dataDebugType, dataOffset);
        members.push_back(dataDebugField);

        DIType* debugType = EmitClassTypeWithMembers(pInfo->Name, dataOffset, members);
        return debugType;
    }

    DIType* EmitPointerType(CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO* pInfo)
    {
        DIType* debugPointeeType = GetEmittedType(pInfo->ElementType);
        DIType* debugPointerType;
        if (pInfo->IsReference != 0)
        {
            // Reference to a reference is not valid C++; our target debuggers cannot handle it. Emit reference to
            // a pointer instead.
            if (debugPointeeType->getTag() == DW_TAG_reference_type)
            {
                debugPointeeType = llvm::cast<DIDerivedType>(debugPointeeType)->getBaseType();
                debugPointeeType = m_diBuilder.createPointerType(debugPointeeType, TARGET_POINTER_BITS);
            }

            debugPointerType =
                m_diBuilder.createReferenceType(DW_TAG_reference_type, debugPointeeType, TARGET_POINTER_BITS);
        }
        else
        {
            debugPointerType = m_diBuilder.createPointerType(debugPointeeType, TARGET_POINTER_BITS);
        }

        return debugPointerType;
    }

    DIType* EmitForwardType(CORINFO_LLVM_FORWARD_TYPE_DEBUG_INFO* pInfo)
    {
        unsigned tag;
        switch (pInfo->Kind)
        {
            case CORINFO_LLVM_DEBUG_TYPE_FORWARD_STRUCT:
                tag = DW_TAG_structure_type;
                break;
            case CORINFO_LLVM_DEBUG_TYPE_FORWARD_ENUM:
                tag = DW_TAG_enumeration_type;
                break;
            default:
                unreached();
        }
        return m_diBuilder.createForwardDecl(tag, pInfo->Name, nullptr, nullptr, 0);
    }

    DIType* EmitFunctionType(CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO* pInfo)
    {
        llvm::SmallVector<Metadata*> debugParameters;
        debugParameters.push_back(GetEmittedType(pInfo->ReturnType));
        if (pInfo->TypeOfThisPointer != NO_DEBUG_TYPE)
        {
            DIType* objPtrType = GetEmittedType(pInfo->TypeOfThisPointer);
            debugParameters.push_back(m_diBuilder.createObjectPointerType(objPtrType));
        }
        for (size_t i = 0; i < pInfo->NumberOfArguments; i++)
        {
            debugParameters.push_back(GetEmittedType(pInfo->ArgumentTypes[i]));
        }

        llvm::DITypeRefArray debugParametersArray = m_diBuilder.getOrCreateTypeArray(debugParameters);
        DIType* debugFuncType = m_diBuilder.createSubroutineType(debugParametersArray);
        return debugFuncType;
    }

    DIType* EmitClassTypeWithMembers(StringRef name, unsigned size, ArrayRef<Metadata*> elements)
    {
        // We create a distinct array here because we may later need to append declared methods to it.
        DINodeArray members = llvm::MDTuple::getDistinct(m_context->Context, elements);
        DIType* debugType = m_diBuilder.createClassType(nullptr, name, nullptr, 0, size * BITS_PER_BYTE, 0, 0,
            DINode::FlagZero, nullptr, members);
        return debugType;
    }

    DIDerivedType* CreateMember(StringRef name, llvm::DIType* debugType, unsigned offset)
    {
        return m_diBuilder.createMemberType(nullptr, name, nullptr, 0, debugType->getSizeInBits(),
            debugType->getAlignInBits(), offset * BITS_PER_BYTE, DINode::FlagZero, debugType);
    }

    DIType* CreateFixedArrayType(DIType* elementDebugType, unsigned size)
    {
        uint64_t sizeInBits = elementDebugType->getSizeInBits() * size;
        llvm::DISubrange* boundsRange = m_diBuilder.getOrCreateSubrange(0, size);
        DINodeArray boundsArray = m_diBuilder.getOrCreateArray(boundsRange);
        DIType* debugType =
            m_diBuilder.createArrayType(sizeInBits, elementDebugType->getAlignInBits(), elementDebugType, boundsArray);

        return debugType;
    }

    void EmitDebugInfoRootFunction()
    {
        if ((m_context->Flags & CORINFO_LLVM_STCC_ROOT_DEBUG_TYPES) == 0)
        {
            // This is a code module, all the necessary types are already referenced.
            return;
        }

        // Create our no-op defined function.
        Type* llvmPtrType = llvm::PointerType::getUnqual(m_context->Context);
        FunctionType* llvmFuncType = FunctionType::get(llvmPtrType, /* isVarArg */ true);
        Function* llvmFunc = Function::Create(
            llvmFuncType, llvm::GlobalValue::ExternalLinkage, "dotnet_debug_types_root", m_context->Module);
        llvm::BasicBlock* llvmBlock = llvm::BasicBlock::Create(m_context->Context, "", llvmFunc);
        llvm::ReturnInst::Create(m_context->Context, llvm::Constant::getNullValue(llvmPtrType), llvmBlock);

        // Make sure it's preserved by the linker.
        llvm::ArrayType* usedType = llvm::ArrayType::get(llvmFunc->getType(), 1);
        llvm::Constant* usedValue = llvm::ConstantArray::get(usedType, {llvmFunc});
        new llvm::GlobalVariable(m_context->Module, usedType, true, llvm::GlobalValue::AppendingLinkage, usedValue, "llvm.used");

        // Now for the last step - pretend it's got a huge function pointer containing all of the types we've emitted.
        // This way, our consumers (wasmtime's DI GC) will preserve the types. This is a workaround which wouldn't be
        // needed had LLVM had a way to emit DWARF's DW_FORM_ref_addr relocations directly. Alas, we have to rely on
        // forward declarations, which won't be resolved by the simple-minded GC algorithm in wasmtime.
        // TODO-LLVM-DI: make this into a linked list of some kind to avoid algorithmic problems in consumers.
        llvm::DITypeRefArray diRootedTypes = llvm::DINode::getDistinct(m_context->Context, {nullptr});
        for (int i = 0; i < m_diTypes.size(); i++)
        {
            DIType* diType = m_diTypes[i];
            if ((diType != nullptr) &&
                (diType->getTag() == DW_TAG_structure_type || diType->getTag() == DW_TAG_enumeration_type) &&
                !diType->isForwardDecl())
            {
                // We could optimize this by only including roots of strongly connected components.
                diRootedTypes->push_back(diType);
            }
        }
        DIType* diRootTypes = m_diBuilder.createSubroutineType(diRootedTypes);
        diRootTypes = m_diBuilder.createPointerType(diRootTypes, TARGET_POINTER_BITS);

        DISubroutineType* debugFuncType = m_diBuilder.createSubroutineType(m_diBuilder.getOrCreateTypeArray({diRootTypes}));
        DISubprogram* debugFunc = m_diBuilder.createFunction(
            nullptr, llvmFunc->getName(), "", nullptr, 0, debugFuncType, 0, DINode::FlagZero, DISubprogram::SPFlagDefinition);
        llvmFunc->setSubprogram(debugFunc);
    }
};

void Llvm::initializeDebugInfo()
{
    if (!_compiler->opts.compDbgInfo)
    {
        return;
    }

    CORINFO_LLVM_METHOD_DEBUG_INFO info;
    GetDebugInfoForCurrentMethod(&info);

    if (info.FileName == nullptr || info.LineNumberCount == 0)
    {
        return;
    }

    assert(info.SortedLineNumbers != nullptr);
    m_lineNumberCount = info.LineNumberCount;
    m_lineNumbers = info.SortedLineNumbers;

    TypeDebugInfoModule* debugTypes = m_context->GetDebugTypes();
    m_diBuilder = debugTypes->GetDIBuilder();

    // For debug type unification across compile units to work, we need to first declare our methods. This is not
    // really specified anywhere, but it is how C++ DI is emitted and how LLDB expects these things to be shaped.
    DISubprogram* debugDecl = debugTypes->GetEmittedMethodDecl(info.Decl);
    unsigned lineNo = m_lineNumbers[0].LineNumber;
    DISubprogram::DISPFlags funcFlags = DISubprogram::SPFlagDefinition;

    DIFile* debugFile = m_diBuilder->createFile(info.FileName, info.Directory);
    m_diFunction = m_diBuilder->createFunction(nullptr, debugDecl->getName(), debugDecl->getLinkageName(),
        debugFile, lineNo, debugDecl->getType(), 0, debugDecl->getFlags(), funcFlags, nullptr, debugDecl);

    initializeDebugVariables(&info);

    // TODO-LLVM-EH: debugging in funclets.
    getRootLlvmFunction()->setSubprogram(m_diFunction);
}

void Llvm::initializeDebugVariables(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
{
    DIFile* debugFile = m_diFunction->getFile();
    TypeDebugInfoModule* debugTypes = m_context->GetDebugTypes();
    for (size_t i = 0; i < pInfo->VariableCount; i++)
    {
        CORINFO_LLVM_VARIABLE_DEBUG_INFO* pVariableInfo = &pInfo->Variables[i];
        DIType* debugType = debugTypes->GetEmittedType(pVariableInfo->Type);
        unsigned num = pVariableInfo->VarNumber;
        unsigned lclNum = _compiler->compMapILvarNum(num);

        llvm::DILocalVariable* debugVariable;
        if (num < m_info->compILargsCount)
        {
            bool isThis = m_info->compThisArg == lclNum;
            DINode::DIFlags flags = isThis ? (DINode::FlagObjectPointer | DINode::FlagArtificial) : DINode::FlagZero;

            debugVariable = m_diBuilder->createParameterVariable(m_diFunction, pVariableInfo->Name, num + 1, debugFile,
                                                                 0, debugType, false, flags);
        }
        else
        {
            debugVariable = m_diBuilder->createAutoVariable(m_diFunction, pVariableInfo->Name, debugFile, 0, debugType);
        }
        m_debugVariablesMap.Set(lclNum, debugVariable);
    }
}

void Llvm::declareDebugVariables()
{
    // We only expect to declare variables in prologs.
    assert(_builder.getCurrentDebugLocation().get() == nullptr);

    if (m_diFunction == nullptr)
    {
        return;
    }

    DILocation* debugLocation = getArtificialDebugLocation();
    Instruction* insertInst = _builder.GetInsertBlock()->getTerminator();
    Value* spilledShadowStackAddr = nullptr;
    for (auto lcl : decltype(m_debugVariablesMap)::KeyValueIteration(&m_debugVariablesMap))
    {
        unsigned lclNum = lcl->GetKey();
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);
        if (_compiler->lvaInSsa(lclNum))
        {
            // Managed with "assignDebugVariable".
            continue;
        }

        Value* addressValue;
        ArrayStack<uint64_t> diExpression(_compiler->getAllocator(CMK_DebugInfo));
        if (isShadowFrameLocal(varDsc))
        {
            // The obvious way to implement this (by just passing the shadow stack to dbg.declare) does not
            // work due to downstream issues. We use a workaround of spilling the shadow stack to an alloca.
            if (spilledShadowStackAddr == nullptr)
            {
                spilledShadowStackAddr = _builder.CreateAlloca(getPtrLlvmType());
                JITDUMPEXEC(displayValue(spilledShadowStackAddr));
                Instruction* storeInst = _builder.CreateStore(getShadowStack(), spilledShadowStackAddr);
                JITDUMPEXEC(displayValue(storeInst));
            }

            addressValue = spilledShadowStackAddr;
            diExpression.Push(DW_OP_deref);
            diExpression.Push(DW_OP_plus_uconst);
            diExpression.Push(varDsc->GetStackOffset());
        }
        else if (varDsc->lvRefCnt() != 0)
        {
            addressValue = getLocalAddr(lclNum);
        }
        else
        {
            continue;
        }
        if (_compiler->lvaIsImplicitByRefLocal(lclNum))
        {
            diExpression.Push(DW_OP_deref);
        }

        llvm::DILocalVariable* debugVariable = lcl->GetValue();
        DIExpression* debugExpression = m_diBuilder->createExpression(AsRef(diExpression));
        Instruction* debugInst =
            m_diBuilder->insertDeclare(addressValue, debugVariable, debugExpression, debugLocation, insertInst);
        JITDUMP("Declaring V%02u:\n", lclNum);
        JITDUMPEXEC(displayValue(debugInst));
    }
}

void Llvm::assignDebugVariable(unsigned lclNum, Value* value)
{
    assert(_compiler->lvaInSsa(lclNum));

    llvm::DILocalVariable* debugVariable;
    if (m_debugVariablesMap.Lookup(lclNum, &debugVariable))
    {
        DIExpression* diExpression = _compiler->lvaIsImplicitByRefLocal(lclNum)
            ? m_diBuilder->createExpression({DW_OP_deref})
            : m_diBuilder->createExpression();

        DILocation* debugLocation = getCurrentOrArtificialDebugLocation();
        Instruction* debugInst;
        if (_builder.GetInsertPoint() == _builder.GetInsertBlock()->end())
        {
            debugInst = m_diBuilder->insertDbgValueIntrinsic(
                value, debugVariable, diExpression, debugLocation, _builder.GetInsertBlock());
        }
        else
        {
            debugInst = m_diBuilder->insertDbgValueIntrinsic(
                value, debugVariable, diExpression, debugLocation, &*_builder.GetInsertPoint());
        }
        DBEXEC(CurrentBlock() == nullptr, JITDUMPEXEC(displayValue(debugInst)));
    }
}

void Llvm::finalizeDebugInfo()
{
    if (m_diFunction != nullptr)
    {
        m_diBuilder->finalizeSubprogram(m_diFunction);
    }
}

unsigned Llvm::getLineNumberForILOffset(unsigned ilOffset)
{
    // The line number array we have is sorted; we'll use a blend of binary and linear search to find the mapping.
    const int LINEAR_SEARCH_THRESHOLD = 8;

    unsigned lowIndex = 0;
    unsigned highIndex = m_lineNumberCount;
    while ((highIndex - lowIndex) > LINEAR_SEARCH_THRESHOLD)
    {
        unsigned middleIndex = (lowIndex + highIndex) / 2;
        if (ilOffset < m_lineNumbers[middleIndex].ILOffset)
        {
            highIndex = middleIndex;
        }
        else
        {
            lowIndex = middleIndex;
        }
    }

    unsigned lineNumber = m_lineNumbers[lowIndex].LineNumber;
    for (unsigned index = lowIndex; index < highIndex; index++)
    {
        if (ilOffset < m_lineNumbers[index].ILOffset)
        {
            break;
        }

        lineNumber = m_lineNumbers[index].LineNumber;
    }

    return lineNumber;
}

DILocation* Llvm::getDebugLocation(unsigned lineNo)
{
    assert(m_diFunction != nullptr);
    return DILocation::get(m_context->Context, lineNo, 0, m_diFunction);
}

DILocation* Llvm::getArtificialDebugLocation()
{
    if (m_diFunction == nullptr)
    {
        return nullptr;
    }

    // Line number "0" is used to represent non-user code in DWARF.
    return getDebugLocation(0);
}

DILocation* Llvm::getCurrentOrArtificialDebugLocation()
{
    DILocation* debugLocation = _builder.getCurrentDebugLocation();
    if (debugLocation == nullptr)
    {
        debugLocation = getArtificialDebugLocation();
    }

    return debugLocation;
}

CORINFO_LLVM_DEBUG_TYPE_HANDLE SingleThreadedCompilationContext::EmitDebugTypeInfo(
    SingleThreadedCompilationContext* context, CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo)
{
    return context->GetDebugTypes()->EmitType(pInfo);
}

CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE SingleThreadedCompilationContext::EmitDebugMethodDecl(
    SingleThreadedCompilationContext* context, CORINFO_LLVM_METHOD_DECL_DEBUG_INFO* pInfo)
{
    return context->GetDebugTypes()->EmitMethodDecl(pInfo);
}

TypeDebugInfoModule* SingleThreadedCompilationContext::GetDebugTypes()
{
    if (DebugTypes == nullptr)
    {
        DebugTypes = new TypeDebugInfoModule(this);
    }
    return DebugTypes;
}

void SingleThreadedCompilationContext::FinishDebugInfo()
{
    if (DebugTypes != nullptr)
    {
        DebugTypes->Finish();
        delete DebugTypes;
        DebugTypes = nullptr;
    }
}
