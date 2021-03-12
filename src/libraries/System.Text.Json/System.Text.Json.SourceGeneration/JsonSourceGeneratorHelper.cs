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
using System.Runtime.Serialization.Json;
using System.Text.Json.Serialization;

namespace System.Text.Json.SourceGeneration
{
    internal sealed partial class JsonSourceGeneratorHelper
    {
        private readonly Type _ienumerableType;
        private readonly Type _listOfTType;
        private readonly Type _ienumerableOfTType;
        private readonly Type _ilistOfTType;
        private readonly Type _dictionaryType;
        private readonly Type _stringType;

        // Generation namespace for source generation code.
        private readonly string _generationNamespace;

        private const string JsonContextDeclarationSource = "internal partial class JsonContext : JsonSerializerContext";

        private readonly HashSet<Type> _knownTypes = new();

        // Contains used JsonTypeInfo<T> identifiers.
        private readonly HashSet<string> _usedFriendlyTypeNames = new();

        /// <summary>
        /// Type information for member types in input object graphs.
        /// </summary>
        private readonly Dictionary<Type, TypeMetadata> _typeMetadataCache = new();

        /// <summary>
        /// Types that we have initiated serialization metadata generation for. A type may be discoverable in the object graph,
        /// but not reachable for serialization (e.g. it is [JsonIgnore]'d); thus we maintain a separate cache.
        /// </summary>
        private readonly HashSet<TypeMetadata> _typesWithMetadataGenerated = new();

        /// <summary>
        /// Types that were specified with System.Text.Json.Serialization.JsonSerializableAttribute.
        /// </summary>
        private readonly Dictionary<string, (Type, bool)> _rootSerializableTypes;

        private readonly GeneratorExecutionContext _executionContext;

        public JsonSourceGeneratorHelper(
            GeneratorExecutionContext executionContext,
            MetadataLoadContext metadataLoadContext,
            Dictionary<string, (Type, bool)> rootSerializableTypes)
        {
            _generationNamespace = $"{executionContext.Compilation.AssemblyName}.JsonSourceGeneration";
            _executionContext = executionContext;
            _rootSerializableTypes = rootSerializableTypes;

            _ienumerableType = metadataLoadContext.Resolve(typeof(IEnumerable));
            _listOfTType = metadataLoadContext.Resolve(typeof(List<>));
            _ienumerableOfTType = metadataLoadContext.Resolve(typeof(IEnumerable<>));
            _ilistOfTType = metadataLoadContext.Resolve(typeof(IList<>));
            _dictionaryType = metadataLoadContext.Resolve(typeof(Dictionary<,>));
            _stringType = metadataLoadContext.Resolve(typeof(string));

            PopulateKnownTypes(metadataLoadContext);
            InitializeDiagnosticDescriptors();
        }

        public void GenerateSerializationMetadata()
        {
            if (_rootSerializableTypes == null || _rootSerializableTypes.Count == 0)
            {
                throw new InvalidOperationException("Serializable types must be provided to this helper via the constructor.");
            }

            foreach (KeyValuePair<string, (Type, bool)> pair in _rootSerializableTypes)
            {
                (Type type, bool canBeDynamic) = pair.Value;
                TypeMetadata typeMetadata = GetOrAddTypeMetadata(type, canBeDynamic);
                GenerateSerializationMetadataForType(typeMetadata);
            }

            // Add base default instance source.
            AddBaseJsonContextImplementation();

            // Add GetJsonClassInfo override implementation.
            _executionContext.AddSource("JsonContext.GetJsonClassInfo.g.cs", SourceText.From(Get_GetClassInfo_Implementation(), Encoding.UTF8));
        }

