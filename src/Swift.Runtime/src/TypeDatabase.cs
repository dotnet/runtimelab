// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Swift.Runtime
{
    /// <summary>
    /// Represents a database for mapping Swift type names to C# type names.
    /// </summary>
    public unsafe class TypeDatabase
    {
        // TODO: Encapsulate _swiftToCSharpMapping and _swiftTypeInfo in a class/struct
        /// <summary>
        /// The mapping from Swift type names to C# type names.
        /// </summary>
        private readonly Dictionary<string, string> _swiftToCSharpMapping = new Dictionary<string, string>();

        /// <summary>
        /// The mapping from Swift type names to Swift type information.
        /// </summary>
        private readonly Dictionary<string, SwiftTypeInfo> _swiftTypeInfo = new Dictionary<string, SwiftTypeInfo>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeDatabase"/> class.
        /// </summary>
        /// <param name="file">The path to the XML file containing the type mappings.</param>
        public TypeDatabase(string file)
        {
            XmlDocument xmlDoc = new XmlDocument();
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
                    
                string? swiftName = typeDeclarationNode?.Attributes?["name"]?.Value;
                string? csharpName = entityNode?.Attributes?["managedTypeName"]?.Value;
                if (swiftName == null || csharpName == null)
                    throw new Exception("Invalid XML structure: Missing attributes.");

                _swiftToCSharpMapping.Add(swiftName, csharpName);
            }
        }

        /// <summary>
        /// Gets the C# type name corresponding to the specified Swift type name. 
        /// The method first tries to find a known mapping, and if that fails, it looks for a type in Swift.Runtime.
        /// </summary>
        /// <param name="swiftTypeName">The Swift type name.</param>
        /// <returns>The corresponding C# type name.</returns>
        public string[] GetCSharpTypeName(string swiftTypeName)
        {
            // Try to find a known mapping
            if (_swiftToCSharpMapping.TryGetValue(swiftTypeName, out string? csharpTypeName))
            {
                return new string [] { "System", csharpTypeName };
            }

            // Try to find a type in Swift.Runtime
            Type? swiftRuntimeType = Type.GetType($"Swift.Runtime.{swiftTypeName}");
            if (swiftRuntimeType != null)
                return new string [] { "Swift.Runtime", swiftRuntimeType.Name };

            // TODO: The ABI parser should search for the type within this and imported modules and lazy-load it
            return new string [] { "System", swiftTypeName };
        }

        /// <summary>
        /// Gets the Swift type information from the specified library.
        /// </summary>
        /// <param name="swiftTypeName">The Swift type name.</param>
        /// <param name="libraryPath">The path to the library containing the type information.</param>
        /// <param name="functionName">The name of the function to call to get the type information.</param>
        /// <returns>The Swift type information.</returns>
        public SwiftTypeInfo GetSwiftTypeInfo(string swiftTypeName, string? libraryPath = null, string? functionName = null)
        {
            if (_swiftTypeInfo.TryGetValue(swiftTypeName, out SwiftTypeInfo typeInfo))
                return typeInfo;

            if (libraryPath != null && functionName != null)
            {
                IntPtr metadataPtr = DynamicLibraryLoader.execute(libraryPath, functionName);
                typeInfo = new SwiftTypeInfo { MetadataPtr = metadataPtr };

                _swiftTypeInfo.Add(swiftTypeName, typeInfo);
                return typeInfo;
            }

            throw new Exception($"No metadata found for type '{swiftTypeName}'.");
        }
    }
}
