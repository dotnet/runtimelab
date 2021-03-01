// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class JsonObjectInfo<T> : JsonTypeInfo<T>
    {
        private HashSet<string>? _ignoredMembers;

        private JsonNumberHandling? _numberHandling;

        internal JsonObjectInfo(Type type, JsonSerializerOptions options) :
            base(type, options, ClassType.Object)
        { }

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="createObjectFunc"></param>
        /// <param name="numberHandling"></param>
        /// <param name="options"></param>
        public JsonObjectInfo(
            ConstructorDelegate? createObjectFunc,
            JsonNumberHandling? numberHandling,
            JsonSerializerOptions options) : base(typeof(T), options, ClassType.Object)
        {
            CreateObject = createObjectFunc;
            _numberHandling = numberHandling;
            JsonConverter converter = new ObjectDefaultConverter<T>();
            ConverterBase = converter;
            PropertyInfoForClassInfo = SourceGenCreatePropertyInfoForClassInfo(Type, Type, runtimeClassInfo: this, converter, numberHandling, Options);
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="clrPropertyName"></param>
        /// <param name="memberType"></param>
        /// <param name="declaringType"></param>
        /// <param name="classInfo"></param>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        /// <param name="jsonPropertyName"></param>
        /// <param name="ignoreCondition"></param>
        /// <param name="numberHandling"></param>
        public JsonPropertyInfo<TProperty> AddProperty<TProperty>(
            string clrPropertyName,
            MemberTypes memberType,
            Type declaringType,
            JsonTypeInfo<TProperty> classInfo,
            Func<object, TProperty>? getter = null,
            Action<object, TProperty>? setter = null,
            string? jsonPropertyName = null,
            JsonIgnoreCondition? ignoreCondition = null,
            JsonNumberHandling? numberHandling = null)
        {
            if (clrPropertyName == null)
            {
                throw new ArgumentNullException(nameof(clrPropertyName));
            }

            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }

            if (memberType != MemberTypes.Property && memberType != MemberTypes.Field)
            {
                throw new ArgumentException("Member has to be a property or field.", nameof(memberType));
            }

            if (classInfo == null)
            {
                throw new ArgumentNullException(nameof(classInfo));
            }

            JsonPropertyInfo<TProperty> jsonPropertyInfo;
            if (ignoreCondition == JsonIgnoreCondition.Always)
            {
                jsonPropertyInfo = JsonPropertyInfo.CreateIgnoredPropertyPlaceholder<TProperty>(clrPropertyName, Options, jsonPropertyName);
            }
            else
            {
                jsonPropertyInfo = new JsonPropertyInfo<TProperty>();
                jsonPropertyInfo.Options = Options;

                if (getter != null)
                {
                    jsonPropertyInfo.Get = getter;
                    jsonPropertyInfo.HasGetter = true;
                }

                if (setter != null)
                {
                    jsonPropertyInfo.Set = setter;
                    jsonPropertyInfo.HasSetter = true;
                }

                jsonPropertyInfo.Converter = (JsonConverter<TProperty>)classInfo.ConverterBase ??
                    throw new InvalidOperationException($"'JsonClassInfo' '{classInfo}' cannot return a 'null' converter.");
                jsonPropertyInfo.RuntimeClassInfo = classInfo;
                jsonPropertyInfo.ClassType = jsonPropertyInfo.Converter!.ClassType;
                jsonPropertyInfo.DeclaredPropertyType = typeof(TProperty);
                jsonPropertyInfo.RuntimePropertyType = typeof(TProperty);
                jsonPropertyInfo.ParentClassType = declaringType;

                // jsonPropertyInfo._converterIsExternalAndPolymorphic = false;
                jsonPropertyInfo.PropertyTypeCanBeNull = typeof(TProperty).CanBeNull();
                jsonPropertyInfo._propertyTypeEqualsTypeToConvert = true;

                jsonPropertyInfo.DeterminePropertyName(clrPropertyName, jsonPropertyName);
                jsonPropertyInfo.DetermineIgnoreCondition(ignoreCondition);
                jsonPropertyInfo.DetermineNumberHandlingForProperty(parentTypeNumberHandling: _numberHandling, propertyNumberHandling: numberHandling);
                jsonPropertyInfo.DetermineSerializationCapabilities(ignoreCondition, memberType);
            }

            CacheMember(jsonPropertyInfo, PropertyCache!, ref _ignoredMembers);
            return jsonPropertyInfo;
        }

        /// <summary>
        /// Changes here should be reflected in <see cref="JsonClassInfo.CacheMember"/>
        /// </summary>
        /// <param name="jsonPropertyInfo"></param>
        /// <param name="cache"></param>
        /// <param name="ignoredMembers"></param>
        private void CacheMember(
            JsonPropertyInfo jsonPropertyInfo,
            Dictionary<string, JsonPropertyInfo> cache,
            ref HashSet<string>? ignoredMembers)
        {
            Debug.Assert(jsonPropertyInfo.NameAsString != null);

            string memberName = jsonPropertyInfo.ClrName!;

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
                    ignoredMembers?.Contains(memberName) != true)
                {
                    // We throw if we have two public properties that have the same JSON property name, and neither have been ignored.
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(Type, jsonPropertyInfo);
                }
                // Ignore the current property.
            }

            if (jsonPropertyInfo.IsIgnored)
            {
                (ignoredMembers ??= new HashSet<string>()).Add(memberName);
            }
        }

        /// <summary>
        /// todo
        /// </summary>
        public override void CompleteInitialization(bool canBeDynamic)
        {
            _ignoredMembers = null;
            CompleteObjectInitialization();

            if (canBeDynamic)
            {
                Options.AddJsonClassInfoToCompleteInitialization(this);
            }
        }
    }
}
