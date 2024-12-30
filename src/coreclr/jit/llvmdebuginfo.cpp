// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                            DWARF debug info generation for the LLVM backend                                  |
// ================================================================================================================

#include "llvm.h"

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
    CORINFO_LLVM_METHOD_DECL_DEBUG_INFO Decl;
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

static DICompileUnit* CreateCompileUnit(DIBuilder* diBuilder, DIFile* debugFile)
{
    return diBuilder->createCompileUnit(DW_LANG_C_plus_plus, debugFile, "ILC", false, "", 1, "",
        DICompileUnit::FullDebug, 0, false);
}

template <typename TGetTypeFunc>
static DISubprogram* CreateMethodDecl(DIBuilder* diBuilder, CORINFO_LLVM_METHOD_DECL_DEBUG_INFO* pInfo, TGetTypeFunc getType)
{
    DIType* debugOwnerType = getType(pInfo->OwnerType);
    DISubroutineType* debugFuncType = llvm::cast<DISubroutineType>(getType(pInfo->Type));

    DISubprogram* debugDecl = diBuilder->createMethod(debugOwnerType, pInfo->Name, AsRef(pInfo->LinkageName), nullptr, 0,
        debugFuncType, 0, 0, nullptr, DINode::FlagPrototyped);
    return debugDecl;
}

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
    DIFile* debugFile = initializeDebugInfoBuilder(&info);

    // For debug type unification across compile units to work, we need to first declare our methods. This is not
    // really specified anywhere, but it is how C++ DI is emitted and how LLDB expects these things to be shaped.
    DISubprogram* debugDecl = CreateMethodDecl(
        m_diBuilder, &info.Decl, [this](CORINFO_LLVM_DEBUG_TYPE_HANDLE hnd) { return getOrCreateDebugType(hnd); });
    unsigned lineNo = m_lineNumbers[0].LineNumber;
    DISubprogram::DISPFlags funcFlags = DISubprogram::SPFlagDefinition;

    m_diFunction = m_diBuilder->createFunction(nullptr, debugDecl->getName(), debugDecl->getLinkageName(),
        debugFile, lineNo, debugDecl->getType(), 0, DINode::FlagPrototyped, funcFlags, nullptr, debugDecl);

    initializeDebugVariables(&info);

    // TODO-LLVM-EH: debugging in funclets.
    getRootLlvmFunction()->setSubprogram(m_diFunction);
}

