// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class EnumConverterFactory : JsonConverterFactory
    {
        public EnumConverterFactory()
        {
        }

        public override bool CanConvert(Type type)
        {
            return type.IsEnum;
        }

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            Create(type, options);

        internal static JsonConverter Create(Type enumType, JsonSerializerOptions serializerOptions)
        {
            return (JsonConverter)Activator.CreateInstance(
                GetEnumConverterType(enumType),
                new object[] { serializerOptions })!;
        }

        internal static JsonConverter Create(Type enumType, EnumConverterOptions converterOptions, JsonNamingPolicy? namingPolicy, JsonSerializerOptions serializerOptions)
        {
            return (JsonConverter)Activator.CreateInstance(
                GetEnumConverterType(enumType),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                new object?[] { converterOptions, namingPolicy, serializerOptions },
                culture: null)!;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2055:MakeGenericType",
            Justification = "'EnumConverter<T> where T : struct' implies 'T : new()', so the trimmer is warning calling MakeGenericType here because enumType's constructors are not annotated. " +
            "But EnumConverter doesn't call new T(), so this is safe.")]
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
        private static Type GetEnumConverterType(Type enumType) => typeof(EnumConverter<>).MakeGenericType(enumType);
    }
}
