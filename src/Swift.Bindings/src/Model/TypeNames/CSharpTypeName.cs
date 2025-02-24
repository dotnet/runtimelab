// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

/// <summary>
/// Represents a C# type name.
/// </summary>
public record CSharpTypeName
{
    /// <summary>
    /// The namespace name.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// The type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The fully qualified type name, including namespace. The name is not assembly qualified.
    /// </summary>
    public string FullyQualifiedName { get; }

    /// <inheritdoc />
    public override string ToString() => FullyQualifiedName;

    private CSharpTypeName(string @namespace, string name, string fullyQualifiedName)
    {
        Namespace = @namespace;
        Name = name;
        FullyQualifiedName = fullyQualifiedName;
    }

    /// <summary>
    /// Creates a new CSharpTypeName from a namespace and type name.
    /// </summary>
    /// <param name="namespace">The namespace.</param>
    /// <param name="name">The type name.</param>
    /// <returns>The CSharpTypeName.</returns>
    public static CSharpTypeName FromNamespaceAndName(string @namespace, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentException.ThrowIfNullOrEmpty(@namespace, nameof(@namespace));

        if (name.Contains('<'))
        {
            throw new ArgumentException("Cannot create a CSharpTypeName from a generic type.");
        }

        var fullyQualifiedName = $"{@namespace}.{name}";
        return new CSharpTypeName(@namespace, name, fullyQualifiedName);
    }

    /// <summary>
    /// C# type name for void.
    /// </summary>
    public static readonly CSharpTypeName VoidType = new CSharpTypeName("", "", "void");

    /// <summary>
    /// C# type name for object.
    /// </summary>
    public static readonly CSharpTypeName AnyType = new CSharpTypeName("Swift", "AnyType", "Swift.AnyType");
}