DIFile* Llvm::initializeDebugInfoBuilder(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
{
    assert((pInfo->FileName != nullptr) && (pInfo->Directory != nullptr));

    DIFile* debugFile = DIFile::get(m_context->Context, pInfo->FileName, pInfo->Directory);

    DICompileUnit* debugCompileUnit = nullptr;
    m_context->DebugCompileUnitsMap.Lookup(debugFile, &debugCompileUnit);

    m_diBuilder =
        new (_compiler->getAllocator(CMK_DebugInfo)) llvm::DIBuilder(m_context->Module, true, debugCompileUnit);

    if (debugCompileUnit == nullptr)
    {
        debugCompileUnit = CreateCompileUnit(m_diBuilder, debugFile);
        m_context->DebugCompileUnitsMap.Set(debugFile, debugCompileUnit);
    }

    return debugFile;
}

void Llvm::initializeDebugVariables(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
{
    DIFile* debugFile = m_diFunction->getFile();
    for (size_t i = 0; i < pInfo->VariableCount; i++)
    {
        CORINFO_LLVM_VARIABLE_DEBUG_INFO* pVariableInfo = &pInfo->Variables[i];
        DIType* debugType = getOrCreateDebugType(pVariableInfo->Type);
        unsigned num = pVariableInfo->VarNumber;

        llvm::DILocalVariable* debugVariable;
        if (num < m_info->compILargsCount)
        {
            bool isThis = (m_info->compThisArg != BAD_VAR_NUM) && (num == 0);
            DINode::DIFlags flags = isThis ? (DINode::FlagObjectPointer | DINode::FlagArtificial) : DINode::FlagZero;

            debugVariable = m_diBuilder->createParameterVariable(m_diFunction, pVariableInfo->Name, num + 1, debugFile,
                                                                 0, debugType, flags);
        }
        else
        {
            debugVariable = m_diBuilder->createAutoVariable(m_diFunction, pVariableInfo->Name, debugFile, 0, debugType);
        }

        unsigned lclNum = _compiler->compMapILvarNum(num);
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
        DIExpression* debugExpression;
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
            unsigned offset = static_cast<unsigned>(varDsc->GetStackOffset());
            debugExpression = m_diBuilder->createExpression({DW_OP_deref, DW_OP_plus_uconst, offset});
        }
        else if (varDsc->lvRefCnt() != 0)
        {
            addressValue = getLocalAddr(lclNum);
            debugExpression = m_diBuilder->createExpression();
        }
        else
        {
            continue;
        }

        llvm::DILocalVariable* debugVariable = lcl->GetValue();
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
        DILocation* debugLocation = getCurrentOrArtificialDebugLocation();
        Instruction* debugInst;
        if (_builder.GetInsertPoint() == _builder.GetInsertBlock()->end())
        {
            debugInst = m_diBuilder->insertDbgValueIntrinsic(value, debugVariable, m_diBuilder->createExpression(),
                                                             debugLocation, _builder.GetInsertBlock());
        }
        else
        {
            debugInst = m_diBuilder->insertDbgValueIntrinsic(value, debugVariable, m_diBuilder->createExpression(),
                                                             debugLocation, &*_builder.GetInsertPoint());
        }
        DBEXEC(CurrentBlock() == nullptr, JITDUMPEXEC(displayValue(debugInst)));
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

static DIType* CreatePrimitiveType(DIBuilder* m_diBuilder, CorInfoType type)
{
    switch (type)
    {
        case CORINFO_TYPE_VOID:
            return nullptr;
        case CORINFO_TYPE_BOOL:
            return m_diBuilder->createBasicType("bool", 8, DW_ATE_boolean);
        case CORINFO_TYPE_CHAR:
            return m_diBuilder->createBasicType("char16_t", 16, DW_ATE_UTF);
        case CORINFO_TYPE_BYTE:
            return m_diBuilder->createBasicType("signed char", 8, DW_ATE_signed);
        case CORINFO_TYPE_UBYTE:
            return m_diBuilder->createBasicType("unsigned char", 8, DW_ATE_unsigned);
        case CORINFO_TYPE_SHORT:
            return m_diBuilder->createBasicType("short", 16, DW_ATE_signed);
        case CORINFO_TYPE_USHORT:
            return m_diBuilder->createBasicType("unsigned short", 16, DW_ATE_unsigned);
        case CORINFO_TYPE_INT:
            return m_diBuilder->createBasicType("int", 32, DW_ATE_signed);
        case CORINFO_TYPE_UINT:
            return m_diBuilder->createBasicType("unsigned int", 32, DW_ATE_unsigned);
        case CORINFO_TYPE_LONG:
            return m_diBuilder->createBasicType("long long", 64, DW_ATE_signed);
        case CORINFO_TYPE_ULONG:
            return m_diBuilder->createBasicType("unsigned long long", 64, DW_ATE_unsigned);
        case CORINFO_TYPE_NATIVEINT:
            return m_diBuilder->createBasicType("long", TARGET_POINTER_BITS, DW_ATE_signed);
        case CORINFO_TYPE_NATIVEUINT:
            return m_diBuilder->createBasicType("unsigned long", TARGET_POINTER_BITS, DW_ATE_unsigned);
        case CORINFO_TYPE_FLOAT:
            return m_diBuilder->createBasicType("float", 32, DW_ATE_float);
        case CORINFO_TYPE_DOUBLE:
            return m_diBuilder->createBasicType("double", 64, DW_ATE_float);
        default:
            unreached();
    }
}

template <typename TGetTypeFunc>
static DIType* CreatePointerType(DIBuilder* diBuilder, CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO* pInfo, TGetTypeFunc getType)
{
    DIType* debugPointeeType = getType(pInfo->ElementType);
    DIType* debugPointerType;
    if (pInfo->IsReference != 0)
    {
        // Reference to a reference is not valid C++; our target debuggers cannot handle it. Emit reference to
        // a pointer instead.
        if (debugPointeeType->getTag() == DW_TAG_reference_type)
        {
            debugPointeeType = llvm::cast<DIDerivedType>(debugPointeeType)->getBaseType();
            debugPointeeType = diBuilder->createPointerType(debugPointeeType, TARGET_POINTER_BITS);
        }

        debugPointerType =
            diBuilder->createReferenceType(DW_TAG_reference_type, debugPointeeType, TARGET_POINTER_BITS);
    }
    else
    {
        debugPointerType = diBuilder->createPointerType(debugPointeeType, TARGET_POINTER_BITS);
    }

    return debugPointerType;
}

static DIType* CreateForwardType(DIBuilder* diBuilder, CORINFO_LLVM_FORWARD_TYPE_DEBUG_INFO* pInfo)
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
    return diBuilder->createForwardDecl(tag, pInfo->Name, nullptr, nullptr, 0);
}

template <typename TGetTypeFunc, typename TAlloc>
static DIType* CreateFunctionType(
    DIBuilder* diBuilder, CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO* pInfo, TAlloc alloc, TGetTypeFunc getType)
{
    unsigned debugParameterCount = 1 + (pInfo->TypeOfThisPointer != NO_DEBUG_TYPE) + pInfo->NumberOfArguments;
    Metadata** debugParameters = alloc.template allocate<Metadata*>(debugParameterCount);

    size_t index = 0;
    debugParameters[index++] = getType(pInfo->ReturnType);
    if (pInfo->TypeOfThisPointer != NO_DEBUG_TYPE)
    {
        debugParameters[index++] = getType(pInfo->TypeOfThisPointer);
    }
    for (size_t i = 0; i < pInfo->NumberOfArguments; i++)
    {
        debugParameters[index++] = getType(pInfo->ArgumentTypes[i]);
    }
    llvm::DITypeRefArray debugParametersArray =
        diBuilder->getOrCreateTypeArray(ArrayRef(debugParameters, debugParameterCount));
    DIType* debugFuncType = diBuilder->createSubroutineType(debugParametersArray);

    alloc.deallocate(debugParameters);
    return debugFuncType;
}

static DIType* CreateClassType(
    LLVMContext& context, DIBuilder* diBuilder, StringRef name, unsigned size, ArrayRef<Metadata*> elements)
{
    // We create a distinct array here because we may later need to append declared methods to it.
    DINodeArray members = llvm::MDTuple::getDistinct(context, elements);
    DIType* debugType = diBuilder->createClassType(nullptr, name, nullptr, 0, size * BITS_PER_BYTE, 0, 0,
        DINode::FlagZero, nullptr, members);
    return debugType;
}

static DIType* CreateFixedArrayType(DIBuilder* diBuilder, DIType* elementDebugType, unsigned size)
{
    uint64_t sizeInBits = elementDebugType->getSizeInBits() * size;
    llvm::DISubrange* boundsRange = diBuilder->getOrCreateSubrange(0, size);
    DINodeArray boundsArray = diBuilder->getOrCreateArray(boundsRange);
    DIType* debugType =
        diBuilder->createArrayType(sizeInBits, elementDebugType->getAlignInBits(), elementDebugType, boundsArray);

    return debugType;
}

static DIDerivedType* CreateMember(
    DIBuilder* diBuilder, StringRef name, llvm::DIType* debugType, unsigned offset)
{
    return diBuilder->createMemberType(nullptr, name, nullptr, 0, debugType->getSizeInBits(),
        debugType->getAlignInBits(), offset * BITS_PER_BYTE, DINode::FlagZero, debugType);
}

DIType* Llvm::getOrCreateDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle)
{
    DIType** pDebugType = m_context->DebugTypesMap.LookupPointerOrAdd(debugTypeHandle, nullptr);
    if (*pDebugType == nullptr)
    {
        *pDebugType = createDebugType(debugTypeHandle);
    }
    return *pDebugType;
}

DIType* Llvm::createDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle)
{
    CORINFO_LLVM_TYPE_DEBUG_INFO info;
    GetDebugInfoForDebugType(debugTypeHandle, &info);

    switch (info.Kind)
    {
        case CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE:
            return CreatePrimitiveType(m_diBuilder, info.PrimitiveType);
        case CORINFO_LLVM_DEBUG_TYPE_POINTER:
            return CreatePointerType(m_diBuilder, &info.PointerInfo,
                [this](CORINFO_LLVM_DEBUG_TYPE_HANDLE hnd) { return getOrCreateDebugType(hnd); });
        case CORINFO_LLVM_DEBUG_TYPE_FORWARD:
            return CreateForwardType(m_diBuilder, &info.ForwardInfo);
        case CORINFO_LLVM_DEBUG_TYPE_FUNCTION:
            return CreateFunctionType(m_diBuilder, &info.FunctionInfo, _compiler->getAllocator(CMK_DebugInfo),
                [this](CORINFO_LLVM_DEBUG_TYPE_HANDLE hnd) { return getOrCreateDebugType(hnd); });
        default:
            unreached();
    }
}

