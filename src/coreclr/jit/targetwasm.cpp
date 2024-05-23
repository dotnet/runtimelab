// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*****************************************************************************/

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#include "llvm.h"
#include "target.h"

const char*            Target::g_tgtCPUName           = "wasm";
const Target::ArgOrder Target::g_tgtArgOrder          = ARG_ORDER_R2L;
const Target::ArgOrder Target::g_tgtUnmanagedArgOrder = ARG_ORDER_R2L;

// clang-format on

const regNumber intArgRegs[] = { REG_STK };
const regNumber fltArgRegs[] = { REG_STK };

//-----------------------------------------------------------------------------
// WasmClassifier:
//   Construct a new instance of the Wasm ABI classifier.
//
// Parameters:
//   info - Info about the method being classified.
//
WasmClassifier::WasmClassifier(const ClassifierInfo& info)
    : m_info(info)
{
}

//-----------------------------------------------------------------------------
// Classify:
//   Classify a parameter for the Wasm ABI.
//
// Parameters:
//   comp           - Compiler instance
//   type           - The type of the parameter
//   structLayout   - The layout of the struct. Expected to be non-null if
//                    varTypeIsStruct(type) is true.
//   wellKnownParam - Well known type of the parameter (if it may affect its ABI classification)
//
// Returns:
//   Classification information for the parameter.
//
ABIPassingInformation WasmClassifier::Classify(Compiler*    comp,
                                               var_types    type,
                                               ClassLayout* structLayout,
                                               WellKnownArg wellKnownParam)
{
    if (type == TYP_STRUCT)
    {
        structPassingKind wbPassStruct;
        type = comp->m_llvm->GetArgTypeForStructWasm(structLayout->GetClassHandle(), &wbPassStruct);
    }

    assert(type != TYP_STRUCT);

    unsigned typeSize = genTypeSize(type);

    ABIPassingSegment segment = ABIPassingSegment::OnStack(m_stackArgSize, 0, typeSize);
    m_stackArgSize += typeSize;

    return ABIPassingInformation::FromSegment(comp, segment);
}
