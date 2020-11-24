// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace System.Text.Json.SourceGeneration
{
    internal sealed partial class JsonSourceGeneratorHelper
    {
        private readonly Type _ienumerableType;
        private readonly Type _listOfTType;
        private readonly Type _ienumerableOfTType;
        private readonly Type _dictionaryType;
        private readonly Type _nullableOfTType;

        // Generation namespace for source generation code.
        const string GenerationNamespace = "JsonCodeGeneration";

        private readonly HashSet<Type> _knownTypes = new();

        private Dictionary<Type, TypeMetadata> _handledTypes = new();

        // Contains used JsonTypeInfo<T> identifiers.
        private HashSet<string> _usedCompilableTypeNames = new();

        private Dictionary<Type, TypeMetadata> _typeMetadataCache = new();

        private readonly GeneratorExecutionContext _executionContext;

        public JsonSourceGeneratorHelper(GeneratorExecutionContext executionContext, MetadataLoadContext metadataLoadContext)
        {
            _executionContext = executionContext;

            _ienumerableType = metadataLoadContext.Resolve(typeof(IEnumerable));
            _listOfTType = metadataLoadContext.Resolve(typeof(List<>));
            _ienumerableOfTType = metadataLoadContext.Resolve(typeof(IEnumerable<>));
            _dictionaryType = metadataLoadContext.Resolve(typeof(Dictionary<,>));
            _nullableOfTType = metadataLoadContext.Resolve(typeof(Nullable<>));

            PopulateSimpleTypes(metadataLoadContext);

            // Initiate diagnostic descriptors.
            InitializeDiagnosticDescriptors();
        }

        public void GenerateSerializationMetadata(Dictionary<string, Type> serializableTypes)
        {
            // Add base default instance source.
            _executionContext.AddSource("JsonContext.g.cs", SourceText.From(BaseJsonContextImplementation, Encoding.UTF8));

#if LAUNCH_DEBUGGER_ON_EXECUTE
            try
            {
#endif
                foreach (KeyValuePair<string, Type> pair in serializableTypes)
                {
                    TypeMetadata typeMetadata = GetOrAddTypeMetadata(pair.Value);
                    GenerateMetadataForType(typeMetadata);
                }
#if LAUNCH_DEBUGGER_ON_EXECUTE
            }
            catch (Exception e)
            {
                throw e;
            }
#endif

            // Add GetJsonClassInfo override implementation.
            _executionContext.AddSource("JsonContext.GetJsonClassInfo.cs", SourceText.From(Get_GetClassInfo_Implementation(), Encoding.UTF8));
        }

        public void GenerateMetadataForType(TypeMetadata typeMetadata)
        {
            if (_handledTypes.ContainsKey(typeMetadata.Type))
            {
                return;
            }

            _handledTypes.Add(typeMetadata.Type, typeMetadata);

            string metadataFileName = $"{typeMetadata.FriendlyName}.g.cs";

            switch (typeMetadata.ClassType)
            {
                case ClassType.KnownType:
                    {
                        // Generate nothing. Default `JsonSerializerContext.GetClassInfo()` implementation will provide the metadata.
                        return;
                    }
                case ClassType.TypeWithCustomConverter:
                    {
                        // TODO: Generate code similar to ClassInfo for known types.
                        return;
                    }
                case ClassType.Enumerable:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForEnumerable(typeMetadata, collectionType: typeMetadata.CollectionType), Encoding.UTF8));

                        GenerateMetadataForType(typeMetadata.CollectionValueTypeMetadata);
                    }
                    break;
                case ClassType.Dictionary:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForDictionary(typeMetadata, collectionType: typeMetadata.CollectionType), Encoding.UTF8));

                        GenerateMetadataForType(typeMetadata.CollectionKeyTypeMetadata);
                        GenerateMetadataForType(typeMetadata.CollectionValueTypeMetadata);
                    }
                    break;
                case ClassType.Object:
                    {
                        // TODO: this codepath assumes deserialization with a public parameterless ctor.
                        // Add mechanism to detect otherwise and opt-in for runtime metadata generation and/or serialization
                        // with JsonSerializer's dynamic code paths.

                        StringBuilder sb = new();

                        // Add using statements.
                        sb.Append(GetUsingStatementsString(typeMetadata));

                        // Add declarations for JsonContext and the JsonTypeInfo<T> property for the type.
                        sb.Append(Get_ContextClass_And_TypeInfoProperty_Declarations(typeMetadata));

                        // Add declarations for the JsonTypeInfo<T> wrapper and nested property.
                        sb.Append(Get_TypeInfoClassWrapper_And_TypeInfoProperty_Declarations(typeMetadata));

                        // Add declarations for JsonPropertyInfo<T>s for each property.
                        // TODO: Add declarations for JsonPropertyInfo<T>s for each field.
                        foreach (PropertyMetadata propertyMetadata in typeMetadata.PropertiesMetadata)
                        {
                            sb.Append($@"
            private JsonPropertyInfo<{propertyMetadata.TypeMetadata.CompilableName}> _property_{propertyMetadata.Name};
");
                        }

                        // Add constructor which initializers the JsonPropertyInfo<T>s for the type.
                        sb.Append(GetTypeInfoConstructor(typeMetadata));

                        // Add CreateObjectFunc.
                        sb.Append($@"
            private object CreateObjectFunc()
            {{
                return new {typeMetadata.CompilableName}();
            }}
");
                        // Add Serialize func.
                        sb.Append(GenerateSerializeFunc(typeMetadata));

                        // Add DeserializeFunc.
                        sb.Append(GenerateDeserializeFunc(typeMetadata));

                        // Add end braces.
                        sb.Append($@"
        }} // End of {typeMetadata.FriendlyName}TypeInfo class.
    }} // End of JsonContext class.
}} // End of {GenerationNamespace} namespace.
");

                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(sb.ToString(), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));

                        // If type had its JsonTypeInfo name changed, report to the user.
                        if (typeMetadata.Type.Name != typeMetadata.CompilableName)
                        {
                            //"Duplicate type name detected. Setting the JsonTypeInfo<T> property for type {0} in assembly {1} to {2}. To use please call JsonContext.Instance.{2}",
                            _executionContext.ReportDiagnostic(Diagnostic.Create(
                                _typeNameClash,
                                Location.None,
                                new string[] { typeMetadata.CompilableName, typeMetadata.Type.Assembly.FullName, typeMetadata.FriendlyName }));
                        }

                        // Generate serialization metadata for each property type.
                        // TODO: Generate serialization metadata for each field type.
                        foreach (PropertyMetadata propertyMetadata in typeMetadata.PropertiesMetadata)
                        {
                            GenerateMetadataForType(propertyMetadata.TypeMetadata);
                        }
                    }
                    break;
                case ClassType.TypeUnsupportedBySourceGen:
                    {
                        _executionContext.ReportDiagnostic(
                            Diagnostic.Create(_failedToGenerateTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));
                        return;
                    }
                default:
                    {
                        throw new InvalidOperationException();
                    }
            }
        }

        private static string GenerateForEnumerable(TypeMetadata typeMetadata, CollectionType collectionType)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            TypeMetadata? collectionValueTypeMetadata = typeMetadata.CollectionValueTypeMetadata;
            Debug.Assert(collectionValueTypeMetadata != null);

            string valueTypeCompilableName = collectionValueTypeMetadata.CompilableName;
            string valueTypeReadableName = collectionValueTypeMetadata.FriendlyName;

            string elementClassInfo = collectionValueTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                ? "null"
                : $"this.{valueTypeReadableName}";

            return @$"{GetUsingStatementsString(typeMetadata)}

