// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    [DebuggerDisplay("MemberInfo={MemberInfo}")]
    public abstract class JsonPropertyInfo
    {
        internal static readonly JsonPropertyInfo s_missingProperty = GetPropertyPlaceholder();

        private JsonClassInfo? _runtimeClassInfo;

        internal ClassType ClassType;

        /// <summary>
        /// TODO
        /// </summary>
        public JsonIgnoreCondition? IgnoreCondition { get; set; }

        /// <summary>
        /// TODO
        /// </summary>
        public JsonNumberHandling? NumberHandling { get; set; }

        /// <summary>
        /// TODO
        /// </summary>
        public MemberTypes MemberType { get; set; }

        internal JsonPropertyInfo() { }

        /// <summary>
        /// todo
        /// </summary>
        public abstract JsonConverter ConverterBase { get; set; }

        internal static JsonPropertyInfo GetPropertyPlaceholder()
        {
            JsonPropertyInfo info = new JsonPropertyInfo<object>();

            Debug.Assert(!info.IsForClassInfo);
            Debug.Assert(!info.ShouldDeserialize);
            Debug.Assert(!info.ShouldSerialize);

            info.NameAsString = string.Empty;

            return info;
        }

        // Create a property that is ignored at run-time. It uses the same type (typeof(sbyte)) to help
        // prevent issues with unsupported types and helps ensure we don't accidently (de)serialize it.
        internal static JsonPropertyInfo CreateIgnoredPropertyPlaceholder(MemberInfo? memberInfo, JsonSerializerOptions options)
        {
            JsonPropertyInfo jsonPropertyInfo = new JsonPropertyInfo<sbyte>();
            jsonPropertyInfo.Options = options;
            jsonPropertyInfo.MemberInfo = memberInfo;
            jsonPropertyInfo.DeterminePropertyName();
            jsonPropertyInfo.IsIgnored = true;

            Debug.Assert(!jsonPropertyInfo.ShouldDeserialize);
            Debug.Assert(!jsonPropertyInfo.ShouldSerialize);

            return jsonPropertyInfo;
        }

        // Create a property that is ignored at run-time. It uses the same type (typeof(sbyte)) to help
        // prevent issues with unsupported types and helps ensure we don't accidently (de)serialize it.
        internal static JsonPropertyInfo<TProperty> CreateIgnoredPropertyPlaceholder<TProperty>(
            string clrPropertyName,
            JsonSerializerOptions options,
            string? jsonPropertyName)
        {
            JsonPropertyInfo<TProperty> jsonPropertyInfo = new();
            jsonPropertyInfo.Options = options;
            // TODO: test [JsonIgnore] + [JsonPropertyName(name)] for both code gen and non-code-gen paths.
            jsonPropertyInfo.DeterminePropertyName(clrPropertyName, jsonPropertyName);
            jsonPropertyInfo.IsIgnored = true;

            Debug.Assert(!jsonPropertyInfo.ShouldDeserialize);
            Debug.Assert(!jsonPropertyInfo.ShouldSerialize);

            return jsonPropertyInfo;
        }

        /// <summary>
        /// todo
        /// </summary>
        public Type DeclaredPropertyType { get; set; } = null!;

        internal Type? RuntimePropertyType { get; set; }

        internal virtual void GetPolicies(JsonIgnoreCondition? ignoreCondition, JsonNumberHandling? parentTypeNumberHandling)
        {
            if (IsForClassInfo)
            {
                Debug.Assert(MemberInfo == null);
                DetermineNumberHandlingForClassInfo(parentTypeNumberHandling);
            }
            else
            {
                Debug.Assert(MemberInfo != null);
                DetermineSerializationCapabilities(ignoreCondition, MemberInfo!.MemberType);
                DeterminePropertyName();
                DetermineIgnoreCondition(ignoreCondition);

                JsonNumberHandling? propertyNumberHandling = null;
                JsonNumberHandlingAttribute? attribute = GetAttribute<JsonNumberHandlingAttribute>(MemberInfo);
                if (attribute != null)
                {
                    propertyNumberHandling = attribute.Handling;
                }
                DetermineNumberHandlingForProperty(propertyNumberHandling ?? parentTypeNumberHandling);
            }
        }

        private void DeterminePropertyName()
        {
            if (MemberInfo == null)
            {
                return;
            }

            string? jsonPropertyName;
            JsonPropertyNameAttribute? nameAttribute = GetAttribute<JsonPropertyNameAttribute>(MemberInfo);

            if (nameAttribute == null)
            {
                jsonPropertyName = null;
            }
            else
            {
                jsonPropertyName = nameAttribute.Name;
                if (jsonPropertyName == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(DeclaringType, this);
                }
            }

            DeterminePropertyName(MemberInfo.Name, jsonPropertyName);
        }

        internal void DeterminePropertyName(string clrPropertyName, string? jsonPropertyName)
        {
            ClrName = clrPropertyName;

            Debug.Assert(Options != null);

            if (jsonPropertyName != null)
            {
                NameAsString = jsonPropertyName;
            }
            else if (Options.PropertyNamingPolicy != null)
            {
                Debug.Assert(clrPropertyName != null);
                NameAsString = Options.PropertyNamingPolicy.ConvertName(clrPropertyName);
                if (NameAsString == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(DeclaringType, this);
                }
            }
            else
            {
                NameAsString = clrPropertyName;
            }

            Debug.Assert(NameAsString != null);
            NameAsUtf8Bytes = Encoding.UTF8.GetBytes(NameAsString);
            EscapedNameSection = JsonHelpers.GetEscapedPropertyNameSection(NameAsUtf8Bytes, Options.Encoder);
        }

        internal void DetermineSerializationCapabilities(JsonIgnoreCondition? ignoreCondition, MemberTypes memberType)
        {
            Debug.Assert(memberType == MemberTypes.Property || memberType == MemberTypes.Field);

            if ((ClassType & (ClassType.Enumerable | ClassType.Dictionary)) == 0)
            {
                Debug.Assert(ignoreCondition != JsonIgnoreCondition.Always);

                // Three possible values for ignoreCondition:
                // null = JsonIgnore was not placed on this property, global IgnoreReadOnlyProperties/Fields wins
                // WhenNull = only ignore when null, global IgnoreReadOnlyProperties/Fields loses
                // Never = never ignore (always include), global IgnoreReadOnlyProperties/Fields loses
                bool serializeReadOnlyProperty = ignoreCondition != null || (memberType == MemberTypes.Property
                    ? !Options.IgnoreReadOnlyProperties
                    : !Options.IgnoreReadOnlyFields);

                // We serialize if there is a getter + not ignoring readonly properties.
                ShouldSerialize = HasGetter && (HasSetter || serializeReadOnlyProperty);

                // We deserialize if there is a setter.
                ShouldDeserialize = HasSetter;
            }
            else
            {
                if (HasGetter)
                {
                    Debug.Assert(ConverterBase != null);

                    ShouldSerialize = true;

                    if (HasSetter)
                    {
                        ShouldDeserialize = true;
                    }
                }
            }
        }

        internal void DetermineIgnoreCondition(JsonIgnoreCondition? ignoreCondition)
        {
            if (ignoreCondition != null)
            {
                // This is not true for CodeGen scenarios since we do not cache this as of yet.
                // Debug.Assert(MemberInfo != null);
                Debug.Assert(ignoreCondition != JsonIgnoreCondition.Always);

                if (ignoreCondition == JsonIgnoreCondition.WhenWritingDefault)
                {
                    IgnoreDefaultValuesOnWrite = true;
                }
                else if (ignoreCondition == JsonIgnoreCondition.WhenWritingNull)
                {
                    if (PropertyTypeCanBeNull)
                    {
                        IgnoreDefaultValuesOnWrite = true;
                    }
                    else
                    {
                        ThrowHelper.ThrowInvalidOperationException_IgnoreConditionOnValueTypeInvalid(ClrName!, DeclaringType);
                    }
                }
            }
#pragma warning disable CS0618 // IgnoreNullValues is obsolete
            else if (Options.IgnoreNullValues)
            {
                Debug.Assert(Options.DefaultIgnoreCondition == JsonIgnoreCondition.Never);
                if (PropertyTypeCanBeNull)
                {
                    IgnoreDefaultValuesOnRead = true;
                    IgnoreDefaultValuesOnWrite = true;
                }
            }
            else if (Options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
            {
                Debug.Assert(!Options.IgnoreNullValues);
                if (PropertyTypeCanBeNull)
                {
                    IgnoreDefaultValuesOnWrite = true;
                }
            }
            else if (Options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingDefault)
            {
                Debug.Assert(!Options.IgnoreNullValues);
                IgnoreDefaultValuesOnWrite = true;
            }
#pragma warning restore CS0618 // IgnoreNullValues is obsolete
        }

        internal void DetermineNumberHandlingForClassInfo(JsonNumberHandling? numberHandling)
        {
            if (numberHandling != null && !ConverterBase.IsInternalConverter)
            {
                ThrowHelper.ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(this);
            }

            if (NumberHandingIsApplicable())
            {
                // This logic is to honor JsonNumberHandlingAttribute placed on
                // custom collections e.g. public class MyNumberList : List<int>.

                // Priority 1: Get handling from the type (parent type in this case is the type itself).
                NumberHandling = numberHandling;

                // Priority 2: Get handling from JsonSerializerOptions instance.
                if (!NumberHandling.HasValue && Options.NumberHandling != JsonNumberHandling.Strict)
                {
                    NumberHandling = Options.NumberHandling;
                }
            }
        }

        internal void DetermineNumberHandlingForProperty(JsonNumberHandling? propertyNumberHandling)
        {
            bool numberHandlingIsApplicable = NumberHandingIsApplicable();

            if (numberHandlingIsApplicable)
            {
                // Priority 1: Get handling from attribute on property/field, or its parent class type.
                JsonNumberHandling? handling = propertyNumberHandling;

                // Priority 2: Get handling from JsonSerializerOptions instance.
                if (!handling.HasValue && Options.NumberHandling != JsonNumberHandling.Strict)
                {
                    handling = Options.NumberHandling;
                }

                NumberHandling = handling;
            }
            else if (propertyNumberHandling.HasValue)
            {
                ThrowHelper.ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(this);
            }
        }

        private bool NumberHandingIsApplicable()
        {
            if (ConverterBase.IsInternalConverterForNumberType)
            {
                return true;
            }

            if (!ConverterBase.IsInternalConverter ||
                ((ClassType.Enumerable | ClassType.Dictionary) & ClassType) == 0)
            {
                return false;
            }

            Type? elementType = ConverterBase.ElementType;
            Debug.Assert(elementType != null);

            elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

            if (elementType == typeof(byte) ||
                elementType == typeof(decimal) ||
                elementType == typeof(double) ||
                elementType == typeof(short) ||
                elementType == typeof(int) ||
                elementType == typeof(long) ||
                elementType == typeof(sbyte) ||
                elementType == typeof(float) ||
                elementType == typeof(ushort) ||
                elementType == typeof(uint) ||
                elementType == typeof(ulong) ||
                elementType == JsonClassInfo.ObjectType)
            {
                return true;
            }

            return false;
        }

        internal static TAttribute? GetAttribute<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute
        {
            return (TAttribute?)memberInfo.GetCustomAttribute(typeof(TAttribute), inherit: false);
        }

        internal abstract bool GetMemberAndWriteJson(object obj, ref WriteStack state, Utf8JsonWriter writer);
        internal abstract bool GetMemberAndWriteJsonExtensionData(object obj, ref WriteStack state, Utf8JsonWriter writer);

        internal abstract object? GetValueAsObject(object obj);

        internal bool HasGetter { get; set; }
        internal bool HasSetter { get; set; }

        internal virtual void Initialize(
            Type parentClassType,
            Type declaredPropertyType,
            Type? runtimePropertyType,
            ClassType runtimeClassType,
            MemberInfo? memberInfo,
            JsonConverter converter,
            JsonIgnoreCondition? ignoreCondition,
            JsonNumberHandling? parentTypeNumberHandling,
            JsonSerializerOptions options)
        {
            Debug.Assert(converter != null);

            ClrName = memberInfo?.Name;
            DeclaringType = parentClassType;
            DeclaredPropertyType = declaredPropertyType;
            RuntimePropertyType = runtimePropertyType;
            ClassType = runtimeClassType;
            MemberInfo = memberInfo;
            ConverterBase = converter;
            Options = options;
        }

        internal abstract void SourceGenInitializePropertyInfoForClassInfo(
            Type declaredPropertyType,
            Type? runtimePropertyType,
            ClassType runtimeClassType,
            JsonClassInfo runtimeClassInfo,
            JsonConverter converter,
            JsonNumberHandling? parentTypeNumberHandling,
            JsonSerializerOptions options);

        internal bool IgnoreDefaultValuesOnRead { get; private set; }
        internal bool IgnoreDefaultValuesOnWrite { get; private set; }

        /// <summary>
        /// True if the corresponding cref="JsonClassInfo.PropertyInfoForClassInfo"/> is this instance.
        /// </summary>
        internal bool IsForClassInfo { get; set; }

        internal string? ClrName { get; private set; }

        // There are 3 copies of the property name:
        // 1) NameAsString. The unescaped property name.
        // 2) NameAsUtf8Bytes. The Utf8 version of NameAsString. Used during during deserialization for property lookup.
        // 3) EscapedNameSection. The escaped verson of NameAsUtf8Bytes plus the wrapping quotes and a trailing colon. Used during serialization.

        /// <summary>
        /// The unescaped name of the property.
        /// Is either the actual CLR property name,
        /// the value specified in JsonPropertyNameAttribute,
        /// or the value returned from PropertyNamingPolicy(clrPropertyName).
        /// </summary>
        public string NameAsString { get; set; } = null!;

        /// <summary>
        /// Utf8 version of NameAsString.
        /// </summary>
        public byte[] NameAsUtf8Bytes { get; set; } = null!;

        /// <summary>
        /// The escaped name passed to the writer.
        /// </summary>
        public byte[] EscapedNameSection { get; set; } = null!;

        /// <summary>
        /// TODO
        /// </summary>
        // Options can be referenced here since all JsonPropertyInfos originate from a JsonClassInfo that is cached on JsonSerializerOptions.
        public JsonSerializerOptions Options { get; set; } = null!; // initialized in Init method

        internal bool ReadJsonAndAddExtensionProperty(object obj, ref ReadStack state, ref Utf8JsonReader reader)
        {
            object propValue = GetValueAsObject(obj)!;

            if (propValue is IDictionary<string, object?> dictionaryObject)
            {
                // Handle case where extension property is System.Object-based.

                if (reader.TokenType == JsonTokenType.Null)
                {
                    // A null JSON value is treated as a null object reference.
                    dictionaryObject[state.Current.JsonPropertyNameAsString!] = null;
                }
                else
                {
                    JsonConverter<object> converter = (JsonConverter<object>)Options.GetConverter(JsonClassInfo.ObjectType)!;
                    Debug.Assert(converter != null);

                    if (!converter.TryRead(ref reader, typeof(JsonElement), Options, ref state, out object? value))
                    {
                        return false;
                    }

                    dictionaryObject[state.Current.JsonPropertyNameAsString!] = value;
                }
            }
            else
            {
                // Handle case where extension property is JsonElement-based.

                Debug.Assert(propValue is IDictionary<string, JsonElement>);
                IDictionary<string, JsonElement> dictionaryJsonElement = (IDictionary<string, JsonElement>)propValue;

                JsonConverter<JsonElement> converter = (JsonConverter<JsonElement>)Options.GetConverter(typeof(JsonElement))!;
                Debug.Assert(converter != null);

                if (!converter.TryRead(ref reader, typeof(JsonElement), Options, ref state, out JsonElement value))
                {
                    return false;
                }

                dictionaryJsonElement[state.Current.JsonPropertyNameAsString!] = value;
            }

            return true;
        }

        internal abstract bool ReadJsonAndSetMember(object obj, ref ReadStack state, ref Utf8JsonReader reader);

        internal abstract bool ReadJsonAsObject(ref ReadStack state, ref Utf8JsonReader reader, out object? value);

        internal bool ReadJsonExtensionDataValue(ref ReadStack state, ref Utf8JsonReader reader, out object? value)
        {
            Debug.Assert(this == state.Current.JsonClassInfo.DataExtensionProperty);

            if (RuntimeClassInfo.ElementType == JsonClassInfo.ObjectType && reader.TokenType == JsonTokenType.Null)
            {
                value = null;
                return true;
            }

            JsonConverter<JsonElement> converter = (JsonConverter<JsonElement>)Options.GetConverter(typeof(JsonElement))!;
            Debug.Assert(converter != null);
            if (!converter.TryRead(ref reader, typeof(JsonElement), Options, ref state, out JsonElement jsonElement))
            {
                // JsonElement is a struct that must be read in full.
                value = null;
                return false;
            }

            value = jsonElement;
            return true;
        }

        /// <summary>
        /// TODO
        /// </summary>
        public Type DeclaringType { get; set; } = null!;

        internal MemberInfo? MemberInfo { get; private set; }

        /// <summary>
        /// TODO
        /// </summary>
        public JsonClassInfo RuntimeClassInfo
        {
            get
            {
                if (_runtimeClassInfo == null)
                {
                    _runtimeClassInfo = Options.GetOrAddClass(RuntimePropertyType!);
                }

                return _runtimeClassInfo;
            }
            set
            {
                // Used with code-gen
                Debug.Assert(_runtimeClassInfo == null);
                _runtimeClassInfo = value;
            }
        }

        internal abstract void SetExtensionDictionaryAsObject(object obj, object? extensionDict);

        /// <summary>
        /// todo
        /// </summary>
        public bool ShouldSerialize { get; internal set; }

        /// <summary>
        /// todo
        /// </summary>
        public bool ShouldDeserialize { get; internal set; }

        internal bool IsIgnored { get; set; }

        //  Whether the property type can be null.
        internal bool PropertyTypeCanBeNull { get; set; }

        /// <summary>
        /// Returns true if the property's converter is external (a user's custom converter)
        /// and the type to convert is not the same as the declared property type (polymorphic).
        /// Used to determine whether to perform additional validation on the value returned by the
        /// converter on deserialization.
        /// </summary>
        internal bool _converterIsExternalAndPolymorphic;

        // Since a converter's TypeToConvert (which is the T value in this type) can be different than
        // the property's type, we track that and whether the property type can be null.
        internal bool _propertyTypeEqualsTypeToConvert;
    }
}
