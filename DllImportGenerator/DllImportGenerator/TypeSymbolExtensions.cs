using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

#nullable enable

namespace Microsoft.Interop
{
    static class TypeSymbolExtensions
    {
        public static bool HasOnlyBlittableFields(this INamedTypeSymbol type, params ITypeParameterSymbol[] conditionalGenericTypeParameters)
        {
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
            {
                bool? fieldBlittable = field switch
                {
                    { IsStatic : true } => null,
                    { Type : { IsReferenceType : true }} => false,
                    { Type : IPointerTypeSymbol ptr } => IsConsideredBlittable(ptr.PointedAtType),
                    { Type : IFunctionPointerTypeSymbol } => true,
                    not { Type : { SpecialType : SpecialType.None }} => IsSpecialTypeBlittable(field.Type.SpecialType),
                    { Type : ITypeParameterSymbol t } => conditionalGenericTypeParameters.Contains(t),
                    { Type : { IsValueType : false }} => false,
                    // A recursive struct type isn't blittable.
                    // It's also illegal in C#, but I believe that source generators run
                    // before that is detected, so we check here to avoid a stack overflow.
                    // [TODO]: Handle mutual recursion.
                    _ => !SymbolEqualityComparer.Default.Equals(field.Type, type) && IsConsideredBlittable(field.Type)
                };

                if (fieldBlittable is false)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSpecialTypeBlittable(SpecialType specialType)
         => specialType switch 
         {
            SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_IntPtr
            or SpecialType.System_UIntPtr => true,
            _ => false
         };
        
        public static bool IsConsideredBlittable(this ITypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
            {
                return IsSpecialTypeBlittable(type.SpecialType);
            }
            
            if (!type.IsValueType || type.IsReferenceType)
            {
                return false;
            }

            bool hasNativeMarshallingAttribute = false;
            bool hasGeneratedMarshallingAttribute = false;
            ImmutableArray<TypedConstant> conditionalBlittableIndices = default;
            // [TODO]: Match attributes on full name or symbol, not just on type name.
            foreach (var attr in type.GetAttributes())
            {
                if (attr.AttributeClass is null)
                {
                    continue;
                }
                if (attr.AttributeClass.Name == "BlittableTypeAttribute")
                {
                    return true;
                }
                else if (attr.AttributeClass.Name == "GeneratedMarshallingAttribute")
                {
                    hasGeneratedMarshallingAttribute = true;
                }
                else if (attr.AttributeClass.Name == "NativeMarshallingAttribute")
                {
                    hasNativeMarshallingAttribute = true;
                }
                else if (attr.AttributeClass.Name == "BlittableTypeIfGenericParametersBlittableAttribute")
                {
                    conditionalBlittableIndices = attr.ConstructorArguments[0].Values;
                }
            }

            if (!conditionalBlittableIndices.IsDefaultOrEmpty &&
                type is INamedTypeSymbol {TypeArguments: ImmutableArray<ITypeSymbol> typeArguments})
            {
                foreach (var idx in conditionalBlittableIndices)
                {
                    var idxValue = (int)idx.Value!;
                    if (idxValue < typeArguments.Length && !typeArguments[idxValue].IsConsideredBlittable())
                    {
                        return false;
                    }
                }
                return true;
            }

            if (hasGeneratedMarshallingAttribute && !hasNativeMarshallingAttribute)
            {
                // The struct type has generated marshalling via a source generator.
                // We can't guarantee that we can see the results of the struct source generator,
                // so we re-calculate if the type is blittable here.
                return ((INamedTypeSymbol)type).HasOnlyBlittableFields();
            }
            return false;
        }

        public static bool IsAutoLayout(this INamedTypeSymbol type, ITypeSymbol structLayoutAttributeType)
        {
            bool defaultAuto = type.IsReferenceType;
            foreach (var attr in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(structLayoutAttributeType, attr.AttributeClass))
                {
                    return (LayoutKind)(int)attr.ConstructorArguments[0].Value! == LayoutKind.Auto;
                }
            }
            return defaultAuto;
        }
    }
}