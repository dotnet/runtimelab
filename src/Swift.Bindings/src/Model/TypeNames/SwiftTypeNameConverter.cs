// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Utility class for converting things to Swift type names.
/// </summary>
public static class SwiftTypeNameConverter
{
    /// <summary>
    /// Creates a new SwiftTypeName from a type specification
    /// </summary>
    /// <param name="typeSpec">The type specification.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName Convert(NamedTypeSpec typeSpec)
    {
        ArgumentNullException.ThrowIfNull(typeSpec);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeSpec.Module);

        return SwiftTypeName.FromModuleQualifiedName(typeSpec.Name);
    }
}
