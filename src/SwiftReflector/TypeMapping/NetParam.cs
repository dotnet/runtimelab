// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SwiftRuntimeLibrary;

namespace SwiftReflector.TypeMapping
{
    public class NetParam
    {
        public NetParam(string name, NetTypeBundle bundle)
        {
            ArgumentNullException.ThrowIfNull(name, nameof(name));
            Name = name;
            Type = bundle;
        }
        public string Name { get; private set; }
        public NetTypeBundle Type { get; private set; }
    }

}

