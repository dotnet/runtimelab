// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace System.Text.Json.SourceGeneration
{
    internal sealed class JsonSourceGeneratorHelper
    {
        // Simple handled types with typeinfo.
        private static readonly HashSet<Type> s_simpleTypes = new HashSet<Type>
        {
            typeof(bool),
            typeof(int),
            typeof(long),
            typeof(string),
            typeof(char),
            typeof(DateTime),
            typeof(DateTimeOffset),
        };

        // Generation namespace for source generation code.
        private string _generationNamespace;

        // Full assembly type name for key and a generated-source for value.
        public Dictionary<Type, string> _types = new Dictionary<Type, string>();

        // Contains name of types that failed to be generated.
        private HashSet<Type> _failedTypes = new HashSet<Type>();
        public List<Type> FailedTypes { get { return _failedTypes.ToList(); } }

        // Contains list of diagnostics for the code generator.
        private List<Diagnostic> _diagnostics = new List<Diagnostic>();
        public List<Diagnostic> Diagnostics { get { return _diagnostics; } }

        // Diagnostic descriptors for user.
        private DiagnosticDescriptor _initiatingTypeClass;
        private DiagnosticDescriptor _failedToGenerateTypeClass;
        private DiagnosticDescriptor _failedToAddNewTypesFromMembers;

        public JsonSourceGeneratorHelper(string generationNamespace)
        {
            _generationNamespace = generationNamespace;

            // Initiate diagnostic descriptors.
            _initiatingTypeClass ??=
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "[GENERATING] Initiating type class generation",
                    "[GENERATING] Generating type class {1} for root type {0}",
                    "category",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true);
            _failedToGenerateTypeClass ??=
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "[FAILED] Failed to generate typeclass",
                    "[FAILED] {0} could not be source generated due to failure in sourcegenerating nested type {1}.",
                    "category",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true,
                    description: "Error message: {2}");
            _failedToAddNewTypesFromMembers ??=
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "[FAILED] Failed to add new types from current type",
                    "[FAILED] Failed to iterate fields and properties for current type {1} for root type {0}",
                    "category",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true,
                    description: "Error message: {2}");
        }

        // Base source generation context partial class.
        public string GenerateHelperContextInfo()
        {
            return @$"
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace {_generationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        private static JsonContext _sDefault;
        public static JsonContext Default
        {{
            get
            {{
                if (_sDefault == null)
                {{
                    _sDefault = new JsonContext();
                }}

                return _sDefault;
            }}
        }}
    }}
}}
            ";
        }

        // Returns a Tuple<isSuccessful, isCyclic>.
        private Tuple<bool, bool> DFS(HashSet<Type> seenTypes, Stack<Type> typeStack, string className, Type type)
        {
            _diagnostics.Add(Diagnostic.Create(_initiatingTypeClass, Location.None, new string[] { "idk", className }));

            // Add current type to generated types.
            seenTypes.Add(type);
            // Add current type to stack.
            typeStack.Push(type);

            StringBuilder source = new StringBuilder();
            bool isSuccessful = true;
            bool isCyclic = false;

            // Try to recursively generate necessary field and property types.
            Type handlingType;
            FieldInfo[] fields = type.GetFields();
            PropertyInfo[] properties = type.GetProperties();

            foreach (FieldInfo field in fields)
            {
                handlingType = GetTypeToGenerate(field.FieldType);
                RecursivelyGenerate(handlingType, seenTypes, typeStack, ref isSuccessful, ref isCyclic);
            }

            foreach (PropertyInfo property in properties)
            {
                handlingType = GetTypeToGenerate(property.PropertyType);
                RecursivelyGenerate(handlingType, seenTypes, typeStack, ref isSuccessful, ref isCyclic);
            }

            // Try to generate current type info now that fields and property types have been resolved.
            isSuccessful &= InitializeContextClass(className, source);
            isSuccessful &= InitializeTypeClass(className, source);
            isSuccessful &= TypeInfoGetterSetter(className, source);
            isSuccessful &= InitializeTypeInfoProperties(properties, source);
            isSuccessful &= GenerateTypeInfoConstructor(className, properties, fields, source);
            isSuccessful &= GenerateCreateObject(className, source);
            isSuccessful &= GenerateSerialize(className, properties, source);
            isSuccessful &= GenerateDeserialize(className, properties, source);
            isSuccessful &= FinalizeTypeAndContextClasses(source);

            if (isSuccessful)
            {
                // Add generated typeinfo for current traversal.
                _types.Add(type, source.ToString());
            }
            else
            {
                // Get rid of all its descendants that got generated if this type is cyclic.
                // If not cyclic, then we conserve and cache already-generated metadata.
                if (isCyclic)
                {
                    Type descendantType;
                    while(typeStack.Any() && typeStack.Peek() != type)
                    {
                        descendantType = typeStack.Pop();
                        seenTypes.Remove(descendantType);
                        _failedTypes.Add(descendantType);
                    }
                }

                _diagnostics.Add(Diagnostic.Create(_failedToGenerateTypeClass, Location.None, new string[] { "idk", className }));

                // If not successful remove it from found types hashset and add to failed types list.
                seenTypes.Remove(type);
                _failedTypes.Add(type);
            }

            return new Tuple<bool, bool>(isSuccessful, isCyclic);

            // Call recursive type generation if unseen type and check for success and cycles.
            void RecursivelyGenerate(Type type, HashSet<Type> seenTypes, Stack<Type> typeStack, ref bool isSuccessful, ref bool isCyclic)
            {
                //_diagnostics.Add(Diagnostic.Create(trying, Location.None, new string[] { "idk", type.FullName }));
                // If a field/property type has already been seen, it means that the type is cyclic.
                if (seenTypes.Contains(type))
                {
                    isCyclic = true;
                }

                // If new type, recurse.
                if (IsNewType(type, seenTypes))
                {
                    (bool wasSuccessful, bool wasCyclic) = DFS(seenTypes, typeStack, type.Name, type);
                    isSuccessful &= wasSuccessful;
                    isCyclic |= wasCyclic;
                }
            }
        }

        // Returns name of types traversed that can be looked up in the dictionary.
        public List<Type> GenerateClassInfoDFS(string className, Type rootType)
        {
            Stack<Type> typeStack = new Stack<Type>();
            HashSet<Type> foundTypes = new HashSet<Type>();
            DFS(foundTypes, typeStack, className, rootType);
            return foundTypes.ToList();
        }

        private Type GetTypeToGenerate(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }
            if (type.IsGenericType)
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        private bool IsNewType(Type type, HashSet<Type> foundTypes) => (
            !_types.ContainsKey(type) &&
            !foundTypes.Contains(type) &&
            !s_simpleTypes.Contains(type));

        // Includes necessary imports, namespace decl and initializes class.
        private bool InitializeContextClass(string className, StringBuilder source) {
            source.Append($@"
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace {_generationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        private {className}TypeInfo _{className};
        public JsonTypeInfo<{className}> {className} 
        {{
            get
            {{
                if (_{className} == null)
                {{
                    _{className} = new {className}TypeInfo(this);
                }}

                return _{className}.TypeInfo;
            }}
        }}
        ");

            return true;
        }

        private bool InitializeTypeClass(string className, StringBuilder source) {
            source.Append($@"
        private class {className}TypeInfo 
        {{
        ");

            return true;
        }

        private bool TypeInfoGetterSetter(string className, StringBuilder source) {
            source.Append($@"
            public JsonTypeInfo<{className}> TypeInfo {{ get; private set; }}
            ");

            return true;
        }

        private bool InitializeTypeInfoProperties(PropertyInfo[] properties, StringBuilder source) {
            Type genericType;
            string typeName;
            string propertyName;

            foreach (PropertyInfo property in properties)
            {
                // Verify if the type is generated.
                //if (_failedTypes.Contains(property.PropertyType) || !)

                // Find type and property name to use for property definition.
                typeName = property.PropertyType.Name;
                propertyName = property.Name;
                if (property.PropertyType is TypeWrapper type)
                {
                    // Check if IEnumerable.
                    if (type.IsIEnumerable())
                    {
                        genericType = GetTypeToGenerate(type);
                        if (type.IsList())
                        {
                            typeName = $"List<{genericType.Name}>";
                        }
                        else if (type.IsDictionary())
                        {
                            // todo: Add support and get generic arguments and add them to typeName.
                            return false;
                        }
                        else
                        {
                            // todo: Add support for rest of the IEnumerables.
                            return false;
                        }
                    }
                }

                source.Append($@"
            private JsonPropertyInfo<{typeName}> _property_{propertyName};
                ");
            }

            return true;
        }

        private bool GenerateTypeInfoConstructor(string className, PropertyInfo[] properties, FieldInfo[] fields, StringBuilder source)
        {
            source.Append($@"
            public {className}TypeInfo(JsonContext context)
            {{
                var typeInfo = new JsonObjectInfo<{className}>(CreateObjectFunc, SerializeFunc, DeserializeFunc, context.GetOptions());
            ");

            Type genericType;
            string typeClassInfoCall;
            foreach (PropertyInfo property in properties)
            {
                // Default classtype for values.
                typeClassInfoCall = $"context.{property.PropertyType.Name}";

                if (property.PropertyType is TypeWrapper type)
                {
                    // Check if IEnumerable.
                    if (type.IsIEnumerable())
                    {
                        genericType = GetTypeToGenerate(type);
                        if (type.IsList())
                        {
                            typeClassInfoCall = $"KnownCollectionTypeInfos<{genericType.Name}>.GetList(context.{genericType.Name}TypeInfo, context)";
                        }
                        else if (type.IsDictionary())
                        {
                            // todo: Add support and get generic arguments and add them to typeName.
                            return false;
                        }
                        else
                        {
                            // todo: Add support for rest of the IEnumerables.
                            return false;
                        }
                    }
                }

                source.Append($@"
                _property_{property.Name} = typeInfo.AddProperty(nameof(MyNamespace.{className}.{property.Name}),
                    (obj) => {{ return (({className})obj).{property.Name}; }},
                    (obj, value) => {{ (({className})obj).{property.Name} = value; }},
                    {typeClassInfoCall});
                ");
            }

            // Finalize constructor.
            source.Append($@"
                typeInfo.CompleteInitialization();
                TypeInfo = typeInfo;
            }}
            ");

            return true;
        }

        private bool GenerateCreateObject(string className, StringBuilder source)
        {
            source.Append($@"
            private object CreateObjectFunc()
            {{
                return new {className}();
            }}
            ");

            return true;
        }

        private bool GenerateSerialize(string className, PropertyInfo[] properties, StringBuilder source)
        {
            // Start function.
            source.Append($@"
            private void SerializeFunc(Utf8JsonWriter writer, object value, ref WriteStack writeStack, JsonSerializerOptions options)
            {{");

            // Create base object.
            source.Append($@"
                {className} obj = ({className})value;
            ");

            foreach (PropertyInfo property in properties)
            {
                source.Append($@"
                _property_{property.Name}.WriteValue(obj.{property.Name}, ref writeStack, writer);");
            }

            // End function.
            source.Append($@"
            }}
            ");

            return true;
        }

        private bool GenerateDeserialize(string className, PropertyInfo[] properties, StringBuilder source) {
            // Start deserialize function.
            source.Append($@"
            private {className} DeserializeFunc(ref Utf8JsonReader reader, ref ReadStack readStack, JsonSerializerOptions options)
            {{
            ");

            // Create helper function to check for property name.
            source.Append($@"
                bool ReadPropertyName(ref Utf8JsonReader reader)
                {{
                    return reader.Read() && reader.TokenType == JsonTokenType.PropertyName;
                }}
            ");

            // Start loop to read properties.
            source.Append($@"
                ReadOnlySpan<byte> propertyName;
                {className} obj = new {className}();

                while(ReadPropertyName(ref reader))
                {{
                    propertyName = reader.ValueSpan;
            ");

            // Read and set each property.
            foreach ((PropertyInfo property, int i) in properties.Select((p, i) => (p, i)))
            {
                source.Append($@"
                    {((i == 0) ? "" : "else ")}if (propertyName.SequenceEqual(_property_{property.Name}.NameAsUtf8Bytes))
                    {{
                        reader.Read();
                        _property_{property.Name}.ReadValueAndSetMember(ref reader, ref readStack, obj);
                    }}");
            }

            // Base condition for unhandled properties.
            source.Append($@"
                    else
                    {{
                        reader.Read();
                    }}");

            // Finish property reading loops.
            source.Append($@"
                }}
            ");

            // Verify the final received token and return object.
            source.Append($@"
                if (reader.TokenType != JsonTokenType.EndObject)
                {{
                    throw new JsonException(""todo"");
                }}
                return obj;
            ");

            // End deserialize function.
            source.Append($@"
            }}
            ");

            return true;
        }

        private bool FinalizeTypeAndContextClasses(StringBuilder source)
        {
            source.Append($@"
        }} // End of typeinfo class.
    }} // End of context class.
}} // End of namespace.
            ");

            return true;
        }
    }
}
