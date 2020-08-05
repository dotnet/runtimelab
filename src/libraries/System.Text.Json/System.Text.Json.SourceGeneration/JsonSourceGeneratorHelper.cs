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
        // Generation namespace for source generation code.
        private string _generationNamespace;

        // Context used to report diagnostics to the user.
        private SourceGeneratorContext _context;

        // Full assembly type name for key and a generated-source for value.
        public Dictionary<string, string> _types = new Dictionary<string, string>();

        // Contains source code of currently building type.
        private StringBuilder _currentSource = new StringBuilder();

        // Simple handled types with typeinfo.
        private static readonly HashSet<string> s_simpleTypes = new HashSet<string>
        {
            typeof(bool).FullName,
            typeof(int).FullName,
            typeof(string).FullName,
            typeof(char).FullName,
            typeof(DateTime).FullName,
            typeof(DateTimeOffset).FullName,
        };

        public JsonSourceGeneratorHelper(string generationNamespace, ref SourceGeneratorContext context)
        {
            _context = context;
            _generationNamespace = generationNamespace;
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

        public Dictionary<string, string> GenerateClassInfo(string className, Type rootType)
        {
            DiagnosticDescriptor failedToGenerateTypeClass =
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "Failed to generate typeclass",
                    "{0} could not be source generated due to failure in sourcegenerating nested type {1}.",
                    "category",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true,
                    description: "Error message: {2}");

            DiagnosticDescriptor failedToAddNewTypesFromCurrentType =
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "Failed to add new types from current type",
                    "Failed to iterate fields and properties for current type {1} for root type {0}",
                    "category",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true,
                    description: "Error message: {2}");


            DiagnosticDescriptor initiatingTypeClass =
                new DiagnosticDescriptor(
                    "JsonSourceGeneration",
                    "Initiating type class generation",
                    "Generating type class {1} for root type {0}",
                    "category",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true);

            // Traversal type graph from root type.
            Dictionary<string, string> foundTypes = new Dictionary<string, string>();

            // Generate stack to recursively go through type graph using BFS.
            Queue<Type> typeGraph = new Queue<Type>();
            typeGraph.Enqueue(rootType);

            bool failFast = false;
            string currentClassName = className;

            // For each type in the typegraph including root.
            int counter = 0;
            while (typeGraph.Any() && !failFast)
            {
                Type currentType = typeGraph.Dequeue();
                // Root type can have className passed in by parameter.
                if (counter != 0)
                {
                    currentClassName = currentType.Name;
                }
                counter++;

                // Info diagnostic on type creation.
                _context.ReportDiagnostic(Diagnostic.Create(initiatingTypeClass, Location.None, new string[] { rootType.Name, currentClassName }));

                // Get all constructors, fields and property to find new types.
                ConstructorInfo[] constructors = currentType.GetConstructors();
                FieldInfo[] fields = currentType.GetFields();
                PropertyInfo[] properties = currentType.GetProperties();

                // Generate the current typeinfo class.
                try
                {
                    InitializeContextClass(currentClassName);
                    InitializeTypeClass(currentClassName);
                    TypeInfoGetterSetter(currentClassName);
                    InitializeTypeInfoProperties(properties);
                    GenerateTypeInfoConstructor(currentClassName, properties, fields);
                    GenerateCreateObject(currentClassName);
                    GenerateSerialize(currentClassName, properties);
                    GenerateDeserialize(currentClassName, properties);
                    FinalizeTypeAndContextClasses();

                    // Save generated typeinfo class into traversed typegraph.
                    foundTypes[currentClassName] = _currentSource.ToString();
                }
                catch (Exception e)
                {
                    // Report warning to user for failed typeclass generation.
                    _context.ReportDiagnostic(Diagnostic.Create(failedToGenerateTypeClass, Location.None, new string[] { rootType.Name, currentClassName, e.Message }));
                    failFast = true;
                }

                // Add newly found types to typegraph.
                try
                {
                    AddTypesToTypeGraph(currentType, ref typeGraph, ref foundTypes);
                }
                catch (Exception e)
                {
                    // Report warning to user for failed adding types from currenttype.
                    _context.ReportDiagnostic(Diagnostic.Create(failedToAddNewTypesFromCurrentType, Location.None, new string[] { rootType.Name, currentClassName, e.Message }));
                    failFast = true;
                }

                // Get rid of current source generation string.
                _currentSource.Clear();
            }

            // Copy traversed typegraph to global found types only if the whole typegraph was successfully handled.
            if (!failFast)
            {
                foreach (KeyValuePair<string, string> pair in foundTypes)
                {
                    _types.Add(pair.Key, pair.Value);
                }
            }

            // Return traversed typegraph from given root type.
            return foundTypes;
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

        private void AddIfNewType(Type currentType, Type typeToAdd, ref Dictionary<string, string> foundTypes, ref Queue<Type> typeGraph)
        {
            if (!_types.ContainsKey(typeToAdd.FullName) &&
                !foundTypes.ContainsKey(typeToAdd.FullName) &&
                !s_simpleTypes.Contains(typeToAdd.FullName) &&
                currentType != typeToAdd)
            {
                _currentSource.Append($@"
                        [FIELD] ADDING TYPE {typeToAdd.FullName}
                                            {typeToAdd.Name}
                    ");
                foundTypes[typeToAdd.FullName] = "";
                typeGraph.Enqueue(typeToAdd);
            }
        }
        
        private void AddTypesToTypeGraph(Type currentType, ref Queue<Type> typeGraph, ref Dictionary<string, string> foundTypes)
        {
            Type handlingType;
            FieldInfo[] fields = currentType.GetFields();
            PropertyInfo[] properties = currentType.GetProperties();

            // If found new types, add them to the typegraph.
            foreach(FieldInfo field in fields)
            {
                handlingType = GetTypeToGenerate(field.FieldType);
                AddIfNewType(currentType, handlingType, ref foundTypes, ref typeGraph);
            }

            foreach(PropertyInfo property in properties)
            {
                handlingType = GetTypeToGenerate(property.PropertyType);
                AddIfNewType(currentType, handlingType, ref foundTypes, ref typeGraph);
            }
        }

        // Includes necessary imports, namespace decl and initializes class.
        private void InitializeContextClass(string className) {
            _currentSource.Append($@"
using System;

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
        }

        private void InitializeTypeClass(string className) {
            _currentSource.Append($@"
        private class {className}TypeInfo 
        {{
        ");
        }

        private void TypeInfoGetterSetter(string className) {
            _currentSource.Append($@"
            public JsonTypeInfo<{className}> TypeInfo {{ get; private set; }}
            ");
        }

        private void InitializeTypeInfoProperties(PropertyInfo[] properties) {
            foreach (PropertyInfo property in properties)
            {
                _currentSource.Append($@"
            private JsonPropertyInfo<{property.PropertyType.Name}> _property_{property.Name};
                ");
            }
        }

        private void GenerateTypeInfoConstructor(string className, PropertyInfo[] properties, FieldInfo[] fields)
        {
            _currentSource.Append($@"
            public {className}TypeInfo(JsonContext context)
            {{
                var typeInfo = new JsonObjectInfo<{className}>(CreateObjectFunc, SerializeFunc, DeserializeFunc, context.GetOptions());
            ");

            foreach (PropertyInfo property in properties)
            {
                _currentSource.Append($@"
                _property_{property.Name} = typeInfo.AddProperty(nameof(MyNamespace.{className}.{property.Name}),
                    (obj) => {{ return (({className})obj).{property.Name}; }},
                    (obj, value) => {{ (({className})obj).{property.Name} = value; }},
                    context.{property.PropertyType.Name});
                ");
            }

            // Finalize constructor.
            _currentSource.Append($@"
                typeInfo.CompleteInitialization();
                TypeInfo = typeInfo;
            }}
            ");
        }

        private void GenerateCreateObject(string className) {
            _currentSource.Append($@"
            private object CreateObjectFunc()
            {{
                return new {className}();
            }}
            ");
        }

        private void GenerateSerialize(string className, PropertyInfo[] properties) {
            // Start function.
            _currentSource.Append($@"
            private void SerializeFunc(Utf8JsonWriter writer, object value, ref WriteStack writeStack, JsonSerializerOptions options)
            {{
            ");

            // Create base object.
            _currentSource.Append($@"
                {className} obj = ({className})value;
            ");

            foreach (PropertyInfo property in properties)
            {
                _currentSource.Append($@"
                _property_{property.Name}.WriteValue(obj.{property.Name}, writer); 
                ");
            }

            // End function.
            _currentSource.Append($@"
            }}
            ");
        }

        private void GenerateDeserialize(string className, PropertyInfo[] properties) {
            // Start deserialize function.
            _currentSource.Append($@"
            private {className} DeserializeFunc(ref Utf8JsonReader reader, ref ReadStack readStack, JsonSerializerOptions options)
            {{
            ");

            // Create helper function to check for property name.
            _currentSource.Append($@"
                bool ReadPropertyName(ref Utf8JsonReader reader)
                {{
                    return reader.Read() && reader.TokenType == JsonTokenType.PropertyName;
                }}
            ");

            // Start loop to read properties.
            _currentSource.Append($@"
                ReadOnlySpan<byte> propertyName;
                {className} obj = new {className}();

                while(ReadPropertyName(ref reader))
                {{
                    propertyName = reader.ValueSpan;
            ");

            // Read and set each property.
            foreach ((PropertyInfo property, int i) in properties.Select((p, i) => (p, i)))
            {
                _currentSource.Append($@"
                    {((i == 0) ? "" : "else ")}if (propertyName.SequenceEqual(_property_{property.Name}.NameAsUtf8Bytes))
                    {{
                        reader.Read();
                        _property_{property.Name}.ReadValueAndSetMember(ref reader, ref readStack, obj);
                    }}
                ");
            }

            // Base condition for unhandled properties.
            _currentSource.Append($@"
                    else
                    {{
                        reader.Read();
                    }}
            ");

            // Finish property reading loops.
            _currentSource.Append($@"
                }}
            ");

            // Verify the final received token and return object.
            _currentSource.Append($@"
                if (reader.TokenType != JsonTokenType.EndObject)
                {{
                    throw new JsonException(""todo"");
                }}
                return obj;
            ");

            // End deserialize function.
            _currentSource.Append($@"
            }}
            ");
        }

        private void FinalizeTypeAndContextClasses()
        {
            _currentSource.Append($@"
        }} // End of typeinfo class.
    }} // End of context class.
}} // End of namespace.
            ");    
        }
    }
}
