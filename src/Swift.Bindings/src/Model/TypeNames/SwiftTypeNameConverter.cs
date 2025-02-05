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
    public static SwiftTypeName Convert(NamedTypeSpec typeSpec) =>
        FromTypeSpecInternal(typeSpec, namedTypeSpec => namedTypeSpec.Name);

    /// <summary>
    /// Creates a new SwiftTypeName from a type specification with generic parameters.
    /// </summary>
    /// <param name="typeSpec">The type specification.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName ConvertWithGenericParameters(NamedTypeSpec typeSpec) =>
        FromTypeSpecInternal(typeSpec, namedTypeSpec => namedTypeSpec.NameWithGenericParameters); // TODO: Remove this once we have a better way to handle bound generics.

    /// <summary>
    /// Creates a new SwiftTypeName from a type specification
    /// </summary>
    /// <param name="typeSpec">The type specification.</param>
    /// <param name="nameSelector">The function to select the name from the named type spec.</param>
    /// <returns>The SwiftTypeName.</returns>
    private static SwiftTypeName FromTypeSpecInternal(NamedTypeSpec typeSpec, Func<NamedTypeSpec, string> nameSelector)
    {
        ArgumentNullException.ThrowIfNull(typeSpec);

        if (typeSpec.Module is null)
        {
            throw new ArgumentException($"Type spec does not have a module: {typeSpec}");
        }

        return SwiftTypeName.FromModuleQualifiedName(nameSelector(typeSpec));
    }
}
