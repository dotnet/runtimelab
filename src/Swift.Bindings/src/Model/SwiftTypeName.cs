// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/// <summary>
/// Represents a Swift type name.
/// </summary>
/// <param name="Module">The module name.</param>
/// <param name="Name">The type name.</param>
/// <param name="ModuleQualifiedName">The module-qualified type name. Includes all parent types.</param>
public record SwiftTypeName(string Module, string Name, string ModuleQualifiedName)
{
    public static SwiftTypeName NestedType(SwiftTypeName parent, string name) => new SwiftTypeName(parent.Module, $"{parent.Name}.{name}", $"{parent.ModuleQualifiedName}.{name}");
    public static SwiftTypeName ModuleLevelType(string module, string name) => new SwiftTypeName(module, name, $"{module}.{name}");
}
