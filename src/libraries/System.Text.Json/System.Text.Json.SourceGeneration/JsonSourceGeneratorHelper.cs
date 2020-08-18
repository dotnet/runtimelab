// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace System.Text.Json.SourceGeneration
{
    internal sealed partial class JsonSourceGeneratorHelper
    {
        // Simple handled types with typeinfo.
        private static readonly HashSet<Type> s_simpleTypes = new HashSet<Type>
        {
            typeof(bool),
            typeof(int),
            typeof(double),
            typeof(long),
            typeof(string),
            typeof(char),
            typeof(DateTime),
            typeof(DateTimeOffset),
        };

        // Generation namespace for source generation code.
        const string GenerationNamespace = "JsonCodeGeneration";

        // Type for key and a generated-source for value.
        public Dictionary<Type, string> Types { get; }

        // Contains types that failed to be generated.
        private HashSet<Type> _failedTypes = new HashSet<Type>();

        // Contains list of diagnostics for the code generator.
        public List<Diagnostic> Diagnostics { get; }

        public JsonSourceGeneratorHelper()
        {
            // Initialize auto properties.
            Types = new Dictionary<Type, string>();
            Diagnostics = new List<Diagnostic>();

            // Initiate diagnostic descriptors.
            InitializeDiagnosticDescriptors();
        }

        // Base source generation context partial class.
        public string GenerateHelperContextInfo()
        {
            return @$"
using System.Text.Json.Serialization;

namespace {GenerationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        private static JsonContext s_instance;
        public static JsonContext Instance
        {{
            get
            {{
                if (s_instance == null)
                {{
                    s_instance = new JsonContext();
                }}

                return s_instance;
            }}
        }}
    }}
}}
            ";
        }

        // Generates metadata for type and returns if it was successful.
        private bool GenerateClassInfo(Type rootType, Type currentType, HashSet<Type> seenTypes, string className)
        {
            // Add current type to generated types.
            seenTypes.Add(currentType);

            StringBuilder source = new StringBuilder();
            bool isSuccessful = true;

            // Try to recursively generate necessary field and property types.
            FieldInfo[] fields = currentType.GetFields();
            PropertyInfo[] properties = currentType.GetProperties();

            foreach (FieldInfo field in fields)
            {
                if (!IsSupportedType(field.FieldType))
                {
                    Diagnostics.Add(Diagnostic.Create(_notSupported, Location.None, new string[] { rootType.Name, field.FieldType.Name }));
                    return false;
                }
                foreach (Type handlingType in GetTypesToGenerate(field.FieldType))
                {
                    GenerateForMembers(rootType, handlingType, seenTypes, ref isSuccessful);
                }
            }

            foreach (PropertyInfo property in properties)
            {
                if (!IsSupportedType(property.PropertyType))
                {
                    Diagnostics.Add(Diagnostic.Create(_notSupported, Location.None, new string[] { rootType.Name, property.PropertyType.Name }));
                    return false;
                }
                foreach (Type handlingType in GetTypesToGenerate(property.PropertyType))
                {
                    GenerateForMembers(rootType, handlingType, seenTypes, ref isSuccessful);
                }
            }

            // Try to generate current type info now that fields and property types have been resolved.
            isSuccessful &= AddImportsToTypeClass(currentType, properties, fields, source);
            isSuccessful &= InitializeContextClass(currentType, className, source);
            isSuccessful &= InitializeTypeClass(className, source);
            isSuccessful &= TypeInfoGetterSetter(currentType, source);
            isSuccessful &= InitializeTypeInfoProperties(properties, source);
            isSuccessful &= GenerateTypeInfoConstructor(currentType, className, properties, fields, source);
            isSuccessful &= GenerateCreateObject(currentType, source);
            isSuccessful &= GenerateSerialize(currentType, properties, source);
            isSuccessful &= GenerateDeserialize(currentType, properties, source);
            isSuccessful &= FinalizeTypeAndContextClasses(source);

            if (isSuccessful)
            {
                Diagnostics.Add(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { rootType.Name, className }));

                // Add generated typeinfo for current traversal.
                Types.Add(currentType, source.ToString());
            }
            else
            {
                Diagnostics.Add(Diagnostic.Create(_failedToGenerateTypeClass, Location.None, new string[] { rootType.Name, className }));

                // If not successful remove it from found types hashset and add to failed types list.
                seenTypes.Remove(currentType);
                _failedTypes.Add(currentType);
            }

            return isSuccessful;
        }

        // Call recursive type generation if unseen type and check for success and cycles.
        void GenerateForMembers(Type rootType, Type currentType, HashSet<Type> seenTypes, ref bool isSuccessful)
        {
            // If new type, recurse.
            if (IsNewType(currentType, seenTypes))
            {
                bool wasSuccessful = GenerateClassInfo(rootType, currentType, seenTypes, currentType.Name);
                isSuccessful &= wasSuccessful;

                if (!wasSuccessful)
                {
                    Diagnostics.Add(Diagnostic.Create(_failedToAddNewTypesFromMembers, Location.None, new string[] { rootType.Name, currentType.Name }));
                }
            }
        }

        // Check if current type is supported to be iterated over.
        private bool IsSupportedType(Type type)
        {
            if (type is TypeWrapper typeWrapper)
            {
                if (typeWrapper.IsIEnumerable)
                {
                    // todo: Add more support to collections.
                    if (!typeWrapper.IsIList)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Returns name of types traversed that can be looked up in the dictionary.
        public void GenerateClassInfo(string className, Type type)
        {
            HashSet<Type> foundTypes = new HashSet<Type>();
            GenerateClassInfo(rootType: type, currentType: type, foundTypes, className);
        }

        private Type[] GetTypesToGenerate(Type type)
        {
            if (type.IsArray)
            {
                return new Type[] { type.GetElementType() };
            }
            if (type.IsGenericType)
            {
                return type.GetGenericArguments();
            }

            return new Type[] { type };
        }

        private bool IsNewType(Type type, HashSet<Type> foundTypes) => (
            !Types.ContainsKey(type) &&
            !foundTypes.Contains(type) &&
            !s_simpleTypes.Contains(type));

        private bool AddImportsToTypeClass(Type type, PropertyInfo[] properties, FieldInfo[] fields, StringBuilder source)
        {
            HashSet<string> imports = new HashSet<string>();

            // Add base imports.
            imports.Add("System");
            imports.Add("System.Text.Json");
            imports.Add("System.Text.Json.Serialization");
            imports.Add("System.Text.Json.Serialization.Metadata");

            // Add imports to root type.
            if (type is TypeWrapper typeWrapper)
            {
                imports.Add(typeWrapper.FullNamespace);
            }

            foreach (PropertyInfo property in properties)
            {
                foreach (Type handlingType in GetTypesToGenerate(property.PropertyType))
                {
                    if (property.PropertyType is TypeWrapper baseType)
                    {
                        imports.Add(baseType.FullNamespace);
                    }
                    if (!handlingType.Equals(property.PropertyType) && handlingType is TypeWrapper genericType)
                    {
                        imports.Add(genericType.FullNamespace);
                    }
                }
            }
            foreach (FieldInfo field in fields)
            {
                foreach (Type handlingType in GetTypesToGenerate(field.FieldType))
                {
                    if (field.FieldType is TypeWrapper baseType)
                    {
                        imports.Add(baseType.FullNamespace);
                    }
                    if (!handlingType.Equals(field.FieldType) && handlingType is TypeWrapper genericType)
                    {
                        imports.Add(genericType.FullNamespace);
                    }
                }
            }

            foreach (string import in imports)
            {
                source.Append($@"
using {import};");
            }

            return true;
        }

        // Includes necessary imports, namespace decl and initializes class.
        private bool InitializeContextClass(Type type, string className, StringBuilder source)
        {
            source.Append($@"

namespace {GenerationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        private {className}TypeInfo _{className};
        public JsonTypeInfo<{type.Name}> {className} 
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

        private bool TypeInfoGetterSetter(Type type, StringBuilder source)
        {
            source.Append($@"
            public JsonTypeInfo<{type.Name}> TypeInfo {{ get; private set; }}
            ");

            return true;
        }

        private bool InitializeTypeInfoProperties(PropertyInfo[] properties, StringBuilder source)
        {
            Type genericType;
            string typeName;
            string propertyName;

            foreach (PropertyInfo property in properties)
            {
                // todo: Verify if the type has already failed in sourcegeneration.

                // Find type and property name to use for property definition.
                typeName = property.PropertyType.Name;
                propertyName = property.Name;
                if (property.PropertyType is TypeWrapper type)
                {
                    // Check if IEnumerable.
                    if (type.IsIEnumerable)
                    {
                        genericType = GetTypesToGenerate(type).First();
                        if (type.IsIList)
                        {
                            typeName = $"List<{genericType.Name}>";
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

        private bool GenerateTypeInfoConstructor(Type root, string className, PropertyInfo[] properties, FieldInfo[] fields, StringBuilder source)
        {
            source.Append($@"
            public {className}TypeInfo(JsonContext context)
            {{
                var typeInfo = new JsonObjectInfo<{root.Name}>(CreateObjectFunc, SerializeFunc, DeserializeFunc, context.GetOptions());
            ");

            Type genericType;
            string typeClassInfoCall;
            foreach (PropertyInfo property in properties)
            {
                // Default classtype for values.
                typeClassInfoCall = $"context.{property.PropertyType.Name}";

                if (property.PropertyType is TypeWrapper typeWrapper)
                {
                    // Check if IEnumerable.
                    if (typeWrapper.IsIEnumerable)
                    {
                        genericType = GetTypesToGenerate(typeWrapper).First();

                        if (typeWrapper.IsIList)
                        {
                            typeClassInfoCall = $"KnownCollectionTypeInfos<{genericType.Name}>.GetList(context.{genericType.Name}, context)";
                        }
                        else
                        {
                            // todo: Add support for rest of the IEnumerables.
                            return false;
                        }
                    }
                }

                source.Append($@"
                _property_{property.Name} = typeInfo.AddProperty(nameof({((TypeWrapper)root).FullNamespace}.{root.Name}.{property.Name}),
                    (obj) => {{ return (({root.Name})obj).{property.Name}; }},
                    (obj, value) => {{ (({root.Name})obj).{property.Name} = value; }},
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

        private bool GenerateCreateObject(Type type, StringBuilder source)
        {
            source.Append($@"
            private object CreateObjectFunc()
            {{
                return new {type.Name}();
            }}
            ");

            return true;
        }

        private bool GenerateSerialize(Type type, PropertyInfo[] properties, StringBuilder source)
        {
            // Start function.
            source.Append($@"
            private void SerializeFunc(Utf8JsonWriter writer, object value, ref WriteStack writeStack, JsonSerializerOptions options)
            {{");

            // Create base object.
            source.Append($@"
                {type.Name} obj = ({type.Name})value;
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

        private bool GenerateDeserialize(Type type, PropertyInfo[] properties, StringBuilder source)
        {
            // Start deserialize function.
            source.Append($@"
            private {type.Name} DeserializeFunc(ref Utf8JsonReader reader, ref ReadStack readStack, JsonSerializerOptions options)
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
                {type.Name} obj = new {type.Name}();

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
            if (properties.Length > 0)
            {
                source.Append($@"
                    else
                    {{
                        reader.Read();
                    }}");
            }
            else
            {
                source.Append($@"
                    reader.Read();");
            }

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
