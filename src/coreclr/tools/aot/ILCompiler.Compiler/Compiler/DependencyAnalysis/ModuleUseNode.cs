// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    internal class ModuleUseNode : DependencyNodeCore<NodeFactory>
    {
        private readonly ModuleDesc _module;

        public ModuleUseNode(ModuleDesc module)
        {
            _module = module;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            MethodDesc moduleInitializer = _module.GetGlobalModuleType().GetStaticConstructor();
            if (moduleInitializer != null)
            {
                return new DependencyListEntry[]
                {
                    new DependencyListEntry(factory.MethodEntrypoint(moduleInitializer), "Module initializer"),
                };
            }
            return Array.Empty<DependencyListEntry>();
        }

        protected override string GetName(NodeFactory factory)
        {
            return $"Module use: {_module.Assembly.GetName()}";
        }

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
    }
}
