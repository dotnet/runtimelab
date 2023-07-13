// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ILCompiler.DependencyAnalysis
{
    public partial class NodeFactory
    {
        // WASM uses the shadow stack calling convention, which makes native entry points to runtime
        // exports incur a penalty of saving the shadow stack and setting up arguments. This method may
        // be overriden by a derived class to redirect from a native entry point to the managed one.
        public virtual IMethodNode RuntimeExportManagedEntrypoint(string name) => null;
    }
}
