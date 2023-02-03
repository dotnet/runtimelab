// ================================================================================================================
// |                                     "Type system" for the LLVM backend                                       |
// ================================================================================================================

#include "llvm.h"

StructDesc* Llvm::getStructDesc(CORINFO_CLASS_HANDLE structHandle)
{
    if (_structDescMap->find(structHandle) == _structDescMap->end())
    {
        TypeDescriptor structTypeDescriptor = GetTypeDescriptor(structHandle);
        unsigned structSize                 = m_info->compCompHnd->getClassSize(structHandle); // TODO-LLVM: add to TypeDescriptor?

        std::vector<CORINFO_FIELD_HANDLE> sparseFields = std::vector<CORINFO_FIELD_HANDLE>(structSize);
        std::vector<unsigned> sparseFieldSizes = std::vector<unsigned>(structSize);

        for (unsigned i = 0; i < structSize; i++)
            sparseFields[i] = nullptr;

        // determine the largest field for unions, and get fields in order of offset
        for (unsigned i = 0; i < structTypeDescriptor.getFieldCount(); i++)
        {
            CORINFO_FIELD_HANDLE fieldHandle = structTypeDescriptor.getField(i);
            unsigned             fldOffset   = m_info->compCompHnd->getFieldOffset(fieldHandle);

            assert(fldOffset < structSize);

            CORINFO_CLASS_HANDLE fieldClass;
            CorInfoType corInfoType = m_info->compCompHnd->getFieldType(fieldHandle, &fieldClass);

            unsigned fieldSize = getElementSize(fieldClass, corInfoType);

            // store the biggest field at the offset for unions
            if (sparseFields[fldOffset] == nullptr || fieldSize > sparseFieldSizes[fldOffset])
            {
                sparseFields[fldOffset] = fieldHandle;
                sparseFieldSizes[fldOffset] = fieldSize;
            }
        }

        // count the struct fields after replacing fields with equal offsets
        unsigned fieldCount = 0;
        unsigned i          = 0;
        while(i < structSize)
        {
            if (sparseFields[i] != nullptr)
            {
                fieldCount++;
                // clear out any fields that are covered by this field
                for (unsigned j = 1; j < sparseFieldSizes[i]; j++)
                {
                    sparseFields[i + j] = nullptr;
                }
                i += sparseFieldSizes[i];
            }
            else
            {
                i++;
            }
        }

        FieldDesc*  fields     = new FieldDesc[fieldCount];
        StructDesc* structDesc = new StructDesc(fieldCount, fields, structTypeDescriptor.hasSignificantPadding());

        unsigned fieldIx = 0;
        for (unsigned fldOffset = 0; fldOffset < structSize; fldOffset++)
        {
            if (sparseFields[fldOffset] == nullptr)
            {
                continue;
            }

            CORINFO_FIELD_HANDLE fieldHandle = sparseFields[fldOffset];
            CORINFO_CLASS_HANDLE fieldClassHandle = NO_CLASS_HANDLE;

            const CorInfoType corInfoType = m_info->compCompHnd->getFieldType(fieldHandle, &fieldClassHandle);
            fields[fieldIx] = FieldDesc(fldOffset, corInfoType, fieldClassHandle);
            fieldIx++;
        }

        _structDescMap->insert({structHandle, structDesc});
    }
    return _structDescMap->at(structHandle);
}

Type* Llvm::getLlvmTypeForStruct(ClassLayout* classLayout)
{
    if (classLayout->IsBlockLayout())
    {
        return llvm::ArrayType::get(Type::getInt8Ty(_llvmContext), classLayout->GetSize());
    }

    return getLlvmTypeForStruct(classLayout->GetClassHandle());
}

