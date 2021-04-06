// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization.SourceGeneration
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class JsonCollectionInfo<T> : JsonTypeInfo<T>
    {
        public JsonCollectionInfo(
            JsonSerializerOptions options,
            MetadataServices.ConstructorDelegate createObjectFunc,
            JsonConverter<T> converter,
            JsonTypeInfo elementInfo,
            JsonNumberHandling? numberHandling) : base(typeof(T), options, ClassType.Enumerable)
        {
            Converter = converter;
            ElementType = converter.ElementType;
            ElementTypeInfo = elementInfo;
            CreateObject = createObjectFunc;
            NumberHandling = numberHandling;
            PropertyInfoForTypeInfo = SourceGenCreatePropertyInfoForTypeInfo<T>(Type, runtimeTypeInfo: this, converter, Options);
        }

        public JsonCollectionInfo(
            JsonSerializerOptions options,
            MetadataServices.ConstructorDelegate createObjectFunc,
            JsonConverter<T> converter,
            JsonTypeInfo keyInfo,
            JsonTypeInfo elementInfo,
            JsonNumberHandling? numberHandling) : base(typeof(T), options, ClassType.Dictionary)
        {
            Converter = converter;
            KeyType = converter.KeyType;
            KeyTypeInfo = keyInfo;
            ElementType = converter.ElementType;
            ElementTypeInfo = elementInfo;
            CreateObject = createObjectFunc;
            NumberHandling = numberHandling;
            PropertyInfoForTypeInfo = SourceGenCreatePropertyInfoForTypeInfo<T>(Type, runtimeTypeInfo: this, converter, Options);
        }
    }
}
