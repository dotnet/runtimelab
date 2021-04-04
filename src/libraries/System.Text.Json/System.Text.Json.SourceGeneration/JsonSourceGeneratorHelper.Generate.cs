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

        private const string PropertyCreationMethodName = "CreateProperty";

        // Generation namespace for source generation code.
        private readonly string _generationNamespace;

        private const string JsonContextDeclarationSource = "internal partial class JsonContext : JsonSerializerContext";

        private const string OptionsInstanceVariableName = "Options";

        private const string PropInitFuncVarName = "propInitFunc";

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

            string metadataInitSource = $@"_{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(new {typeFriendlyName}Converter(), {OptionsInstanceVariableName});
                    _{typeFriendlyName}.NumberHandling = {GetNumberHandlingAsStr(typeMetadata.NumberHandling)};";

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
                                actualConverter = converterFactory.CreateConverter(underlyingType, {OptionsInstanceVariableName});

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

                    _{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(converter, {OptionsInstanceVariableName});
                    _{typeFriendlyName}.NumberHandling = {GetNumberHandlingAsStr(typeMetadata.NumberHandling)};";

            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForNullable(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            TypeMetadata? underlyingTypeMetadata = typeMetadata.NullableUnderlyingTypeMetadata;
            Debug.Assert(underlyingTypeMetadata != null);
            string underlyingTypeCompilableName = underlyingTypeMetadata.CompilableName;
            string underlyingTypeFriendlyName = underlyingTypeMetadata.FriendlyName;
            string underlyingConverterNamedArg = underlyingTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                ? "converter: null"
                : $"converter: (JsonConverter<{underlyingTypeCompilableName}>){underlyingTypeFriendlyName}.ConverterBase";

            string metadataInitSource = @$"_{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(
                        new NullableConverter<{underlyingTypeCompilableName}>({underlyingConverterNamedArg}),
                        {OptionsInstanceVariableName});
                    _{typeFriendlyName}.NumberHandling = {GetNumberHandlingAsStr(typeMetadata.NumberHandling)};
";

            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForEnum(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            string metadataInitSource = $@"_{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(new EnumConverter<{typeCompilableName}>({OptionsInstanceVariableName}), {OptionsInstanceVariableName});
                    _{typeFriendlyName}.NumberHandling = {GetNumberHandlingAsStr(typeMetadata.NumberHandling)};";

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

            CollectionType collectionType = typeMetadata.CollectionType;
            string collectionTypeInfoValue = collectionType switch
            {
                CollectionType.Array => GetEnumerableTypeInfoAssignment(),
                CollectionType.List => GetEnumerableTypeInfoAssignment(),
                CollectionType.IEnumerable => GetEnumerableTypeInfoAssignment(),
                CollectionType.IList => GetEnumerableTypeInfoAssignment(),
                CollectionType.Dictionary => $@"KnownDictionaryTypeInfos<{keyTypeCompilableName!}, {valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this)",
                _ => throw new NotSupportedException()
            };

            string GetEnumerableTypeInfoAssignment() => $@"KnownCollectionTypeInfos<{valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this)";

            string metadataInitSource = @$"_{typeFriendlyName} = {collectionTypeInfoValue};
                    _{typeFriendlyName}.NumberHandling = {GetNumberHandlingAsStr(typeMetadata.NumberHandling)};";
            return GenerateForType(typeMetadata, metadataInitSource);
        }

        private string GenerateForObject(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            string createObjectFuncTypeArg = typeMetadata.ConstructionStrategy == ObjectConstructionStrategy.ParameterlessConstructor
                ? $"createObjectFunc: static () => new {typeMetadata.CompilableName}()"
                : "createObjectFunc: null";

            List<PropertyMetadata>? properties = typeMetadata.PropertiesMetadata;

            StringBuilder sb = new();

            sb.Append($@"JsonObjectInfo<{typeCompilableName}> objectInfo = new({OptionsInstanceVariableName});
                    _{typeFriendlyName} = objectInfo;
");

            bool containsOnlyPrimitives = typeMetadata.ContainsOnlyPrimitives;
            string serializeFuncName = $"{typeFriendlyName}SerializeFunc";
            string serializeFuncNamedArg = containsOnlyPrimitives
                ? $"serializeObjectFunc: {serializeFuncName}"
                : "serializeObjectFunc: null";

            string propInitFuncVarName = $"{typeFriendlyName}{PropInitFuncVarName}";

            sb.Append($@"
                    objectInfo.Initialize(
                        {createObjectFuncTypeArg},
                        {serializeFuncNamedArg},
                        {propInitFuncVarName},
                        {GetNumberHandlingAsStr(typeMetadata.NumberHandling)});");

            string metadataInitSource = sb.ToString();

            string? propInitFuncSource = GeneratePropMetadataInitFunc(typeMetadata.IsValueType, propInitFuncVarName, properties);
            string? serializeFuncSource = containsOnlyPrimitives
                ? GenerateFastPathSerializationLogic(typeCompilableName, serializeFuncName, typeMetadata.CanBeNull, properties)
                : null;

            string additionalSource = $@"

        {propInitFuncSource}

        {serializeFuncSource}";

            return GenerateForType(typeMetadata, metadataInitSource, additionalSource);
        }

        private string GeneratePropMetadataInitFunc(
            bool declaringTypeIsValueType,
            string propInitFuncVarName,
            List<PropertyMetadata>? properties)
        {
            const string PropVarName = "properties";
            const string JsonContextVarName = "jsonContext";

            string propertyArrayInstantiationValue = properties == null
                ? "System.Array.Empty<JsonPropertyInfo>()"
                : $"new JsonPropertyInfo[{properties.Count}]";

            StringBuilder sb = new();

            sb.Append($@"private static JsonPropertyInfo[] {propInitFuncVarName}(JsonSerializerContext context)
        {{
            JsonContext {JsonContextVarName} = (JsonContext)context;

            JsonPropertyInfo[] {PropVarName} = {propertyArrayInstantiationValue};
");

            if (properties != null)
            {
                for (int i = 0; i < properties.Count; i++)
                {
                    PropertyMetadata memberMetadata = properties[i];

                    TypeMetadata memberTypeMetadata = memberMetadata.TypeMetadata;

                    string clrPropertyName = memberMetadata.ClrName;

                    string declaringTypeCompilableName = memberMetadata.DeclaringTypeCompilableName;

                    string memberTypeFriendlyName = memberTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                        ? "null"
                        : $"{JsonContextVarName}.{memberTypeMetadata.FriendlyName}";

                    string typeTypeInfoNamedArg = $"typeInfo: {memberTypeFriendlyName}";

                    string jsonPropertyNameNamedArg = memberMetadata.JsonPropertyName != null
                        ? @$"jsonPropertyName: ""{memberMetadata.JsonPropertyName}"""
                        : "jsonPropertyName: null";

                    string getterNamedArg = memberMetadata.HasGetter
                        ? $"getter: static (obj) => {{ return (({declaringTypeCompilableName})obj).{clrPropertyName}; }}"
                        : "getter: null";

                    string setterNamedArg;
                    if (memberMetadata.HasSetter)
                    {
                        string propMutation = declaringTypeIsValueType
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
                        byte[] name = Encoding.UTF8.GetBytes(memberMetadata.JsonPropertyName ?? clrPropertyName);
                        string nameAsStr = string.Join(",", name.Select(b => $"{b}"));
                        // code points for " and : are 34 and 58.
                        string nameSection = name.Length > 0
                            ? @"34," + nameAsStr + @",34,58"
                            : "34,34,58";

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
            {PropVarName}[{i}] = {JsonContextVarName}.{PropertyCreationMethodName}<{memberTypeCompilableName}>(
                clrPropertyName: ""{clrPropertyName}"",
                memberType: System.Reflection.MemberTypes.{memberMetadata.MemberType},
                declaringType: typeof({memberMetadata.DeclaringTypeCompilableName}),
                {typeTypeInfoNamedArg},
                {converterNamedArg},
                {getterNamedArg},
                {setterNamedArg},
                {jsonPropertyNameNamedArg},
                {nameAsUtf8BytesNamedArg},
                {escapedNameSectionNamedArg},
                {ignoreConditionNamedArg},
                numberHandling: {GetNumberHandlingAsStr(memberMetadata.NumberHandling)});
            ");
                }
            }

            sb.Append(@$"
            return {PropVarName};
        }}");

            return sb.ToString();

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

        private string GenerateFastPathSerializationLogic(
            string typeCompilableName,
            string serializeFuncName,
            bool canBeNull,
            List<PropertyMetadata>? properties)
        {
            StringBuilder sb = new();

            sb.Append(@$"private void {serializeFuncName}(Utf8JsonWriter writer, {typeCompilableName} value, JsonSerializerOptions options)
        {{");

            if (canBeNull)
            {
                sb.Append(@"
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
    ");
            }

            sb.Append(@"
            writer.WriteStartObject();");

            if (properties != null)
            {
                for (int i = 0; i < properties.Count; i++)
                {
                    PropertyMetadata propertyMetadata = properties[i];
                    Type propertyType = propertyMetadata.TypeMetadata.Type;
                    string name = propertyMetadata.JsonPropertyName ?? propertyMetadata.ClrName;
                    string propertyNameArg = @$"""{name}""";
                    string propertyValueArg = $"value.{propertyMetadata.ClrName}";
                    string writeMethodArgs = $"{propertyNameArg}, {propertyValueArg}";

                    if (IsStringBasedType(propertyType))
                    {
                        sb.Append(@$"
            writer.WriteString({writeMethodArgs});");
                    }
                    else if (propertyType == _booleanType)
                    {
                        sb.Append(@$"
            writer.WriteBoolean({writeMethodArgs});");
                    }
                    else if (propertyType == _byteArrayType)
                    {
                        sb.Append(@$"
            writer.WriteBase64String({writeMethodArgs});");
                    }
                    else if (propertyType == _charType)
                    {
                        sb.Append(@$"
            writer.WriteString({writeMethodArgs}.ToString());");
                    }
                    else if (propertyType == _dateTimeType)
                    {
                        sb.Append(@$"
            writer.WriteString({writeMethodArgs});");
                    }
                    else if (_numberTypes.Contains(propertyType))
                    {
                        sb.Append(@$"
            writer.WriteNumber({writeMethodArgs});");
                    }
                }
            }

            sb.Append(@"
            writer.WriteEndObject();
        }");

            return sb.ToString();
        }

        private string GenerateForType(TypeMetadata typeMetadata, string metadataInitSource, string? additionalSource = null)
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
                    {WrapWithCheckForCustomConverterIfRequired(metadataInitSource, typeCompilableName, typeFriendlyName, GetNumberHandlingAsStr(typeMetadata.NumberHandling))}
                }}

                return _{typeFriendlyName};
            }}
        }}{additionalSource}
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

            return @$"JsonConverter customConverter;
                    if ({OptionsInstanceVariableName}.Converters.Count > 0 && (customConverter = {RuntimeCustomConverterFetchingMethodName}(typeof({typeCompilableName}))) != null)
                    {{
                        _{typeFriendlyName} = new JsonValueInfo<{typeCompilableName}>(customConverter, {OptionsInstanceVariableName});
                        _{typeFriendlyName}.NumberHandling = {numberHandlingNamedArg};
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
                ? @"
            Initialize();"
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
        {{{initializeMethodCallStatement}
        }}

        private JsonContext(JsonSerializerOptions options) : base(options)
        {{{initializeMethodCallStatement}
        }}

        public static JsonContext From(JsonSerializerOptions options) => new JsonContext(options);

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

            sb.Append(@"
        private void Initialize()
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

            return @$"private JsonConverter {RuntimeCustomConverterFetchingMethodName}(System.Type type)
        {{
            System.Collections.Generic.IList<JsonConverter> converters = {OptionsInstanceVariableName}.Converters;

            for (int i = 0; i < converters.Count; i++)
            {{
                JsonConverter converter = converters[i];

                if (converter.CanConvert(type))
                {{
                    if (converter is JsonConverterFactory factory)
                    {{
                        converter = factory.CreateConverter(type, {OptionsInstanceVariableName});
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

        private static string CreatePropertyImplementation =
            @$"public JsonPropertyInfo<TProperty> {PropertyCreationMethodName}<TProperty>(
                string clrPropertyName,
                System.Reflection.MemberTypes memberType,
                System.Type declaringType,
                JsonTypeInfo<TProperty> typeInfo,
                JsonConverter converter,
                System.Func<object, TProperty> getter,
                System.Action<object, TProperty> setter,
                string jsonPropertyName,
                byte[] nameAsUtf8Bytes,
                byte[] escapedNameSection,
                JsonIgnoreCondition? ignoreCondition,
                JsonNumberHandling? numberHandling)
        {{
            JsonPropertyInfo<TProperty> jsonPropertyInfo = JsonPropertyInfo<TProperty>.Create();
            jsonPropertyInfo.Options = {OptionsInstanceVariableName};
            // Property name settings.
            // TODO: consider whether we need to examine Options.Encoder here as well.
            if (nameAsUtf8Bytes != null && Options.PropertyNamingPolicy == null)
            {{
                jsonPropertyInfo.NameAsString = jsonPropertyName ?? clrPropertyName;
                jsonPropertyInfo.NameAsUtf8Bytes = nameAsUtf8Bytes;
                jsonPropertyInfo.EscapedNameSection = escapedNameSection;
            }}
            else
            {{
                jsonPropertyInfo.NameAsString = jsonPropertyName
                    ?? Options.PropertyNamingPolicy?.ConvertName(clrPropertyName)
                    ?? (Options.PropertyNamingPolicy == null
                            ? null
                            : throw new System.InvalidOperationException(""TODO: PropertyNamingPolicy cannot return null.""));
                // NameAsUtf8Bytes and EscapedNameSection will be set in CompleteInitialization() below.
            }}
            if (ignoreCondition != JsonIgnoreCondition.Always)
            {{
                jsonPropertyInfo.Get = getter;
                jsonPropertyInfo.Set = setter;
                jsonPropertyInfo.ConverterBase = converter ?? throw new System.NotSupportedException(""TODO: need custom converter here?"");
                jsonPropertyInfo.RuntimeTypeInfo = typeInfo;
                jsonPropertyInfo.DeclaredPropertyType = typeof(TProperty);
                jsonPropertyInfo.DeclaringType = declaringType;
                jsonPropertyInfo.IgnoreCondition = ignoreCondition;
                jsonPropertyInfo.NumberHandling = numberHandling;
                jsonPropertyInfo.MemberType = memberType;
            }}
            jsonPropertyInfo.CompleteInitialization();
            return jsonPropertyInfo;
        }}";

        private string GetGetTypeInfoImplementation()
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
        public override JsonTypeInfo GetTypeInfo(System.Type type)
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

            sb.Append(@"
            return null!;
        }
    }
}
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

        private static string GetNumberHandlingAsStr(JsonNumberHandling? numberHandling) =>
             numberHandling.HasValue
                ? $"(JsonNumberHandling){(int)numberHandling.Value}"
                : "null";
    }
}
