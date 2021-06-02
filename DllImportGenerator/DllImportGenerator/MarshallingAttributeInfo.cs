using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    /// Simple User-application of System.Runtime.InteropServices.MarshalAsAttribute
    /// </summary>
    internal sealed record MarshalAsInfo(
        UnmanagedType UnmanagedType,
        CharEncoding CharEncoding) : MarshallingInfoStringSupport(CharEncoding)
    {
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
        None = 0,
        ManagedToNative = 0x1,
        NativeToManaged = 0x2,
        ManagedToNativeStackalloc = 0x4,
        Pinning = 0x8,
        All = -1
    }

    internal abstract record CountInfo;

    internal sealed record NoCountInfo : CountInfo
    {
        public static readonly NoCountInfo Instance = new NoCountInfo();

        private NoCountInfo() { }
    }

    internal sealed record ConstSizeCountInfo(int Size) : CountInfo;

    internal sealed record CountElementCountInfo(TypePositionInfo ElementInfo) : CountInfo
    {
        public const string ReturnValueElementName = "return-value";
    }

    internal sealed record SizeAndParamIndexInfo(int ConstSize, int ParamIndex) : CountInfo
    {
        public const int UnspecifiedData = -1;

        public static readonly SizeAndParamIndexInfo Unspecified = new(UnspecifiedData, UnspecifiedData);
    }

    /// <summary>
    /// User-applied System.Runtime.InteropServices.NativeMarshallingAttribute
    /// </summary>
    internal record NativeMarshallingAttributeInfo(
        ITypeSymbol NativeMarshallingType,
        ITypeSymbol? ValuePropertyType,
        SupportedMarshallingMethods MarshallingMethods,
        bool NativeTypePinnable,
        bool UseDefaultMarshalling) : MarshallingInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.GeneratedMarshallingAttribute
    /// on a non-blittable type in source in this compilation.
    /// </summary>
    internal sealed record GeneratedNativeMarshallingAttributeInfo(
        string NativeMarshallingFullyQualifiedTypeName) : MarshallingInfo;

    /// <summary>
    /// The type of the element is a SafeHandle-derived type with no marshalling attributes.
    /// </summary>
    internal sealed record SafeHandleMarshallingInfo(bool AccessibleDefaultConstructor) : MarshallingInfo;

    /// <summary>
    /// User-applied System.Runtime.InteropServices.NativeMarshalllingAttribute
    /// with a contiguous collection marshaller
    internal sealed record NativeContiguousCollectionMarshallingInfo(
        ITypeSymbol NativeMarshallingType,
        ITypeSymbol? ValuePropertyType,
        SupportedMarshallingMethods MarshallingMethods,
        bool NativeTypePinnable,
        bool UseDefaultMarshalling,
        CountInfo ElementCountInfo,
        ITypeSymbol ElementType,
        MarshallingInfo ElementMarshallingInfo) : NativeMarshallingAttributeInfo(
            NativeMarshallingType,
            ValuePropertyType,
            MarshallingMethods,
            NativeTypePinnable,
            UseDefaultMarshalling
        );

    class MarshallingAttributeInfoParser
    {
        private readonly Compilation compilation;
        private readonly GeneratorDiagnostics diagnostics;
        private readonly DefaultMarshallingInfo defaultInfo;
        private readonly ISymbol contextSymbol;
        private readonly ITypeSymbol marshalAsAttribute;
        private readonly ITypeSymbol marshalUsingAttribute;

        public MarshallingAttributeInfoParser(
            Compilation compilation,
            GeneratorDiagnostics diagnostics,
            DefaultMarshallingInfo defaultInfo,
            ISymbol contextSymbol)
        {
            this.compilation = compilation;
            this.diagnostics = diagnostics;
            this.defaultInfo = defaultInfo;
            this.contextSymbol = contextSymbol;
            marshalAsAttribute = compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_MarshalAsAttribute)!;
            marshalUsingAttribute = compilation.GetTypeByMetadataName(TypeNames.MarshalUsingAttribute)!;
        }

        internal MarshallingInfo ParseMarshallingInfo(
            ITypeSymbol managedType,
            IEnumerable<AttributeData> useSiteAttributes)
        {
            Dictionary<int, AttributeData> marshallingAttributesByIndirectionLevel = new();
            foreach (AttributeData attribute in useSiteAttributes)
            {
                if (TryGetAttributeIndirectionLevel(attribute, out int indirectionLevel))
                {
                    if (marshallingAttributesByIndirectionLevel.ContainsKey(indirectionLevel))
                    {
                        diagnostics.ReportConfigurationNotSupported(attribute, "Marshalling Data for Indirection Level", indirectionLevel.ToString());
                        return NoMarshallingInfo.Instance;
                    }
                    marshallingAttributesByIndirectionLevel.Add(indirectionLevel, attribute);
                }
            }

            return GetMarshallingInfo(managedType, marshallingAttributesByIndirectionLevel);
        }

        private MarshallingInfo GetMarshallingInfo(ITypeSymbol type, Dictionary<int, AttributeData> useSiteAttributes, int indirectionLevel = 0)
        {
            CountInfo parsedCountInfo = NoCountInfo.Instance;

            if (useSiteAttributes.TryGetValue(indirectionLevel, out AttributeData useSiteAttribute))
            {
                INamedTypeSymbol attributeClass = useSiteAttribute.AttributeClass!;

                if (indirectionLevel == 0
                    && SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.System_Runtime_InteropServices_MarshalAsAttribute), attributeClass))
                {
                    // https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.marshalasattribute
                    return CreateInfoFromMarshalAs(type, useSiteAttribute);
                }
                else if (SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(TypeNames.MarshalUsingAttribute), attributeClass))
                {
                    if (parsedCountInfo != NoCountInfo.Instance)
                    {
                        diagnostics.ReportConfigurationNotSupported(useSiteAttribute, "Duplicate Count Info");
                        return NoMarshallingInfo.Instance;
                    }
                    parsedCountInfo = CreateCountInfo(useSiteAttribute);
                    if (useSiteAttribute.ConstructorArguments.Length != 0)
                    {
                        return CreateNativeMarshallingInfo(type, useSiteAttribute, isMarshalUsingAttribute: true, indirectionLevel, parsedCountInfo, useSiteAttributes);
                    }
                }
            }

            // If we aren't overriding the marshalling at usage time,
            // then fall back to the information on the element type itself.
            foreach (var typeAttribute in type.GetAttributes())
            {
                INamedTypeSymbol attributeClass = typeAttribute.AttributeClass!;

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
                    return CreateNativeMarshallingInfo(type, typeAttribute, isMarshalUsingAttribute: false, indirectionLevel, parsedCountInfo, useSiteAttributes);
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
                parsedCountInfo,
                indirectionLevel,
                useSiteAttributes,
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
                TypePositionInfo? elementInfo = CreateForElementName(elementName);
                if (elementInfo is null)
                {
                    diagnostics.ReportConfigurationNotSupported(marshalUsingData, "CountElementName", elementName);
                    return NoCountInfo.Instance;
                }
                return new CountElementCountInfo(elementInfo);
            }

            return NoCountInfo.Instance;
        }

        private TypePositionInfo? CreateForElementName(string elementName)
        {
            if (contextSymbol is IMethodSymbol method)
            {
                if (elementName == CountElementCountInfo.ReturnValueElementName)
                {
                    return TypePositionInfo.CreateForType(
                        method.ReturnType,
                        ParseMarshallingInfo(method.ReturnType, method.GetReturnTypeAttributes())) with
                    {
                        ManagedIndex = TypePositionInfo.ReturnIndex
                    };
                }

                foreach (var param in method.Parameters)
                {
                    if (param.Name == elementName)
                    {
                        return TypePositionInfo.CreateForParameter(param, this, compilation);
                    }
                }
            }
            else if (contextSymbol is INamedTypeSymbol _)
            {
                // TODO: Handle when we create a struct marshalling generator
                // Do we want to support CountElementName pointing to only fields, or properties as well?
                // If only fields, how do we handle properties with generated backing fields?
            }

            return null;
        }

        MarshallingInfo CreateInfoFromMarshalAs(
            ITypeSymbol type,
            AttributeData attrData)
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

            if (type is not IArrayTypeSymbol { ElementType: ITypeSymbol elementType })
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
                elementMarshallingInfo = GetMarshallingInfo(elementType, new Dictionary<int, AttributeData>(), 0);
            }

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
                return NoMarshallingInfo.Instance;
            }

            return new NativeContiguousCollectionMarshallingInfo(
                NativeMarshallingType: arrayMarshaller,
                ValuePropertyType: ManualTypeMarshallingHelper.FindValueProperty(arrayMarshaller)?.Type,
                MarshallingMethods: ~SupportedMarshallingMethods.Pinning,
                NativeTypePinnable: true,
                UseDefaultMarshalling: true,
                ElementCountInfo: arraySizeInfo,
                ElementType: elementType,
                ElementMarshallingInfo: elementMarshallingInfo);
        }

        MarshallingInfo CreateNativeMarshallingInfo(ITypeSymbol type, AttributeData attrData, bool isMarshalUsingAttribute, int indirectionLevel, CountInfo parsedCountInfo, Dictionary<int, AttributeData> useSiteAttributes)
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
                    GetMarshallingInfo(elementType, useSiteAttributes, indirectionLevel + 1));
            }

            return new NativeMarshallingAttributeInfo(
                nativeType,
                valueProperty?.Type,
                methods,
                NativeTypePinnable: ManualTypeMarshallingHelper.FindGetPinnableReference(nativeType) is not null,
                UseDefaultMarshalling: !isMarshalUsingAttribute);
        }

        bool TryCreateTypeBasedMarshallingInfo(
            ITypeSymbol type,
            CountInfo parsedCountInfo,
            int indirectionLevel,
            Dictionary<int, AttributeData> useSiteAttributes,
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
                            hasAccessibleDefaultConstructor = compilation.IsSymbolAccessibleWithin(ctor, contextSymbol.ContainingType);
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

                marshallingInfo = new NativeContiguousCollectionMarshallingInfo(
                    NativeMarshallingType: arrayMarshaller,
                    ValuePropertyType: ManualTypeMarshallingHelper.FindValueProperty(arrayMarshaller)?.Type,
                    MarshallingMethods: ~SupportedMarshallingMethods.Pinning,
                    NativeTypePinnable: true,
                    UseDefaultMarshalling: true,
                    ElementCountInfo: parsedCountInfo,
                    ElementType: elementType,
                    ElementMarshallingInfo: GetMarshallingInfo(elementType, useSiteAttributes, indirectionLevel + 1));
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

        private bool TryGetAttributeIndirectionLevel(AttributeData attrData, out int indirectionLevel)
        {
            if (SymbolEqualityComparer.Default.Equals(attrData.AttributeClass, marshalAsAttribute))
            {
                indirectionLevel = 0;
                return true;
            }

            if (!SymbolEqualityComparer.Default.Equals(attrData.AttributeClass, marshalUsingAttribute))
            {
                indirectionLevel = 0;
                return false;
            }

            foreach (var arg in attrData.NamedArguments)
            {
                if (arg.Key == "ElementIndirectionLevel")
                {
                    indirectionLevel = (int)arg.Value.Value!;
                    return true;
                }
            }
            indirectionLevel = 0;
            return true;
        }
    }
}
