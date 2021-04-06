// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace System.Text.Json.Serialization.SourceGeneration
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class JsonTypeInfo<T> : JsonTypeInfo
    {
        internal JsonTypeInfo(Type type, JsonSerializerOptions options, ClassType classType) :
            base(type, options, classType)
        { }

        internal JsonTypeInfo(Type type, ClassType classType) :
            base(type, classType)
        { }

        /// <summary>
        /// todo
        /// </summary>
        // TODO: remove this and perform this action based on Create methods/ctors on derived classes.
        public void RegisterToOptions()
        {
            Options.AddJsonTypeInfoToCompleteInitialization(this);
        }

        /// <summary>
        /// TODO
        /// </summary>
        public MetadataServices.SerializeObjectDelegate<T>? SerializeObject { get; internal set; }
    }
}
