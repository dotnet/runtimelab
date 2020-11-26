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
        // TODO: should we verify these are set only once?

        public string CompilableName { get; set; }

        public string FriendlyName { get; set; }

        public Type Type { get; set; }

        public ClassType ClassType { get; set; }

        public CollectionType CollectionType { get; set; }

        public TypeMetadata? CollectionKeyTypeMetadata { get; set; }

        public TypeMetadata? CollectionValueTypeMetadata { get; set; }

        public List<PropertyMetadata>? PropertiesMetadata { get; set; }

        // TODO: perhaps this can be consolidated to PropertiesMetadata above, even when field support is added.
        public List<PropertyMetadata>? FieldsMetadata { get; set; }

        public bool IsValueType { get; set; }
    }
}
