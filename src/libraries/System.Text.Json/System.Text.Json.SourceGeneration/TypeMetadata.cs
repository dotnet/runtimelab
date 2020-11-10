// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json.SourceGeneration
{
    [DebuggerDisplay("Type={Type}, ClassType={ClassType}")]
    internal class TypeMetadata
    {
        public string CompilableName { get; init; }

        public string FriendlyName { get; init; }

        public Type Type { get; init; }

        public ClassType ClassType { get; init; }

        public CollectionType CollectionType { get; init; }

        public TypeMetadata? CollectionKeyTypeMetadata { get; init; }

        public TypeMetadata? CollectionValueTypeMetadata { get; init; }

        public List<PropertyMetadata>? PropertiesMetadata { get; init; }

        // TODO: perhaps this can be consolidated to PropertiesMetadata above, even when field support is added.
        public List<PropertyMetadata>? FieldsMetadata { get; init; }

        public bool IsValueType { get; init; }
    }
}
