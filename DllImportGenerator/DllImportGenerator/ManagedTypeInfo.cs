using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Interop
{
    /// <summary>
    /// A discriminated union that contains enough info about a managed type to determine a marshalling generator and generate code.
    /// </summary>
    internal abstract record ManagedTypeInfo(string FullTypeName)
    {
        public TypeSyntax Syntax { get; } = SyntaxFactory.ParseTypeName(FullTypeName);

        public static ManagedTypeInfo CreateTypeInfoForTypeSymbol(ITypeSymbol type)
        {
            string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (type.SpecialType != SpecialType.None)
            {
                return new SpecialTypeInfo(typeName, type.SpecialType);
            }
            if (type.TypeKind == TypeKind.Enum)
            {
                return new EnumTypeInfo(typeName, ((INamedTypeSymbol)type).EnumUnderlyingType!.SpecialType);
            }
            if (type.TypeKind == TypeKind.Pointer)
            {
                return new PointerTypeInfo(typeName, IsFunctionPointer: false);
            }
            if (type.TypeKind == TypeKind.FunctionPointer)
            {
                return new PointerTypeInfo(typeName, IsFunctionPointer: true);
            }
            if (type.TypeKind == TypeKind.Array && type is IArrayTypeSymbol { IsSZArray: true } arraySymbol)
            {
                return new SzArrayType(CreateTypeInfoForTypeSymbol(arraySymbol.ElementType));
            }
            if (type.TypeKind == TypeKind.Delegate)
            {
                return new DelegateTypeInfo(typeName);
            }
            return new SimpleManagedTypeInfo(typeName);
        }
    }

    internal sealed record SpecialTypeInfo(string FullTypeName, SpecialType SpecialType) : ManagedTypeInfo(FullTypeName)
    {
        public static readonly SpecialTypeInfo Int32 = new("int", SpecialType.System_Int32);

        public bool Equals(SpecialTypeInfo? other)
        {
            return other is not null && SpecialType == other.SpecialType;
        }

        public override int GetHashCode()
        {
            return (int)SpecialType;
        }
    }

    internal sealed record EnumTypeInfo(string FullTypeName, SpecialType UnderlyingType) : ManagedTypeInfo(FullTypeName);

    internal sealed record PointerTypeInfo(string FullTypeName, bool IsFunctionPointer) : ManagedTypeInfo(FullTypeName);

    internal sealed record SzArrayType(ManagedTypeInfo ElementTypeInfo) : ManagedTypeInfo($"{ElementTypeInfo.FullTypeName}[]");

    internal sealed record DelegateTypeInfo(string FullTypeName) : ManagedTypeInfo(FullTypeName);

    internal sealed record SimpleManagedTypeInfo(string FullTypeName) : ManagedTypeInfo(FullTypeName);
}
