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
        private readonly Type _ilistOfTType;
        private readonly Type _dictionaryType;
        private readonly Type _nullableOfTType;

        // Generation namespace for source generation code.
        private readonly string _generationNamespace;

        private const string JsonContextDeclarationSource = "internal partial class JsonContext : JsonSerializerContext";

        private readonly HashSet<Type> _knownTypes = new();

        // Contains used JsonTypeInfo<T> identifiers.
        private HashSet<string> _usedFriendlyTypeNames = new();

        /// <summary>
        /// TypeInfo for serializable types.
        /// </summary>
        private Dictionary<Type, TypeMetadata> _typeMetadataCache = new();

        /// <summary>
        /// Types that we have initiated metadata generation for.
        /// </summary>
        private HashSet<Type> _typesWithMetadataGenerated = new();

        private readonly GeneratorExecutionContext _executionContext;

        public JsonSourceGeneratorHelper(GeneratorExecutionContext executionContext, MetadataLoadContext metadataLoadContext)
        {
            _generationNamespace = $"{executionContext.Compilation.AssemblyName}.JsonSourceGeneration";
            _executionContext = executionContext;

            _ienumerableType = metadataLoadContext.Resolve(typeof(IEnumerable));
            _listOfTType = metadataLoadContext.Resolve(typeof(List<>));
            _ienumerableOfTType = metadataLoadContext.Resolve(typeof(IEnumerable<>));
            _ilistOfTType = metadataLoadContext.Resolve(typeof(IList<>));
            _dictionaryType = metadataLoadContext.Resolve(typeof(Dictionary<,>));
            _nullableOfTType = metadataLoadContext.Resolve(typeof(Nullable<>));

            PopulateSimpleTypes(metadataLoadContext);

            // Initiate diagnostic descriptors.
            InitializeDiagnosticDescriptors();
        }

        public void GenerateSerializationMetadata(Dictionary<string, (Type, bool)> serializableTypes)
        {
            // Add base default instance source.
            AddBaseJsonContextImplementation();

            foreach (KeyValuePair<string, (Type, bool)> pair in serializableTypes)
            {
                (Type type, bool canBeDynamic) = pair.Value;
                TypeMetadata typeMetadata = GetOrAddTypeMetadata(type, canBeDynamic);
                GenerateSerializationMetadataForType(typeMetadata);
            }

            // Add GetJsonClassInfo override implementation.
            _executionContext.AddSource("JsonContext.GetJsonClassInfo.cs", SourceText.From(Get_GetClassInfo_Implementation(), Encoding.UTF8));
        }

        public void AddBaseJsonContextImplementation()
        {
            _executionContext.AddSource("JsonContext.g.cs", SourceText.From(BaseJsonContextImplementation, Encoding.UTF8));
        }

        public void GenerateSerializationMetadataForType(TypeMetadata typeMetadata)
        {
            Type type = typeMetadata.Type;

            if (_typesWithMetadataGenerated.Contains(type))
            {
                return;
            }

            _typesWithMetadataGenerated.Add(type);

            string metadataFileName = $"{typeMetadata.FriendlyName}.g.cs";

            switch (typeMetadata.ClassType)
            {
                case ClassType.KnownType:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForTypeWithConverter(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));
                    }
                    break;
                case ClassType.TypeWithCustomConverter:
                    {
                        // TODO: Generate code similar to ClassInfo for known types.
                        return;
                    }
                case ClassType.Enumerable:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForCollection(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));

                        GenerateSerializationMetadataForType(typeMetadata.CollectionValueTypeMetadata);
                    }
                    break;
                case ClassType.Dictionary:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForCollection(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));

                        GenerateSerializationMetadataForType(typeMetadata.CollectionKeyTypeMetadata);
                        GenerateSerializationMetadataForType(typeMetadata.CollectionValueTypeMetadata);
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

                        // Add constructor which initializers the JsonPropertyInfo<T>s for the type.
                        sb.Append(GetTypeInfoConstructorAndInitializeMethod(typeMetadata));

                        // Add CreateObjectFunc.
                        sb.Append($@"
            private object CreateObjectFunc()
            {{
                return new {typeMetadata.CompilableName}();
            }}
");

                        // Add end braces.
                        sb.Append($@"
        }}
    }}
}}
");

                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(sb.ToString(), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));

                        // If type had its JsonTypeInfo name changed, report to the user.
                        if (type.Name != typeMetadata.FriendlyName)
                        {
                            //"Duplicate type name detected. Setting the JsonTypeInfo<T> property for type {0} in assembly {1} to {2}. To use please call JsonContext.Instance.{2}",
                            _executionContext.ReportDiagnostic(Diagnostic.Create(
                                _typeNameClash,
                                Location.None,
                                new string[] { typeMetadata.CompilableName, type.Assembly.FullName, typeMetadata.FriendlyName }));
                        }

                        // Generate serialization metadata for each property type.
                        // TODO: Generate serialization metadata for each field type.
                        foreach (PropertyMetadata propertyMetadata in typeMetadata.PropertiesMetadata)
                        {
                            GenerateSerializationMetadataForType(propertyMetadata.TypeMetadata);
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

        private string GenerateForTypeWithConverter(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            return @$"{GetUsingStatementsString(typeMetadata)}

namespace {_generationNamespace}
{{
    {JsonContextDeclarationSource}
    {{
        private JsonTypeInfo<{typeCompilableName}> _{typeFriendlyName};
        public JsonTypeInfo<{typeCompilableName}> {typeFriendlyName}
        {{
            get
            {{
                if (_{typeFriendlyName} == null)
                {{
                    var typeInfo = new JsonValueInfo<{typeCompilableName}>(new {typeFriendlyName}Converter(), GetOptions());
                    typeInfo.CompleteInitialization(canBeDynamic: {GetBoolAsStr(typeMetadata.CanBeDynamic)});
                    _{typeFriendlyName} = typeInfo;
                }}
      
                return _{typeFriendlyName};
            }}
        }}
    }}
}}
";
        }

        private static string GetBoolAsStr(bool value) => value ? "true" : "false";

        private string  GenerateForCollection(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            // Key metadata
            TypeMetadata? collectionKeyTypeMetadata = typeMetadata.CollectionKeyTypeMetadata;
            Debug.Assert(!(typeMetadata.CollectionType == CollectionType.Dictionary && collectionKeyTypeMetadata == null));
            string keyTypeCompilableName = collectionKeyTypeMetadata?.CompilableName;
            string keyTypeReadableName = collectionKeyTypeMetadata?.FriendlyName;

            // Value metadata
            TypeMetadata? collectionValueTypeMetadata = typeMetadata.CollectionValueTypeMetadata;
            Debug.Assert(collectionValueTypeMetadata != null);
            string valueTypeCompilableName = collectionValueTypeMetadata.CompilableName;
            string valueTypeReadableName = collectionValueTypeMetadata.FriendlyName;

            string valueTypeMetadataPropertyName = collectionValueTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                ? "null"
                : $"this.{valueTypeReadableName}";

            CollectionType collectionType = typeMetadata.CollectionType;

            string collectionTypeInfoValue = collectionType switch
            {
                CollectionType.Array => GetEnumerableTypeInfoAssignment(),
                CollectionType.List => GetEnumerableTypeInfoAssignment(),
                CollectionType.IEnumerable => GetEnumerableTypeInfoAssignment(),
                CollectionType.IList => GetEnumerableTypeInfoAssignment(),
                CollectionType.Dictionary => $@"(JsonCollectionTypeInfo<{typeCompilableName}>)KnownDictionaryTypeInfos<{keyTypeCompilableName!}, {valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this)",
                _ => throw new NotSupportedException()
            };

            string GetEnumerableTypeInfoAssignment() => $@"(JsonCollectionTypeInfo<{typeCompilableName}>)KnownCollectionTypeInfos<{valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this)";

            return @$"{GetUsingStatementsString(typeMetadata)}

namespace {_generationNamespace}
{{
    {JsonContextDeclarationSource}
    {{
        private JsonTypeInfo<{typeCompilableName}> _{typeFriendlyName};
        public JsonTypeInfo<{typeCompilableName}> {typeFriendlyName}
        {{
            get
            {{
                if (_{typeFriendlyName} == null)
                {{
                    var typeInfo = {collectionTypeInfoValue};
                    typeInfo.CompleteInitialization(canBeDynamic: {GetBoolAsStr(typeMetadata.CanBeDynamic)});
                    _{typeFriendlyName} = typeInfo;
                }}
      
                return _{typeFriendlyName};
            }}
        }}
    }}
}}
";
        }

        public TypeMetadata GetOrAddTypeMetadata(Type type, bool canBeDynamic = false)
        {
            if (_typeMetadataCache.TryGetValue(type, out TypeMetadata? typeMetadata))
            {
                return typeMetadata!;
            }

            // Add metadata to cache now to prevent stack overflow when the same type is found somewhere else in the object graph.
            typeMetadata = new();
            _typeMetadataCache[type] = typeMetadata;

            ClassType classType;
            Type? collectionKeyType = null;
            Type? collectionValueType = null;
            List<PropertyMetadata>? propertiesMetadata = null;
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
                    else if (genericTypeDef == _ilistOfTType)
                    {
                        classType = ClassType.Enumerable;
                        collectionType = CollectionType.IList;
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

                propertiesMetadata = new List<PropertyMetadata>(properties.Length);

                foreach (PropertyInfo property in properties)
                {
                    PropertyMetadata propMetadata = new()
                    {
                        Name = property.Name,
                        TypeMetadata = GetOrAddTypeMetadata(property.PropertyType)
                    };
                    propertiesMetadata.Add(propMetadata);
                }

                // TODO: consider fields.
            }

            string compilableName = type.GetUniqueCompilableTypeName();
            string friendlyName = type.GetFriendlyTypeName();

            if (_usedFriendlyTypeNames.Contains(friendlyName))
            {
                friendlyName = type.GetUniqueFriendlyTypeName();
            }
            else
            {
                _usedFriendlyTypeNames.Add(friendlyName);
            }

            typeMetadata.Initialize(
                compilableName,
                friendlyName,
                type,
                classType,
                isValueType: type.IsValueType,
                canBeDynamic,
                propertiesMetadata,
                fieldsMetadata: null,
                collectionType,
                collectionKeyTypeMetadata: collectionKeyType != null ? GetOrAddTypeMetadata(collectionKeyType) : null,
                collectionValueTypeMetadata: collectionValueType != null ? GetOrAddTypeMetadata(collectionValueType) : null);

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
            _knownTypes.Add(metadataLoadContext.Resolve(typeof(object)));
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

namespace {_generationNamespace}
{{
    {JsonContextDeclarationSource}
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
            foreach (TypeMetadata typeMetadata in _typeMetadataCache.Values)
            {
                usingStatements.UnionWith(GetUsingStatements(typeMetadata));
            }

            sb.Append(@$"{GetUsingStatementsString(usingStatements)}

namespace {_generationNamespace}
{{
    {JsonContextDeclarationSource}
    {{
        public override JsonClassInfo GetJsonClassInfo(Type type)
        {{");

            // TODO: Make this Dictionary-lookup-based if _handledType.Count > 64.
            foreach (TypeMetadata typeMetadata in _typeMetadataCache.Values)
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
            return null!;
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
            //usingStatements.Add(FormatAsUsingStatement("System"));
            usingStatements.Add(FormatAsUsingStatement("System.Runtime.CompilerServices"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json.Serialization"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json.Serialization.Converters"));
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

                        // TODO: consider fields.
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
        private string Get_ContextClass_And_TypeInfoProperty_Declarations(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            return $@"

namespace {_generationNamespace}
{{
    {JsonContextDeclarationSource}
    {{
        private {typeFriendlyName}TypeInfo _{typeFriendlyName};
        public JsonTypeInfo<{typeCompilableName}> {typeFriendlyName}
        {{
            get
            {{
                if (_{typeFriendlyName} == null)
                {{
                    _{typeFriendlyName} = new {typeFriendlyName}TypeInfo();
                    _{typeFriendlyName}.Initialize(this);
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

        private static string GetTypeInfoConstructorAndInitializeMethod(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            StringBuilder sb = new();

            sb.Append($@"
            internal {typeMetadata.FriendlyName}TypeInfo()
            {{
            }}

            public void Initialize(JsonContext context)
            {{
                JsonObjectInfo<{typeMetadata.CompilableName}> typeInfo = new(CreateObjectFunc, context.GetOptions());
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
                typeInfo.AddProperty(
                    ""{propertyName}"",
                    (obj) => {{ return (({typeCompilableName})obj).{propertyName}; }},
                    (obj, value) => {propMutation},
                    {typeClassInfo});
                ");
            }

            // TODO: consider fields.

            sb.Append($@"
                typeInfo.CompleteInitialization(canBeDynamic: {GetBoolAsStr(typeMetadata.CanBeDynamic)});
                TypeInfo = typeInfo;
            }}
");

            return sb.ToString();
        }
    }
}