namespace {GenerationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        private JsonTypeInfo<{typeCompilableName}> _{typeFriendlyName};
        public JsonTypeInfo<{typeCompilableName}> {typeFriendlyName}
        {{
            get
            {{
                _{typeFriendlyName} ??= KnownCollectionTypeInfos<{valueTypeCompilableName}>.Get{collectionType}({elementClassInfo}, this);
                return _{typeFriendlyName};
            }}
        }}
    }}
}}
            ";
        }

        private static string GenerateForDictionary(TypeMetadata typeMetadata, CollectionType collectionType)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            // Key metadata
            TypeMetadata? collectionKeyTypeMetadata = typeMetadata.CollectionKeyTypeMetadata;
            Debug.Assert(collectionKeyTypeMetadata != null);

            string keyTypeCompilableName = collectionKeyTypeMetadata.CompilableName;
            string keyTypeReadableName = collectionKeyTypeMetadata.FriendlyName;

            // Value metadata
            TypeMetadata? collectionValueTypeMetadata = typeMetadata.CollectionValueTypeMetadata;
            Debug.Assert(collectionValueTypeMetadata != null);

            string valueTypeCompilableName = collectionValueTypeMetadata.CompilableName;
            string valueTypeReadableName = collectionValueTypeMetadata.FriendlyName;

            string elementClassInfo = collectionValueTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                ? "null"
                : $"this.{valueTypeReadableName}";

            return @$"{GetUsingStatementsString(typeMetadata)}

