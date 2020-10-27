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

    internal abstract record MarshallingInfo
    {
    }

    internal sealed record NoMarshallingInfo : MarshallingInfo
    {
        public static readonly MarshallingInfo Instance = new NoMarshallingInfo();

        private NoMarshallingInfo() { }
    }

    /// <summary>
    /// Character encoding enumeration.
    /// </summary>
    internal enum CharEncoding
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
    internal record MarshallingInfoStringSupport(
        CharEncoding CharEncoding
    ) : MarshallingInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.MarshalAsAttribute
    /// </summary>
    internal sealed record MarshalAsInfo(
        UnmanagedType UnmanagedType,
        UnmanagedType UnmanagedArraySubType,
        int ArraySizeConst,
        short ArraySizeParamIndex,
        CharEncoding CharEncoding) : MarshallingInfoStringSupport(CharEncoding)
    {
        public MarshalAsInfo(UnmanagedType unmanagedType, CharEncoding charEncoding)
            :this(unmanagedType, (UnmanagedType)UnspecifiedData, UnspecifiedData, UnspecifiedData, charEncoding)
        {
        }

        public MarshallingInfo CreateArraySubTypeMarshalAsInfo()
        {
            Debug.Assert(UnmanagedType is UnmanagedType.LPArray or UnmanagedType.ByValArray or UnmanagedType.SafeArray);
            if (UnmanagedArraySubType == (UnmanagedType)UnspecifiedData)
            {
                return NoMarshallingInfo.Instance;
            }
            return new MarshalAsInfo(UnmanagedArraySubType, CharEncoding);
        }

        /// <summary>
        /// Helper method to enable cleaner pattern matching for the common case of
        /// a MarshalAs attribute that just uses the constructor parameter and no additional properties.
        /// </summary>
        public void Deconstruct(out UnmanagedType unmanagedType) => unmanagedType = UnmanagedType;

        public const short UnspecifiedData = -1;
    }

    /// <summary>
    /// User-applied System.Runtime.InteropServices.BlittableTypeAttribute
    /// or System.Runtime.InteropServices.GeneratedMarshallingAttribute on a blittable type
    /// in source in this compilation.
    /// </summary>
    internal sealed record BlittableTypeAttributeInfo : MarshallingInfo;

    [Flags]
    internal enum SupportedMarshallingMethods
    {
        ManagedToNative = 0x1,
        NativeToManaged = 0x2,
        ManagedToNativeStackalloc = 0x4,
        Pinning = 0x8
    }

    /// <summary>
    /// User-applied System.Runtime.InteropServices.NativeMarshallingAttribute
    /// </summary>
    internal sealed record NativeMarshallingAttributeInfo(
        ITypeSymbol NativeMarshallingType,
        ITypeSymbol? ValuePropertyType,
        SupportedMarshallingMethods MarshallingMethods) : MarshallingInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.GeneratedMarshallingAttribute
    /// on a non-blittable type in source in this compilation.
    /// </summary>
    internal sealed record GeneratedNativeMarshallingAttributeInfo(
        string NativeMarshallingFullyQualifiedTypeName) : MarshallingInfo;

    /// <summary>
    /// The type of the element is a SafeHandle-derived type with no marshalling attributes.
    /// </summary>
    internal sealed record SafeHandleMarshallingInfo : MarshallingInfo;
        
}
