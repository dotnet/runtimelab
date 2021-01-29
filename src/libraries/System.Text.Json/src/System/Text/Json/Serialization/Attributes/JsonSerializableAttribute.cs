// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed in a module and the System.Text.Json.SourceGeneration generator is enabled, the generator will
    /// attempt to generate source code to help optimize the start-up and throughput performance when serializing and
    /// deserializing instances of the specified type and types in its object graph.
    /// </summary>
    /// <remarks>
    /// Must take into account that type discovery using this attribute is at compile time.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class JsonSerializableAttribute : JsonAttribute
    {
        /// <summary>
        /// Indicates whether the specified type might be the runtime type of an object instance which was declared as
        /// a different type (polymorphic serialization), or might be passed in dynamically to the serializer.
        /// </summary>
        public bool CanBeDynamic { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="JsonSerializableAttribute"/> with the specified type.
        /// </summary>
        /// <param name="type">The Type of the property.</param>
        public JsonSerializableAttribute(Type type) { }
    }
}
