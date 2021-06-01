using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    /// <summary>
    /// Type used to pass on default marshalling details.
    /// </summary>
    internal sealed record DefaultMarshallingInfo (
        CharEncoding CharEncoding
    );

    /// <summary>
    /// Describes how to marshal the contents of a value in comparison to the value itself.
    /// Only makes sense for array-like types. For example, an "out" array doesn't change the
    /// pointer to the array value, but it marshals the contents of the native array back to the
    /// contents of the managed array.
    /// </summary>
    [Flags]
    internal enum ByValueContentsMarshalKind
    {
        /// <summary>
        /// Marshal contents from managed to native only.
        /// This is the default behavior.
        /// </summary>
        Default = 0x0,
        /// <summary>
        /// Marshal contents from managed to native only.
        /// This is the default behavior.
        /// </summary>
        In = 0x1,
        /// <summary>
        /// Marshal contents from native to managed only.
        /// </summary>
        Out = 0x2,
        /// <summary>
        /// Marshal contents both to and from native.
        /// </summary>
        InOut = In | Out
    }

    /// <summary>
    /// Positional type information involved in unmanaged/managed scenarios.
    /// </summary>
    internal sealed record TypePositionInfo
    {
        public const int UnsetIndex = int.MinValue;
        public const int ReturnIndex = UnsetIndex + 1;

// We don't need the warnings around not setting the various
// non-nullable fields/properties on this type in the constructor
// since we always use a property initializer.
#pragma warning disable 8618
        private TypePositionInfo()
        {
            this.ManagedIndex = UnsetIndex;
            this.NativeIndex = UnsetIndex;
        }
#pragma warning restore

        public string InstanceIdentifier { get; init; }
        public ITypeSymbol ManagedType { get; init; }

        public RefKind RefKind { get; init; }
        public SyntaxKind RefKindSyntax { get; init; }

        public bool IsByRef => RefKind != RefKind.None;

        public ByValueContentsMarshalKind ByValueContentsMarshalKind { get; init; }

        public bool IsManagedReturnPosition { get => this.ManagedIndex == ReturnIndex; }
        public bool IsNativeReturnPosition { get => this.NativeIndex == ReturnIndex; }

        public int ManagedIndex { get; init; }
        public int NativeIndex { get; init; }

        public MarshallingInfo MarshallingAttributeInfo { get; init; }

        public static TypePositionInfo CreateForParameter(IParameterSymbol paramSymbol, DefaultMarshallingInfo defaultInfo, Compilation compilation, GeneratorDiagnostics diagnostics, IMethodSymbol methodSymbol)
        {
            var marshallingInfo = GetMarshallingInfo(paramSymbol.Type, paramSymbol.GetAttributes(), defaultInfo, compilation, diagnostics, methodSymbol);
            var typeInfo = new TypePositionInfo()
            {
                ManagedType = paramSymbol.Type,
                InstanceIdentifier = ParseToken(paramSymbol.Name).IsReservedKeyword() ? $"@{paramSymbol.Name}" : paramSymbol.Name,
                RefKind = paramSymbol.RefKind,
                RefKindSyntax = RefKindToSyntax(paramSymbol.RefKind),
                MarshallingAttributeInfo = marshallingInfo,
                ByValueContentsMarshalKind = GetByValueContentsMarshalKind(paramSymbol.GetAttributes(), compilation)
            };

            return typeInfo;
        }

        public static TypePositionInfo CreateForType(ITypeSymbol type, IEnumerable<AttributeData> attributes, DefaultMarshallingInfo defaultInfo, Compilation compilation, GeneratorDiagnostics diagnostics, ISymbol symbol)
        {
            var marshallingInfo = GetMarshallingInfo(type, attributes, defaultInfo, compilation, diagnostics, symbol);
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

        public static TypePositionInfo CreateForType(ITypeSymbol type, MarshallingInfo marshallingInfo)
        {
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

        private static MarshallingInfo GetMarshallingInfo(ITypeSymbol type, IEnumerable<AttributeData> attributes, DefaultMarshallingInfo defaultInfo, Compilation compilation, GeneratorDiagnostics diagnostics, ISymbol contextSymbol, int indirectionLevel = 0)
        {
            CountInfo parsedCountInfo = NoCountInfo.Instance;
            // Look at attributes passed in - usage specific.
            foreach (var attrData in attributes)
            {
                INamedTypeSymbol attributeClass = attrData.AttributeClass!;

                if (indirectionLevel == 0
                    && SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_MarshalAsAttribute), attributeClass))
                {
                    // https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.marshalasattribute
                    return CreateMarshalAsInfo(type, attrData, defaultInfo, compilation);
                }
                else if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.MarshalUsingAttribute), attributeClass)
                    && AttributeAppliesToCurrentIndirectionLevel(attrData, indirectionLevel))
                {
                    if (parsedCountInfo != NoCountInfo.Instance)
                    {
                        diagnostics.ReportConfigurationNotSupported(attrData, "Duplicate Count Info");
                        return NoMarshallingInfo.Instance;
                    }
                    parsedCountInfo = CreateCountInfo(attrData);
                    if (attrData.ConstructorArguments.Length != 0)
                    {
                        return CreateNativeMarshallingInfo(type, attrData, isMarshalUsingAttribute: true, indirectionLevel, parsedCountInfo);
                    }
                }
            }

            // If we aren't overriding the marshalling at usage time,
            // then fall back to the information on the element type itself.
            foreach (var attrData in type.GetAttributes())
            {
                INamedTypeSymbol attributeClass = attrData.AttributeClass!;

                if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.BlittableTypeAttribute), attributeClass))
                {
                    // If type is generic, then we need to re-evaluate that it is blittable at usage time.
                    if (type is INamedTypeSymbol { IsGenericType: false } || type.HasOnlyBlittableFields())
                    {
                        return new BlittableTypeAttributeInfo();
                    }
                    break;
                }
                else if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.NativeMarshallingAttribute), attributeClass))
                {
                    return CreateNativeMarshallingInfo(type, attrData, isMarshalUsingAttribute: false, indirectionLevel, parsedCountInfo);
                }
                else if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.GeneratedMarshallingAttribute), attributeClass))
                {
                    return type.IsConsideredBlittable() ? new BlittableTypeAttributeInfo() : new GeneratedNativeMarshallingAttributeInfo(null! /* TODO: determine naming convention */);
                }
            }

            // If the type doesn't have custom attributes that dictate marshalling,
            // then consider the type itself.
            if (TryCreateTypeBasedMarshallingInfo(
                type,
                defaultInfo,
                compilation,
                diagnostics,
                contextSymbol.ContainingType,
                parsedCountInfo,
                indirectionLevel,
                out MarshallingInfo infoMaybe))
            {
                return infoMaybe;
            }

            // No marshalling info was computed, but a character encoding was provided.
            // If the type is a character or string then pass on these details.
            if (defaultInfo.CharEncoding != CharEncoding.Undefined
                && (type.SpecialType == SpecialType.System_Char
                    || type.SpecialType == SpecialType.System_String))
            {
                return new MarshallingInfoStringSupport(defaultInfo.CharEncoding);
            }

            return NoMarshallingInfo.Instance;

            MarshallingInfo CreateMarshalAsInfo(
                ITypeSymbol type,
                AttributeData attrData,
                DefaultMarshallingInfo defaultInfo,
                Compilation compilation)
            {
                object unmanagedTypeObj = attrData.ConstructorArguments[0].Value!;
                UnmanagedType unmanagedType = unmanagedTypeObj is short
                    ? (UnmanagedType)(short)unmanagedTypeObj
                    : (UnmanagedType)unmanagedTypeObj;
                if (!Enum.IsDefined(typeof(UnmanagedType), unmanagedType)
                    || unmanagedType == UnmanagedType.CustomMarshaler
                    || unmanagedType == UnmanagedType.SafeArray)
                {
                    diagnostics.ReportConfigurationNotSupported(attrData, nameof(UnmanagedType), unmanagedType.ToString());
                }
                bool isArrayType = unmanagedType == UnmanagedType.LPArray || unmanagedType == UnmanagedType.ByValArray;
                UnmanagedType elementUnmanagedType = (UnmanagedType)SizeAndParamIndexInfo.UnspecifiedData;
                SizeAndParamIndexInfo arraySizeInfo = SizeAndParamIndexInfo.Unspecified;

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
                        case nameof(MarshalAsAttribute.MarshalTypeRef):
                        case nameof(MarshalAsAttribute.MarshalType):
                        case nameof(MarshalAsAttribute.MarshalCookie):
                            diagnostics.ReportConfigurationNotSupported(attrData, $"{attrData.AttributeClass!.Name}{Type.Delimiter}{namedArg.Key}");
                            break;
                        case nameof(MarshalAsAttribute.ArraySubType):
                            if (!isArrayType)
                            {
                                diagnostics.ReportConfigurationNotSupported(attrData, $"{attrData.AttributeClass!.Name}{Type.Delimiter}{namedArg.Key}");
                            }
                            elementUnmanagedType = (UnmanagedType)namedArg.Value.Value!;
                            break;
                        case nameof(MarshalAsAttribute.SizeConst):
                            if (!isArrayType)
                            {
                                diagnostics.ReportConfigurationNotSupported(attrData, $"{attrData.AttributeClass!.Name}{Type.Delimiter}{namedArg.Key}");
                            }
                            arraySizeInfo = arraySizeInfo with { ConstSize = (int)namedArg.Value.Value! };
                            break;
                        case nameof(MarshalAsAttribute.SizeParamIndex):
                            if (!isArrayType)
                            {
                                diagnostics.ReportConfigurationNotSupported(attrData, $"{attrData.AttributeClass!.Name}{Type.Delimiter}{namedArg.Key}");
                            }
                            arraySizeInfo = arraySizeInfo with { ParamIndex = (short)namedArg.Value.Value! };
                            break;
                    }
                }

                if (!isArrayType)
                {
                    return new MarshalAsInfo(unmanagedType, defaultInfo.CharEncoding);
                }

                if (type is not IArrayTypeSymbol {  ElementType: ITypeSymbol elementType })
                {
                    diagnostics.ReportConfigurationNotSupported(attrData, nameof(UnmanagedType), unmanagedType.ToString());
                    return NoMarshallingInfo.Instance;
                }

                MarshallingInfo elementMarshallingInfo = NoMarshallingInfo.Instance;
                if (elementUnmanagedType != (UnmanagedType)SizeAndParamIndexInfo.UnspecifiedData)
                {
                    elementMarshallingInfo = new MarshalAsInfo(elementUnmanagedType, defaultInfo.CharEncoding);
                }
                else
                {
                    // Indirection level does not matter since we don't pass down attributes to be inspected.
                    elementMarshallingInfo = GetMarshallingInfo(elementType, Array.Empty<AttributeData>(), defaultInfo, compilation, diagnostics, contextSymbol, 0);
                }

                INamedTypeSymbol? arrayMarshaller;

                if (elementType is IPointerTypeSymbol { PointedAtType: ITypeSymbol pointedAt })
                {
                    arrayMarshaller = compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_GeneratedMarshalling_PtrArrayMarshaller_Metadata)?.Construct(pointedAt);
                }
                else
                {
                    arrayMarshaller  = compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_GeneratedMarshalling_ArrayMarshaller_Metadata)?.Construct(elementType);
                }

                if (arrayMarshaller is null)
                {
                    // If the array marshaler type is not available, then we cannot marshal arrays.
                    return NoMarshallingInfo.Instance;
                }

                return new NativeContiguousCollectionMarshallingInfo(
                    NativeMarshallingType: arrayMarshaller,
                    ValuePropertyType: ManualTypeMarshallingHelper.FindValueProperty(arrayMarshaller)?.Type,
                    MarshallingMethods: ~SupportedMarshallingMethods.Pinning,
                    NativeTypePinnable : true,
                    UseDefaultMarshalling: true,
                    ElementCountInfo: arraySizeInfo,
                    ElementType: elementType,
                    ElementMarshallingInfo: elementMarshallingInfo);
            }

            CountInfo CreateCountInfo(AttributeData marshalUsingData)
            {
                int? constSize = null;
                string? elementName = null;
                foreach (var arg in marshalUsingData.NamedArguments)
                {
                    if (arg.Key == "ConstantElementCount")
                    {
                        constSize = (int)arg.Value.Value!;
                    }
                    else if (arg.Key == "CountElementName")
                    {
                        if (arg.Value.Value is null)
                        {
                            diagnostics.ReportConfigurationNotSupported(marshalUsingData, "CountElementName", "null");
                            return NoCountInfo.Instance;
                        }
                        elementName = (string)arg.Value.Value!;
                    }
                }

                if (constSize is not null && elementName is not null)
                {
                    diagnostics.ReportConfigurationNotSupported(marshalUsingData, "ConstantElementCount and CountElementName combined");
                }
                else if (constSize is not null)
                {
                    return new ConstSizeCountInfo(constSize.Value);
                }
                else if (elementName is not null)
                {
                    TypePositionInfo? elementInfo = CreateForElementName(compilation, diagnostics, defaultInfo, contextSymbol, elementName);
                    if (elementInfo is null)
                    {
                        diagnostics.ReportConfigurationNotSupported(marshalUsingData, "CountElementName", elementName);
                        return NoCountInfo.Instance;
                    }
                    return new CountElementCountInfo(elementInfo);
                }

                return NoCountInfo.Instance;
            }

            MarshallingInfo CreateNativeMarshallingInfo(ITypeSymbol type, AttributeData attrData, bool isMarshalUsingAttribute, int indirectionLevel, CountInfo parsedCountInfo)
            {
                SupportedMarshallingMethods methods = SupportedMarshallingMethods.None;

                if (!isMarshalUsingAttribute && ManualTypeMarshallingHelper.FindGetPinnableReference(type) is not null)
                {
                    methods |= SupportedMarshallingMethods.Pinning;
                }

                ITypeSymbol spanOfByte = compilation.GetTypeByMetadataName(TypeNames.System_Span_Metadata)!.Construct(compilation.GetSpecialType(SpecialType.System_Byte));

                INamedTypeSymbol nativeType = (INamedTypeSymbol)attrData.ConstructorArguments[0].Value!;

                if (nativeType.IsUnboundGenericType)
                {
                    if (isMarshalUsingAttribute)
                    {
                        diagnostics.ReportConfigurationNotSupported(attrData, "Native Type", nativeType.ToDisplayString());
                        return NoMarshallingInfo.Instance;
                    }
                    else if (type is INamedTypeSymbol namedType)
                    {
                        if (namedType.Arity != nativeType.Arity)
                        {
                            diagnostics.ReportConfigurationNotSupported(attrData, "Native Type", nativeType.ToDisplayString());
                            return NoMarshallingInfo.Instance;
                        }
                        else
                        {
                            nativeType = nativeType.ConstructedFrom.Construct(namedType.TypeArguments.ToArray());
                        }
                    }
                    else
                    {
                        diagnostics.ReportConfigurationNotSupported(attrData, "Native Type", nativeType.ToDisplayString());
                        return NoMarshallingInfo.Instance;
                    }    
                }

                ITypeSymbol contiguousCollectionMarshalerAttribute = compilation.GetTypeByMetadataName(TypeNames.GenericContiguousCollectionMarshallerAttribute)!;

                bool isContiguousCollectionMarshaller = nativeType.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, contiguousCollectionMarshalerAttribute));
                IPropertySymbol? valueProperty = ManualTypeMarshallingHelper.FindValueProperty(nativeType);

                bool hasInt32Constructor = false;
                foreach (var ctor in nativeType.Constructors)
                {
                    if (ManualTypeMarshallingHelper.IsManagedToNativeConstructor(ctor, type, isCollectionMarshaller: isContiguousCollectionMarshaller)
                        && (valueProperty is null or { GetMethod: not null }))
                    {
                        methods |= SupportedMarshallingMethods.ManagedToNative;
                    }
                    else if (ManualTypeMarshallingHelper.IsStackallocConstructor(ctor, type, spanOfByte, isCollectionMarshaller: isContiguousCollectionMarshaller)
                        && (valueProperty is null or { GetMethod: not null }))
                    {
                        methods |= SupportedMarshallingMethods.ManagedToNativeStackalloc;
                    }
                    else if (ctor.Parameters.Length == 1 && ctor.Parameters[0].Type.SpecialType == SpecialType.System_Int32)
                    {
                        hasInt32Constructor = true;
                    }
                }

                // The constructor that takes only the native element size is required for collection marshallers
                // in the native-to-managed scenario.
                if ((!isContiguousCollectionMarshaller
                        || (hasInt32Constructor && ManualTypeMarshallingHelper.HasSetUnmarshalledCollectionLengthMethod(nativeType)))
                    && ManualTypeMarshallingHelper.HasToManagedMethod(nativeType, type)
                    && (valueProperty is null or { SetMethod: not null }))
                {
                    methods |= SupportedMarshallingMethods.NativeToManaged;
                }

                if (methods == SupportedMarshallingMethods.None)
                {
                    diagnostics.ReportConfigurationNotSupported(attrData, "Native Type", nativeType.ToDisplayString());
                    return NoMarshallingInfo.Instance;
                }

                if (isContiguousCollectionMarshaller)
                {
                    if (!ManualTypeMarshallingHelper.HasNativeValueStorageProperty(nativeType, spanOfByte))
                    {
                        diagnostics.ReportConfigurationNotSupported(attrData, "Native Type", nativeType.ToDisplayString());
                        return NoMarshallingInfo.Instance;
                    }

                    if (!ManualTypeMarshallingHelper.TryGetElementTypeFromContiguousCollectionMarshaller(nativeType, out ITypeSymbol elementType))
                    {
                        diagnostics.ReportConfigurationNotSupported(attrData, "Native Type", nativeType.ToDisplayString());
                        return NoMarshallingInfo.Instance;
                    }

                    return new NativeContiguousCollectionMarshallingInfo(
                        nativeType,
                        valueProperty?.Type,
                        methods,
                        NativeTypePinnable: ManualTypeMarshallingHelper.FindGetPinnableReference(nativeType) is not null,
                        UseDefaultMarshalling: !isMarshalUsingAttribute,
                        parsedCountInfo,
                        elementType,
                        GetMarshallingInfo(elementType, attributes, defaultInfo, compilation, diagnostics, contextSymbol, indirectionLevel + 1));
                }

                return new NativeMarshallingAttributeInfo(
                    nativeType,
                    valueProperty?.Type,
                    methods,
                    NativeTypePinnable: ManualTypeMarshallingHelper.FindGetPinnableReference(nativeType) is not null,
                    UseDefaultMarshalling: !isMarshalUsingAttribute);
            }

            static bool TryCreateTypeBasedMarshallingInfo(
                ITypeSymbol type,
                DefaultMarshallingInfo defaultInfo,
                Compilation compilation,
                GeneratorDiagnostics diagnostics,
                INamedTypeSymbol scopeSymbol,
                CountInfo parsedCountInfo,
                int indirectionLevel,
                out MarshallingInfo marshallingInfo)
            {
                var conversion = compilation.ClassifyCommonConversion(type, compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_SafeHandle)!);
                if (conversion.Exists 
                    && conversion.IsImplicit 
                    && (conversion.IsReference || conversion.IsIdentity))
                {
                    bool hasAccessibleDefaultConstructor = false;
                    if (type is INamedTypeSymbol named && !named.IsAbstract && named.InstanceConstructors.Length > 0)
                    {
                        foreach (var ctor in named.InstanceConstructors)
                        {
                            if (ctor.Parameters.Length == 0)
                            {
                                hasAccessibleDefaultConstructor = compilation.IsSymbolAccessibleWithin(ctor, scopeSymbol);
                                break;
                            }
                        }
                    }
                    marshallingInfo = new SafeHandleMarshallingInfo(hasAccessibleDefaultConstructor);
                    return true;
                }

                if (type is IArrayTypeSymbol { ElementType: ITypeSymbol elementType })
                {
                    INamedTypeSymbol? arrayMarshaller;

                    if (elementType is IPointerTypeSymbol { PointedAtType: ITypeSymbol pointedAt })
                    {
                        arrayMarshaller = compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_GeneratedMarshalling_PtrArrayMarshaller_Metadata)?.Construct(pointedAt);
                    }
                    else
                    {
                        arrayMarshaller = compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_GeneratedMarshalling_ArrayMarshaller_Metadata)?.Construct(elementType);
                    }

                    if (arrayMarshaller is null)
                    {
                        // If the array marshaler type is not available, then we cannot marshal arrays.
                        marshallingInfo = NoMarshallingInfo.Instance;
                        return false;
                    }

                    marshallingInfo = new  NativeContiguousCollectionMarshallingInfo(
                        NativeMarshallingType: arrayMarshaller,
                        ValuePropertyType: ManualTypeMarshallingHelper.FindValueProperty(arrayMarshaller)?.Type,
                        MarshallingMethods: ~SupportedMarshallingMethods.Pinning,
                        NativeTypePinnable: true,
                        UseDefaultMarshalling: true,
                        ElementCountInfo: parsedCountInfo,
                        ElementType: elementType,
                        ElementMarshallingInfo: GetMarshallingInfo(elementType, Array.Empty<AttributeData>(), defaultInfo, compilation, diagnostics, scopeSymbol, indirectionLevel + 1));
                    return true;
                }

                if (type is INamedTypeSymbol { IsValueType: true } valueType
                    && !valueType.IsExposedOutsideOfCurrentCompilation()
                    && valueType.IsConsideredBlittable())
                {
                    // Allow implicit [BlittableType] on internal value types.
                    marshallingInfo = new BlittableTypeAttributeInfo();
                    return true;
                }

                marshallingInfo = NoMarshallingInfo.Instance;
                return false;
            }
        }

        private static TypePositionInfo? CreateForElementName(Compilation compilation, GeneratorDiagnostics diagnostics, DefaultMarshallingInfo defaultInfo, ISymbol context, string elementName)
        {
            if (context is IMethodSymbol method)
            {
                if (elementName == CountElementCountInfo.ReturnValueElementName)
                {
                    return CreateForType(
                        method.ReturnType,
                        method.GetReturnTypeAttributes(),
                        defaultInfo,
                        compilation,
                        diagnostics,
                        method) with
                    {
                        ManagedIndex = ReturnIndex
                    };
                }

                foreach (var param in method.Parameters)
                {
                    if (param.Name == elementName)
                    {
                        return CreateForParameter(param, defaultInfo, compilation, diagnostics, method);
                    }
                }
            }
            else if (context is INamedTypeSymbol _)
            {
                // TODO: Handle when we create a struct marshalling generator
                // Do we want to support CountElementName pointing to only fields, or properties as well?
                // If only fields, how do we handle properties with generated backing fields?
            }

            return null;
        }

        private static bool AttributeAppliesToCurrentIndirectionLevel(AttributeData attrData, int indirectionLevel)
        {
            int elementIndirectionLevel = 0;
            foreach (var arg in attrData.NamedArguments)
            {
                if (arg.Key == "ElementIndirectionLevel")
                {
                    elementIndirectionLevel = (int)arg.Value.Value!;
                }
            }
            return elementIndirectionLevel == indirectionLevel;
        }

        private static ByValueContentsMarshalKind GetByValueContentsMarshalKind(IEnumerable<AttributeData> attributes, Compilation compilation)
        {
            INamedTypeSymbol outAttributeType = compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_OutAttribute)!;
            INamedTypeSymbol inAttributeType = compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_InAttribute)!;

            ByValueContentsMarshalKind marshalKind = ByValueContentsMarshalKind.Default;

            foreach (var attr in attributes)
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, outAttributeType))
                {
                    marshalKind |= ByValueContentsMarshalKind.Out;
                }
                else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, inAttributeType))
                {
                    marshalKind |= ByValueContentsMarshalKind.In;
                }
            }

            return marshalKind;
        }

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
