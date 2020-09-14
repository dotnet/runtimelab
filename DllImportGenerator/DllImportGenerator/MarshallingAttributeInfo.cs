using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

#nullable enable

namespace Microsoft.Interop
{
    // The following types are modeled to fit with the current prospective spec
    // for C# 10 discriminated unions. Once discriminated unions are released,
    // these should be updated to be implemented as a discriminated union.

    internal abstract record MarshallingAttributeInfo {}

    /// <summary>
    /// User-applied System.Runtime.InteropServices.MarshalAsAttribute
    /// </summary>
    internal sealed record MarshalAsInfo(
        UnmanagedType UnmanagedType, 
        string? CustomMarshallerTypeName,
        string? CustomMarshallerCookie,
        UnmanagedType UnmanagedArraySubType,
        int ArraySizeConst,
        short ArraySizeParamIndex) : MarshallingAttributeInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.BlittableTypeAttribute
    /// or System.Runtime.InteropServices.GeneratedMarshallingAttribute on a blittable type
    /// in source in this compilation.
    /// </summary>
    internal sealed record BlittableTypeAttributeInfo : MarshallingAttributeInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.NativeMarshallingAttribute
    /// </summary>
    internal sealed record NativeMarshallingAttributeInfo(
        ITypeSymbol NativeMarshallingType,
        ITypeSymbol? ValuePropertyType,
        bool HasGetPinnableReference) : MarshallingAttributeInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.GeneratedMarshallingAttribute
    /// on a non-blittable type in source in this compilation.
    /// </summary>
    internal sealed record GeneratedNativeMarshallingAttributeInfo(
        string NativeMarshallingFullyQualifiedTypeName) : MarshallingAttributeInfo;
}