        public void AddBaseJsonContextImplementation()
        {
            _executionContext.AddSource("JsonContext.g.cs", SourceText.From(BaseJsonContextImplementation(), Encoding.UTF8));
        }

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
                            SourceText.From(GenerateForTypeWithConverter(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));
                    }
                    break;
                case ClassType.TypeWithCustomConverter:
                    {
                        // TODO: Generate code similar to ClassInfo for known types.
                        return;
                    }
                case ClassType.Nullable:
                    {
                        _executionContext.AddSource(
                            metadataFileName,
                            SourceText.From(GenerateForNullableType(typeMetadata), Encoding.UTF8));

                        _executionContext.ReportDiagnostic(Diagnostic.Create(_generatedTypeClass, Location.None, new string[] { typeMetadata.CompilableName }));

                        // TODO: do we need to generate metadata for the underlying type?
                        GenerateSerializationMetadataForType(typeMetadata.NullableUnderlyingTypeMetadata);
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

                        StringBuilder sb = new();

                        // Add using statements.
                        sb.Append(GetUsingStatementsString(typeMetadata));

                        // Add declarations for JsonContext and the JsonTypeInfo<T> property for the type.
                        sb.Append(Get_ContextClass_And_TypeInfoProperty_Declarations(typeMetadata));

                        // Add declarations for the JsonTypeInfo<T> wrapper and nested property.
                        sb.Append(Get_TypeInfoClassWrapper(typeMetadata));

                        // Add constructor which initializers the JsonPropertyInfo<T>s for the type.
                        sb.Append(GetTypeInfoConstructorAndInitializeMethod(typeMetadata));

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

                        Type type = typeMetadata.Type;

                        // If type had its JsonTypeInfo name changed, report to the user.
                        if (type.Name != typeMetadata.FriendlyName)
                        {
                            // "Duplicate type name detected. Setting the JsonTypeInfo<T> property for type {0} in assembly {1} to {2}. To use please call JsonContext.Instance.{2}",
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
                    var typeInfo = new JsonValueInfo<{typeCompilableName}>(new {typeFriendlyName}Converter(), {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)}, GetOptions());
                    // TODO: remove this for types that can be dynamic since they are initialized in JsonContext ctor.
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

        private string GenerateForNullableType(TypeMetadata typeMetadata)
        {
            string typeCompilableName = typeMetadata.CompilableName;
            string typeFriendlyName = typeMetadata.FriendlyName;

            TypeMetadata? underlyingTypeMetadata = typeMetadata.NullableUnderlyingTypeMetadata;
            Debug.Assert(underlyingTypeMetadata != null && _knownTypes.Contains(underlyingTypeMetadata.Type));
            string underlyingTypeCompilableName = underlyingTypeMetadata.CompilableName;
            string underlyingTypeFriendlyName = underlyingTypeMetadata.FriendlyName;

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
                    // TODO: avoid new allocation for underlying type converter. Should this reuse converter from options.GetConverter()?
                    var typeInfo = new JsonValueInfo<{typeCompilableName}>(new NullableConverter<{underlyingTypeCompilableName}>(new {underlyingTypeFriendlyName}Converter()), {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)}, GetOptions());
                    // TODO: remove this for types that can be dynamic since they are initialized in JsonContext ctor.
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
                CollectionType.Dictionary => $@"(JsonCollectionTypeInfo<{typeCompilableName}>)KnownDictionaryTypeInfos<{keyTypeCompilableName!}, {valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this, {numberHandlingNamedArg})",
                _ => throw new NotSupportedException()
            };

            string GetEnumerableTypeInfoAssignment() => $@"(JsonCollectionTypeInfo<{typeCompilableName}>)KnownCollectionTypeInfos<{valueTypeCompilableName}>.Get{collectionType}({valueTypeMetadataPropertyName}, this, {numberHandlingNamedArg})";

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
                    // TODO: remove this for types that can be dynamic since they are initialized in JsonContext ctor.
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

        private TypeMetadata GetOrAddTypeMetadata(Type type, bool canBeDynamic)
        {
            if (_typeMetadataCache.TryGetValue(type, out TypeMetadata? typeMetadata))
            {
                return typeMetadata!;
            }

            return GetTypeMetadata(type, canBeDynamic);
        }

        private TypeMetadata GetOrAddTypeMetadata(Type type)
        {
            if (_typeMetadataCache.TryGetValue(type, out TypeMetadata? typeMetadata))
            {
                return typeMetadata!;
            }

            bool found = _rootSerializableTypes.TryGetValue(type.FullName, out (Type, bool) typeInfo);

            if (!found && type.IsValueType)
            {
                // To help callers, check if `CanBeDynamic` was specified for the nullable equivalent.
                string nullableTypeFullName = $"System.Nullable`1[[{type.AssemblyQualifiedName}]]";
                _rootSerializableTypes.TryGetValue(nullableTypeFullName, out typeInfo);
            }

            bool canBeDynamic = typeInfo.Item2;
            return GetTypeMetadata(type, canBeDynamic);
        }