namespace {GenerationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        private JsonTypeInfo<{typeCompilableName}> _{typeFriendlyName};
        public JsonTypeInfo<{typeCompilableName}> {typeFriendlyName}
        {{
            get
            {{
                _{typeFriendlyName} ??= KnownDictionaryTypeInfos<{keyTypeCompilableName}, {valueTypeCompilableName}>.Get{collectionType}({elementClassInfo}, this);
                return _{typeFriendlyName};
            }}
        }}
    }}
}}
            ";
        }

        public TypeMetadata GetOrAddTypeMetadata(Type type)
        {
            if (_typeMetadataCache.TryGetValue(type, out TypeMetadata? typeMetadata))
            {
                return typeMetadata!;
            }

            ClassType classType;
            Type? collectionKeyType = null;
            Type? collectionValueType = null;
            List<PropertyMetadata>? propertiesMetadata = null;
            List<PropertyMetadata>? fieldsMetadata = null;
            CollectionType collectionType = CollectionType.NotApplicable;

            // TODO: first check for custom converter.
            if (_knownTypes.Contains(type))
            {
                classType = ClassType.KnownType;
            }
            else if (type.IsEnum || IsNullableValueType(type))
            {
                classType = ClassType.TypeUnsupportedBySourceGen;
            }
            else if (_ienumerableType.IsAssignableFrom(type))
            {
                // Only T[], List<T>, Dictionary<Tkey, TValue>, and IEnumerable<T> are supported.

                if (type.IsArray)
                {
                    classType = ClassType.Enumerable;
                    collectionType = CollectionType.Array;
                    collectionValueType = type.GetElementType();
                }
                else if (!type.IsGenericType)
                {
                    classType = ClassType.TypeUnsupportedBySourceGen;
                }
                else
                {
                    Type genericTypeDef = type.GetGenericTypeDefinition();
                    Type[] genericTypeArgs = type.GetGenericArguments();

                    if (genericTypeDef == _listOfTType)
                    {
                        classType = ClassType.Enumerable;
                        collectionType = CollectionType.List;
                        collectionValueType = genericTypeArgs[0];
                    }
                    else if (genericTypeDef == _ienumerableOfTType)
                    {
                        classType = ClassType.Enumerable;
                        collectionType = CollectionType.IEnumerable;
                        collectionValueType = genericTypeArgs[0];
                    }
                    else if (genericTypeDef == _dictionaryType)
                    {
                        classType = ClassType.Dictionary;
                        collectionType = CollectionType.Dictionary;
                        collectionKeyType = genericTypeArgs[0];
                        collectionValueType = genericTypeArgs[1];
                    }
                    else
                    {
                        classType = ClassType.TypeUnsupportedBySourceGen;
                    }
                }
            }
            else
            {
                classType = ClassType.Object;

                // TODO: support for non-public members.
                PropertyInfo[] properties = type.GetProperties();
                FieldInfo[] fields = type.GetFields();

                propertiesMetadata = new List<PropertyMetadata>(properties.Length);
                fieldsMetadata = new List<PropertyMetadata>(fields.Length);

                foreach (PropertyInfo property in properties)
                {
                    PropertyMetadata propMetadata = new()
                    {
                        Name = property.Name,
                        TypeMetadata = GetOrAddTypeMetadata(property.PropertyType)
                    };
                    propertiesMetadata.Add(propMetadata);
                }

                foreach (FieldInfo field in fields)
                {
                    fieldsMetadata.Add(new PropertyMetadata
                    {
                        Name = field.Name,
                        TypeMetadata = GetOrAddTypeMetadata(field.FieldType)
                    });
                }
            }

            string compilableName = type.GetCompilableTypeName();
            string friendlyName = type.GetFriendlyTypeName();

            if (_usedCompilableTypeNames.Contains(type.Name))
            {
                compilableName = type.GetUniqueCompilableTypeName();
                friendlyName = type.GetUniqueFriendlyTypeName();
            }
            else
            {
                _usedCompilableTypeNames.Add(compilableName);
            }

            typeMetadata = new TypeMetadata
            {
                CompilableName = compilableName,
                FriendlyName = friendlyName,
                Type = type,
                ClassType = classType,
                CollectionType = collectionType,
                CollectionKeyTypeMetadata = collectionKeyType != null ? GetOrAddTypeMetadata(collectionKeyType) : null,
                CollectionValueTypeMetadata = collectionValueType != null ? GetOrAddTypeMetadata(collectionValueType) : null,
                PropertiesMetadata = propertiesMetadata,
                FieldsMetadata = fieldsMetadata,
                IsValueType = type.IsValueType
            };

            _typeMetadataCache[type] = typeMetadata;
            return typeMetadata;
        }

        public bool IsNullableValueType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == _nullableOfTType;
        }

        private void PopulateSimpleTypes(MetadataLoadContext metadataLoadContext)
        {
            Debug.Assert(_knownTypes != null);

            _knownTypes.Add(metadataLoadContext.Resolve(typeof(bool)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(byte[])));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(byte)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(char)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(DateTime)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(DateTimeOffset)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(Decimal)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(double)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(Guid)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(short)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(int)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(long)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(sbyte)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(float)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(string)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(ushort)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(uint)));
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(ulong)));

            // TODO: confirm that this is true.
            // System.Private.Uri may not be loaded in input compilation.
            Type? uriType = metadataLoadContext.Resolve(typeof(Uri));
            if (uriType != null)
            {
                _knownTypes.Add(uriType);
            }

            _knownTypes.Add(metadataLoadContext.Resolve(typeof(Version)));
        }

        // Base source generation context partial class.
        private string BaseJsonContextImplementation => @$"
