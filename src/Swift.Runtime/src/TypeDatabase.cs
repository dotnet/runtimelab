// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;

namespace Swift.Runtime
{
    /// <summary>
    /// Manages a mapping database between Swift types and C# types.
    /// </summary>
    public unsafe class TypeDatabase
    {
        /// <summary>
        /// The module and type records associated with the database.
        /// </summary>
        public readonly TypeRegistrar Registrar = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeDatabase"/> class.
        /// </summary>
        /// <param name="file">The path to the XML file containing the type mappings.</param>
        public TypeDatabase(string file)
        {
            XmlDocument xmlDoc = new();
            xmlDoc.Load(file);
            if (!ValidateXmlSchema(xmlDoc))
                throw new Exception(string.Format($"Invalid XML schema in {0}.", file));

            var version = xmlDoc.DocumentElement?.Attributes?["version"]?.Value;
            switch (version)
            {
                case "1.0":
                    ReadVersion1_0(xmlDoc);
                    break;
                default:
                    throw new Exception(string.Format($"Unsupported database version {0} in {1}.", version, file));
            }
        }

        /// <summary>
        /// Validates the XML schema of the provided document.
        /// </summary>
        /// <param name="xmlDoc">The XML document to validate.</param>
        /// <returns>True if the XML schema is valid; otherwise, false.</returns>
        public static bool ValidateXmlSchema(XmlDocument xmlDoc)
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
        private void ReadVersion1_0(XmlDocument xmlDoc)
        {
            XmlNode? entitiesNode = xmlDoc.SelectSingleNode("//swifttypedatabase/entities");

            if (entitiesNode == null)
            	throw new Exception("Invalid XML structure: 'entities' node not found.");

            foreach (XmlNode? entityNode in entitiesNode.ChildNodes)
            {
                XmlNode? typeDeclarationNode = entityNode?.SelectSingleNode("typedeclaration");
                if (typeDeclarationNode == null)
                    throw new Exception("Invalid XML structure: 'typedeclaration' node not found.");

                string moduleName = typeDeclarationNode?.Attributes?["module"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'module' attribute.");
                string swiftTypeIdentifier = typeDeclarationNode?.Attributes?["name"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'name' attribute.");
                string swiftMetadataAccessor = typeDeclarationNode?.Attributes?["metadataaccessor"]?.Value ?? string.Empty;
                string csharpTypeIdentifier = entityNode?.Attributes?["managedTypeName"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'managedTypeName' attribute.");
                string @namespace = entityNode?.Attributes?["managedNameSpace"]?.Value ?? throw new Exception("Invalid XML structure: Missing 'managedNameSpace' attribute.");
                if (swiftTypeIdentifier == null || csharpTypeIdentifier == null)
                    throw new Exception("Invalid XML structure: Missing attributes.");

                var moduleRecord = Registrar.RegisterModule(moduleName);
                moduleRecord.Path = "/System/Library/Frameworks/Foundation.framework/Foundation";

                var typeRecord = Registrar.RegisterType(moduleName, swiftTypeIdentifier);
                typeRecord.TypeIdentifier = csharpTypeIdentifier;
                typeRecord.MetadataAccessor = $"{swiftMetadataAccessor}Ma";
                typeRecord.IsProcessed = true;
            }
        }

        /// <summary>
        /// Gets the C# type for the specified Swift type.
        /// The method first tries to find a known mapping, and if that fails, it looks for a type in Swift.Runtime.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <param name="typeIdentifier">The Swift type identifier.</param>
        /// <returns>The corresponding C# type record.</returns>
        public TypeRecord GetTypeMapping(string moduleName, string typeIdentifier)
        {
            // Try to find a mapping in the database
            var typeRecord = Registrar.GetType(moduleName, typeIdentifier);
            if (typeRecord != null)
                return typeRecord;

            // Try to find a type within Swift.Runtime namespace
            var swiftRuntimeType = Type.GetType($"Swift.Runtime.{typeIdentifier}");
            if (swiftRuntimeType != null)
            {
                // Add the type to the database
                typeRecord = Registrar.RegisterType(moduleName, typeIdentifier);
                typeRecord.Namespace = "Swift.Runtime";
                typeRecord.TypeIdentifier = swiftRuntimeType.Name;
                typeRecord.IsProcessed = true;

                return typeRecord;
            }

            // Type not found; register it for lazy-loading
            typeRecord = Registrar.RegisterType(moduleName, typeIdentifier);
            typeRecord.IsProcessed = false;

            return typeRecord;
        }

        /// <summary>
        /// Determines whether the specified module has been processed.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns>True if the module has been processed; otherwise, false.</returns>
        public bool IsModuleProcessed(string moduleName)
        {
            var moduleRecord = Registrar.GetModule(moduleName);
            if (moduleRecord != null)
                return moduleRecord.IsProcessed;

            return false;
        }

        /// <summary>
        /// Determines whether the specified module has been processed.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <param name="typeIdentifier">The Swift type identifier.</param>
        /// <returns>True if the module has been processed; otherwise, false.</returns>
        public bool IsTypeProcessed(string moduleName, string typeIdentifier)
        {
            var typeRecord = Registrar.GetType(moduleName, typeIdentifier);
            if (typeRecord != null)
                return typeRecord.IsProcessed;
            
            return false;
        }

        /// <summary>
        /// Gets the Swift type information for the specified Swift type name.
        /// </summary>
        /// <returns>The list of unprocessed modules.</returns>
        public List<string> GetUnprocessedModules()
        {
            // Exclude built-in modules and processed modules
            var modules = Registrar.GetModules();
            return modules.Where(x => !x.IsProcessed && x.Name != "" && x.Name != "Swift").Select(x => x.Name).ToList();
        }

        /// <summary>
        /// Gets the unprocessed type records for the specified module.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns>The list of unprocessed type records.</returns>
        public List<string> GetUnprocessedTypes(string moduleName)
        {
            // Exclude processed types
            var types = Registrar.GetTypes(moduleName);
            return types.Where(x => !x.IsProcessed).Select(x => x.TypeIdentifier).ToList();
        }

        /// <summary>
        /// Gets the library name for the specified module.
        /// </summary>
        /// <param name="moduleName">The Swift module name.</param>
        /// <returns>The library name.</returns>
        public string GetLibraryName(string moduleName)
        {
            var moduleRecord = Registrar.GetModule(moduleName);
            return moduleRecord?.Path ?? "";
        }
    }
}
