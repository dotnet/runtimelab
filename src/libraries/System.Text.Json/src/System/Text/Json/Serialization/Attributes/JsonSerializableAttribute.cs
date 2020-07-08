// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed on a type, will source generate de/serialization for the specified type and it's descendants.
    /// </summary>
    /// <remarks>
    /// Must take into account that type discovery using this attribute is at compile time using Source Generators.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class JsonSerializableAttribute : JsonAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="JsonSerializableAttribute"/>.
        /// </summary>
        public JsonSerializableAttribute() { }

        /// <summary>
        /// Initializes a new instance of <see cref="JsonSerializableAttribute"/> with the specified type.
        /// </summary>
        /// <param name="type">The Type of the property.</param>
        public JsonSerializableAttribute(Type type) { }
    }
}
