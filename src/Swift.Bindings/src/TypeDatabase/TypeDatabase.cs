// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace BindingsGeneration
{

    /// <summary>
    /// Manages a mapping database between Swift types and C# types.
    /// </summary>
    public class TypeDatabase : ITypeDatabase
    {
        private readonly ConcurrentDictionary<string, ModuleTypeDatabase> _modules = new();

        // This store is intended for types which are encountered in one module but should belong to another.
        // This is true for closed generics, where a generic definition is in one module and instantiation is in another.
        // TODO: This is a temporary solution and should be replaced with a more robust mechanism.
        private readonly ConcurrentDictionary<SwiftTypeName, TypeRecord> _outOfModuleTypes = new();

        public TypeDatabase()
        {
        }

        /// <summary>
        /// Loads a module database from a specified file.
        /// </summary>
        /// <param name="file">The file path of the module database to load.</param>
        public async Task LoadModuleDatabaseFromFile(string file)
        {
            var fileContent = await File.ReadAllTextAsync(file);

            XmlDocument xmlDoc = new();
            // TODO: This is synchronous, consider other xml parsers, other formats
            xmlDoc.LoadXml(fileContent);
            if (!ValidateXmlSchema(xmlDoc))
                throw new Exception(string.Format($"Invalid XML schema in {0}.", file));

            var version = xmlDoc.DocumentElement?.Attributes?["version"]?.Value;
            var moduleDatabase = version switch
            {
                "1.0" => ReadVersion1_0(xmlDoc),
                _ => throw new Exception(string.Format($"Unsupported database version {0} in {1}.", version, file))
            };

            AddModuleDatabase(moduleDatabase);
        }


        /// <summary>
        /// Adds a module database to the type database.
        /// </summary>
        /// <param name="moduleDatabase">The module database to add.</param>
        /// <exception cref="Exception">Thrown if a module with the same name already exists in the database.</exception>
        public void AddModuleDatabase(ModuleTypeDatabase moduleDatabase)
        {
            if (!_modules.TryAdd(moduleDatabase.Name, moduleDatabase))
            {
                throw new Exception($"Module {moduleDatabase.Name} already exists in the database.");
            }
        }

        /// <summary>
        /// Validates the XML schema of the provided document.
        /// </summary>
        /// <param name="xmlDoc">The XML document to validate.</param>
        /// <returns>True if the XML schema is valid; otherwise, false.</returns>
        private static bool ValidateXmlSchema(XmlDocument xmlDoc)
        {
            if (xmlDoc == null)
                return false;

            if (xmlDoc?.DocumentElement?.Name != "swifttypedatabase")
                return false;

            if (xmlDoc.DocumentElement.Attributes["version"]?.Value != "1.0")
                return false;

            XmlNode? entitiesNode = xmlDoc?.SelectSingleNode("//swifttypedatabase/entities");
            if (entitiesNode == null)
                return false;

            if (entitiesNode.ChildNodes.Count == 0)
                return false;

            foreach (XmlNode entityNode in entitiesNode.ChildNodes)
            {
                if (entityNode.Name != "entity")
                    return false;

                XmlNode? typeDeclarationNode = entityNode?.SelectSingleNode("typedeclaration");
                if (typeDeclarationNode == null)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Reads and parses the XML document containing type mappings based on the version 1.0.
        /// </summary>
        /// <param name="xmlDoc">The XML document to read.</param>
        /// <returns>The module database.</returns>
        private static ModuleTypeDatabase ReadVersion1_0(XmlDocument xmlDoc)
        {
            XmlNode? rootNode = xmlDoc.SelectSingleNode("//swifttypedatabase");
            if (rootNode == null)
                throw new Exception("Invalid XML structure: 'swifttypedatabase' node not found.");

            var databaseModuleName = rootNode.Attributes?["moduleName"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'moduleName' attribute.");
            var databaseModulePath = rootNode.Attributes?["modulePath"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'modulePath' attribute.");

            var moduleDatabase = new ModuleTypeDatabase(databaseModuleName, databaseModulePath);

            XmlNode? entitiesNode = xmlDoc.SelectSingleNode("//swifttypedatabase/entities");

            if (entitiesNode == null)
                throw new Exception("Invalid XML structure: 'entities' node not found.");

            foreach (XmlNode? entityNode in entitiesNode.ChildNodes)
            {
                XmlNode? typeDeclarationNode = entityNode?.SelectSingleNode("typedeclaration");
                if (typeDeclarationNode == null)
                    throw new Exception("Invalid XML structure: 'typedeclaration' node not found.");

                string moduleName = typeDeclarationNode?.Attributes?["module"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'module' attribute."); // TODO: Closed generics
                string swiftTypeIdentifier = typeDeclarationNode?.Attributes?["name"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'name' attribute.");
                string swiftMangledName = typeDeclarationNode?.Attributes?["mangledName"]?.Value ?? string.Empty;
                string csharpTypeIdentifier = entityNode?.Attributes?["managedTypeName"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'managedTypeName' attribute.");
                string @namespace = entityNode?.Attributes?["managedNameSpace"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'managedNameSpace' attribute.");
                string frozen = typeDeclarationNode?.Attributes?["frozen"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'frozen' attribute.");
                string blittable = typeDeclarationNode?.Attributes?["blittable"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'blittable' attribute.");
                if (swiftTypeIdentifier == null || csharpTypeIdentifier == null)
                    throw new Exception("Invalid XML structure: Missing attributes.");


                var swiftTypeName = SwiftTypeName.FromModuleQualifiedName($"{moduleName}.{swiftTypeIdentifier}");
                var csharpTypeName = CSharpTypeName.FromNamespaceAndName(@namespace, csharpTypeIdentifier);
                var typeRecord = new TypeRecord()
                {
                    CSharpTypeName = csharpTypeName,
                    SwiftTypeName = swiftTypeName,
                    MetadataAccessor = swiftMangledName,
                    IsBlittable = blittable.ToLower() == "true",
                    IsFrozen = frozen.ToLower() == "true",
                };

                moduleDatabase.RegisterType(swiftTypeName, typeRecord);
            }

            return moduleDatabase;
        }

        /// <inheritdoc/>
        public bool TryGetTypeRecord(SwiftTypeName swiftTypeName, [NotNullWhen(returnValue: true)] out TypeRecord? record)
        {
            if (_modules.TryGetValue(swiftTypeName.Module, out var moduleDatabase))
            {
                if (moduleDatabase.TryGetTypeRecord(swiftTypeName, out record))
                    return true;
            }

            // Try looking in the out-of-module types
            if (_outOfModuleTypes.TryGetValue(swiftTypeName, out record))
                return true;

            return false;
        }

        /// <summary>
        /// Determines whether the specified module has been processed.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns><c>true</c> if the module has been processed; otherwise, <c>false</c>.</returns>
        public bool IsModuleProcessed(string moduleName)
        {
            return _modules.ContainsKey(moduleName);
        }

        /// <inheritdoc/>
        public bool IsTypeProcessed(SwiftTypeName swiftTypeName)
        {
            if (_modules.TryGetValue(swiftTypeName.Module, out var moduleDatabase))
                return moduleDatabase.IsTypeProcessed(swiftTypeName);

            return false;
        }

        /// <summary>
        /// Retrieves the library path for the specified module.
        /// </summary>
        /// <param name="moduleName">The name of the module.</param>
        /// <returns>The file path of the library associated with the module.</returns>
        /// <exception cref="Exception">Thrown if the library path does not exist for the specified module.</exception>
        public string GetLibraryPath(string moduleName)
        {
            if (!_modules.TryGetValue(moduleName, out var moduleDatabase))
            {
                throw new Exception($"Module {moduleName} does not exist in the database.");
            }

            return moduleDatabase.Path;
        }

        /// <summary>
        /// Populates the out-of-module types store with the specified types.
        /// </summary>
        /// <param name="types">The types to add.</param>
        public void AddOutOfModuleTypes(IEnumerable<(SwiftTypeName identifier, TypeRecord record)> types)
        {
            foreach (var (identifier, record) in types)
            {
                _outOfModuleTypes.TryAdd(identifier, record);
            }
        }
    }
}
