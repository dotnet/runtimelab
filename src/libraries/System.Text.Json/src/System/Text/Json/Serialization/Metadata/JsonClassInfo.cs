// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    [DebuggerDisplay("ClassType.{ClassType}, {Type.Name}")]
    // todo: add JsonObjectInfo and JsonArrayInfo classes deriving from JsonClassInfo
    // also add a sealed version of these for internal use (JsonObjectInfoInternal JsonClassInfoInternal)
    public partial class JsonClassInfo
    {
        internal bool _isInitialized;
        // todo: add immutable checks in all setters like we do in JsonSerializerOptions

        /// <summary>
        /// todo
        /// </summary>
        /// <returns></returns>
        public delegate object? ConstructorDelegate();

        internal delegate T ParameterizedConstructorDelegate<T>(object[] arguments);

        internal delegate T ParameterizedConstructorDelegate<T, TArg0, TArg1, TArg2, TArg3>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3);

        private ConstructorDelegate? _createObject;
        /// <summary>
        /// todo
        /// </summary>
        public ConstructorDelegate? CreateObject
        {
            get
            {
                return _createObject;
            }
            set
            {
                if (_isInitialized)
                {
                    throw new InvalidOperationException("todo");
                }

                _createObject = value;
            }
        }

        // Add method delegate for non-generic Stack and Queue; and types that derive from them.
        internal object? AddMethodDelegate { get; set; }

        internal object? CreateObjectWithArgs { get; set; }

        internal ClassType ClassType { get; private set; }

        internal JsonPropertyInfo? DataExtensionProperty { get; private set; }

        // If enumerable, the JsonClassInfo for the element type.
        private JsonClassInfo? _elementClassInfo;

        /// <summary>
        /// todo
        /// </summary>
        public JsonConverter ConverterBase { get; internal set; }

        /// <summary>
        /// Return the JsonClassInfo for the element type, or null if the type is not an enumerable or dictionary.
        /// </summary>
        /// <remarks>
        /// This should not be called during warm-up (initial creation of JsonClassInfos) to avoid recursive behavior
        /// which could result in a StackOverflowException.
        /// </remarks>
        internal JsonClassInfo? ElementClassInfo
        {
            get
            {
                if (_elementClassInfo == null && ElementType != null)
                {
                    Debug.Assert(ClassType == ClassType.Enumerable ||
                        ClassType == ClassType.Dictionary);

                    _elementClassInfo = Options.GetOrAddClass(ElementType);
                }

                return _elementClassInfo;
            }
            set
            {
                // Used with code-gen scenarios.
                Debug.Assert(_elementClassInfo == null);
                _elementClassInfo = value;
            }
        }

        internal Type? ElementType { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        public JsonSerializerOptions Options { get; private set; }

        /// <summary>
        /// todo
        /// </summary>
        public Type Type { get; private set; }

        /// <summary>
        /// The JsonPropertyInfo for this JsonClassInfo. It is used to obtain the converter for the ClassInfo.
        /// </summary>
        /// <remarks>
        /// The returned JsonPropertyInfo does not represent a real property; instead it represents either:
        /// a collection element type,
        /// a generic type parameter,
        /// a property type (if pushed to a new stack frame),
        /// or the root type passed into the root serialization APIs.
        /// For example, for a property returning <see cref="Collections.Generic.List{T}"/> where T is a string,
        /// a JsonClassInfo will be created with .Type=typeof(string) and .PropertyInfoForClassInfo=JsonPropertyInfo{string}.
        /// Without this property, a "Converter" property would need to be added to JsonClassInfo and there would be several more
        /// `if` statements to obtain the converter from either the actual JsonPropertyInfo (for a real property) or from the
        /// ClassInfo (for the cases mentioned above). In addition, methods that have a JsonPropertyInfo argument would also likely
        /// need to add an argument for JsonClassInfo.
        /// </remarks>
        internal JsonPropertyInfo PropertyInfoForClassInfo { get; set; }

        private GenericMethodHolder? _genericMethods;

        /// <summary>
        /// Returns a helper class used when generic methods need to be invoked on Type.
        /// </summary>
        internal GenericMethodHolder GenericMethods
        {
            get
            {
                if (_genericMethods == null)
                {
                    Type runtimePropertyClass = typeof(GenericMethodHolder<>).MakeGenericType(new Type[] { Type })!;
                    _genericMethods = (GenericMethodHolder)Activator.CreateInstance(runtimePropertyClass)!;
                }

                return _genericMethods;
            }
        }

        internal JsonClassInfo(Type type, JsonSerializerOptions options, ClassType classType)
        {
            Type = type;

            Options = options ?? throw new ArgumentNullException(nameof(options));

            // todo: fix up nullability to avoid this.
            PropertyInfoForClassInfo = null!;
            ConverterBase = null!;

            if (classType == ClassType.Object)
            {
                PropertyCache = new Dictionary<string, JsonPropertyInfo>(
                    Options.PropertyNameCaseInsensitive
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal);
            }

            ClassType = classType;
        }

        internal JsonClassInfo(Type type, JsonSerializerOptions options)
        {
            Type = type;
            Options = options;

            JsonConverter converter = GetConverter(
                Type,
                parentClassType: null, // A ClassInfo never has a "parent" class.
                memberInfo: null, // A ClassInfo never has a "parent" property.
                out Type runtimeType,
                Options);

            ConverterBase = converter;
            ClassType = converter.ClassType;
            JsonNumberHandling? typeNumberHandling = GetNumberHandlingForType(Type);

            PropertyInfoForClassInfo = CreatePropertyInfoForClassInfo(Type, runtimeType, converter, Options);

            switch (ClassType)
            {
                case ClassType.Object:
                    {
                        CreateObject = options.MemberAccessorStrategy.CreateConstructor(type);
                        PropertyCache = new Dictionary<string, JsonPropertyInfo>(
                            Options.PropertyNameCaseInsensitive
                                ? StringComparer.OrdinalIgnoreCase
                                : StringComparer.Ordinal);

                        Dictionary<string, MemberInfo>? ignoredMembers = null;

                        // We start from the most derived type.
                        for (Type? currentType = type; currentType != null; currentType = currentType.BaseType)
                        {
                            const BindingFlags bindingFlags =
                                BindingFlags.Instance |
                                BindingFlags.Public |
                                BindingFlags.NonPublic |
                                BindingFlags.DeclaredOnly;

                            foreach (PropertyInfo propertyInfo in currentType.GetProperties(bindingFlags))
                            {
                                // Ignore indexers and virtual properties that have overrides that were [JsonIgnore]d.
                                if (propertyInfo.GetIndexParameters().Length > 0 || PropertyIsOverridenAndIgnored(propertyInfo, ignoredMembers))
                                {
                                    continue;
                                }

                                // For now we only support public properties (i.e. setter and/or getter is public).
                                if (propertyInfo.GetMethod?.IsPublic == true ||
                                    propertyInfo.SetMethod?.IsPublic == true)
                                {
                                    CacheMember(currentType, propertyInfo.PropertyType, propertyInfo, typeNumberHandling, PropertyCache, ref ignoredMembers);
                                }
                                else
                                {
                                    if (JsonPropertyInfo.GetAttribute<JsonIncludeAttribute>(propertyInfo) != null)
                                    {
                                        ThrowHelper.ThrowInvalidOperationException_JsonIncludeOnNonPublicInvalid(propertyInfo, currentType);
                                    }

                                    // Non-public properties should not be included for (de)serialization.
                                }
                            }

                            foreach (FieldInfo fieldInfo in currentType.GetFields(bindingFlags))
                            {
                                if (PropertyIsOverridenAndIgnored(fieldInfo, ignoredMembers))
                                {
                                    continue;
                                }

                                bool hasJsonInclude = JsonPropertyInfo.GetAttribute<JsonIncludeAttribute>(fieldInfo) != null;

                                if (fieldInfo.IsPublic)
                                {
                                    if (hasJsonInclude || Options.IncludeFields)
                                    {
                                        CacheMember(currentType, fieldInfo.FieldType, fieldInfo, typeNumberHandling, PropertyCache, ref ignoredMembers);
                                    }
                                }
                                else
                                {
                                    if (hasJsonInclude)
                                    {
                                        ThrowHelper.ThrowInvalidOperationException_JsonIncludeOnNonPublicInvalid(fieldInfo, currentType);
                                    }

                                    // Non-public fields should not be included for (de)serialization.
                                }
                            }
                        }

                        if (DetermineExtensionDataProperty(PropertyCache))
                        {
                            // Remove from cache since it is handled independently.
                            PropertyCache.Remove(DataExtensionProperty!.NameAsString);

                            PropertyCacheArray = new JsonPropertyInfo[PropertyCache.Count + 1];

                            // Set the last element to the extension property.
                            PropertyCacheArray[PropertyCache.Count] = DataExtensionProperty;
                        }

                        CompleteObjectInitialization();
                    }
                    break;
                case ClassType.Enumerable:
                case ClassType.Dictionary:
                    {
                        ElementType = converter.ElementType;
                        CreateObject = Options.MemberAccessorStrategy.CreateConstructor(runtimeType);
                    }
                    break;
                case ClassType.Value:
                case ClassType.NewValue:
                    {
                        CreateObject = Options.MemberAccessorStrategy.CreateConstructor(type);
                    }
                    break;
                case ClassType.None:
                    {
                        ThrowHelper.ThrowNotSupportedException_SerializationNotSupported(type);
                    }
                    break;
                default:
                    Debug.Fail($"Unexpected class type: {ClassType}");
                    throw new InvalidOperationException();
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Changes here should be reflected in <see cref="JsonObjectInfo{T}.CacheMember"/>
        /// </summary>
        private void CacheMember(
            Type declaringType,
            Type memberType,
            MemberInfo memberInfo,
            JsonNumberHandling? typeNumberHandling,
            Dictionary<string, JsonPropertyInfo> cache,
            ref Dictionary<string, MemberInfo>? ignoredMembers)
        {
            JsonPropertyInfo jsonPropertyInfo = AddProperty(memberInfo, memberType, declaringType, typeNumberHandling, Options);
            Debug.Assert(jsonPropertyInfo.NameAsString != null);

            string memberName = memberInfo.Name;

            // The JsonPropertyNameAttribute or naming policy resulted in a collision.
            if (!JsonHelpers.TryAdd(cache, jsonPropertyInfo.NameAsString, jsonPropertyInfo))
            {
                JsonPropertyInfo other = cache[jsonPropertyInfo.NameAsString];

                if (other.IsIgnored)
                {
                    // Overwrite previously cached property since it has [JsonIgnore].
                    cache[jsonPropertyInfo.NameAsString] = jsonPropertyInfo;
                }
                else if (
                    // Does the current property have `JsonIgnoreAttribute`?
                    !jsonPropertyInfo.IsIgnored &&
                    // Is the current property hidden by the previously cached property
                    // (with `new` keyword, or by overriding)?
                    other.ClrName! != memberName &&
                    // Was a property with the same CLR name ignored? That property hid the current property,
                    // thus, if it was ignored, the current property should be ignored too.
                    ignoredMembers?.ContainsKey(memberName) != true)
                {
                    // We throw if we have two public properties that have the same JSON property name, and neither have been ignored.
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(Type, jsonPropertyInfo);
                }
                // Ignore the current property.
            }

            if (jsonPropertyInfo.IsIgnored)
            {
                (ignoredMembers ??= new Dictionary<string, MemberInfo>()).Add(memberName, memberInfo);
            }
        }

        internal void CompleteObjectInitialization()
        {
            PropertyCacheArray ??= new JsonPropertyInfo[PropertyCache!.Count];

            // Copy the dictionary cache to the array cache.
            PropertyCache!.Values.CopyTo(PropertyCacheArray, 0);

            JsonConverter converter = PropertyInfoForClassInfo.ConverterBase;

            // Allow constructor parameter logic to remove items from the dictionary since the JSON
            // property values will be passed to the constructor and do not call a property setter.
            if (converter.ConstructorIsParameterized)
            {
                InitializeConstructorParameters(converter.ConstructorInfo!);
            }

            _isInitialized = true;
        }

        private sealed class ParameterLookupKey
        {
            public ParameterLookupKey(string name, Type type)
            {
                Name = name;
                Type = type;
            }

            public string Name { get; }
            public Type Type { get; }

            public override int GetHashCode()
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
            }

            public override bool Equals([NotNullWhen(true)] object? obj)
            {
                Debug.Assert(obj is ParameterLookupKey);

                ParameterLookupKey other = (ParameterLookupKey)obj;
                return Type == other.Type && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class ParameterLookupValue
        {
            public ParameterLookupValue(JsonPropertyInfo jsonPropertyInfo)
            {
                JsonPropertyInfo = jsonPropertyInfo;
            }

            public string? DuplicateName { get; set; }
            public JsonPropertyInfo JsonPropertyInfo { get; }
        }

        private void InitializeConstructorParameters(ConstructorInfo constructorInfo)
        {
            ParameterInfo[] parameters = constructorInfo.GetParameters();
            var parameterCache = new Dictionary<string, JsonParameterInfo>(
                parameters.Length, Options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : null);

            static Type GetMemberType(MemberInfo memberInfo)
            {
                Debug.Assert(memberInfo is PropertyInfo || memberInfo is FieldInfo);

                return memberInfo is PropertyInfo propertyInfo
                    ? propertyInfo.PropertyType
                    : Unsafe.As<FieldInfo>(memberInfo).FieldType;
            }

            // Cache the lookup from object property name to JsonPropertyInfo using a case-insensitive comparer.
            // Case-insensitive is used to support both camel-cased parameter names and exact matches when C#
            // record types or anonymous types are used.
            // The property name key does not use [JsonPropertyName] or PropertyNamingPolicy since we only bind
            // the parameter name to the object property name and do not use the JSON version of the name here.
            var nameLookup = new Dictionary<ParameterLookupKey, ParameterLookupValue>(PropertyCacheArray!.Length);

            foreach (JsonPropertyInfo jsonProperty in PropertyCacheArray!)
            {
                string propertyName = jsonProperty.MemberInfo!.Name;
                var key = new ParameterLookupKey(propertyName, GetMemberType(jsonProperty.MemberInfo));
                var value= new ParameterLookupValue(jsonProperty);
                if (!JsonHelpers.TryAdd(nameLookup, key, value))
                {
                    // More than one property has the same case-insensitive name and Type.
                    // Remember so we can throw a nice exception if this property is used as a parameter name.
                    ParameterLookupValue existing = nameLookup[key];
                    existing!.DuplicateName = propertyName;
                }
            }

            foreach (ParameterInfo parameterInfo in parameters)
            {
                var paramToCheck = new ParameterLookupKey(parameterInfo.Name!, parameterInfo.ParameterType);

                if (nameLookup.TryGetValue(paramToCheck, out ParameterLookupValue? matchingEntry))
                {
                    if (matchingEntry.DuplicateName != null)
                    {
                        // Multiple object properties cannot bind to the same constructor parameter.
                        ThrowHelper.ThrowInvalidOperationException_MultiplePropertiesBindToConstructorParameters(
                            Type,
                            parameterInfo.Name!,
                            matchingEntry.JsonPropertyInfo.NameAsString,
                            matchingEntry.DuplicateName,
                            constructorInfo);
                    }

                    Debug.Assert(matchingEntry.JsonPropertyInfo != null);
                    JsonPropertyInfo jsonPropertyInfo = matchingEntry.JsonPropertyInfo;
                    JsonParameterInfo jsonParameterInfo = AddConstructorParameter(parameterInfo, jsonPropertyInfo, Options);
                    parameterCache.Add(jsonPropertyInfo.NameAsString, jsonParameterInfo);

                    // Remove property from deserialization cache to reduce the number of JsonPropertyInfos considered during JSON matching.
                    PropertyCache!.Remove(jsonPropertyInfo.NameAsString);
                }
            }

            // It is invalid for the extension data property to bind with a constructor argument.
            if (DataExtensionProperty != null &&
                parameterCache.ContainsKey(DataExtensionProperty.NameAsString))
            {
                ThrowHelper.ThrowInvalidOperationException_ExtensionDataCannotBindToCtorParam(DataExtensionProperty.MemberInfo!, Type, constructorInfo);
            }

            ParameterCache = parameterCache;
            ParameterCount = parameters.Length;
        }

        private static bool PropertyIsOverridenAndIgnored(MemberInfo currentMember, Dictionary<string, MemberInfo>? ignoredMembers)
        {
            if (ignoredMembers == null || !ignoredMembers.TryGetValue(currentMember.Name, out MemberInfo? ignoredProperty))
            {
                return false;
            }

            Debug.Assert(currentMember is PropertyInfo || currentMember is FieldInfo);
            PropertyInfo? currentPropertyInfo = currentMember as PropertyInfo;
            Type currentMemberType = currentPropertyInfo == null
                ? Unsafe.As<FieldInfo>(currentMember).FieldType
                : currentPropertyInfo.PropertyType;

            Debug.Assert(ignoredProperty is PropertyInfo || ignoredProperty is FieldInfo);
            PropertyInfo? ignoredPropertyInfo = ignoredProperty as PropertyInfo;
            Type ignoredPropertyType = ignoredPropertyInfo == null
                ? Unsafe.As<FieldInfo>(ignoredProperty).FieldType
                : ignoredPropertyInfo.PropertyType;

            return currentMemberType == ignoredPropertyType &&
                PropertyIsVirtual(currentPropertyInfo) &&
                PropertyIsVirtual(ignoredPropertyInfo);
        }

        private static bool PropertyIsVirtual(PropertyInfo? propertyInfo)
        {
            return propertyInfo != null && (propertyInfo.GetMethod?.IsVirtual == true || propertyInfo.SetMethod?.IsVirtual == true);
        }

        internal bool DetermineExtensionDataProperty(Dictionary<string, JsonPropertyInfo> cache)
        {
            JsonPropertyInfo? jsonPropertyInfo = GetPropertyWithUniqueAttribute(Type, typeof(JsonExtensionDataAttribute), cache);
            if (jsonPropertyInfo != null)
            {
                Type declaredPropertyType = jsonPropertyInfo.DeclaredPropertyType;
                if (typeof(IDictionary<string, object>).IsAssignableFrom(declaredPropertyType) ||
                    typeof(IDictionary<string, JsonElement>).IsAssignableFrom(declaredPropertyType))
                {
                    JsonConverter converter = Options.GetConverter(declaredPropertyType)!;
                    Debug.Assert(converter != null);
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationDataExtensionPropertyInvalid(Type, jsonPropertyInfo);
                }

                DataExtensionProperty = jsonPropertyInfo;
                return true;
            }

            return false;
        }

        private static JsonPropertyInfo? GetPropertyWithUniqueAttribute(Type classType, Type attributeType, Dictionary<string, JsonPropertyInfo> cache)
        {
            JsonPropertyInfo? property = null;

            foreach (JsonPropertyInfo jsonPropertyInfo in cache.Values)
            {
                Debug.Assert(jsonPropertyInfo.MemberInfo != null);
                Attribute? attribute = jsonPropertyInfo.MemberInfo.GetCustomAttribute(attributeType);
                if (attribute != null)
                {
                    if (property != null)
                    {
                        ThrowHelper.ThrowInvalidOperationException_SerializationDuplicateTypeAttribute(classType, attributeType);
                    }

                    property = jsonPropertyInfo;
                }
            }

            return property;
        }

        private static JsonParameterInfo AddConstructorParameter(
            ParameterInfo parameterInfo,
            JsonPropertyInfo jsonPropertyInfo,
            JsonSerializerOptions options)
        {
            if (jsonPropertyInfo.IsIgnored)
            {
                return JsonParameterInfo.CreateIgnoredParameterPlaceholder(jsonPropertyInfo);
            }

            JsonConverter converter = jsonPropertyInfo.ConverterBase;
            JsonParameterInfo jsonParameterInfo = converter.CreateJsonParameterInfo();

            jsonParameterInfo.Initialize(
                jsonPropertyInfo.RuntimePropertyType!,
                parameterInfo,
                jsonPropertyInfo,
                options);

            return jsonParameterInfo;
        }

        // This method gets the runtime information for a given type or property.
        // The runtime information consists of the following:
        // - class type,
        // - runtime type,
        // - element type (if the type is a collection),
        // - the converter (either native or custom), if one exists.
        internal static JsonConverter GetConverter(
            Type type,
            Type? parentClassType,
            MemberInfo? memberInfo,
            out Type runtimeType,
            JsonSerializerOptions options)
        {
            Debug.Assert(type != null);
            ValidateType(type, parentClassType, memberInfo, options);

            JsonConverter? converter = options.DetermineConverter(parentClassType, type, memberInfo);
            if (converter == null)
            {
                Debug.Assert(JsonHelpers.DisableJsonSerializerDynamicFallback);
                throw new NotSupportedException("Built-in converters not initialized; thus no converter found for type.");
            }

            runtimeType = GetRuntimeType(type, converter);

            Debug.Assert(!IsInvalidForSerialization(runtimeType));

            return converter;
        }

        internal static Type GetRuntimeType(Type declaredType, JsonConverter converter)
        {
            // The runtimeType is the actual value being assigned to the property.
            // There are three types to consider for the runtimeType:
            // 1) The declared type (the actual property type).
            // 2) The converter.TypeToConvert (the T value that the converter supports).
            // 3) The converter.RuntimeType (used with interfaces such as IList).
            Type runtimeType;

            Type converterRuntimeType = converter.RuntimeType;
            if (declaredType == converterRuntimeType)
            {
                runtimeType = declaredType;
            }
            else
            {
                if (declaredType.IsInterface)
                {
                    runtimeType = converterRuntimeType;
                }
                else if (converterRuntimeType.IsInterface)
                {
                    runtimeType = declaredType;
                }
                else
                {
                    // Use the most derived version from the converter.RuntimeType or converter.TypeToConvert.
                    if (declaredType.IsAssignableFrom(converterRuntimeType))
                    {
                        runtimeType = converterRuntimeType;
                    }
                    else if (converterRuntimeType.IsAssignableFrom(declaredType) || converter.TypeToConvert.IsAssignableFrom(declaredType))
                    {
                        runtimeType = declaredType;
                    }
                    else
                    {
                        runtimeType = default!;
                        ThrowHelper.ThrowNotSupportedException_SerializationNotSupported(declaredType);
                    }
                }
            }

            return runtimeType;
        }

        private static void ValidateType(Type type, Type? parentClassType, MemberInfo? memberInfo, JsonSerializerOptions options)
        {
            if (!options.TypeIsCached(type) && IsInvalidForSerialization(type))
            {
                ThrowHelper.ThrowInvalidOperationException_CannotSerializeInvalidType(type, parentClassType, memberInfo);
            }
        }

        private static bool IsInvalidForSerialization(Type type)
        {
            return type.IsPointer || IsByRefLike(type) || type.ContainsGenericParameters;
        }

        private static bool IsByRefLike(Type type)
        {
#if BUILDING_INBOX_LIBRARY
            return type.IsByRefLike;
#else
            if (!type.IsValueType)
            {
                return false;
            }

            object[] attributes = type.GetCustomAttributes(inherit: false);

            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].GetType().FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute")
                {
                    return true;
                }
            }

            return false;
#endif
        }

        private static JsonNumberHandling? GetNumberHandlingForType(Type type)
        {
            var numberHandlingAttribute =
                (JsonNumberHandlingAttribute?)JsonSerializerOptions.GetAttributeThatCanHaveMultiple(type, typeof(JsonNumberHandlingAttribute));

            return numberHandlingAttribute?.Handling;
        }
    }
}
