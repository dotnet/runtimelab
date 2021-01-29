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
        private bool _hasBeenInitialized;

        public string CompilableName { get; private set; }

        public string FriendlyName { get; private set; }

        public Type Type { get; private set; }

        public ClassType ClassType { get; private set; }

        public bool IsValueType { get; private set; }

        public bool CanBeDynamic { get; private set; }

        public CollectionType CollectionType { get; private set; }

        public TypeMetadata? CollectionKeyTypeMetadata { get; private set; }

        public TypeMetadata? CollectionValueTypeMetadata { get; private set; }

        public List<PropertyMetadata>? PropertiesMetadata { get; private set; }

        // TODO: perhaps this can be consolidated to PropertiesMetadata above, even when field support is added;
        // unless we have to distiguish here to only allow support based on static or runtime opt-in.
        public List<PropertyMetadata>? FieldsMetadata { get; private set; }

        public void Initialize(
            string compilableName,
            string friendlyName,
            Type type,
            ClassType classType,
            bool isValueType,
            bool canBeDynamic,
            List<PropertyMetadata>? propertiesMetadata,
            List<PropertyMetadata>? fieldsMetadata,
            CollectionType collectionType,
            TypeMetadata? collectionKeyTypeMetadata,
            TypeMetadata? collectionValueTypeMetadata)
        {
            if (_hasBeenInitialized)
            {
                throw new InvalidOperationException("Type metadata has already been initialized.");
            }

            _hasBeenInitialized = true;

            CompilableName = compilableName;
            FriendlyName = friendlyName;
            Type = type;
            ClassType = classType;
            IsValueType = isValueType;
            CanBeDynamic = canBeDynamic;
            PropertiesMetadata = propertiesMetadata;
            FieldsMetadata = fieldsMetadata;
            CollectionType = collectionType;
            CollectionKeyTypeMetadata = collectionKeyTypeMetadata;
            CollectionValueTypeMetadata = collectionValueTypeMetadata;
        }
    }
}
