using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
            this.UnmanagedIndex = UnsetIndex;
            this.UnmanagedLCIDConversionArgIndex = UnsetIndex;
        }

        public ITypeSymbol TypeSymbol { get; private set; }
        public string InstanceIdentifier { get; private set; }

        public RefKind RefKind { get; private set; }
        public string RefKindDecl { get => RefKindToString(this.RefKind); }
        public string ManagedTypeDecl { get; private set; }
        public string UnmanagedTypeDecl { get; private set; }

        public bool IsManagedReturnPosition { get => this.ManagedIndex == ReturnIndex; }
        public bool IsUnmanagedReturnPosition { get => this.UnmanagedIndex == ReturnIndex; }

        public int ManagedIndex { get; set; }
        public int UnmanagedIndex { get; set; }
        public int UnmanagedLCIDConversionArgIndex { get; private set; }

        public MarshallingAttributeInfo MarshallingAttributeInfo { get; private set; }

        public static TypePositionInfo CreateForParameter(IParameterSymbol paramSymbol, SemanticModel model)
        {
            var marshallingInfo = GetMarshallingAttributeInfo(paramSymbol.GetAttributes());
            var typeInfo = new TypePositionInfo()
            {
                TypeSymbol = paramSymbol.Type,
                InstanceIdentifier = paramSymbol.Name,
                ManagedTypeDecl = ComputeTypeForManaged(paramSymbol.Type, paramSymbol.RefKind),
                UnmanagedTypeDecl = ComputeTypeForUnmanaged(paramSymbol.Type, paramSymbol.RefKind, marshallingInfo, model),
                RefKind = paramSymbol.RefKind,
                MarshallingAttributeInfo = marshallingInfo
            };

            typeInfo.MarshallingAttributeInfo = GetMarshallingAttributeInfo(paramSymbol.GetAttributes());

            return typeInfo;
        }

        public static TypePositionInfo CreateForType(ITypeSymbol type, IEnumerable<AttributeData> attributes, SemanticModel model)
        {
            var marshallingInfo = GetMarshallingAttributeInfo(attributes);
            var typeInfo = new TypePositionInfo()
            {
                TypeSymbol = type,
                InstanceIdentifier = string.Empty,
                ManagedTypeDecl = ComputeTypeForManaged(type, RefKind.None),
                UnmanagedTypeDecl = ComputeTypeForUnmanaged(type, RefKind.None, marshallingInfo, model),
                RefKind = RefKind.None,
                MarshallingAttributeInfo = marshallingInfo
            };

            return typeInfo;
        }

#nullable enable
        private static MarshallingAttributeInfo? GetMarshallingAttributeInfo(IEnumerable<AttributeData> attributes)
        {
            MarshallingAttributeInfo? marshallingInfo = null;
            // Look at attributes on the type.
            foreach (var attrData in attributes)
            {
                string attributeName = attrData.AttributeClass!.Name;

                if (nameof(MarshalAsAttribute).Equals(attributeName))
                {
                    if (marshallingInfo is not null)
                    {
                        // TODO: diagnostic
                    }
                    // https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.marshalasattribute
                    marshallingInfo = CreateMarshalAsInfo(attrData);
                }
            }

            return null;

            static MarshalAsInfo CreateMarshalAsInfo(AttributeData attrData)
            {
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
                    UnmanagedType: (UnmanagedType)attrData.ConstructorArguments[0].Value!,
                    CustomMarshallerTypeName: customMarshallerTypeName,
                    CustomMarshallerCookie: customMarshallerCookie,
                    UnmanagedArraySubType: unmanagedArraySubType,
                    ArraySizeConst: arraySizeConst,
                    ArraySizeParamIndex: arraySizeParamIndex
                );
            }
        }