        private TypeMetadata GetTypeMetadata(Type type, bool canBeDynamic)
        {
            // Add metadata to cache now to prevent stack overflow when the same type is found somewhere else in the object graph.
            TypeMetadata typeMetadata = new();
            _typeMetadataCache[type] = typeMetadata;

            ClassType classType;
            Type? collectionKeyType = null;
            Type? collectionValueType = null;
            Type? nullableUnderlyingType = null;
            List<PropertyMetadata>? propertiesMetadata = null;
            CollectionType collectionType = CollectionType.NotApplicable;
            ObjectConstructionStrategy constructionStrategy = default;
            JsonNumberHandling? numberHandling = null;

            IList<CustomAttributeData> attributeDataList = CustomAttributeData.GetCustomAttributes(type);
            foreach (CustomAttributeData attributeData in attributeDataList)
            {
                Type attributeType = attributeData.AttributeType;

                if (attributeType.Assembly.FullName != "System.Text.Json")
                {
                    continue;
                }

                if (attributeType.FullName == "System.Text.Json.Serialization.JsonNumberHandlingAttribute")
                {
                    IList<CustomAttributeTypedArgument> ctorArgs = attributeData.ConstructorArguments;
                    if (ctorArgs.Count != 1)
                    {
                        throw new InvalidOperationException($"Invalid use of 'JsonNumberHandlingAttribute' detected on '{type}'.");
                    }

                    numberHandling = (JsonNumberHandling)ctorArgs[0].Value;
                }
            }

            // TODO: first check for custom converter.
            if (_knownTypes.Contains(type))
            {
                classType = ClassType.KnownType;
            }
            else if (type.IsNullableValueType(out Type underlyingType))
            {
                Debug.Assert(underlyingType != null);

                // Limited support for nullable (temporary): underlying type should be primitive. This way we know what the converter's friendly name is.
                // TODO: add full support.
                if (_knownTypes.Contains(underlyingType))
                {
                    nullableUnderlyingType = underlyingType;
                    classType = ClassType.Nullable;
                }
                else
                {
                    classType = ClassType.TypeUnsupportedBySourceGen;
                }
            }
            else if (type.IsEnum)
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

                if (!type.IsAbstract && !type.IsInterface)
                {
                    // TODO: account for parameterized ctors.
                    constructionStrategy = ObjectConstructionStrategy.ParameterlessConstructor;
                }

                Dictionary<string, PropertyMetadata>? ignoredMembers = null;

                for (Type? currentType = type; currentType != null; currentType = currentType.BaseType)
                {
                    const BindingFlags bindingFlags =
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly;

                    foreach (Reflection.PropertyInfo propertyInfo in currentType.GetProperties(bindingFlags))
                    {
                        PropertyMetadata metadata = GetPropertyMetadata(propertyInfo);

                        // Ignore indexers and virtual properties that have overrides that were [JsonIgnore]d.
                        if (propertyInfo.GetIndexParameters().Length > 0 || PropertyIsOverridenAndIgnored(metadata, ignoredMembers))
                        {
                            continue;
                        }

                        string key = metadata.JsonPropertyName ?? metadata.ClrName;

                        if (metadata.HasGetter || metadata.HasSetter) // Don't have to check for JsonInclude since that is used to determine these values.
                        {
                            (propertiesMetadata ??= new()).Add(metadata);
                        }
                    }

                    foreach (FieldInfo fieldInfo in currentType.GetFields(bindingFlags))
                    {
                        PropertyMetadata metadata = GetPropertyMetadata(fieldInfo);

                        if (PropertyIsOverridenAndIgnored(metadata, ignoredMembers))
                        {
                            continue;
                        }

                        if (metadata.HasGetter || metadata.HasSetter) // TODO: don't have to check for JsonInclude since that is used to determine these values?
                        {
                            (propertiesMetadata ??= new()).Add(metadata);
                        }
                    }
                }
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
                numberHandling,
                propertiesMetadata,
                collectionType,
                collectionKeyTypeMetadata: collectionKeyType != null ? GetOrAddTypeMetadata(collectionKeyType) : null,
                collectionValueTypeMetadata: collectionValueType != null ? GetOrAddTypeMetadata(collectionValueType) : null,
                constructionStrategy,
                nullableUnderlyingTypeMetadata: nullableUnderlyingType != null ? GetOrAddTypeMetadata(nullableUnderlyingType) : null);

