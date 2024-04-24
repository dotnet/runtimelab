// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ================================================================================================================
// |                                     "Type system" for the LLVM backend                                       |
// ================================================================================================================

#include "llvm.h"

StructDesc* Llvm::getStructDesc(CORINFO_CLASS_HANDLE structHandle)
{
    StructDesc* structDesc;
    if (!m_context->StructDescMap.Lookup(structHandle, &structDesc))
    {
        TypeDescriptor structTypeDescriptor;
        GetTypeDescriptor(structHandle, &structTypeDescriptor);

        unsigned structSize = structTypeDescriptor.Size;
        jitstd::vector<CORINFO_FIELD_HANDLE> sparseFields(
            structSize, NO_FIELD_HANDLE, _compiler->getAllocator(CMK_Codegen));
        jitstd::vector<unsigned> sparseFieldSizes(structSize, 0, _compiler->getAllocator(CMK_Codegen));

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

        FieldDesc* fields = new FieldDesc[fieldCount];
        structDesc = new StructDesc(fieldCount, fields, structTypeDescriptor.HasSignificantPadding);

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

        m_context->StructDescMap.Set(structHandle, structDesc);
    }

    return structDesc;
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
    Type* llvmStructType;
    if (!m_context->LlvmStructTypesMap.Lookup(structHandle, &llvmStructType))
    {
        // We treat trivial structs like their underlying types for compatibility with the native ABI.
        CorInfoType primitiveType = GetPrimitiveTypeForTrivialWasmStruct(structHandle);
        if (primitiveType != CORINFO_TYPE_UNDEF)
        {
            llvmStructType = getLlvmTypeForCorInfoType(primitiveType, NO_CLASS_HANDLE);
        }
        else
        {
            StructDesc* structDesc = getStructDesc(structHandle);
            size_t fieldCount = structDesc->getFieldCount();

            unsigned lastOffset = 0;
            unsigned totalSize = 0;
            ArrayStack<Type*> llvmFields(_compiler->getAllocator(CMK_Codegen));

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
                Type* fieldLlvmType = getLlvmTypeForCorInfoType(fieldCorType, fieldDesc->getClassHandle());
                llvmFields.Push(fieldLlvmType);

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

            llvmStructType = llvm::StructType::get(m_context->Context, AsRef(llvmFields), /* isPacked */ true);
        }

        m_context->LlvmStructTypesMap.Set(structHandle, llvmStructType);
    }

    return llvmStructType;
}

Type* Llvm::getLlvmTypeForVarType(var_types type)
{
    switch (type)
    {
        case TYP_VOID:
            return Type::getVoidTy(m_context->Context);
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

void Llvm::addPaddingFields(unsigned paddingSize, ArrayStack<Type*>& llvmFields)
{
    unsigned numInts = paddingSize / 4;
    unsigned numBytes = paddingSize - numInts * 4;
    for (unsigned i = 0; i < numInts; i++)
    {
        llvmFields.Push(Type::getInt32Ty(m_context->Context));
    }
    for (unsigned i = 0; i < numBytes; i++)
    {
        llvmFields.Push(Type::getInt8Ty(m_context->Context));
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
