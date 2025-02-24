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
    /// Creates a new SwiftTypeName from a module-qualified name.
    /// </summary>
    /// <param name="moduleQualifiedName">The module-qualified name.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName FromModuleQualifiedName(string moduleQualifiedName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleQualifiedName, nameof(moduleQualifiedName));

        if (moduleQualifiedName.Contains('<'))
        {
            throw new ArgumentException("Cannot create a SwiftTypeName from a generic type.");
        }

        var parts = moduleQualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid module-qualified name: {moduleQualifiedName}");
        }

        return new SwiftTypeName(parts.First(), parts.Last(), moduleQualifiedName);
    }

    /// <summary>
    /// Creates a new SwiftTypeName from a NamedTypeSpec.
    /// </summary>
    /// <param name="namedTypeSpec">The NamedTypeSpec.</param>
    /// <returns>The SwiftTypeName.</returns>
    public static SwiftTypeName FromTypeSpec(NamedTypeSpec namedTypeSpec)
    {
        ArgumentNullException.ThrowIfNull(namedTypeSpec, nameof(namedTypeSpec));
        return FromModuleQualifiedName(namedTypeSpec.Name);
    }

    /// <summary>
    /// Swift type name for void.
    /// </summary>
    public static readonly SwiftTypeName VoidType = new SwiftTypeName(string.Empty, "()", "()");

    /// <summary>
    /// Swift type name for Any.
    /// </summary>
    public static readonly SwiftTypeName AnyType = new SwiftTypeName(string.Empty, "Any", "Any");
}
