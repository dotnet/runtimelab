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
        /// <param name="properties"></param>
        /// <param name="numberHandling"></param>
        public void Initialize(
            ConstructorDelegate? createObjectFunc,
            SerializeObjectDelegate? serializeObjectFunc,
            JsonPropertyInfo[] properties,
            JsonNumberHandling? numberHandling)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            CreateObject = createObjectFunc;
            SerializeObject = serializeObjectFunc;
            NumberHandling = numberHandling;

            for (int i = 0; i < properties.Length; i++)
            {
                JsonPropertyInfo jsonPropertyInfo = properties[i];
                if (jsonPropertyInfo == null)
                {
                    throw new InvalidOperationException("Cannot provide null JsonPropertyInfo.");
                }

                if (!JsonHelpers.TryAdd(PropertyCache!, jsonPropertyInfo.NameAsString, jsonPropertyInfo))
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(Type, jsonPropertyInfo);
                }
            }

            if (SerializeObject != null)
            {
                ObjectFastPathOnWrite = Options.Encoder == null &&
                    !Options.IgnoreReadOnlyProperties &&
                    !Options.IgnoreReadOnlyFields &&
                    Options.NumberHandling == JsonNumberHandling.Strict &&
                    (Options.PropertyNamingPolicy == null || Options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase) &&
                    Options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.None;
            }

            CompleteObjectInitialization();
        }
    }
}
