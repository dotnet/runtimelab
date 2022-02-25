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
private:
    unsigned              m_fieldCount;
    CORINFO_FIELD_HANDLE* m_fields;
    unsigned              m_isExplicitLayout;

    TypeDescriptor()
    {
    }

public:
    unsigned getFieldCount()
    {
        return m_fieldCount;
    }

    CORINFO_FIELD_HANDLE getField(unsigned index)
    {
        return m_fields[index];
    }

    unsigned getIsExplicitLayout()
    {
        return m_isExplicitLayout;
    }
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

    bool isGcPointer()
    {
        return m_corType == CORINFO_TYPE_CLASS || m_corType == CORINFO_TYPE_BYREF;
    }
};

struct StructDesc
{
private:
    size_t     m_fieldCount;
    FieldDesc* m_fields;
    unsigned   m_isExplicitLayout;

public:
    // This constructor takes the ownership of the passed in array of field descriptors.
    StructDesc(size_t fieldCount, FieldDesc* fieldDesc, bool isExplicitLayout)
        : m_fieldCount(fieldCount), m_fields(fieldDesc), m_isExplicitLayout(isExplicitLayout)
    {
    }

    ~StructDesc()
    {
        delete[] m_fields;
    }

    size_t getFieldCount()
    {
        return m_fieldCount;
    }

    FieldDesc* getFieldDesc(unsigned index)
    {
        return &m_fields[index];
    }

    unsigned isExplicitLayout()
    {
        return m_isExplicitLayout;
    }
};

#endif // _LLVM_TYPES_H_
