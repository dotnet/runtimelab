// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

public static class TypeDatabaseExtensions
{
    /// <summary>
    /// Determines whether the specified Swift type has been processed.
    /// </summary>
    /// <param name="typeDatabase">The type database.</param>
    /// <param name="typeSpec">The Swift type specification.</param>
    /// <returns>True if the type has been processed; otherwise, false.</returns>
    public static bool IsTypeProcessed(this ITypeDatabase typeDatabase, TypeSpec typeSpec)
    {
        switch (typeSpec)
        {
            case NamedTypeSpec namedTypeSpec:
                string typeIdentifier = namedTypeSpec.NameWithoutModuleWithGenericParameters;
                return typeDatabase.IsTypeProcessed(namedTypeSpec.Module, typeIdentifier);
            case TupleTypeSpec tupleTypeSpec:
                return typeDatabase.IsTypeProcessed(string.Empty, tupleTypeSpec.ToString(true));
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets the type record for the specified Swift type or throws an exception if the type is not found.
    /// </summary>
    /// <param name="typeDatabase">The type database.</param>
    /// <param name="typeSpec">The Swift type specification.</param>
    /// <returns>The type record.</returns>
    public static TypeRecord GetTypeRecordOrAnyType(this ITypeDatabase typeDatabase, TypeSpec typeSpec)
    {
        switch (typeSpec)
        {
            case NamedTypeSpec namedTypeSpec:
                string typeIdentifier = namedTypeSpec.NameWithoutModuleWithGenericParameters;
                return typeDatabase.GetTypeRecordOrAnyType(namedTypeSpec.Module, typeIdentifier);
            case TupleTypeSpec tupleTypeSpec:
                return typeDatabase.GetTypeRecordOrAnyType(string.Empty, tupleTypeSpec.ToString(true));
            default:
                return GetAnyType();
        }
    }

    /// <summary>
    /// Gets the type record for the specified Swift type or throws an exception if the type is not found.
    /// </summary>
    /// <param name="typeDatabase">The type database.</param>
    /// <param name="typeSpec">The Swift type specification.</param>
    /// <returns>The type record.</returns>
    public static TypeRecord GetTypeRecordOrThrow(this ITypeDatabase typeDatabase, TypeSpec typeSpec)
    {
        switch (typeSpec)
        {
            case NamedTypeSpec namedTypeSpec:
                string typeIdentifier = namedTypeSpec.NameWithoutModuleWithGenericParameters;
                return typeDatabase.GetTypeRecordOrThrow(namedTypeSpec.Module, typeIdentifier);
            case TupleTypeSpec tupleTypeSpec:
                return typeDatabase.GetTypeRecordOrThrow(string.Empty, tupleTypeSpec.ToString(true));
            default:
                throw new InvalidOperationException("Cannot get type record for non-named type.");
        }
    }

    /// <summary>
    /// Gets the type record for the specified Swift type or throws an exception if the type is not found.
    /// </summary>
    /// <param name="typeDatabase">The type database.</param>
    /// <param name="moduleName">The Swift module name.</param>
    /// <param name="typeIdentifier">The Swift type identifier.</param>
    /// <returns>The type record.</returns>
    public static TypeRecord GetTypeRecordOrThrow(this ITypeDatabase typeDatabase, string moduleName, string typeIdentifier)
    {
        if (typeDatabase.TryGetTypeRecord(moduleName, typeIdentifier, out var record))
            return record;

        throw new Exception($"Type {moduleName}.{typeIdentifier} not found in database.");
    }

    /// <summary>
    /// Gets the type record for the specified Swift type or the Any type if the type is not found.
    /// </summary>
    /// <param name="typeDatabase">The type database.</param>
    /// <param name="moduleName">The Swift module name.</param>
    /// <param name="typeIdentifier">The Swift type identifier.</param>
    /// <returns>The type record.</returns>
    public static TypeRecord GetTypeRecordOrAnyType(this ITypeDatabase typeDatabase, string moduleName, string typeIdentifier)
    {
        if (typeDatabase.TryGetTypeRecord(moduleName, typeIdentifier, out var record))
            return record;

        return GetAnyType();
    }

    /// <summary>
    /// Gets the type record for the Any type.
    /// </summary>
    /// <returns>The type record for the Any type.</returns>
    public static TypeRecord GetAnyType()
    {
        return new TypeRecord
        {
            Namespace = "Swift",
            CSTypeIdentifier = "AnyType",
            ModuleName = "Swift",
            SwiftTypeIdentifier = "AnyType",
            MetadataAccessor = string.Empty,
            IsBlittable = false,
            IsFrozen = false
        };
    }
}
