// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Types used in LLVM compilation that are abstractions of the VM types.
//

#ifndef _LLVM_TYPES_H_
#define _LLVM_TYPES_H_

#include "corinfo.h"

struct TypeDescriptor
{
    size_t                fieldCount;
    CORINFO_FIELD_HANDLE* fields;
};

struct FieldDesc
{
private:
    int                  _fieldOffset;
    CORINFO_CLASS_HANDLE _classHandle;
    CorInfoType          _corType;

public:
    FieldDesc()
    {
    }

    int getFieldOffset()
    {
        return _fieldOffset;
    }

    CORINFO_CLASS_HANDLE getClassHandle()
    {
        return _classHandle;
    }

    CorInfoType getCorType()
    {
        return _corType;
    }

    void setFieldData(int fieldOffset, CorInfoType corType, CORINFO_CLASS_HANDLE classHandle)
    {
        _fieldOffset = fieldOffset;
        _corType     = corType;
        _classHandle = classHandle;
    }
};

struct StructDesc
{
private:
    size_t     _fieldCount;
    FieldDesc* _fieldDesc;

public:
    // This constructor takes the ownership of the passed in array of field descriptors.
    StructDesc(size_t fieldCount, FieldDesc* fieldDesc)
        : _fieldCount(fieldCount), _fieldDesc(fieldDesc)
    {
    }

    ~StructDesc()
    {
        delete[] _fieldDesc;
    }

    size_t getFieldCount()
    {
        return _fieldCount;
    }

    FieldDesc* getFieldDesc(unsigned index)
    {
        return &_fieldDesc[index];
    }
};

#endif // _LLVM_TYPES_H_