using System.Text.Json;
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

        private JsonContext()
        {{
        }}

        public JsonContext(JsonSerializerOptions options) : base(options)
        {{
        }}
    }}
}}
";

        private string Get_GetClassInfo_Implementation()
        {
            StringBuilder sb = new();

            HashSet<string> usingStatements = new();

            // TODO: should these already be cached somewhere?
            foreach (TypeMetadata typeMetadata in _handledTypes.Values)
            {
                usingStatements.UnionWith(GetUsingStatements(typeMetadata));
            }

            sb.Append(@$"{GetUsingStatementsString(usingStatements)}

namespace {GenerationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        public override JsonClassInfo GetJsonClassInfo(Type type)
        {{");

            // TODO: Make this Dictionary-lookup-based if _handledType.Count > 64.
            foreach (TypeMetadata typeMetadata in _handledTypes.Values)
            {
                if (typeMetadata.ClassType != ClassType.TypeUnsupportedBySourceGen)
                {
                    sb.Append($@"
            if (type == typeof({typeMetadata.Type.GetUniqueCompilableTypeName()}))
            {{
                return this.{typeMetadata.FriendlyName};
            }}
");
                }
            }

            sb.Append(@$"
            return null;
        }}
    }}
}}
");

            return sb.ToString();
        }

        private static string GetUsingStatementsString(TypeMetadata typeMetadata)
        {
            HashSet<string> usingStatements = GetUsingStatements(typeMetadata);
            return GetUsingStatementsString(usingStatements);
        }

        private static string GetUsingStatementsString(HashSet<string> usingStatements)
        {
            string[] usingsArr = usingStatements.ToArray();
            Array.Sort(usingsArr);
            return string.Join("\n", usingsArr);
        }

        private static HashSet<string> GetUsingStatements(TypeMetadata typeMetadata)
        {
            HashSet<string> usingStatements = new();

            // Add library usings.
            usingStatements.Add(FormatAsUsingStatement("System"));
            usingStatements.Add(FormatAsUsingStatement("System.Runtime.CompilerServices"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json.Serialization"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json.Serialization.Metadata"));

            // Add imports to root type.
            usingStatements.Add(FormatAsUsingStatement(typeMetadata.Type.Namespace));

            switch (typeMetadata.ClassType)
            {
                case ClassType.Enumerable:
                    {
                        AddUsingStatementsForType(typeMetadata.CollectionValueTypeMetadata);
                    }
                    break;
                case ClassType.Dictionary:
                    {
                        AddUsingStatementsForType(typeMetadata.CollectionKeyTypeMetadata);
                        AddUsingStatementsForType(typeMetadata.CollectionValueTypeMetadata);
                    }
                    break;
                case ClassType.Object:
                    {
                        foreach (PropertyMetadata property in typeMetadata.PropertiesMetadata)
                        {
                            AddUsingStatementsForType(property.TypeMetadata);
                        }

                        foreach (PropertyMetadata property in typeMetadata.FieldsMetadata)
                        {
                            AddUsingStatementsForType(property.TypeMetadata);
                        }
                    }
                    break;
                default:
                    break;
            }

            void AddUsingStatementsForType(TypeMetadata typeMetadata)
            {
                usingStatements.Add(FormatAsUsingStatement(typeMetadata.Type.Namespace));

                if (typeMetadata.CollectionKeyTypeMetadata != null)
                {
                    Debug.Assert(typeMetadata.CollectionValueTypeMetadata != null);
                    usingStatements.Add(FormatAsUsingStatement(typeMetadata.CollectionKeyTypeMetadata.Type.Namespace));
                }

                if (typeMetadata.CollectionValueTypeMetadata != null)
                {
                    usingStatements.Add(FormatAsUsingStatement(typeMetadata.CollectionValueTypeMetadata.Type.Namespace));
                }
            }

            return usingStatements;
        }

        private static string FormatAsUsingStatement(string @namespace) => $"using {@namespace};";

        // Includes necessary imports, namespace decl and initializes class.
        private static string Get_ContextClass_And_TypeInfoProperty_Declarations(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            return $@"

namespace {GenerationNamespace}
{{
    public partial class JsonContext : JsonSerializerContext
    {{
        private {typeFriendlyName}TypeInfo _{typeFriendlyName};
        public JsonTypeInfo<{typeCompilableName}> {typeFriendlyName}
        {{
            get
            {{
                if (_{typeFriendlyName} == null)
                {{
                    _{typeFriendlyName} = new {typeFriendlyName}TypeInfo(this);
                }}

                return _{typeFriendlyName}.TypeInfo;
            }}
        }}
";
        }

        private static string Get_TypeInfoClassWrapper_And_TypeInfoProperty_Declarations (TypeMetadata typeMetadata) {
            return $@"
        private class {typeMetadata.FriendlyName}TypeInfo 
        {{
            public JsonTypeInfo<{typeMetadata.CompilableName}> TypeInfo {{ get; private set; }}
        ";
        }

        private static string GetTypeInfoConstructor(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            StringBuilder sb = new();

            sb.Append($@"
            public {typeMetadata.FriendlyName}TypeInfo(JsonContext context)
            {{
                JsonObjectInfo<{typeMetadata.CompilableName}> typeInfo = new(CreateObjectFunc, SerializeFunc, DeserializeFunc, context.GetOptions());
            ");

            foreach (PropertyMetadata propertyMetadata in typeMetadata.PropertiesMetadata)
            {
                string propertyName = propertyMetadata.Name;

                TypeMetadata propertyTypeMetadata = propertyMetadata.TypeMetadata;

                string typeClassInfo = propertyTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                    ? "null"
                    : $"context.{propertyTypeMetadata.FriendlyName}";

                string propMutation = typeMetadata.IsValueType
                    ? @$"{{ Unsafe.Unbox<{typeCompilableName}>(obj).{propertyName} = value; }}"
                    : $@"{{ (({typeCompilableName})obj).{propertyName} = value; }}";

                sb.Append($@"
                _property_{propertyName} = typeInfo.AddProperty(
                    ""{propertyName}"",
                    (obj) => {{ return (({typeCompilableName})obj).{propertyName}; }},
                    (obj, value) => {propMutation},
                    {typeClassInfo});
                ");
            }

            // Finalize constructor.
            sb.Append($@"
                typeInfo.CompleteInitialization();
                TypeInfo = typeInfo;
            }}
            ");

            return sb.ToString();
        }

        private static string GenerateSerializeFunc(TypeMetadata typeMetadata)
        {
            StringBuilder sb = new();

            // Start function.
            sb.Append($@"
            private void SerializeFunc(Utf8JsonWriter writer, object value, ref WriteStack writeStack, JsonSerializerOptions options)
            {{");

            // Create base object.
            // TODO: avoid copy here?
            sb.Append($@"
                {typeMetadata.CompilableName} obj = ({typeMetadata.CompilableName})value;
            ");

            foreach (PropertyMetadata propertyMetadata in typeMetadata.PropertiesMetadata)
            {
                sb.Append($@"
                _property_{propertyMetadata.Name}.WriteValue(obj.{propertyMetadata.Name}, ref writeStack, writer);");
            }

            // End function.
            sb.Append($@"
            }}
");

            return sb.ToString();
        }

        private static string GenerateDeserializeFunc(TypeMetadata typeMetadata)
        {
            StringBuilder sb = new();

            string typeCompilableName = typeMetadata.CompilableName;

            // TODO: investigate why this is necessary. Could this simply be typeCompilableName?
            string returnType = typeMetadata.IsValueType ? "object" : typeCompilableName;

            // Start deserialize function.
            sb.Append($@"
            private {returnType} DeserializeFunc(ref Utf8JsonReader reader, ref ReadStack readStack, JsonSerializerOptions options)
            {{");

            // Create helper function to check for property name.
            sb.Append($@"
                bool ReadPropertyName(ref Utf8JsonReader reader)
                {{
                    return reader.Read() && reader.TokenType == JsonTokenType.PropertyName;
                }}
            ");

            // Start loop to read properties.
            sb.Append($@"
                ReadOnlySpan<byte> propertyName;
                {typeCompilableName} obj = new {typeCompilableName}();

                while (ReadPropertyName(ref reader))
                {{
                    propertyName = reader.ValueSpan;
            ");

            // Read and set each property.
            bool isFirstProperty = true;

            foreach (PropertyMetadata propertyMetadata in typeMetadata.PropertiesMetadata)
            {
                string ifCheckPrefix;
                if (isFirstProperty)
                {
                    ifCheckPrefix = "";
                    isFirstProperty = false;
                }
                else
                {
                    ifCheckPrefix = "else ";
                }

                sb.Append($@"
                    {ifCheckPrefix}if (propertyName.SequenceEqual(_property_{propertyMetadata.Name}.NameAsUtf8Bytes))
                    {{
                        reader.Read();
                        _property_{propertyMetadata.Name}.ReadValueAndSetMember(ref reader, ref readStack, obj);
                    }}");
            }

            // Base condition for unhandled properties.
            if (typeMetadata.PropertiesMetadata.Count > 0)
            {
                sb.Append($@"
                    else
                    {{
                        reader.Read();
                    }}");
            }
            else
            {
                sb.Append($@"
                    reader.Read();");
            }

            // Finish property reading loops.
            sb.Append($@"
                }}
            ");

            // Verify the final received token and return object.
            sb.Append($@"
                if (reader.TokenType != JsonTokenType.EndObject)
                {{
                    throw new JsonException(""todo"");
                }}

                return obj;");

            // End deserialize function.
            sb.Append($@"
            }}
            ");

            return sb.ToString();
        }
    }
}
