// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class JsonTypeInfo<T> : JsonTypeInfo
    {
        internal JsonTypeInfo(Type type, JsonSerializerOptions options, ClassType classType) :
            base(type, options, classType)
        { }

        /// <summary>
        /// todo
        /// </summary>
        // TODO: remove this and perform this action based on Create methods/ctors on derived classes.
        public void RegisterToOptions()
        {
            //_isInitialized = true;
            Options.AddJsonTypeInfoToCompleteInitialization(this);
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public delegate void SerializeObjectDelegate(Utf8JsonWriter writer, T value, JsonSerializerOptions options);

        /// <summary>
        /// TODO
        /// </summary>
        public SerializeObjectDelegate? SerializeObject { get; internal set; }
    }
}
