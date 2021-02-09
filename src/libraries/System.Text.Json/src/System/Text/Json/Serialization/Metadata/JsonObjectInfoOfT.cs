// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class JsonObjectInfo<T> : JsonTypeInfo<T>
    {
        internal JsonObjectInfo(Type type, JsonSerializerOptions options) :
            base(type, options, ClassType.Object)
        { }

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="createObjectFunc"></param>
        /// <param name="options"></param>
        public JsonObjectInfo(
            ConstructorDelegate createObjectFunc,
            JsonSerializerOptions options) : base(typeof(T), options, ClassType.Object)
        {
            if (createObjectFunc == null)
            {
                throw new ArgumentNullException(nameof(createObjectFunc));
            }

            CreateObject = createObjectFunc;

            JsonConverter converter = new ObjectDefaultConverter<T>();

            ConverterBase = converter;
            PropertyInfoForClassInfo = CreatePropertyInfoForClassInfo(Type, Type, converter, Options);
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertyName"></param>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        /// <param name="classInfo"></param>
        public JsonPropertyInfo<TProperty> AddProperty<TProperty>(
            string propertyName,
            Func<object, TProperty>? getter,
            Action<object, TProperty>? setter,
            JsonTypeInfo<TProperty> classInfo)
        {
            var jsonPropertyInfo = new JsonPropertyInfo<TProperty>();
            if (getter != null)
            {
                jsonPropertyInfo.Get = getter;
                jsonPropertyInfo.ShouldSerialize = true;
            }

            if (setter != null)
            {
                jsonPropertyInfo.Set = setter;
                jsonPropertyInfo.ShouldDeserialize = true;
            }

            if (classInfo == null)
            {
                JsonConverter? converter = Options.DetermineConverter(typeof(T), typeof(TProperty), memberInfo: null);
                jsonPropertyInfo.Converter = (JsonConverter<TProperty>)converter! ?? throw new NotSupportedException("No registered converter for the type");
            }
            else
            {
                jsonPropertyInfo.Converter = (JsonConverter<TProperty>)classInfo.ConverterBase;
                jsonPropertyInfo.RuntimeClassInfo = classInfo;
            }

            jsonPropertyInfo.ClassType = jsonPropertyInfo.Converter!.ClassType;

            jsonPropertyInfo.NameAsString = propertyName;
            jsonPropertyInfo.NameAsUtf8Bytes = Encoding.UTF8.GetBytes(propertyName);
            jsonPropertyInfo.EscapedNameSection = JsonHelpers.GetEscapedPropertyNameSection(jsonPropertyInfo.NameAsUtf8Bytes, Options.Encoder);

            jsonPropertyInfo.DeclaredPropertyType = typeof(TProperty);
            jsonPropertyInfo.Options = Options;
            jsonPropertyInfo.RuntimePropertyType = typeof(TProperty);

            PropertyCache!.Add(jsonPropertyInfo.NameAsString, jsonPropertyInfo);

            return jsonPropertyInfo;
        }

        /// <summary>
        /// todo
        /// </summary>
        public void CompleteInitialization(bool canBeDynamic)
        {
            CompleteObjectInitialization();

            if (canBeDynamic)
            {
                Options.AddJsonClassInfo(this);
            }
        }
    }
}
