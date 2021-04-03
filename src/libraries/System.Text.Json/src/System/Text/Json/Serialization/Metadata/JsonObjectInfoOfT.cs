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
            ConstructorDelegate? createObjectFunc,
            JsonSerializerOptions options) : base(typeof(T), options, ClassType.Object)
        {
            CreateObject = createObjectFunc;
            JsonConverter converter = new ObjectDefaultConverter<T>();
            ConverterBase = converter;
            PropertyInfoForTypeInfo = SourceGenCreatePropertyInfoForTypeInfo<T>(Type, runtimeTypeInfo: this, converter, Options);
        }

        /// <summary>
        /// todo
        /// </summary>
        // TODO: leverage this method for property ordering on serialization.
        public void AddProperty(JsonPropertyInfo jsonPropertyInfo)
        {
            if (!JsonHelpers.TryAdd(PropertyCache!, jsonPropertyInfo.NameAsString, jsonPropertyInfo))
            {
                ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(Type, jsonPropertyInfo);
            }
        }

        /// <summary>
        /// todo
        /// </summary>
        public void CompleteInitialization()
        {
            CompleteObjectInitializationInternal();
        }
    }
}
