// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Types used in LLVM compilation that are abstractions of the VM types.
//

#ifndef _LLVM_TYPES_H_
#define _LLVM_TYPES_H_

#include "jitpch.h"

struct TypeDescriptor
{
    unsigned              Size;
    unsigned              ElementCount;
    unsigned              FieldCount;
    CORINFO_FIELD_HANDLE* Fields;
    bool                  HasSignificantPadding;
};

struct FieldDesc
{
private:
    unsigned             m_fieldOffset;
    CorInfoType          m_corType;
    CORINFO_CLASS_HANDLE m_classHandle;

public:
    FieldDesc()
    {
    }

    FieldDesc(unsigned fieldOffset, CorInfoType corType, CORINFO_CLASS_HANDLE classHandle)
        : m_fieldOffset(fieldOffset), m_corType(corType), m_classHandle(classHandle)
    {
    }

    int getFieldOffset()
    {
        return m_fieldOffset;
    }

    CORINFO_CLASS_HANDLE getClassHandle()
    {
        return m_classHandle;
    }

    CorInfoType getCorType()
    {
        return m_corType;
    }
};

struct StructDesc
{
private:
    unsigned   m_size;
    size_t     m_fieldCount;
    FieldDesc* m_fields;
    bool       m_hasSignificantPadding;
    unsigned   m_isInlineArray;

public:
    ~StructDesc()
    {
        delete[] m_fields;
    }

    // This factory function takes the ownership of the passed in array of field descriptors.
    static StructDesc* ForStruct(unsigned size, size_t fieldCount, FieldDesc* fields, bool hasSignificantPadding)
    {
        StructDesc* dsc = new StructDesc;
        dsc->m_size = size;
        dsc->m_fieldCount = fieldCount;
        dsc->m_fields = fields;
        dsc->m_hasSignificantPadding = hasSignificantPadding;
        dsc->m_isInlineArray = false;
        return dsc;
    }

    // This factory function takes the ownership of the passed in array of field descriptors.
    static StructDesc* ForInlineArray(unsigned size, unsigned elementCount, FieldDesc* field, bool hasSignificantPadding)
    {
        StructDesc* dsc = new StructDesc;
        dsc->m_size = size;
        dsc->m_fieldCount = elementCount;
        dsc->m_fields = field;
        dsc->m_hasSignificantPadding = hasSignificantPadding;
        dsc->m_isInlineArray = true;
        return dsc;
    }

    unsigned getSize()
    {
        return m_size;
    }

    size_t getFieldCount()
    {
        return m_fieldCount;
    }

    FieldDesc getFieldDesc(unsigned index)
    {
        assert(index < m_fieldCount);
        if (m_isInlineArray)
        {
            assert(m_fieldCount >= 1);
            unsigned offset = static_cast<unsigned>(m_size / m_fieldCount) * index;
            FieldDesc field(offset, m_fields[0].getCorType(), m_fields[0].getClassHandle());
            return field;
        }

        return m_fields[index];
    }

    unsigned hasSignificantPadding()
    {
        return m_hasSignificantPadding;
    }
};

#endif // _LLVM_TYPES_H_