class TypeDebugInfoModule
{
    SingleThreadedCompilationContext* m_context;
    DIBuilder m_diBuilder;
    jitstd::vector<DIType*, MallocAllocator> m_diTypes;
    unsigned m_declIndex = 0;

public:
    TypeDebugInfoModule(SingleThreadedCompilationContext* context)
        : m_context(context)
        , m_diBuilder(context->Module, /* AllowUnresolved */ true)
        , m_diTypes({})
    {
        m_diTypes.push_back(nullptr);
        DIFile* debugFile = m_diBuilder.createFile(".NET Types", "");
        CreateCompileUnit(&m_diBuilder, debugFile);
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
                debugType = CreatePrimitiveType(&m_diBuilder, pInfo->PrimitiveType);
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
                debugType = CreatePointerType(&m_diBuilder, &pInfo->PointerInfo,
                    [this](CORINFO_LLVM_DEBUG_TYPE_HANDLE hnd) { return GetEmittedType(hnd); });
                break;
            case CORINFO_LLVM_DEBUG_TYPE_FORWARD:
                debugType = CreateForwardType(&m_diBuilder, &pInfo->ForwardInfo);
                break;
            case CORINFO_LLVM_DEBUG_TYPE_FUNCTION:
                debugType = CreateFunctionType(&m_diBuilder, &pInfo->FunctionInfo, MallocAllocator{},
                    [this](CORINFO_LLVM_DEBUG_TYPE_HANDLE hnd) { return GetEmittedType(hnd); });
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
        DISubprogram* debugDecl = CreateMethodDecl(
            &m_diBuilder, pInfo, [this](CORINFO_LLVM_DEBUG_TYPE_HANDLE hnd) { return GetEmittedType(hnd); });

        // We've previously made the elements distinct, hence we can add to them.
        DICompositeType* debugOwnerType = llvm::cast<DICompositeType>(GetEmittedType(pInfo->OwnerType));
        debugOwnerType->getElements()->push_back(debugDecl);
        return ++m_declIndex;
    }

private:
    DIType* GetEmittedType(CORINFO_LLVM_DEBUG_TYPE_HANDLE handle)
    {
        unsigned index = handle;
        assert(index < m_diTypes.size());
        return m_diTypes[index];
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
            DIDerivedType* debugField =
                CreateMember(&m_diBuilder, pFieldInfo->Name, fieldDebugType, pFieldInfo->Offset);
            debugElements.push_back(debugField);
        }

