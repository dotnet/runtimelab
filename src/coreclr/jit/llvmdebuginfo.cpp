// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                            DWARF debug info generation for the LLVM backend                                  |
// ================================================================================================================

#include "llvm.h"

using namespace llvm::dwarf;

using llvm::Metadata;
using llvm::DINode;
using llvm::DINodeArray;
using llvm::DIFile;
using llvm::DIType;
using llvm::DIDerivedType;
using llvm::DISubroutineType;
using llvm::DILocation;
using llvm::DIExpression;

enum CorInfoLlvmDebugTypeKind
{
    CORINFO_LLVM_DEBUG_TYPE_UNDEF,
    CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE,
    CORINFO_LLVM_DEBUG_TYPE_COMPOSITE,
    CORINFO_LLVM_DEBUG_TYPE_ENUM,
    CORINFO_LLVM_DEBUG_TYPE_ARRAY,
    CORINFO_LLVM_DEBUG_TYPE_POINTER,
    CORINFO_LLVM_DEBUG_TYPE_FUNCTION,
    CORINFO_LLVM_DEBUG_TYPE_COUNT
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

struct CORINFO_LLVM_METHOD_DEBUG_INFO
{
    const char* Name;
    const char* Directory;
    const char* FileName;
    unsigned LineNumberCount;
    CORINFO_LLVM_LINE_NUMBER_DEBUG_INFO* SortedLineNumbers;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE OwnerType;
    CORINFO_LLVM_DEBUG_TYPE_HANDLE Type;
    unsigned VariableCount;
    CORINFO_LLVM_VARIABLE_DEBUG_INFO* Variables;
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

    DIFile* debugFile = initializeDebugInfoBuilder(&info);
    DIType* ownerDebugType = getOrCreateDebugType(info.OwnerType);
    unsigned lineNum = m_lineNumbers[0].LineNumber;
    DISubroutineType* debugFuncType = llvm::cast<DISubroutineType>(getOrCreateDebugType(info.Type));
    StringRef linkageName = getRootLlvmFunction()->getName();
    llvm::DISubprogram::DISPFlags flags = llvm::DISubprogram::SPFlagDefinition | llvm::DISubprogram::SPFlagLocalToUnit;

    m_diFunction = m_diBuilder->createMethod(ownerDebugType, info.Name, linkageName, debugFile, lineNum, debugFuncType,
                                             0, 0, nullptr, DINode::FlagZero, flags);

    initializeDebugVariables(&info);

