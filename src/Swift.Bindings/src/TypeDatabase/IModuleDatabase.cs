// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace BindingsGeneration;

/// <summary>
/// Module database. Holds type records for a Swift module.
/// </summary>
public interface IModuleDatabase
{
    /// <summary>
    /// Registers a type record for the specified type identifier in the module.
    /// </summary>
    /// <param name="typeIdentifier">The identifier for the Swift type.</param>
    /// <param name="record">The type record to register.</param>
    public void RegisterType(string typeIdentifier, TypeRecord record);

    /// <summary>
    /// Checks whether the specified type has been processed in the module.
    /// </summary>
    /// <param name="typeIdentifier">The identifier for the Swift type.</param>
    /// <returns><c>true</c> if the type has been processed; otherwise, <c>false</c>.</returns>
    public bool IsTypeProcessed(string typeIdentifier);

    /// <summary>
    /// Attempts to retrieve the type record for the specified type identifier.
    /// </summary>
    /// <param name="typeIdentifier">The identifier for the Swift type.</param>
    /// <param name="record">
    /// When this method returns, contains the type record if found; otherwise, <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the type record was found; otherwise, <c>false</c>.</returns>
    public bool TryGetTypeRecord(string typeIdentifier, [NotNullWhen(returnValue: true)] out TypeRecord? record);

    /// <summary>
    /// Retrieves all type records registered in the module.
    /// </summary>
    /// <returns>A list of all type records in the module.</returns>
    public List<TypeRecord> GetTypes();

    /// <summary>
    /// Gets the name of the module.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the file path of the module.
    /// </summary>
    public string Path { get; }

}
