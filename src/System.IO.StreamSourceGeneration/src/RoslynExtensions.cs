// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using System.Linq;

namespace System.IO.StreamSourceGeneration
{
    internal static class RoslynExtensions
    {
        public static Location? GetLocation(this ISymbol typeSymbol)
            => typeSymbol.Locations.Length > 0 ? typeSymbol.Locations[0] : null;

        public static bool IsAssignableFrom(this ITypeSymbol? baseType, ITypeSymbol? type)
        {
            if (baseType is null || type is null)
            {
                return false;
            }

            if (baseType.TypeKind is TypeKind.Interface)
            {
                if (type.AllInterfaces.Contains(baseType, SymbolEqualityComparer.Default))
                {
                    return true;
                }
            }

            for (INamedTypeSymbol? current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, current))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