    // TODO-LLVM-EH: debugging in funclets.
    getRootLlvmFunction()->setSubprogram(m_diFunction);
}

DIFile* Llvm::initializeDebugInfoBuilder(CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
{
    assert((pInfo->FileName != nullptr) && (pInfo->Directory != nullptr));

    DIFile* debugFile = DIFile::get(_llvmContext, pInfo->FileName, pInfo->Directory);

    llvm::DICompileUnit* debugCompileUnit = nullptr;
    s_debugCompileUnitsMap.Lookup(debugFile, &debugCompileUnit);

    m_diBuilder = new (_compiler->getAllocator(CMK_DebugInfo)) llvm::DIBuilder(*_module, true, debugCompileUnit);

    if (debugCompileUnit == nullptr)
    {
        debugCompileUnit = m_diBuilder->createCompileUnit(DW_LANG_C_plus_plus, debugFile, "ILC", false, "", 1, "",
                                                          llvm::DICompileUnit::FullDebug, 0, false);
        s_debugCompileUnitsMap.Set(debugFile, debugCompileUnit);
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
    for (auto lcl = m_debugVariablesMap.Begin(); !lcl.Equal(m_debugVariablesMap.End()); ++lcl)
    {
        unsigned lclNum = lcl.Get();
        LclVarDsc* varDsc = _compiler->lvaGetDesc(lclNum);

        Value* addressValue;
        DIExpression* debugExpression;
        if (isShadowFrameLocal(varDsc))
        {
            // The obvious way to implement this (by just passing the shadow stack to dbg.declare) does not
            // work due to downstream issues. We use a workaround of spilling the shadow stack to an alloca.
            if (spilledShadowStackAddr == nullptr)
            {
                spilledShadowStackAddr = _builder.CreateAlloca(getPtrLlvmType());
                JITDUMPEXEC(spilledShadowStackAddr->dump());
                Instruction* storeInst = _builder.CreateStore(getShadowStack(), spilledShadowStackAddr);
                JITDUMPEXEC(storeInst->dump());
            }

            addressValue = spilledShadowStackAddr;
            unsigned offset = static_cast<unsigned>(varDsc->GetStackOffset());
            debugExpression = m_diBuilder->createExpression({DW_OP_deref, DW_OP_plus_uconst, offset});
        }
        else if (!_compiler->lvaInSsa(lclNum) && (varDsc->lvRefCnt() != 0))
        {
            addressValue = getLocalAddr(lclNum);
            debugExpression = m_diBuilder->createExpression();
        }
        else
        {
            continue;
        }

        llvm::DILocalVariable* debugVariable = lcl.GetValue();
        Instruction* debugInst =
            m_diBuilder->insertDeclare(addressValue, debugVariable, debugExpression, debugLocation, insertInst);
        JITDUMP("Declaring V%02u:\n", lclNum);
        JITDUMPEXEC(debugInst->dump());
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
        DBEXEC(CurrentBlock() == nullptr, JITDUMPEXEC(debugInst->dump()));
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
    return DILocation::get(_llvmContext, lineNo, 0, m_diFunction);
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

DIFile* Llvm::getUnknownDebugFile()
{
    return m_diBuilder->createFile("<unknown>", "");
}

DIType* Llvm::getOrCreateDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle)
{
    DIType* debugType;
    if (!s_debugTypesMap.Lookup(debugTypeHandle, &debugType))
    {
        debugType = createDebugType(debugTypeHandle);
        s_debugTypesMap.Set(debugTypeHandle, debugType, decltype(s_debugTypesMap)::Overwrite);
    }

    return debugType;
}

DIType* Llvm::createDebugType(CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle)
{
    CORINFO_LLVM_TYPE_DEBUG_INFO info;
    GetDebugInfoForDebugType(debugTypeHandle, &info);

    switch (info.Kind)
    {
        case CORINFO_LLVM_DEBUG_TYPE_PRIMITIVE:
            return createDebugTypeForPrimitive(info.PrimitiveType);
        case CORINFO_LLVM_DEBUG_TYPE_COMPOSITE:
            return createDebugTypeForCompositeType(debugTypeHandle, &info.CompositeInfo);
        case CORINFO_LLVM_DEBUG_TYPE_ENUM:
            return createDebugTypeForEnumType(&info.EnumInfo);
        case CORINFO_LLVM_DEBUG_TYPE_ARRAY:
            return createDebugTypeForArrayType(&info.ArrayInfo);
        case CORINFO_LLVM_DEBUG_TYPE_POINTER:
            return createDebugTypeForPointerType(&info.PointerInfo);
        case CORINFO_LLVM_DEBUG_TYPE_FUNCTION:
            return createDebugTypeForFunctionType(&info.FunctionInfo);
        default:
            unreached();
    }
}

DIType* Llvm::createDebugTypeForPrimitive(CorInfoType type)
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
            return m_diBuilder->createBasicType("sbyte", 8, DW_ATE_signed);
        case CORINFO_TYPE_UBYTE:
            return m_diBuilder->createBasicType("byte", 8, DW_ATE_unsigned);
        case CORINFO_TYPE_SHORT:
            return m_diBuilder->createBasicType("short", 16, DW_ATE_signed);
        case CORINFO_TYPE_USHORT:
            return m_diBuilder->createBasicType("ushort", 16, DW_ATE_unsigned);
        case CORINFO_TYPE_INT:
            return m_diBuilder->createBasicType("int", 32, DW_ATE_signed);
        case CORINFO_TYPE_UINT:
            return m_diBuilder->createBasicType("uint", 32, DW_ATE_unsigned);
        case CORINFO_TYPE_LONG:
            return m_diBuilder->createBasicType("long", 64, DW_ATE_signed);
        case CORINFO_TYPE_ULONG:
            return m_diBuilder->createBasicType("ulong", 64, DW_ATE_unsigned);
        case CORINFO_TYPE_NATIVEINT:
            return m_diBuilder->createBasicType("nint", TARGET_POINTER_BITS, DW_ATE_signed);
        case CORINFO_TYPE_NATIVEUINT:
            return m_diBuilder->createBasicType("nuint", TARGET_POINTER_BITS, DW_ATE_unsigned);
        case CORINFO_TYPE_FLOAT:
            return m_diBuilder->createBasicType("float", 32, DW_ATE_float);
        case CORINFO_TYPE_DOUBLE:
            return m_diBuilder->createBasicType("double", 64, DW_ATE_float);
        default:
            unreached();
    }
}

DIType* Llvm::createDebugTypeForCompositeType(
    CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle, CORINFO_LLVM_COMPOSITE_TYPE_DEBUG_INFO* pInfo)
{
    // Forward-declare our structure to handle recursion.
    StringRef name = pInfo->Name;
    DIFile* debugFile = getUnknownDebugFile();
    llvm::TempDIType declType = llvm::TempDIType(
        m_diBuilder->createReplaceableCompositeType(DW_TAG_structure_type, name, nullptr, debugFile, 0));
    s_debugTypesMap.Set(debugTypeHandle, declType.get());

    unsigned index = 0;
    std::vector<Metadata*> debugElements((pInfo->BaseClass != NO_DEBUG_TYPE) + pInfo->InstanceFieldCount);
    if (pInfo->BaseClass != NO_DEBUG_TYPE)
    {
        DIType* baseDebugType = getOrCreateDebugType(pInfo->BaseClass);
        debugElements[index++] = m_diBuilder->createInheritance(declType.get(), baseDebugType, 0, 0, DINode::FlagZero);
    }

    for (size_t i = 0; i < pInfo->InstanceFieldCount; i++)
    {
        CORINFO_LLVM_INSTANCE_FIELD_DEBUG_INFO* pFieldInfo = &pInfo->InstanceFields[i];
        DIType* fieldDebugType = getOrCreateDebugType(pFieldInfo->Type);
        debugElements[index++] = createDebugMember(pFieldInfo->Name, fieldDebugType, pFieldInfo->Offset);
    }

    DIType* debugType = createClassDebugType(name, pInfo->Size, debugElements);
    m_diBuilder->replaceTemporary(std::move(declType), debugType);

    // TODO-LLVM-DI: static fields.
    return debugType;
}

DIType* Llvm::createDebugTypeForEnumType(CORINFO_LLVM_ENUM_TYPE_DEBUG_INFO* pInfo)
{
    std::vector<Metadata*> elements(pInfo->ElementCount);
    for (size_t i = 0; i < pInfo->ElementCount; i++)
    {
        CORINFO_LLVM_ENUM_ELEMENT_DEBUG_INFO* pElementInfo = &pInfo->Elements[i];
        llvm::DIEnumerator* element = m_diBuilder->createEnumerator(pElementInfo->Name, pElementInfo->Value);

        elements[i] = element;
    }

    DINodeArray elementsArray = m_diBuilder->getOrCreateArray(elements);
    DIType* underlyingDebugType = getOrCreateDebugType(pInfo->ElementType);
    DIType* enumDebugType =
        m_diBuilder->createEnumerationType(nullptr, pInfo->Name, getUnknownDebugFile(), 0,
                                           underlyingDebugType->getSizeInBits(), underlyingDebugType->getAlignInBits(),
                                           elementsArray, underlyingDebugType);

    return enumDebugType;
}

DIType* Llvm::createDebugTypeForArrayType(CORINFO_LLVM_ARRAY_TYPE_DEBUG_INFO* pInfo)
{
    // Array layout: [void* m_pEEType, int32 Length, [int32 padding on 64 bit], <bounds>, Data].
    // Where <bounds> (for an MD array) is an array of [LowerBound..., Length...].
    unsigned rank = pInfo->Rank;
    bool isMDArray = pInfo->IsMultiDimensional != 0;
    std::vector<Metadata*> members = std::vector<Metadata*>();

    DIType* lengthDebugType = createDebugTypeForPrimitive(CORINFO_TYPE_INT);
    DIDerivedType* lengthDebugField = createDebugMember("Length", lengthDebugType, OFFSETOF__CORINFO_Array__length);
    members.push_back(lengthDebugField);

    if (isMDArray)
    {
        unsigned lowerBoundsOffset = _compiler->eeGetMDArrayLowerBoundOffset(rank, 0);
        DIType* boundsDebugType = createFixedArrayDebugType(lengthDebugType, rank);
        DIDerivedType* lowerBoundsDebugField = createDebugMember("LowerBounds", boundsDebugType, lowerBoundsOffset);
        members.push_back(lowerBoundsDebugField);

        unsigned lengthsOffset = _compiler->eeGetMDArrayLengthOffset(rank, 0);
        DIDerivedType* lengthsDebugField = createDebugMember("Lengths", boundsDebugType, lengthsOffset);
        members.push_back(lengthsDebugField);
    }

    unsigned dataOffset = isMDArray ? _compiler->eeGetMDArrayDataOffset(rank) : _compiler->eeGetArrayDataOffset();
    DIType* elementDebugType = getOrCreateDebugType(pInfo->ElementType);
    DIType* dataDebugType = createFixedArrayDebugType(elementDebugType, 0);
    DIDerivedType* dataDebugField = createDebugMember("Data", dataDebugType, dataOffset);
    members.push_back(dataDebugField);

    DIType* debugType = createClassDebugType(pInfo->Name, dataOffset, members);
    return debugType;
}

DIType* Llvm::createDebugTypeForPointerType(CORINFO_LLVM_POINTER_TYPE_DEBUG_INFO* pInfo)
{
    DIType* debugPointeeType = getOrCreateDebugType(pInfo->ElementType);
    DIType* debugPointerType;
    if (pInfo->IsReference != 0)
    {
        // Reference to a reference is not valid C++; our target debuggers cannot handle it. Emit reference to
        // a pointer instead.
        if (debugPointeeType->getTag() == DW_TAG_reference_type)
        {
            debugPointeeType = createPointerDebugType(llvm::cast<DIDerivedType>(debugPointeeType)->getBaseType());
        }

        debugPointerType =
            m_diBuilder->createReferenceType(DW_TAG_reference_type, debugPointeeType, TARGET_POINTER_BITS);
    }
    else
    {
        debugPointerType = createPointerDebugType(debugPointeeType);
    }

    return debugPointerType;
}

DISubroutineType* Llvm::createDebugTypeForFunctionType(CORINFO_LLVM_FUNCTION_TYPE_DEBUG_INFO* pInfo)
{
    std::vector<Metadata*> debugParameters = std::vector<Metadata*>();
    debugParameters.push_back(getOrCreateDebugType(pInfo->ReturnType));

    if (pInfo->TypeOfThisPointer != NO_DEBUG_TYPE)
    {
        debugParameters.push_back(getOrCreateDebugType(pInfo->TypeOfThisPointer));
    }

    for (size_t i = 0; i < pInfo->NumberOfArguments; i++)
    {
        debugParameters.push_back(getOrCreateDebugType(pInfo->ArgumentTypes[i]));
    }

    llvm::DITypeRefArray debugParametersArray = m_diBuilder->getOrCreateTypeArray(debugParameters);
    return m_diBuilder->createSubroutineType(debugParametersArray);
}

DIType* Llvm::createFixedArrayDebugType(DIType* elementDebugType, unsigned size)
{
    unsigned sizeInBits = elementDebugType->getSizeInBits() * size;
    llvm::DISubrange* boundsRange = m_diBuilder->getOrCreateSubrange(0, size);
    DINodeArray boundsArray = m_diBuilder->getOrCreateArray(boundsRange);
    DIType* debugType =
        m_diBuilder->createArrayType(sizeInBits, elementDebugType->getAlignInBits(), elementDebugType, boundsArray);

    return debugType;
}

DIType* Llvm::createClassDebugType(StringRef name, unsigned size, ArrayRef<Metadata*> elements)
{
    DINodeArray fieldsArray = m_diBuilder->getOrCreateArray(elements);
    DIType* debugType = m_diBuilder->createClassType(nullptr, name, getUnknownDebugFile(), 0, size * BITS_PER_BYTE,
                                                     0, 0, DINode::FlagZero, nullptr, fieldsArray);
    return debugType;
}

DIDerivedType* Llvm::createDebugMember(StringRef name, llvm::DIType* debugType, unsigned offset)
{
    return m_diBuilder->createMemberType(nullptr, name, getUnknownDebugFile(), 0, debugType->getSizeInBits(),
                                         debugType->getAlignInBits(), offset * BITS_PER_BYTE, DINode::FlagZero,
                                         debugType);
}

DIDerivedType* Llvm::createPointerDebugType(DIType* pointeeDebugType)
{
    return m_diBuilder->createPointerType(pointeeDebugType, TARGET_POINTER_BITS);
}