        DIType* debugType =
            CreateClassType(m_context->Context, &m_diBuilder, pInfo->Name, pInfo->Size, debugElements);
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

        DIType* lengthDebugType = CreatePrimitiveType(&m_diBuilder, CORINFO_TYPE_INT);
        DIDerivedType* lengthDebugField =
            CreateMember(&m_diBuilder, "Length", lengthDebugType, OFFSETOF__CORINFO_Array__length);
        members.push_back(lengthDebugField);

        if (isMDArray)
        {
            unsigned lowerBoundsOffset = Compiler::eeGetMDArrayLowerBoundOffset(rank, 0);
            DIType* boundsDebugType = CreateFixedArrayType(&m_diBuilder, lengthDebugType, rank);
            DIDerivedType* lowerBoundsDebugField =
                CreateMember(&m_diBuilder, "LowerBounds", boundsDebugType, lowerBoundsOffset);
            members.push_back(lowerBoundsDebugField);

            unsigned lengthsOffset = Compiler::eeGetMDArrayLengthOffset(rank, 0);
            DIDerivedType* lengthsDebugField = CreateMember(&m_diBuilder, "Lengths", boundsDebugType, lengthsOffset);
            members.push_back(lengthsDebugField);
        }

        unsigned dataOffset = isMDArray ? Compiler::eeGetMDArrayDataOffset(rank) : Compiler::eeGetArrayDataOffset();
        DIType* elementDebugType = GetEmittedType(pInfo->ElementType);
        DIType* dataDebugType = CreateFixedArrayType(&m_diBuilder, elementDebugType, 0);
        DIDerivedType* dataDebugField = CreateMember(&m_diBuilder, "Data", dataDebugType, dataOffset);
        members.push_back(dataDebugField);

        DIType* debugType = CreateClassType(m_context->Context, &m_diBuilder, pInfo->Name, dataOffset, members);
        return debugType;
    }

    void EmitDebugInfoRootFunction()
    {
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

CORINFO_LLVM_DEBUG_TYPE_HANDLE SingleThreadedCompilationContext::EmitDebugTypeInfo(
    SingleThreadedCompilationContext* context, CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo)
{
    if (context->DebugTypes == nullptr)
    {
        context->DebugTypes = new TypeDebugInfoModule(context);
    }
    return context->DebugTypes->EmitType(pInfo);
}

CORINFO_LLVM_DEBUG_METHOD_DECL_HANDLE SingleThreadedCompilationContext::EmitDebugMethodDecl(
    SingleThreadedCompilationContext* context, CORINFO_LLVM_METHOD_DECL_DEBUG_INFO* pInfo)
{
    if (context->DebugTypes == nullptr)
    {
        context->DebugTypes = new TypeDebugInfoModule(context);
    }
    return context->DebugTypes->EmitMethodDecl(pInfo);
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
