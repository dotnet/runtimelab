// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace BindingsGeneration;

/// <summary>
/// Type database. Holds module databases and type records for Swift types.
/// </summary>
public interface ITypeDatabase
{
    /// <summary>
    /// Loads a module database from a specified file.
    /// </summary>
    /// <param name="file">The file path of the module database to load.</param>
    public void LoadModuleDatabaseFromFile(string file);

    /// <summary>
    /// Adds a module database to the type database.
    /// </summary>
    /// <param name="moduleDatabase">The module database to add.</param>
    /// <exception cref="Exception">Thrown if a module with the same name already exists in the database.</exception>
    public void AddModuleDatabase(IModuleDatabase moduleDatabase);

    /// <summary>
    /// Checks whether a specific type in a specified module has been processed.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="typeIdentifier">The identifier for the Swift type.</param>
    /// <returns><c>true</c> if the type has been processed; otherwise, <c>false</c>.</returns>
    public bool IsTypeProcessed(string moduleName, string typeIdentifier);

    /// <summary>
    /// Attempts to retrieve the type record for a specified type identifier within a module.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="typeIdentifier">The identifier for the Swift type.</param>
    /// <param name="record">
    /// When this method returns, contains the type record if found; otherwise, <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the type record was found; otherwise, <c>false</c>.</returns>
    public bool TryGetTypeRecord(string moduleName, string typeIdentifier, [NotNullWhen(returnValue: true)] out TypeRecord? record);

    /// <summary>
    /// Retrieves the library path for the specified module.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <returns>The file path of the library associated with the module.</returns>
    /// <exception cref="Exception">Thrown if the library path does not exist for the specified module.</exception>
    public string GetLibraryPath(string moduleName);
}
