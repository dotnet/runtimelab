using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace System.Reflection
{
    internal class AssemblyWrapper : Assembly
    {
        private readonly MetadataLoadContext _metadataLoadContext;

        public AssemblyWrapper(IAssemblySymbol assembly, MetadataLoadContext metadataLoadContext)
        {
            Symbol = assembly;
            _metadataLoadContext = metadataLoadContext;
        }

        internal IAssemblySymbol Symbol { get; }

        public override Type[] GetExportedTypes()
        {
            return GetTypes();
        }

        public override Type[] GetTypes()
        {
            var types = new List<Type>();
            var stack = new Stack<INamespaceSymbol>();
            stack.Push(Symbol.GlobalNamespace);
            while (stack.Count > 0)
            {
                var current = stack.Pop();

                foreach (var type in current.GetTypeMembers())
                {
                    types.Add(type.AsType(_metadataLoadContext));
                }

                foreach (var ns in current.GetNamespaceMembers())
                {
                    stack.Push(ns);
                }
            }
            return types.ToArray();
        }

        public override Type GetType(string name)
        {
            return Symbol.GetTypeByMetadataName(name)!.AsType(_metadataLoadContext);
        }
    }
}