#nullable restore

        private static string ComputeTypeForManaged(ITypeSymbol type, RefKind refKind)
        {
            return $"{RefKindToString(refKind)}{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";
        }

        private static string ComputeTypeForUnmanaged(ITypeSymbol type, RefKind refKind, MarshallingAttributeInfo attrInfo, SemanticModel model)
        {
#if GENERATE_FORWARDER
            return ComputeTypeForManaged(type, refKind);
#else
#nullable enable
            // TODO: Handle CharSet

            string? unmanagedTypeName = (type, attrInfo) switch
            {
                // New marshalling attributes
                (_, BlittableTypeAttributeInfo _) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                (_, NativeMarshallingAttributeInfo { ValuePropertyType : null, NativeMarshallingType : {} nativeType}) => nativeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                (_, NativeMarshallingAttributeInfo { ValuePropertyType : {} nativeType }) => nativeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                (_, GeneratedNativeMarshallingAttributeInfo { NativeMarshallingFullyQualifiedTypeName: string name }) => name,
                // Primitive types and string default marshalling
                ({ SpecialType: SpecialType.None } or ({ IsValueType : false } and not { SpecialType : SpecialType.System_String }), _) => null,
                ({ SpecialType: SpecialType.System_Boolean }, null or MarshalAsInfo { UnmanagedType: 0 }) => "byte", // [TODO] Determine marshalling default C++ bool or Windows' BOOL
                ({ SpecialType: SpecialType.System_Char }, null or MarshalAsInfo { UnmanagedType: 0 }) => "ushort", // [TODO] Determine based on charset.
                ({ SpecialType: SpecialType.System_String }, null or MarshalAsInfo { UnmanagedType: 0 }) => "ushort*", // [TODO] Determine based on charset.
                ({ SpecialType: SpecialType.System_Void }, null) => "void",
                ({ SpecialType: SpecialType.System_SByte }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I1 }) => "sbyte",
                ({ SpecialType: SpecialType.System_Byte }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U1 }) => "byte",
                ({ SpecialType: SpecialType.System_Int16 }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I2 }) => "short",
                ({ SpecialType: SpecialType.System_UInt16 }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U2 }) => "ushort",
                ({ SpecialType: SpecialType.System_Int32 }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I4 }) => "int",
                ({ SpecialType: SpecialType.System_UInt32 }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U4 }) => "uint",
                ({ SpecialType: SpecialType.System_Int64 }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I8 }) => "long",
                ({ SpecialType: SpecialType.System_UInt64 }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U8 }) => "ulong",
                ({ SpecialType: SpecialType.System_IntPtr }, null or MarshalAsInfo { UnmanagedType: 0 }) => "nint",
                ({ SpecialType: SpecialType.System_UIntPtr }, null or MarshalAsInfo { UnmanagedType: 0 }) => "nuint",
                ({ SpecialType: SpecialType.System_Single }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.R4 }) => "float",
                ({ SpecialType: SpecialType.System_Double }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.R8 }) => "double",
                (IPointerTypeSymbol ptr, null or MarshalAsInfo { UnmanagedType: 0 }) => ptr.PointedAtType.IsConsideredBlittable() ? ptr.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : null,
                (IFunctionPointerTypeSymbol ptr, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.FunctionPtr }) => ptr.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                // Delegate marshalling
                (INamedTypeSymbol _, null) => model.Compilation.ClassifyCommonConversion(type, model.Compilation.GetSpecialType(SpecialType.System_Delegate)).IsImplicit ? "global::System.IntPtr" : null,
                // Bool custom marshalling
                ({ SpecialType: SpecialType.System_Boolean }, MarshalAsInfo { UnmanagedType: UnmanagedType.I1 or UnmanagedType.U1 }) => "byte",
                ({ SpecialType: SpecialType.System_Boolean }, MarshalAsInfo { UnmanagedType: UnmanagedType.VariantBool }) => "short",
                ({ SpecialType: SpecialType.System_Boolean }, MarshalAsInfo { UnmanagedType: UnmanagedType.I4 or UnmanagedType.U4 }) => "int",
                ({ SpecialType: SpecialType.System_String }, MarshalAsInfo { UnmanagedType: UnmanagedType.LPStr }) => "byte*",
                ({ SpecialType: SpecialType.System_String }, MarshalAsInfo { UnmanagedType: UnmanagedType.LPWStr }) => "ushort*",
                ({ SpecialType: SpecialType.System_String }, MarshalAsInfo { UnmanagedType: UnmanagedType.LPTStr }) => "byte*", // [TODO] Determine based on charset.
                ({ SpecialType: SpecialType.System_String }, MarshalAsInfo { UnmanagedType: UnmanagedType.ByValTStr, ArraySizeConst: > 0 } info ) => $"fixed byte[{info.ArraySizeConst}]", // [TODO] Determine based on charset. Fix generation to support fixed size buffers.
                ({ SpecialType: SpecialType.System_String }, MarshalAsInfo { UnmanagedType: (UnmanagedType)48 /* UnmanagedType.LPUTF8Str */ }) => "byte*",
                // Array marshalling
                (IArrayTypeSymbol { IsSZArray: true, ElementType: {} elementType }, null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.LPArray, UnmanagedArraySubType: 0 }) => elementType.IsConsideredBlittable() ? "global::System.IntPtr" : null,
                (IArrayTypeSymbol { IsSZArray: true, ElementType: {} elementType }, MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.LPArray, UnmanagedArraySubType: {} unmanagedType }) => ComputeTypeForUnmanaged(elementType, RefKind.None, new MarshalAsInfo(unmanagedType, null, null, 0, 0, 0), model) is not null ? "global::System.IntPtr" : null,
                // TODO: Support custom MarshalAs for ByVal arrays, other custom MarshalAs types.
                _ => null
            };

            return unmanagedTypeName;
#nullable restore
#endif
        }

        private static string RefKindToString(RefKind refKind)
        {
            return refKind switch
            {
                RefKind.In => "in ",
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.None => string.Empty,
                _ => throw new NotImplementedException("Support for some RefKind"),
            };
        }
    }
}
