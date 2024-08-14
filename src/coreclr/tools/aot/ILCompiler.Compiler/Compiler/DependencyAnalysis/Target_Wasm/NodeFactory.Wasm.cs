// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace ILCompiler.DependencyAnalysis
{
    public abstract partial class NodeFactory
    {
        // TODO-LLVM-Upstream: make the EH model part of "TargetDetails" and delete this hack.
        public virtual bool TargetsEmulatedEH() => throw new NotSupportedException();
    }
}
