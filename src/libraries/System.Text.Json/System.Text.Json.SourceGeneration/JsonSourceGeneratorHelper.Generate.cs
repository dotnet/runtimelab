// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace System.Text.Json.SourceGeneration
{
    internal sealed partial class JsonSourceGeneratorHelper
    {
        // TODO: consider public option for this.
        private bool _honorRuntimeProvidedCustomConverters = true;

        private const string RuntimeCustomConverterFetchingMethodName = "GetRuntimeProvidedCustomConverter";

        // Generation namespace for source generation code.
        private readonly string _generationNamespace;

        private const string JsonContextDeclarationSource = "internal partial class JsonContext : JsonSerializerContext";

        /// <summary>
        /// Types that we have initiated serialization metadata generation for. A type may be discoverable in the object graph,
        /// but not reachable for serialization (e.g. it is [JsonIgnore]'d); thus we maintain a separate cache.
        /// </summary>
        private readonly HashSet<TypeMetadata> _typesWithMetadataGenerated = new();

        /// <summary>
        /// Types that were specified with System.Text.Json.Serialization.JsonSerializableAttribute.

        public void GenerateSerializationMetadataForType(TypeMetadata typeMetadata)
        {
            Debug.Assert(typeMetadata != null);

            if (_typesWithMetadataGenerated.Contains(typeMetadata))
            {
                return;
            }

            _typesWithMetadataGenerated.Add(typeMetadata);

            string metadataFileName = $"{typeMetadata.FriendlyName}.g.cs";

            switch (typeMetadata.ClassType)
            {
                case ClassType.KnownType:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForTypeWithKnownConverter(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));
                    }
                    break;
                case ClassType.TypeWithDesignTimeProvidedCustomConverter:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForTypeWithUnknownConverter(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));
                    }
                    break;
                case ClassType.Nullable:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForNullable(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));

                        GenerateSerializationMetadataForType(typeMetadata.NullableUnderlyingTypeMetadata);
                    }
                    break;
                case ClassType.Enum:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForEnum(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));
                    }
                    break;
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

                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForObject(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));

                        Type type = typeMetadata.Type;

                        // TODO: this needs to be done for all type classes (not just ClassType.Object).
                        // If type had its JsonTypeInfo name changed, report to the user.
                        if (type.Name != typeMetadata.FriendlyName)
                        {
                            // "Duplicate type name detected. Setting the JsonTypeInfo<T> property for type {0} in assembly {1} to {2}. To use please call JsonContext.Default.{2}",
                            _executionContext.ReportDiagnostic(Diagnostic.Create(
                                _typeNameClash,
                                Location.None,
                                new string[] { typeMetadata.CompilableName, type.Assembly.FullName, typeMetadata.FriendlyName }));
                        }

                        if (typeMetadata.PropertiesMetadata != null)
                        {
                            foreach (PropertyMetadata metadata in typeMetadata.PropertiesMetadata)
                            {
                                GenerateSerializationMetadataForType(metadata.TypeMetadata);
                            }
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

        private string GenerateForTypeWithKnownConverter(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            string metadataInitSource = $@"_{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(new {typeFriendlyName}Converter(), {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)}, options);";

            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForTypeWithUnknownConverter(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            StringBuilder sb = new();

            string metadataInitSource = $@"JsonConverter converter = {typeMetadata.ConverterInstantiationLogic};
                    // TODO: consider moving this verification source to common helper.
                    Type typeToConvert = typeof({typeCompilableName});
                    if (!converter.CanConvert(typeToConvert))
                    {{
                        Type underlyingType = Nullable.GetUnderlyingType(typeToConvert);
                        if (underlyingType != null && converter.CanConvert(underlyingType))
                        {{
                            JsonConverter actualConverter = converter;

                            if (converter is JsonConverterFactory converterFactory)
                            {{
                                actualConverter = converterFactory.CreateConverter(underlyingType, GetOptions());

                                if (actualConverter == null || actualConverter is JsonConverterFactory)
                                {{
                                    throw new InvalidOperationException($""JsonConverterFactory '{{converter}} cannot return a 'null' or 'JsonConverterFactory' value."");
                                }}
                            }}

                            // Allow nullable handling to forward to the underlying type's converter.
                            converter = new System.Text.Json.Serialization.Converters.NullableConverter<{typeCompilableName}>((JsonConverter<{typeCompilableName}>)actualConverter);
                        }}
                        else
                        {{
                            throw new InvalidOperationException($""The converter '{{converter.GetType()}}' is not compatible with the type '{{typeToConvert}}'."");
                        }}
                    }}

                    _{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(converter, {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)}, options);";

            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForNullable(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            TypeMetadata? underlyingTypeMetadata = typeMetadata.NullableUnderlyingTypeMetadata;
            Debug.Assert(underlyingTypeMetadata != null && _knownTypes.Contains(underlyingTypeMetadata.Type));
            string underlyingTypeCompilableName = underlyingTypeMetadata.CompilableName;
            string underlyingTypeFriendlyName = underlyingTypeMetadata.FriendlyName;

            string metadataInitSource = @$"_{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(
                        new NullableConverter<{underlyingTypeCompilableName}>((JsonConverter<{underlyingTypeCompilableName}>){underlyingTypeFriendlyName}.ConverterBase),
                        {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)},
                        options);
";

            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForEnum(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            string metadataInitSource = $@"_{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(new EnumConverter<{typeCompilableName}>(options), {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)}, options);
";

            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForCollection(TypeMetadata typeMetadata)
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

            string numberHandlingNamedArg = GetNumberHandlingNamedArg(typeMetadata.NumberHandling);

            CollectionType collectionType = typeMetadata.CollectionType;

            string collectionTypeInfoValue = collectionType switch
            {
                CollectionType.Array => GetEnumerableTypeInfoAssignment(),
                CollectionType.List => GetEnumerableTypeInfoAssignment(),
                CollectionType.IEnumerable => GetEnumerableTypeInfoAssignment(),
                CollectionType.IList => GetEnumerableTypeInfoAssignment(),
                CollectionType.Dictionary => $@"KnownDictionaryTypeInfos<{keyTypeCompilableName!}, {valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this, {numberHandlingNamedArg})",
                _ => throw new NotSupportedException()
            };

            string GetEnumerableTypeInfoAssignment() => $@"KnownCollectionTypeInfos<{valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this, {numberHandlingNamedArg})";

            string metadataInitSource = $"_{typeFriendlyName} = {collectionTypeInfoValue};";
            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForObject(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            string createObjectFuncTypeArg = typeMetadata.ConstructionStrategy == ObjectConstructionStrategy.ParameterlessConstructor
                ? $"createObjectFunc: static () => new {typeMetadata.CompilableName}()"
                : "createObjectFunc: null";

            StringBuilder sb = new();

            sb.Append($@"JsonObjectInfo<{typeCompilableName}> objectInfo = new({createObjectFuncTypeArg}, {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)}, this.GetOptions());
                    _{typeFriendlyName} = objectInfo;
");

            if (typeMetadata.PropertiesMetadata != null)
            {
                foreach (PropertyMetadata memberMetadata in typeMetadata.PropertiesMetadata)
                {
                    TypeMetadata memberTypeMetadata = memberMetadata.TypeMetadata;

                    string clrPropertyName = memberMetadata.ClrName;

                    string declaringTypeCompilableName = memberMetadata.DeclaringTypeCompilableName;

                    string memberTypeFriendlyName = memberTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                        ? "null"
                        : $"this.{memberTypeMetadata.FriendlyName}";

                    string typeClassInfoNamedArg = $"classInfo: {memberTypeFriendlyName}";

                    string jsonPropertyNameNamedArg = memberMetadata.JsonPropertyName != null
                        ? @$"jsonPropertyName: ""{memberMetadata.JsonPropertyName}"""
                        : "jsonPropertyName: null";

                    string getterNamedArg = memberMetadata.HasGetter
                        ? $"getter: static (obj) => {{ return (({declaringTypeCompilableName})obj).{clrPropertyName}; }}"
                        : "getter: null";

                    string setterNamedArg;
                    if (memberMetadata.HasSetter)
                    {
                        string propMutation = typeMetadata.IsValueType
                            ? @$"{{ Unsafe.Unbox<{declaringTypeCompilableName}>(obj).{clrPropertyName} = value; }}"
                            : $@"{{ (({declaringTypeCompilableName})obj).{clrPropertyName} = value; }}";

                        setterNamedArg = $"setter: static (obj, value) => {propMutation}";
                    }
                    else
                    {
                        setterNamedArg = "setter: null";
                    }

                    JsonIgnoreCondition? ignoreCondition = memberMetadata.IgnoreCondition;
                    string ignoreConditionNamedArg = ignoreCondition.HasValue
                        ? $"ignoreCondition: JsonIgnoreCondition.{ignoreCondition.Value}"
                        : "ignoreCondition: null";

                    string nameAsUtf8BytesNamedArg;
                    string escapedNameSectionNamedArg;
                    if (!ContainsNonAscii(clrPropertyName))
                    {
                        byte[] name = Encoding.UTF8.GetBytes(clrPropertyName);
                        string nameAsStr = string.Join(",", name.Select(b => $"{b}"));
                        string nameSection = @"34," + nameAsStr + @",34,58"; // code points for " and : are 34 and 58.

                        nameAsUtf8BytesNamedArg = "nameAsUtf8Bytes: new byte[] {" + nameAsStr + "}";
                        escapedNameSectionNamedArg = "escapedNameSection: new byte[] {" + nameSection + "}";
                    }
                    else
                    {
                        nameAsUtf8BytesNamedArg = "nameAsUtf8Bytes: null";
                        escapedNameSectionNamedArg = "escapedNameSection: null";
                    }

                    string converterNamedArg;
                    if (memberMetadata.ConverterInstantiationLogic != null)
                    {
                        converterNamedArg = $"converter: {memberMetadata.ConverterInstantiationLogic}";
                    }
                    else if (memberTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen)
                    {
                        // TODO: verify that this is safe.
                        converterNamedArg = "converter: null";
                    }
                    else
                    {
                        converterNamedArg = $"converter: {memberTypeFriendlyName}.ConverterBase";
                    }

                    string memberTypeCompilableName = memberTypeMetadata.CompilableName;

                    sb.Append($@"
                    objectInfo.AddProperty(CreateProperty<{memberTypeCompilableName}>(
                        clrPropertyName: ""{clrPropertyName}"",
                        memberType: System.Reflection.MemberTypes.{memberMetadata.MemberType},
                        declaringType: typeof({memberMetadata.DeclaringTypeCompilableName}),
                        {typeClassInfoNamedArg},
                        {converterNamedArg},
                        {getterNamedArg},
                        {setterNamedArg},
                        {jsonPropertyNameNamedArg},
                        {ignoreConditionNamedArg},
                        {nameAsUtf8BytesNamedArg},
                        {escapedNameSectionNamedArg},
                        {GetNumberHandlingNamedArg(memberMetadata.NumberHandling)}));
                ");
                }
            }

            sb.Append(@$"
                    objectInfo.CompleteInitialization();");

            string metadataInitSource = sb.ToString();
            return GenerateForType(typeMetadata, metadataInitSource);

            static bool ContainsNonAscii(string str)
            {
                foreach (char c in str)
                {
                    if (c > 127)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private string GenerateForType(TypeMetadata typeMetadata, string metadataInitSource)
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
                    JsonSerializerOptions options = GetOptions();

                    {WrapWithCheckForCustomConverterIfRequired(metadataInitSource, typeCompilableName, typeFriendlyName, GetNumberHandlingNamedArg(typeMetadata.NumberHandling))}
                }}

                return _{typeFriendlyName};
            }}
        }}
    }}
}}
";
        }

        private string WrapWithCheckForCustomConverterIfRequired(string source, string typeCompilableName, string typeFriendlyName, string numberHandlingNamedArg)
        {
            if (!_honorRuntimeProvidedCustomConverters)
            {
                return source;
            }

            return @$"// TODO (optimization): do not call this method if options.Converter.Length == 0.
                    JsonConverter customConverter = {RuntimeCustomConverterFetchingMethodName}(typeof({typeCompilableName}), options);
                    if (customConverter != null)
                    {{
                        // TODO: should we tighten this to only allow a `T` which is == {typeCompilableName}?
                        //JsonConverter<{typeCompilableName}> typedConverter = customConverter as JsonConverter<{typeCompilableName}>
                        //    ?? throw new System.NotSupportedException($""The custom converter '{{customConverter.GetType()}}' for type '{typeFriendlyName}' must have it as the 'TypeToConvert'."");

                        _{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(customConverter, {numberHandlingNamedArg}, options);
                    }}
                    else
                    {{
                        {source.Replace(Environment.NewLine, $"{Environment.NewLine}    ")}
                    }}";
        }

        public void AddBaseJsonContextImplementation()
        {
            _executionContext.AddSource("JsonContext.g.cs", SourceText.From(BaseJsonContextImplementation(), Encoding.UTF8));
        }

        // Base source generation context partial class.
        private string BaseJsonContextImplementation()
        {
            bool typesCanBeSerializedDynamically = TryGetInitializationForDynamicallySerializableTypes(out string initializationSource);
            string initializeMethodCallStatement = typesCanBeSerializedDynamically
                ? "Initialize();"
                : null;

            StringBuilder sb = new();
            sb.Append(@$"using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace {_generationNamespace}
{{
    {JsonContextDeclarationSource}
    {{
        private static JsonContext s_default;
        public static JsonContext Default => s_default ??= new JsonContext();

        private JsonContext()
        {{
            {initializeMethodCallStatement}
        }}

        public JsonContext(JsonSerializerOptions options) : base(options)
        {{
            {initializeMethodCallStatement}
        }}

        {initializationSource}

        {GetFetchLogicForRuntimeSpecifiedCustomConverter()}

        {CreatePropertyImplementation}
    }}
}}
");

            return sb.ToString();
        }

        private bool TryGetInitializationForDynamicallySerializableTypes(out string source)
        {
            StringBuilder sb = new();

            sb.Append(@"private void Initialize()
        {");

            bool typesCanBeSerializedDynamically = false;

            foreach (TypeMetadata typeMetadata in _typesWithMetadataGenerated)
            {
                if (typeMetadata.ClassType != ClassType.TypeUnsupportedBySourceGen && typeMetadata.CanBeDynamic)
                {
                    sb.Append(@$"
            this.{typeMetadata.FriendlyName}.RegisterToOptions();");

                    typesCanBeSerializedDynamically = true;
                }
            }

            sb.Append(@"
        }");

            if (typesCanBeSerializedDynamically)
            {
                source = sb.ToString();
                return true;
            }

            source = "";
            return false;
        }

        private string GetFetchLogicForRuntimeSpecifiedCustomConverter()
        {
            if (!_honorRuntimeProvidedCustomConverters)
            {
                return "";
            }

            return @$"private static JsonConverter {RuntimeCustomConverterFetchingMethodName}(System.Type type, JsonSerializerOptions options)
        {{
            System.Collections.Generic.IList<JsonConverter> converters = options.Converters;

            for (int i = 0; i < converters.Count; i++)
            {{
                JsonConverter converter = converters[i];

                if (converter.CanConvert(type))
                {{
                    if (converter is JsonConverterFactory factory)
                    {{
                        converter = factory.CreateConverter(type, options);
                        if (converter == null || converter is JsonConverterFactory)
                        {{
                            throw new System.InvalidOperationException($""The converter '{{factory.GetType()}}' cannot return null or a JsonConverterFactory instance."");
                        }}
                    }}

                    return converter;
                }}
            }}

            return null;
        }}";
        }

        private const string CreatePropertyImplementation =
            @"public JsonPropertyInfo<TProperty> CreateProperty<TProperty>(
                string clrPropertyName,
                System.Reflection.MemberTypes memberType,
                System.Type declaringType,
                JsonTypeInfo<TProperty> classInfo,
                JsonConverter converter,
                System.Func<object, TProperty> getter,
                System.Action<object, TProperty> setter,
                string jsonPropertyName,
                byte[] nameAsUtf8Bytes,
                byte[] escapedNameSection,
                JsonIgnoreCondition? ignoreCondition,
                JsonNumberHandling? numberHandling)
            {
                JsonSerializerOptions options = GetOptions();
                JsonPropertyInfo<TProperty> jsonPropertyInfo = JsonPropertyInfo<TProperty>.Create();
                jsonPropertyInfo.Options = options;
                // Property name settings.
                // TODO: consider whether we need to examine options.Encoder here as well.
                if (options.PropertyNamingPolicy == null && nameAsUtf8Bytes != null && escapedNameSection != null)
                {
                    jsonPropertyInfo.NameAsString = jsonPropertyName ?? clrPropertyName;
                    jsonPropertyInfo.NameAsUtf8Bytes = nameAsUtf8Bytes;
                    jsonPropertyInfo.EscapedNameSection = escapedNameSection;
                }
                else
                {
                    jsonPropertyInfo.NameAsString = jsonPropertyName
                        ?? options.PropertyNamingPolicy?.ConvertName(clrPropertyName)
                        ?? (options.PropertyNamingPolicy == null
                                ? null
                                : throw new System.InvalidOperationException(""TODO: PropertyNamingPolicy cannot return null.""));
                    // NameAsUtf8Bytes and EscapedNameSection will be set in CompleteInitialization() below.
                }
                if (ignoreCondition != JsonIgnoreCondition.Always)
                {
                    jsonPropertyInfo.Get = getter;
                    jsonPropertyInfo.Set = setter;
                    jsonPropertyInfo.ConverterBase = converter ?? throw new System.NotSupportedException(""TODO: need custom converter here?"");
                    jsonPropertyInfo.RuntimeClassInfo = classInfo;
                    jsonPropertyInfo.DeclaredPropertyType = typeof(TProperty);
                    jsonPropertyInfo.DeclaringType = declaringType;
                    jsonPropertyInfo.IgnoreCondition = ignoreCondition;
                    jsonPropertyInfo.MemberType = memberType;
                }
                jsonPropertyInfo.CompleteInitialization();
                return jsonPropertyInfo;
            }";

        private string GetGetClassInfoImplementation()
        {
            StringBuilder sb = new();

            HashSet<string> usingStatements = new();

            foreach (TypeMetadata typeMetadata in _typesWithMetadataGenerated)
            {
                usingStatements.UnionWith(GetUsingStatements(typeMetadata));
            }

            sb.Append(@$"{GetUsingStatementsString(usingStatements)}

namespace {_generationNamespace}
{{
    {JsonContextDeclarationSource}
    {{
        public override JsonClassInfo GetJsonClassInfo(System.Type type)
        {{");

            // TODO: Make this Dictionary-lookup-based if _handledType.Count > 64.
            foreach (TypeMetadata typeMetadata in _typesWithMetadataGenerated)
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
            usingStatements.Add(FormatAsUsingStatement("System.Runtime.CompilerServices"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json.Serialization"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json.Serialization.Converters"));
            usingStatements.Add(FormatAsUsingStatement("System.Text.Json.Serialization.Metadata"));

            // Add imports to root type.
            usingStatements.Add(FormatAsUsingStatement(typeMetadata.Type.Namespace));

            switch (typeMetadata.ClassType)
            {
                case ClassType.Nullable:
                    {
                        AddUsingStatementsForType(typeMetadata.NullableUnderlyingTypeMetadata!);
                    }
                    break;
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
                        if (typeMetadata.PropertiesMetadata != null)
                        {
                            foreach (PropertyMetadata metadata in typeMetadata.PropertiesMetadata)
                            {
                                AddUsingStatementsForType(metadata.TypeMetadata);
                            }
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

        private static string GetNumberHandlingNamedArg(JsonNumberHandling? numberHandling) =>
             numberHandling.HasValue
                ? $"numberHandling: (JsonNumberHandling){(int)numberHandling.Value}"
                : "numberHandling: null";
    }
}
