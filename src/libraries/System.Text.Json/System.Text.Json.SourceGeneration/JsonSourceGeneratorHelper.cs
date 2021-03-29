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

        private readonly HashSet<Type> _knownTypes = new();

        // Contains used JsonTypeInfo<T> identifiers.
        private readonly HashSet<string> _usedFriendlyTypeNames = new();

        /// <summary>
        /// Type information for member types in input object graphs.
        /// </summary>
        private readonly Dictionary<Type, TypeMetadata> _typeMetadataCache = new();

        /// </summary>
        private readonly Dictionary<string, (Type, bool)> _rootSerializableTypes;

        private readonly GeneratorExecutionContext _executionContext;

        private readonly MetadataLoadContext _metadataLoadContext;

        private const string JsonConverterAttributeFullName = "System.Text.Json.Serialization.JsonConverterAttribute";

        public JsonSourceGeneratorHelper(
            GeneratorExecutionContext executionContext,
            MetadataLoadContext metadataLoadContext,
            Dictionary<string, (Type, bool)> rootSerializableTypes)
        {
            _generationNamespace = $"{executionContext.Compilation.AssemblyName}.JsonSourceGeneration";
            _executionContext = executionContext;
            _metadataLoadContext = metadataLoadContext;
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
            _executionContext.AddSource("JsonContext.GetJsonClassInfo.g.cs", SourceText.From(GetGetClassInfoImplementation(), Encoding.UTF8));
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

            bool foundDesignTimeCustomConverter = false;
            string? converterInstatiationLogic = null;

            IList<CustomAttributeData> attributeDataList = CustomAttributeData.GetCustomAttributes(type);
            foreach (CustomAttributeData attributeData in attributeDataList)
            {
                Type attributeType = attributeData.AttributeType;
                if (attributeType.FullName == "System.Text.Json.Serialization.JsonNumberHandlingAttribute")
                {
                    IList<CustomAttributeTypedArgument> ctorArgs = attributeData.ConstructorArguments;
                    if (ctorArgs.Count != 1)
                    {
                        throw new InvalidOperationException($"Invalid use of 'JsonNumberHandlingAttribute' detected on '{type}'.");
                    }

                    numberHandling = (JsonNumberHandling)ctorArgs[0].Value;
                    continue;
                }
                else if (!foundDesignTimeCustomConverter && attributeType.GetCompatibleBaseClass(JsonConverterAttributeFullName) != null)
                {
                    foundDesignTimeCustomConverter = true;
                    converterInstatiationLogic = GetConverterInstantiationLogic(attributeData);
                }
            }

            if (foundDesignTimeCustomConverter)
            {
                classType = converterInstatiationLogic != null
                    ? ClassType.TypeWithDesignTimeProvidedCustomConverter
                    : ClassType.TypeUnsupportedBySourceGen; // TODO: provide diagnostic with reason.
            }
            else if (_knownTypes.Contains(type))
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
                classType = ClassType.Enum;
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
                nullableUnderlyingTypeMetadata: nullableUnderlyingType != null ? GetOrAddTypeMetadata(nullableUnderlyingType) : null,
                converterInstatiationLogic);

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

            bool foundDesignTimeCustomConverter = false;
            string? converterInstantiationLogic = null;

            foreach (CustomAttributeData attributeData in attributeDataList)
            {
                Type attributeType = attributeData.AttributeType;

                if (!foundDesignTimeCustomConverter && attributeType.GetCompatibleBaseClass(JsonConverterAttributeFullName) != null)
                {
                    foundDesignTimeCustomConverter = true;
                    converterInstantiationLogic = GetConverterInstantiationLogic(attributeData);
                }
                else if (attributeType.Assembly.FullName == "System.Text.Json")
                {
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
                DeclaringTypeCompilableName = memberInfo.DeclaringType.GetUniqueCompilableTypeName(),
                ConverterInstantiationLogic = converterInstantiationLogic
            };
        }

        private static bool PropertyAccessorCanBeReferenced(MethodInfo? memberAccessor, bool hasJsonInclude) =>
            (memberAccessor != null && !memberAccessor.IsPrivate) && (memberAccessor.IsPublic || hasJsonInclude);

        private string? GetConverterInstantiationLogic(CustomAttributeData attributeData)
        {
            if (attributeData.AttributeType.FullName != JsonConverterAttributeFullName)
            {
                // TODO: need diagnostic here telling user that converter was ignored (derived JsonConverterAttribute not supported in codegen mode.)
                return null;
            }

            Type converterType = new TypeWrapper((ITypeSymbol)attributeData.ConstructorArguments[0].Value, _metadataLoadContext);

            if (converterType == null || converterType.GetConstructor(Type.EmptyTypes) == null || converterType.IsNestedPrivate)
            {
                // TODO: need diagnostic here telling user that converter was ignored.
                return null;
            }

            return $"new {converterType.GetUniqueCompilableTypeName()}()";
        }

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
    }
}
