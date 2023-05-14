// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                                     "Type system" for the LLVM backend                                       |
// ================================================================================================================

#include "llvm.h"

StructDesc* Llvm::getStructDesc(CORINFO_CLASS_HANDLE structHandle)
{
    auto& map = m_context->StructDescMap;

    if (map.find(structHandle) == map.end())
    {
        TypeDescriptor structTypeDescriptor;
        GetTypeDescriptor(structHandle, &structTypeDescriptor);

        unsigned structSize = structTypeDescriptor.Size;
        std::vector<CORINFO_FIELD_HANDLE> sparseFields = std::vector<CORINFO_FIELD_HANDLE>(structSize);
        std::vector<unsigned> sparseFieldSizes = std::vector<unsigned>(structSize);

        for (unsigned i = 0; i < structSize; i++)
            sparseFields[i] = nullptr;

        // determine the largest field for unions, and get fields in order of offset
        for (unsigned i = 0; i < structTypeDescriptor.FieldCount; i++)
        {
            CORINFO_FIELD_HANDLE fieldHandle = structTypeDescriptor.Fields[i];
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
        StructDesc* structDesc = new StructDesc(fieldCount, fields, structTypeDescriptor.HasSignificantPadding);

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

        map.insert({structHandle, structDesc});
    }

    return map.at(structHandle);
}

Type* Llvm::getLlvmTypeForStruct(ClassLayout* classLayout)
{
    if (classLayout->IsBlockLayout())
    {
        return llvm::ArrayType::get(Type::getInt8Ty(m_context->Context), classLayout->GetSize());
    }

    return getLlvmTypeForStruct(classLayout->GetClassHandle());
}

Type* Llvm::getLlvmTypeForStruct(CORINFO_CLASS_HANDLE structHandle)
{
    auto& map = m_context->LlvmStructTypesMap;

    if (map.find(structHandle) == map.end())
    {
        Type* llvmStructType;

        // We treat trivial structs like their underlying types for compatibility with the native ABI.
        CorInfoType primitiveType = GetPrimitiveTypeForTrivialWasmStruct(structHandle);
        if (primitiveType != CORINFO_TYPE_UNDEF)
        {
            llvmStructType = getLlvmTypeForCorInfoType(primitiveType, NO_CLASS_HANDLE);
        }
        else
        {
            StructDesc* structDesc = getStructDesc(structHandle);
            unsigned fieldCount = structDesc->getFieldCount();

            unsigned lastOffset = 0;
            unsigned totalSize = 0;
            std::vector<Type*> llvmFields = std::vector<Type*>();
            unsigned prevElementSize = 0;

            for (unsigned fieldIx = 0; fieldIx < fieldCount; fieldIx++)
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
            unsigned structSize = m_info->compCompHnd->getClassSize(structHandle);
            if (totalSize < structSize)
            {
                addPaddingFields(structSize - totalSize, llvmFields);
            }

            llvmStructType = llvm::StructType::get(m_context->Context, llvmFields, /* isPacked */ true);
        }

        map.insert({structHandle, llvmStructType});
    }

    return map.at(structHandle);
}

Type* Llvm::getLlvmTypeForVarType(var_types type)
{
    switch (type)
    {
        case TYP_VOID:
            return Type::getVoidTy(m_context->Context);
        case TYP_BOOL:
        case TYP_BYTE:
        case TYP_UBYTE:
            return Type::getInt8Ty(m_context->Context);
        case TYP_SHORT:
        case TYP_USHORT:
            return Type::getInt16Ty(m_context->Context);
        case TYP_INT:
        case TYP_UINT:
            return Type::getInt32Ty(m_context->Context);
        case TYP_LONG:
        case TYP_ULONG:
            return Type::getInt64Ty(m_context->Context);
        case TYP_FLOAT:
            return Type::getFloatTy(m_context->Context);
        case TYP_DOUBLE:
            return Type::getDoubleTy(m_context->Context);
        case TYP_REF:
        case TYP_BYREF:
            return getPtrLlvmType();
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
    if (varDsc->lvCorInfoType != CORINFO_TYPE_UNDEF)
    {
        return getLlvmTypeForCorInfoType(varDsc->lvCorInfoType, NO_CLASS_HANDLE);
    }

    return getLlvmTypeForVarType(varDsc->TypeGet());
}

Type* Llvm::getLlvmTypeForCorInfoType(CorInfoType corInfoType, CORINFO_CLASS_HANDLE classHnd)
{
    switch (corInfoType)
    {
        case CORINFO_TYPE_PTR:
            return getPtrLlvmType();

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
        llvmFields.push_back(Type::getInt32Ty(m_context->Context));
    }
    for (unsigned i = 0; i < numBytes; i++)
    {
        llvmFields.push_back(Type::getInt8Ty(m_context->Context));
    }
}

Type* Llvm::getPtrLlvmType()
{
    return llvm::PointerType::getUnqual(m_context->Context);
}

Type* Llvm::getIntPtrLlvmType()
{
#ifdef TARGET_64BIT
    return Type::getInt64Ty(m_context->Context);
#else
    return Type::getInt32Ty(m_context->Context);
#endif
}
