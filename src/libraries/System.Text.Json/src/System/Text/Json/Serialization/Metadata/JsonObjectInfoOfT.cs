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
        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="options"></param>
        public JsonObjectInfo(JsonSerializerOptions options) : base(typeof(T), options, ClassType.Object)
        {
            JsonConverter converter = new ObjectSourceGenConverter<T>();
            ConverterBase = converter;
            PropertyInfoForTypeInfo = SourceGenCreatePropertyInfoForTypeInfo<T>(Type, runtimeTypeInfo: this, converter, Options);
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="createObjectFunc"></param>
        /// <param name="serializeObjectFunc"></param>
        /// <param name="propInitFunc"></param>
        /// <param name="numberHandling"></param>
        public void Initialize(
            ConstructorDelegate? createObjectFunc,
            SerializeObjectDelegate? serializeObjectFunc,
            Func<JsonSerializerContext, JsonPropertyInfo[]> propInitFunc,
            JsonNumberHandling? numberHandling)
        {
            if (propInitFunc == null)
            {
                throw new ArgumentNullException(nameof(propInitFunc));
            }

            CreateObject = createObjectFunc;
            SerializeObject = serializeObjectFunc;
            NumberHandling = numberHandling;

            if (SerializeObject != null)
            {
                ObjectFastPathOnWrite = Options.Encoder == null &&
                    !Options.IgnoreReadOnlyProperties &&
                    !Options.IgnoreReadOnlyFields &&
                    Options.NumberHandling == JsonNumberHandling.Strict &&
                    (Options.PropertyNamingPolicy == null || Options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase) &&
                    Options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.None;
            }

            PropInitFunc = propInitFunc;
        }
    }
}
