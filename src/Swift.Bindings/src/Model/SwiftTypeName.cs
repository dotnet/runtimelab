// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Represents a Swift type name.
/// </summary>
public record SwiftTypeName
{
    /// <summary>
    /// The module name.
    /// </summary>
    public string Module { get; }

    /// <summary>
    /// The type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The module-qualified type name. Includes all parent types.
    /// </summary>
    public string ModuleQualifiedName { get; }

    /// <inheritdoc />
    public override string ToString() => ModuleQualifiedName;

    private SwiftTypeName(string module, string name, string moduleQualifiedName)
    {
        Module = module;
        Name = name;
        ModuleQualifiedName = moduleQualifiedName;
    }

    /// <summary>
    /// Creates a new SwiftTypeName from a parent type and a name.
    /// </summary>
    /// <param name="parent">The parent type.</param>
    /// <param name="name">The name of the type.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName FromParentTypeAndName(SwiftTypeName parent, string name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        return new SwiftTypeName(parent.Module, $"{parent.Name}.{name}", $"{parent.ModuleQualifiedName}.{name}");
    }

    /// <summary>
    /// Creates a new SwiftTypeName from a module and a name.
    /// </summary>
    /// <param name="module">The module name.</param>
    /// <param name="name">The name of the type.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName FromModuleAndName(string module, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(module, nameof(module));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        return new SwiftTypeName(module, name, $"{module}.{name}");
    }

    /// <summary>
    /// Creates a new SwiftTypeName from a type specification
    /// </summary>
    /// <param name="typeSpec">The type specification.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName FromTypeSpec(TypeSpec typeSpec) =>
        FromTypeSpecInternal(typeSpec, namedTypeSpec => namedTypeSpec.NameWithoutModule);

    /// <summary>
    /// Creates a new SwiftTypeName from a type specification with generic parameters.
    /// </summary>
    /// <param name="typeSpec">The type specification.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName FromTypeSpecWithGenericParameters(TypeSpec typeSpec) =>
        FromTypeSpecInternal(typeSpec, namedTypeSpec => namedTypeSpec.NameWithoutModuleWithGenericParameters); // TODO: Remove this once we have a better way to handle bound generics.

    /// <summary>
    /// Swift type name for void.
    /// </summary>
    public static readonly SwiftTypeName VoidType = new SwiftTypeName(string.Empty, "()", "()");

    /// <summary>
    /// Swift type name for Any.
    /// </summary>
    public static readonly SwiftTypeName AnyType = new SwiftTypeName(string.Empty, "Any", "Any");

    /// <summary>
    /// Creates a new SwiftTypeName from a type specification
    /// </summary>
    /// <param name="typeSpec">The type specification.</param>
    /// <param name="nameSelector">The function to select the name from the named type spec.</param>
    /// <returns>The SwiftTypeName.</returns>
    private static SwiftTypeName FromTypeSpecInternal(TypeSpec typeSpec, Func<NamedTypeSpec, string> nameSelector)
    {
        ArgumentNullException.ThrowIfNull(typeSpec);

        if (typeSpec.IsEmptyTuple)
        {
            return VoidType;
        }

        return typeSpec switch
        {
            NamedTypeSpec namedTypeSpec when namedTypeSpec.Module is { } module =>
                new SwiftTypeName(module, nameSelector(namedTypeSpec), $"{module}.{nameSelector(namedTypeSpec)}"),
            _ => throw new ArgumentException($"Unsupported type spec: {typeSpec}")
        };
    }
}
