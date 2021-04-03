// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// todo
    /// </summary>
    public abstract partial class JsonSerializerContext
    {
        internal readonly JsonSerializerOptions _options;

        /// <summary>
        /// Gets the runtime options of the context. Can be modified until the first serialization or deserialization
        /// call using the context instances.
        /// </summary>
        public JsonSerializerOptions Options => _options;

        /// <summary>
        /// todo
        /// </summary>
        protected JsonSerializerContext()
        {
            _options = new JsonSerializerOptions(JsonSerializerOptions.DefaultSourceGenOptions, this);
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="options"></param>
        protected JsonSerializerContext(JsonSerializerOptions options)
        {
            _options = new JsonSerializerOptions(options ?? throw new ArgumentNullException(nameof(options)), this);
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="type"></param>
        public virtual JsonClassInfo GetJsonClassInfo(Type type) => throw new NotImplementedException();

        internal IEnumerable<JsonClassInfo> GetAllJsonClassInfos() => GetAllJsonClassInfosImpl();

        /// <summary>
        /// Gets all the <see cref="JsonClassInfo"/> instances defined in this context.
        /// </summary>
        protected virtual IEnumerable<JsonClassInfo> GetAllJsonClassInfosImpl() => throw new NotImplementedException();
    }
}
