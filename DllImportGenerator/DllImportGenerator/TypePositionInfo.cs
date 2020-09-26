﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DllImportGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Interop
{
    /// <summary>
    /// Positional type information involved in unmanaged/managed scenarios.
    /// </summary>
    internal sealed class TypePositionInfo
    {
        public const int UnsetIndex = int.MinValue;
        public const int ReturnIndex = UnsetIndex + 1;

        private TypePositionInfo()
        {
            this.ManagedIndex = UnsetIndex;
            this.NativeIndex = UnsetIndex;
            this.UnmanagedLCIDConversionArgIndex = UnsetIndex;
        }

        public string InstanceIdentifier { get; private set; }
        public ITypeSymbol ManagedType { get; private set; }

        public RefKind RefKind { get; private set; }
        public SyntaxKind RefKindSyntax { get; private set; }
        
        public bool IsByRef => RefKind != RefKind.None;

        public bool IsManagedReturnPosition { get => this.ManagedIndex == ReturnIndex; }
        public bool IsNativeReturnPosition { get => this.NativeIndex == ReturnIndex; }

        public int ManagedIndex { get; set; }
        public int NativeIndex { get; set; }
        public int UnmanagedLCIDConversionArgIndex { get; private set; }

        public MarshallingInfo MarshallingAttributeInfo { get; private set; }

        public static TypePositionInfo CreateForParameter(IParameterSymbol paramSymbol, Compilation compilation)
        {
            var marshallingInfo = GetMarshallingInfo(paramSymbol.Type, paramSymbol.GetAttributes(), compilation);
            var typeInfo = new TypePositionInfo()
            {
                ManagedType = paramSymbol.Type,
                InstanceIdentifier = paramSymbol.Name,
                RefKind = paramSymbol.RefKind,
                RefKindSyntax = RefKindToSyntax(paramSymbol.RefKind),
                MarshallingAttributeInfo = marshallingInfo
            };

            return typeInfo;
        }

        public static TypePositionInfo CreateForType(ITypeSymbol type, IEnumerable<AttributeData> attributes, Compilation compilation)
        {
            var marshallingInfo = GetMarshallingInfo(type, attributes, compilation);
            var typeInfo = new TypePositionInfo()
            {
                ManagedType = type,
                InstanceIdentifier = string.Empty,
                RefKind = RefKind.None,
                RefKindSyntax = SyntaxKind.None,
                MarshallingAttributeInfo = marshallingInfo
            };

            return typeInfo;
        }

#nullable enable
        private static MarshallingInfo? GetMarshallingInfo(ITypeSymbol type, IEnumerable<AttributeData> attributes, Compilation compilation)
        {
            MarshallingInfo? marshallingInfo = null;
            // Look at attributes on the type.
            foreach (var attrData in attributes)
            {
                INamedTypeSymbol attributeClass = attrData.AttributeClass!;

                if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_MarshalAsAttribute), attributeClass))
                {
                    if (marshallingInfo is not null)
                    {
                        // TODO: diagnostic
                    }
                    // https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.marshalasattribute
                    marshallingInfo = CreateMarshalAsInfo(attrData);
                }
                else if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.MarshalUsingAttribute), attributeClass))
                {
                    if (marshallingInfo is not null)
                    {
                        // TODO: diagnostic
                    }
                    marshallingInfo = CreateNativeMarshallingInfo(attrData, false);
                }
            }

            // If we aren't overriding the marshalling at usage time,
            // then fall back to the information on the element type itself.
            if (marshallingInfo is null)
            {
                foreach (var attrData in type.GetAttributes())
                {
                    INamedTypeSymbol attributeClass = attrData.AttributeClass!;

                    if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.BlittableTypeAttribute), attributeClass))
                    {
                        if (marshallingInfo is not null)
                        {
                            // TODO: diagnostic
                        }
                        marshallingInfo = new BlittableTypeAttributeInfo();
                    }
                    else if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.NativeMarshallingAttribute), attributeClass))
                    {
                        if (marshallingInfo is not null)
                        {
                            // TODO: diagnostic
                        }
                        marshallingInfo = CreateNativeMarshallingInfo(attrData, true);
                    }
                    else if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.GeneratedMarshallingAttribute), attributeClass))
                    {
                        if (marshallingInfo is not null)
                        {
                            // TODO: diagnostic
                        }
                        marshallingInfo = type.IsConsideredBlittable() ? new BlittableTypeAttributeInfo() : new GeneratedNativeMarshallingAttributeInfo(null! /* TODO: determine naming convention */);
                    }
                }
            }

            if (marshallingInfo is null)
            {
                marshallingInfo = CreateTypeBasedMarshallingInfo(type, compilation);
            }

            return marshallingInfo;

            static MarshalAsInfo CreateMarshalAsInfo(AttributeData attrData)
            {
                UnmanagedType unmanagedType = (UnmanagedType)attrData.ConstructorArguments[0].Value!;
                if (unmanagedType == 0)
                {
                    // [TODO] diagnostic
                }
                string? customMarshallerTypeName = null;
                string? customMarshallerCookie = null;
                UnmanagedType unmanagedArraySubType = 0;
                int arraySizeConst = 0;
                short arraySizeParamIndex = 0;

                // All other data on attribute is defined as NamedArguments.
                foreach (var namedArg in attrData.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        default:
                            Debug.Fail($"An unknown member was found on {nameof(MarshalAsAttribute)}");
                            continue;
                        case nameof(MarshalAsAttribute.SafeArraySubType):
                        case nameof(MarshalAsAttribute.SafeArrayUserDefinedSubType):
                        case nameof(MarshalAsAttribute.IidParameterIndex):
                            // [TODO] Report not supported
                            break;
                        case nameof(MarshalAsAttribute.MarshalTypeRef):
                        case nameof(MarshalAsAttribute.MarshalType):
                            // Call ToString() to handle INamedTypeSymbol as well.
                            customMarshallerTypeName = namedArg.Value.Value!.ToString();
                            break;
                        case nameof(MarshalAsAttribute.MarshalCookie):
                            customMarshallerCookie = (string)namedArg.Value.Value!;
                            break;
                        case nameof(MarshalAsAttribute.ArraySubType):
                            unmanagedArraySubType = (UnmanagedType)namedArg.Value.Value!;
                            break;
                        case nameof(MarshalAsAttribute.SizeConst):
                            arraySizeConst = (int)namedArg.Value.Value!;
                            break;
                        case nameof(MarshalAsAttribute.SizeParamIndex):
                            arraySizeParamIndex = (short)namedArg.Value.Value!;
                            break;
                    }

                }
                
                return new MarshalAsInfo(
                    UnmanagedType: unmanagedType,
                    CustomMarshallerTypeName: customMarshallerTypeName,
                    CustomMarshallerCookie: customMarshallerCookie,
                    UnmanagedArraySubType: unmanagedArraySubType,
                    ArraySizeConst: arraySizeConst,
                    ArraySizeParamIndex: arraySizeParamIndex
                );
            }
        
            NativeMarshallingAttributeInfo CreateNativeMarshallingInfo(AttributeData attrData, bool allowGetPinnableReference)
            {
                ITypeSymbol spanOfByte = compilation.GetTypeByMetadataName(TypeNames.System_Span)!.Construct(compilation.GetSpecialType(SpecialType.System_Byte));
                INamedTypeSymbol nativeType = (INamedTypeSymbol)attrData.ConstructorArguments[0].Value!;
                SupportedMarshallingMethods methods = 0;
                IPropertySymbol? valueProperty = ManualTypeMarshallingHelper.FindValueProperty(nativeType);
                foreach (var ctor in nativeType.Constructors)
                {
                    if (ManualTypeMarshallingHelper.IsManagedToNativeConstructor(ctor, type)
                        && (valueProperty is null or { GetMethod: not null }))
                    {
                        methods |= SupportedMarshallingMethods.ManagedToNative;
                    }
                    else if (ManualTypeMarshallingHelper.IsStackallocConstructor(ctor, type, spanOfByte)
                        && (valueProperty is null or { GetMethod: not null }))
                    {
                        methods |= SupportedMarshallingMethods.ManagedToNativeStackalloc;
                    }
                }

                if (ManualTypeMarshallingHelper.HasToManagedMethod(nativeType, type)
                    && (valueProperty is null or { SetMethod: not null }))
                {
                    methods |= SupportedMarshallingMethods.NativeToManaged;
                }

                if (allowGetPinnableReference && ManualTypeMarshallingHelper.FindGetPinnableReference(type) != null)
                {
                    methods |= SupportedMarshallingMethods.Pinning;
                }

                if (methods == 0)
                {
                    // TODO: Diagnostic since no marshalling methods are supported.
                }

                return new NativeMarshallingAttributeInfo(
                    nativeType,
                    valueProperty?.Type,
                    methods);
            }
        
            static MarshallingInfo CreateTypeBasedMarshallingInfo(ITypeSymbol type, Compilation compilation)
            {
                var conversion = compilation.ClassifyCommonConversion(type, compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_SafeHandle)!);
                if (conversion.Exists && 
                    conversion.IsImplicit &&
                    conversion.IsReference &&
                    !type.IsAbstract)
                {
                    return new SafeHandleMarshallingInfo();
                }
                return null!;
            }
        }
#nullable restore

        private static SyntaxKind RefKindToSyntax(RefKind refKind)
        {
            return refKind switch
            {
                RefKind.In => SyntaxKind.InKeyword,
                RefKind.Ref => SyntaxKind.RefKeyword,
                RefKind.Out => SyntaxKind.OutKeyword,
                RefKind.None => SyntaxKind.None,
                _ => throw new NotImplementedException("Support for some RefKind"),
            };
        }
    }
}
