// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using SwiftReflector.Demangling;


namespace SwiftReflector.Inventory
{
    public abstract class Inventory<T>
    {
        protected object valuesLock = new object();
        protected Dictionary<SwiftName, T> values = new Dictionary<SwiftName, T>();

        public IEnumerable<SwiftName> Names { get { return values.Keys; } }
        public IEnumerable<T> Values { get { return values.Values; } }

        public abstract void Add(TLDefinition tlf, Stream srcStm);

        public bool ContainsName(SwiftName name)
        {
            lock (valuesLock)
            {
                return values.ContainsKey(name);
            }
        }
        public bool ContainsName(string name)
        {
            return ContainsName(new SwiftName(name, false));
        }

        public bool TryGetValue(SwiftName name, out T value)
        {
            lock (valuesLock)
            {
                return values.TryGetValue(name, out value);
            }
        }

        public bool TryGetValue(string name, out T value)
        {
            return TryGetValue(new SwiftName(name, false), out value);
        }
    }
}

