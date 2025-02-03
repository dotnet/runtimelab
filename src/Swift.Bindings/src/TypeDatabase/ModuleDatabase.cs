// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a Swift module in C#, managing type records and module metadata.
    /// </summary>
    public class ModuleTypeDatabase
    {
        /// <summary>
        /// The type records associated with the module, where the key is the Swift type identifier.
        /// </summary>
        private readonly ConcurrentDictionary<SwiftTypeName, TypeRecord> _typeRecords;

        public ModuleTypeDatabase(string name, string path)
        {
            Name = name;
            Path = path;

            _typeRecords = new ConcurrentDictionary<SwiftTypeName, TypeRecord>();
        }

        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the file path to the module.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Checks whether a type has already been processed in the module.
        /// </summary>
        /// <param name="typeIdentifier">The identifier for the Swift type.</param>
        /// <returns><c>true</c> if the type has been processed; otherwise, <c>false</c>.</returns>
        public bool IsTypeProcessed(SwiftTypeName swiftTypeName)
        {
            return _typeRecords.ContainsKey(swiftTypeName);
        }

        /// <summary>
        /// Registers a type record with the specified type identifier in the module.
        /// </summary>
        /// <param name="typeIdentifier">The identifier for the Swift type.</param>
        /// <param name="record">The type record to register.</param>
        public void RegisterType(SwiftTypeName swiftTypeName, TypeRecord record)
        {
            _typeRecords.AddOrUpdate(swiftTypeName, record, (_, _) => record);
        }

        /// <summary>
        /// Attempts to retrieve the type record for the specified type identifier.
        /// </summary>
        /// <param name="typeIdentifier">The identifier for the Swift type.</param>
        /// <param name="record">
        /// When this method returns, contains the type record if found; otherwise, <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if the type record is found; otherwise, <c>false</c>.</returns>
        public bool TryGetTypeRecord(SwiftTypeName swiftTypeName, [NotNullWhen(returnValue: true)] out TypeRecord? record)
        {
            if (_typeRecords.TryGetValue(swiftTypeName, out record))
                return true;

            return false;
        }
    }
}
