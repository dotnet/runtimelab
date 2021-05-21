using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Interop
{
    // The following types are modeled to fit with the current prospective spec
    // for C# 10 discriminated unions. Once discriminated unions are released,
    // these should be updated to be implemented as a discriminated union.

    public abstract record MarshallingInfo
    {
        // Add a constructor that can only be called by derived types in the same assembly
        // to enforce that this type cannot be extended by users of this library.
        private protected MarshallingInfo()
        {}
    }

    public sealed record NoMarshallingInfo : MarshallingInfo
    {
        public static readonly MarshallingInfo Instance = new NoMarshallingInfo();

        private NoMarshallingInfo() { }
    }

    /// <summary>
    /// Character encoding enumeration.
    /// </summary>
    public enum CharEncoding
    {
        Undefined,
        Utf8,
        Utf16,
        Ansi,
        PlatformDefined
    }

    /// <summary>
    /// Details that are required when scenario supports strings.
    /// </summary>
    public record MarshallingInfoStringSupport(
        CharEncoding CharEncoding
    ) : MarshallingInfo;

    /// <summary>
    /// Simple User-application of System.Runtime.InteropServices.MarshalAsAttribute
    /// </summary>
    public record MarshalAsInfo(
        UnmanagedType UnmanagedType,
        CharEncoding CharEncoding) : MarshallingInfoStringSupport(CharEncoding)
    {
    }

    public enum UnmanagedArrayType
    {
        LPArray = UnmanagedType.LPArray,
        ByValArray = UnmanagedType.ByValArray
    }

    /// <summary>
    /// User-applied System.Runtime.InteropServices.MarshalAsAttribute with array marshalling info
    /// </summary>
    public sealed record ArrayMarshalAsInfo(
        UnmanagedArrayType UnmanagedArrayType,
        int ArraySizeConst,
        short ArraySizeParamIndex,
        CharEncoding CharEncoding,
        MarshallingInfo ElementMarshallingInfo) : MarshalAsInfo((UnmanagedType)UnmanagedArrayType, CharEncoding)
    {        
        public const short UnspecifiedData = -1;
    }

    /// <summary>
    /// User-applied System.Runtime.InteropServices.BlittableTypeAttribute
    /// or System.Runtime.InteropServices.GeneratedMarshallingAttribute on a blittable type
    /// in source in this compilation.
    /// </summary>
    public sealed record BlittableTypeAttributeInfo : MarshallingInfo;

    [Flags]
    public enum SupportedMarshallingMethods
    {
        ManagedToNative = 0x1,
        NativeToManaged = 0x2,
        ManagedToNativeStackalloc = 0x4,
        Pinning = 0x8,
    }

    /// <summary>
    /// User-applied System.Runtime.InteropServices.NativeMarshallingAttribute
    /// </summary>
    public sealed record NativeMarshallingAttributeInfo(
        ITypeSymbol NativeMarshallingType,
        ITypeSymbol? ValuePropertyType,
        SupportedMarshallingMethods MarshallingMethods,
        bool NativeTypePinnable) : MarshallingInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.GeneratedMarshallingAttribute
    /// on a non-blittable type in source in this compilation.
    /// </summary>
    public sealed record GeneratedNativeMarshallingAttributeInfo(
        string NativeMarshallingFullyQualifiedTypeName) : MarshallingInfo;

    /// <summary>
    /// The type of the element is a SafeHandle-derived type with no marshalling attributes.
    /// </summary>
    public sealed record SafeHandleMarshallingInfo(bool AccessibleDefaultConstructor) : MarshallingInfo;


    /// <summary>
    /// Default marshalling for arrays
    /// </summary>
    public sealed record ArrayMarshallingInfo(MarshallingInfo ElementMarshallingInfo) : MarshallingInfo;
}
