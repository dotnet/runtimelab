// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class JsonValueInfo<T> : JsonTypeInfo<T>
    {
        /// <summary>
        /// todo
        /// </summary>
        /// <param name="converter"></param>
        /// <param name="options"></param>
        public JsonValueInfo(
            JsonConverter<T> converter,
            JsonSerializerOptions options) : base(typeof(T), options, ClassType.Value)
        {
            ConverterBase = converter;
            PropertyInfoForClassInfo = CreatePropertyInfoForClassInfo(Type, Type, converter, Options);
        }

        /// <summary>
        /// todo
        /// </summary>
        public override void CompleteInitialization(bool canBeDynamic)
        {
            _isInitialized = true;

            if (canBeDynamic)
            {
                Options.AddJsonClassInfo(this);
            }
        }
    }
}
