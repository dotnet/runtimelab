// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization.SourceGeneration
{
    /// <summary>
    /// TODO
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial class MetadataServices
    {
        /// <summary>
        /// todo
        /// </summary>
        /// <returns></returns>
        public delegate object? ConstructorDelegate();

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public delegate void SerializeObjectDelegate<T>(Utf8JsonWriter writer, T value, JsonSerializerOptions options);

        /// <summary>
        /// TODO
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static JsonPropertyInfo CreatePropertyInfo<T>(
            JsonSerializerOptions options,
            MemberTypes memberType,
            Type declaringType,
            JsonTypeInfo<T> typeInfo,
            JsonConverter converter,
            Func<object, T> getter,
            Action<object, T> setter,
            JsonIgnoreCondition? ignoreCondition,
            JsonNumberHandling? numberHandling,
            string clrPropertyName,
            string jsonPropertyName,
            byte[] nameAsUtf8Bytes,
            byte[] nameSection)
        {
            JsonPropertyInfo<T> jsonPropertyInfo = new();
            jsonPropertyInfo.Options = options;

            // Property name settings.
            // TODO: consider whether we need to examine Options.Encoder here as well.
            if (nameAsUtf8Bytes != null && options.PropertyNamingPolicy == null)
            {
                jsonPropertyInfo.NameAsString = jsonPropertyName ?? clrPropertyName;
                jsonPropertyInfo.NameAsUtf8Bytes = nameAsUtf8Bytes;
                jsonPropertyInfo.EscapedNameSection = nameSection;
            }
            else
            {
                jsonPropertyInfo.NameAsString = jsonPropertyName!
                    ?? options.PropertyNamingPolicy?.ConvertName(clrPropertyName!)
                    ?? (options.PropertyNamingPolicy == null
                            ? null!
                            : throw new InvalidOperationException("TODO: PropertyNamingPolicy cannot return null."));
                // NameAsUtf8Bytes and EscapedNameSection will be set in CompleteInitialization() below.
            }
            if (ignoreCondition != JsonIgnoreCondition.Always)
            {
                jsonPropertyInfo.Get = getter;
                jsonPropertyInfo.Set = setter;
                jsonPropertyInfo.ConverterBase = converter ?? throw new NotSupportedException("TODO: need custom converter here?");
                jsonPropertyInfo.RuntimeTypeInfo = typeInfo;
                jsonPropertyInfo.DeclaredPropertyType = typeof(T);
                jsonPropertyInfo.DeclaringType = declaringType;
                jsonPropertyInfo.IgnoreCondition = ignoreCondition;
                jsonPropertyInfo.NumberHandling = numberHandling;
                jsonPropertyInfo.MemberType = memberType;
            }
            jsonPropertyInfo.CompleteInitialization();
            return jsonPropertyInfo;
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static JsonTypeInfo<T> CreateObjectInfo<T>() => new JsonObjectInfo<T>();

        /// <summary>
        /// TODO
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="info"></param>
        /// <param name="options"></param>
        /// <param name="createObjectFunc"></param>
        /// <param name="serializeObjectFunc"></param>
        /// <param name="propInitFunc"></param>
        /// <param name="numberHandling"></param>
        public static void InitializeObjectInfo<T>(
            JsonTypeInfo<T> info,
            JsonSerializerOptions options,
            ConstructorDelegate? createObjectFunc,
            SerializeObjectDelegate<T>? serializeObjectFunc,
            Func<JsonSerializerContext, JsonPropertyInfo[]> propInitFunc,
            JsonNumberHandling? numberHandling)
        {
            if (info.ClassType != ClassType.Object)
            {
                throw new ArgumentException("The value must represent an object class type.", nameof(info));
            }

            info.Options = options ?? throw new ArgumentNullException(nameof(options));

            JsonConverter converter = new ObjectSourceGenConverter<T>();
            info.Converter = converter;
            info.PropertyInfoForTypeInfo = JsonTypeInfo.SourceGenCreatePropertyInfoForTypeInfo<T>(info.Type, runtimeTypeInfo: info, converter, options);

            info.CreateObject = createObjectFunc;
            info.SerializeObject = serializeObjectFunc;
            info.NumberHandling = numberHandling;

            if (info.SerializeObject != null)
            {
                info.ObjectFastPathOnWrite = options.Encoder == null &&
                    !options.IgnoreReadOnlyProperties &&
                    !options.IgnoreReadOnlyFields &&
                    options.NumberHandling == JsonNumberHandling.Strict &&
                    (options.PropertyNamingPolicy == null || options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase) &&
                    options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.None;
            }

            info.PropInitFunc = propInitFunc ?? throw new ArgumentNullException(nameof(propInitFunc));
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static JsonTypeInfo<T> CreateValueInfo<T>(JsonSerializerOptions options, JsonConverter converter)
            => new JsonValueInfo<T>(options, converter);
    }
}
