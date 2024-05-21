// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace Swift.Runtime
{
    /// <summary>
    /// Represents a Swift module in C#.
    /// </summary>
    public record ModuleRecord
    {
        /// <summary>
        /// The name of the module.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The file path to the module.
        /// </summary>
        public required string Path { get; set; }

        /// <summary>
        /// The type records associated with the module. The key is the Swift type identifier.
        /// </summary>
        public required ConcurrentDictionary<string, TypeRecord> TypeRecords { get; set; }

        /// <summary>
        /// The list of module dependencies.
        /// </summary>
        public required List<string> Dependencies { get; set; }

        /// <summary>
        /// The value indicating whether the module has been processed.
        /// </summary>
        public required bool IsProcessed { get; set; }
    }

    /// <summary>
    /// Represents a type within a module, including metadata for interfacing with Swift.
    /// </summary>
    public record TypeRecord
    {
        /// <summary>
        /// The C# namespace of the module.
        /// </summary>
        public required string Namespace { get; set; }

        /// <summary>
        /// The C# type identifier.
        /// </summary>
        public required string TypeIdentifier { get; set; }
    
        /// <summary>
        /// The Swift metadata accessor.
        /// </summary>
        public required string MetadataAccessor { get; set; }

        /// <summary>
        /// The Swift runtime type information.
        /// </summary>
        public SwiftTypeInfo? SwiftTypeInfo { get; set; }

        /// <summary>
        /// The value indicating whether the type has been processed.
        /// </summary>
        public required bool IsProcessed { get; set; }
    }

    public class TypeRegistrar
    {
        /// <summary>
        /// The mapping between Swift and C# types. The key is the Swift module name.
        /// </summary>
        private readonly ConcurrentDictionary<string, ModuleRecord> _moduleRecords = new();

        /// <summary>
        /// Registers a module with the specified path.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns>The module record.</returns>
        public ModuleRecord RegisterModule(string moduleName)
        {
            return _moduleRecords.GetOrAdd(moduleName, _ => new ModuleRecord
            {
                Name = moduleName,
                Path = string.Empty,
                TypeRecords = new ConcurrentDictionary<string, TypeRecord>(),
                Dependencies = new List<string>(),
                IsProcessed = false
            });
        }

        /// <summary>
        /// Registers a type with the specified module and type name.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <param name="typeIdentifier">The Swift type name.</param>
        /// <returns>The type record.</returns>
        public TypeRecord RegisterType(string moduleName, string typeIdentifier)
        {
            var moduleRecord = RegisterModule(moduleName); 
            return moduleRecord.TypeRecords.GetOrAdd(typeIdentifier, _ => new TypeRecord
            {
                Namespace = moduleName,
                TypeIdentifier = typeIdentifier,
                SwiftTypeInfo = null,
                IsProcessed = false,
                MetadataAccessor = string.Empty
            });
        }

        /// <summary>
        /// Gets the module record for the specified module name.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns>The type record.</returns>
        public ModuleRecord? GetModule(string moduleName)
        {
            if (_moduleRecords.TryGetValue(moduleName, out ModuleRecord? moduleRecord))
                return moduleRecord;

            return null;
        }

        /// <summary>
        /// Gets all module records.
        /// </summary>
        /// <returns>The list of module records.</returns>
        public List<ModuleRecord> GetModules()
        {
            return new List<ModuleRecord>(_moduleRecords.Values);
        }

        /// <summary>
        /// Gets the type record for the specified module and type name.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <param name="typeIdentifier">The Swift type identifier.</param>
        /// <returns>The type record.</returns>
        public TypeRecord? GetType(string moduleName, string typeIdentifier)
        {
            if (_moduleRecords.TryGetValue(moduleName, out ModuleRecord? moduleRecord))
            {
                if (moduleRecord.TypeRecords.TryGetValue(typeIdentifier, out TypeRecord? typeRecord))
                    return typeRecord;
            }

            return null;
        }

        /// <summary>
        /// Gets all type records for the specified module.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns>The list of type records.</returns>
        public List<TypeRecord> GetTypes(string moduleName)
        {
            if (_moduleRecords.TryGetValue(moduleName, out ModuleRecord? moduleRecord))
            {
                return new List<TypeRecord>(moduleRecord.TypeRecords.Values);
            }

            return new List<TypeRecord>();
        }

        /// <summary>
        /// Updates module dependencies as needed based on the current and target modules.
        /// </summary>
        /// <param name="currentModuleName">The current module name.</param>
        /// <param name="targetModuleName">The target module name.</param>
        public void UpdateDependencies(string currentModuleName, string targetModuleName)
        {
            // Ignore built-in modules and self-references
            if (string.IsNullOrEmpty(targetModuleName) || targetModuleName == "Swift" || targetModuleName == currentModuleName)
                return;

            if (!_moduleRecords[currentModuleName].Dependencies.Contains(targetModuleName))
                _moduleRecords[currentModuleName].Dependencies.Add(targetModuleName);
        }

        /// <summary>
        /// Gets dependencies for the specified module.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns>The list of dependencies.</returns>
        public List<string> GetDependencies(string moduleName)
        {
            return _moduleRecords[moduleName].Dependencies;
        }
    }
}