Type* Llvm::getLlvmTypeForStruct(CORINFO_CLASS_HANDLE structHandle)
{
    if (_llvmStructs->find(structHandle) == _llvmStructs->end())
    {
        Type* llvmType;
        unsigned fieldAlignment;

        // LLVM thinks certain sizes of struct have a different calling convention than Clang does.
        // Treating them as ints fixes that and is more efficient in general

        unsigned structSize = m_info->compCompHnd->getClassSize(structHandle);
        switch (structSize)
        {
            case 1:
                llvmType = Type::getInt8Ty(_llvmContext);
                break;
            case 2:
                fieldAlignment = GetInstanceFieldAlignment(structHandle);
                if (fieldAlignment == 2)
                {
                    llvmType = Type::getInt16Ty(_llvmContext);
                    break;
                }
            case 4:
                fieldAlignment = GetInstanceFieldAlignment(structHandle);
                if (fieldAlignment == 4)
                {
                    if (StructIsWrappedPrimitive(structHandle, CORINFO_TYPE_FLOAT))
                    {
                        llvmType = Type::getFloatTy(_llvmContext);
                    }
                    else
                    {
                        llvmType = Type::getInt32Ty(_llvmContext);
                    }
                    break;
                }
            case 8:
                fieldAlignment = GetInstanceFieldAlignment(structHandle);
                if (fieldAlignment == 8)
                {
                    if (StructIsWrappedPrimitive(structHandle, CORINFO_TYPE_DOUBLE))
                    {
                        llvmType = Type::getDoubleTy(_llvmContext);
                    }
                    else
                    {
                        llvmType = Type::getInt64Ty(_llvmContext);
                    }
                    break;
                }

            default:
                // Forward-declare the struct in case there's a reference to it in the fields.
                // This must be a named struct or LLVM hits a stack overflow
                const char* name = GetTypeName(structHandle);
                llvm::StructType* llvmStructType = llvm::StructType::create(_llvmContext, name);
                llvmType = llvmStructType;
                StructDesc* structDesc = getStructDesc(structHandle);
                unsigned    fieldCnt   = structDesc->getFieldCount();


                unsigned lastOffset = 0;
                unsigned totalSize = 0;
                std::vector<Type*> llvmFields = std::vector<Type*>();
                unsigned prevElementSize = 0;


                for (unsigned fieldIx = 0; fieldIx < fieldCnt; fieldIx++)
                {
                    FieldDesc* fieldDesc = structDesc->getFieldDesc(fieldIx);

                    // Pad to this field if necessary
                    unsigned paddingSize = fieldDesc->getFieldOffset() - lastOffset - prevElementSize;
                    if (paddingSize > 0)
                    {
                        addPaddingFields(paddingSize, llvmFields);
                        totalSize += paddingSize;
                    }

                    CorInfoType fieldCorType = fieldDesc->getCorType();

                    unsigned fieldSize = getElementSize(fieldDesc->getClassHandle(), fieldCorType);

                    llvmFields.push_back(getLlvmTypeForCorInfoType(fieldCorType, fieldDesc->getClassHandle()));

                    totalSize += fieldSize;
                    lastOffset = fieldDesc->getFieldOffset();
                    prevElementSize = fieldSize;
                }

                // If explicit layout is greater than the sum of fields, add padding
                if (totalSize < structSize)
                {
                    addPaddingFields(structSize - totalSize, llvmFields);
                }

                llvmStructType->setBody(llvmFields, true);
                break;
        }
        _llvmStructs->insert({ structHandle, llvmType });
    }
    return _llvmStructs->at(structHandle);
}

Type* Llvm::getLlvmTypeForVarType(var_types type)
{
    switch (type)
    {
        case TYP_VOID:
            return Type::getVoidTy(_llvmContext);
        case TYP_BOOL:
        case TYP_BYTE:
        case TYP_UBYTE:
            return Type::getInt8Ty(_llvmContext);
        case TYP_SHORT:
        case TYP_USHORT:
            return Type::getInt16Ty(_llvmContext);
        case TYP_INT:
        case TYP_UINT:
            return Type::getInt32Ty(_llvmContext);
        case TYP_LONG:
        case TYP_ULONG:
            return Type::getInt64Ty(_llvmContext);
        case TYP_FLOAT:
            return Type::getFloatTy(_llvmContext);
        case TYP_DOUBLE:
            return Type::getDoubleTy(_llvmContext);
        case TYP_REF:
        case TYP_BYREF:
            return getPtrLlvmType();
        case TYP_BLK:
        case TYP_STRUCT:
            failFunctionCompilation();
        default:
            unreached();
    }
}

Type* Llvm::getLlvmTypeForLclVar(LclVarDsc* varDsc)
{
    if (varDsc->TypeGet() == TYP_STRUCT)
    {
        return getLlvmTypeForStruct(varDsc->GetLayout());
    }
    if (varDsc->TypeGet() == TYP_BLK)
    {
        assert(varDsc->lvExactSize != 0);
        return llvm::ArrayType::get(Type::getInt8Ty(_llvmContext), varDsc->lvExactSize);
    }
    if (varDsc->lvCorInfoType != CORINFO_TYPE_UNDEF)
    {
        return getLlvmTypeForCorInfoType(varDsc->lvCorInfoType, varDsc->lvClassHnd);
    }

    return getLlvmTypeForVarType(varDsc->TypeGet());
}

Type* Llvm::getLlvmTypeForCorInfoType(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    switch (corInfoType)
    {
        case CORINFO_TYPE_PTR:
            return Type::getInt8Ty(_llvmContext)->getPointerTo();

        case CORINFO_TYPE_VALUECLASS:
            return getLlvmTypeForStruct(classHnd);

        default:
            return getLlvmTypeForVarType(JITtype2varType(corInfoType));
    }
}

unsigned Llvm::getElementSize(CORINFO_CLASS_HANDLE classHandle, CorInfoType corInfoType)
{
    if (classHandle != NO_CLASS_HANDLE)
    {
        return m_info->compCompHnd->getClassSize(classHandle);
    }

    return genTypeSize(JITtype2varType(corInfoType));
}

void Llvm::addPaddingFields(unsigned paddingSize, std::vector<Type*>& llvmFields)
{
    unsigned numInts = paddingSize / 4;
    unsigned numBytes = paddingSize - numInts * 4;
    for (unsigned i = 0; i < numInts; i++)
    {
        llvmFields.push_back(Type::getInt32Ty(_llvmContext));
    }
    for (unsigned i = 0; i < numBytes; i++)
    {
        llvmFields.push_back(Type::getInt8Ty(_llvmContext));
    }
}

Type* Llvm::getPtrLlvmType()
{
    return llvm::PointerType::getUnqual(_llvmContext);
}

Type* Llvm::getIntPtrLlvmType()
{
#ifdef TARGET_64BIT
    return Type::getInt64Ty(_llvmContext);
#else
    return Type::getInt32Ty(_llvmContext);
#endif
}