            return typeMetadata;
        }

        private static bool PropertyIsOverridenAndIgnored(PropertyMetadata currentMemberMetadata, Dictionary<string, PropertyMetadata>? ignoredMembers)
        {
            if (ignoredMembers == null || !ignoredMembers.TryGetValue(currentMemberMetadata.ClrName, out PropertyMetadata? ignoredMemberMetadata))
            {
                return false;
            }

            return currentMemberMetadata.TypeMetadata.Type == ignoredMemberMetadata.TypeMetadata.Type &&
                PropertyIsVirtual(currentMemberMetadata) &&
                PropertyIsVirtual(ignoredMemberMetadata);
        }

        private static bool PropertyIsVirtual(PropertyMetadata? propertyMetadata)
        {
            return propertyMetadata != null && (propertyMetadata.GetterIsVirtual == true || propertyMetadata.SetterIsVirtual == true);
        }

        private PropertyMetadata GetPropertyMetadata(MemberInfo memberInfo)
        {
            IList<CustomAttributeData> attributeDataList = CustomAttributeData.GetCustomAttributes(memberInfo);

            bool hasJsonInclude = false;
            JsonIgnoreCondition? ignoreCondition = null;
            JsonNumberHandling? numberHandling = null;
            string? jsonPropertyName = null;

            foreach (CustomAttributeData attributeData in attributeDataList)
            {
                Type attributeType = attributeData.AttributeType;

                if (attributeType.Assembly.FullName != "System.Text.Json")
                {
                    continue;
                }

                switch (attributeData.AttributeType.FullName)
                {
                    case "System.Text.Json.Serialization.JsonIgnoreAttribute":
                        {
                            IList<CustomAttributeNamedArgument> namedArgs = attributeData.NamedArguments;

                            if (namedArgs.Count == 0)
                            {
                                ignoreCondition = JsonIgnoreCondition.Always;
                            }
                            else if (namedArgs.Count == 1 &&
                                namedArgs[0].MemberInfo.MemberType == MemberTypes.Property &&
                                ((Reflection.PropertyInfo)namedArgs[0].MemberInfo).PropertyType.FullName == "System.Text.Json.Serialization.JsonIgnoreCondition")
                            {
                                ignoreCondition = (JsonIgnoreCondition)namedArgs[0].TypedValue.Value;
                            }
                        }
                        break;
                    case "System.Text.Json.Serialization.JsonIncludeAttribute":
                        {
                            hasJsonInclude = true;
                        }
                        break;
                    case "System.Text.Json.Serialization.JsonNumberHandlingAttribute":
                        {
                            IList<CustomAttributeTypedArgument> ctorArgs = attributeData.ConstructorArguments;
                            if (ctorArgs.Count != 1)
                            {
                                throw new InvalidOperationException($"Invalid use of 'JsonNumberHandlingAttribute' detected on '{memberInfo.DeclaringType}.{memberInfo.Name}'.");
                            }

                            numberHandling = (JsonNumberHandling)ctorArgs[0].Value;
                        }
                        break;
                    case "System.Text.Json.Serialization.JsonPropertyNameAttribute":
                        {
                            IList<CustomAttributeTypedArgument> ctorArgs = attributeData.ConstructorArguments;
                            if (ctorArgs.Count != 1 || ctorArgs[0].ArgumentType != _stringType)
                            {
                                throw new InvalidOperationException($"Invalid use of 'JsonPropertyNameAttribute' detected on '{memberInfo.DeclaringType}.{memberInfo.Name}'.");
                            }

                            jsonPropertyName = (string)ctorArgs[0].Value;
                            // Null check here is done at runtime within JsonSerializer.
                        }
                        break;
                    default:
                        break;
                }
            }

            Type memberCLRType;
            bool hasGetter = false;
            bool hasSetter = false;
            bool getterIsVirtual = false;
            bool setterIsVirtual = false;

            switch (memberInfo)
            {
                case Reflection.PropertyInfo propertyInfo:
                    {
                        MethodInfo setMethod = propertyInfo.SetMethod;

                        memberCLRType = propertyInfo.PropertyType;
                        hasGetter = PropertyAccessorCanBeReferenced(propertyInfo.GetMethod, hasJsonInclude);
                        hasSetter = PropertyAccessorCanBeReferenced(setMethod, hasJsonInclude) && !setMethod.IsInitOnly();
                        getterIsVirtual = propertyInfo.GetMethod?.IsVirtual == true;
                        setterIsVirtual = propertyInfo.SetMethod?.IsVirtual == true;
                    }
                    break;
                case FieldInfo fieldInfo:
                    {
                        Debug.Assert(fieldInfo.IsPublic);

                        memberCLRType = fieldInfo.FieldType;
                        hasGetter = true;
                        hasSetter = !fieldInfo.IsInitOnly;
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return new PropertyMetadata
            {
                ClrName = memberInfo.Name,
                MemberType = memberInfo.MemberType,
                JsonPropertyName = jsonPropertyName,
                HasGetter = hasGetter,
                HasSetter = hasSetter,
                GetterIsVirtual = getterIsVirtual,
                SetterIsVirtual = setterIsVirtual,
                IgnoreCondition = ignoreCondition,
                NumberHandling = numberHandling,
                HasJsonInclude = hasJsonInclude,
                TypeMetadata = GetOrAddTypeMetadata(memberCLRType),
                DeclaringTypeCompilableName = memberInfo.DeclaringType.GetUniqueCompilableTypeName()
            };
        }

        private static bool PropertyAccessorCanBeReferenced(MethodInfo? memberAccessor, bool hasJsonInclude) =>
            (memberAccessor != null && !memberAccessor.IsPrivate) && (memberAccessor.IsPublic || hasJsonInclude);

        private void PopulateKnownTypes(MetadataLoadContext metadataLoadContext)
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
            _knownTypes.Add(_stringType);
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
        private string BaseJsonContextImplementation()
        {
            StringBuilder sb = new();
            sb.Append(@$"using System.Text.Json;
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
            Initialize();
        }}

        public JsonContext(JsonSerializerOptions options) : base(options)
        {{
            Initialize();
        }}

        private void Initialize()
        {{");

            foreach (TypeMetadata typeMetadata in _typesWithMetadataGenerated)
            {
                if (typeMetadata.ClassType != ClassType.TypeUnsupportedBySourceGen && typeMetadata.CanBeDynamic)
                {
                    sb.Append(@$"
            this.{typeMetadata.FriendlyName}.CompleteInitialization(canBeDynamic: true);");
                }
            }

            sb.Append(@"
        }
    }
}
");

            return sb.ToString();
        }

        private string Get_GetClassInfo_Implementation()
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

        private static string Get_TypeInfoClassWrapper (TypeMetadata typeMetadata) {
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

            string createObjectFuncTypeArg = typeMetadata.ConstructionStrategy == ObjectConstructionStrategy.ParameterlessConstructor
                ? $"createObjectFunc: static () => new {typeMetadata.CompilableName}()"
                : "createObjectFunc: null";

            sb.Append($@"
            internal {typeMetadata.FriendlyName}TypeInfo()
            {{
            }}

            public void Initialize(JsonContext context)
            {{
                JsonObjectInfo<{typeMetadata.CompilableName}> typeInfo = new({createObjectFuncTypeArg}, {GetNumberHandlingNamedArg(typeMetadata.NumberHandling)}, context.GetOptions());
                TypeInfo = typeInfo;
            ");

            if (typeMetadata.PropertiesMetadata != null)
            {
                foreach (PropertyMetadata memberMetadata in typeMetadata.PropertiesMetadata)
                {
                    TypeMetadata memberTypeMetadata = memberMetadata.TypeMetadata;

                    string clrPropertyName = memberMetadata.ClrName;

                    string declaringTypeCompilableName = memberMetadata.DeclaringTypeCompilableName;

                    string typeClassInfoNamedArg = memberTypeMetadata.ClassType == ClassType.TypeUnsupportedBySourceGen
                        ? "classInfo: null"
                        : $"classInfo: context.{memberTypeMetadata.FriendlyName}";

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

                    sb.Append($@"
                typeInfo.AddProperty(
                    clrPropertyName: ""{clrPropertyName}"",
                    memberType: System.Reflection.MemberTypes.{memberMetadata.MemberType},
                    declaringType: typeof({memberMetadata.DeclaringTypeCompilableName}),
                    {typeClassInfoNamedArg},
                    {getterNamedArg},
                    {setterNamedArg},
                    {jsonPropertyNameNamedArg},
                    {ignoreConditionNamedArg},
                    {GetNumberHandlingNamedArg(memberMetadata.NumberHandling)});
                ");
                }
            }

            sb.Append($@"
                typeInfo.CompleteInitialization(canBeDynamic: {GetBoolAsStr(typeMetadata.CanBeDynamic)});
            }}
");

            return sb.ToString();
        }

        private static string GetNumberHandlingNamedArg(JsonNumberHandling? numberHandling) =>
             numberHandling.HasValue
                ? $"numberHandling: (JsonNumberHandling){(int)numberHandling.Value}"
                : "numberHandling: null";
    }
}
