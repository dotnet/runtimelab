// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;

namespace System.Text.Json.Serialization.SourceGeneration
{
    /// <summary>
    /// todo
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract partial class JsonSerializerContext
    {
        internal JsonSerializerOptions? _options;

        /// <summary>
        /// Gets the runtime options of the context. Can be modified until the first serialization or deserialization
        /// call using the context instances.
        /// </summary>
        public JsonSerializerOptions? Options
        {
            get => _options;
            internal set => _options = value;
        }
        /// <summary>
        /// todo
        /// </summary>
        protected JsonSerializerContext()
        {
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="options"></param>
        protected JsonSerializerContext(JsonSerializerOptions options)
        {
            options.SetContext(this);
            Debug.Assert(_options == options);
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="type"></param>
        public virtual JsonTypeInfo GetTypeInfo(Type type) => throw new NotImplementedException("The derived context instance did not provide a valid implementation for 'GetTypeInfo'.");
    }
}
