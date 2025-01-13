// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.ReadyToRunConstants;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class DelayLoadMethodImport : DelayLoadHelperImport, IMethodNode
    {
        private readonly MethodWithGCInfo _localMethod;

        public DelayLoadMethodImport(
            NodeFactory factory,
            ReadyToRunFixupKind fixupKind,
            MethodWithToken method,
            MethodWithGCInfo localMethod,
            bool isInstantiatingStub,
            bool isJump)
            : base(
                  factory,
                  factory.MethodImports,
                  ReadyToRunHelper.DelayLoad_MethodCall,
                  factory.MethodSignature(
                      fixupKind,
                      method,
                      isInstantiatingStub),
                  useJumpableStub: isJump)
        {
            _localMethod = localMethod;
            MethodWithToken = method;
        }

        public MethodWithToken MethodWithToken { get; }
        public MethodDesc Method => MethodWithToken.Method;
        public MethodWithGCInfo MethodCodeNode => _localMethod;

        public override int ClassCode => 459923351;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            foreach (DependencyListEntry entry in base.GetStaticDependencies(factory))
            {
                yield return entry;
            }
            
            // It is possible that an instance of DelayLoadMethodImport is constructed by never get into the graph
            // So we must delay the construction and rooting of the InterpreterImport

            // This is for static calls
            InterpreterImport _interpreterImport = new InterpreterImport();
            InterpreterStub _interpreterStub = new InterpreterStub(_interpreterImport, /* virtual = */false);
            factory.AddInterpreterMapping(this, _interpreterImport, _interpreterStub);
            yield return new DependencyListEntry(_interpreterImport, "Unused reason 1");
            yield return new DependencyListEntry(_interpreterStub, "Unused reason 2");
            yield return new DependencyListEntry(factory.InterpreterMap, "Unused reason 3");
            if (_localMethod != null)
                yield return new DependencyListEntry(_localMethod, "Local method import");
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            if ((_localMethod != null) && (((DelayLoadMethodImport)other)._localMethod != null))
            {
                int result = comparer.Compare(_localMethod, ((DelayLoadMethodImport)other)._localMethod);
                if (result != 0)
                    return result;
            }
            else if (_localMethod != null)
                return 1;
            else if (((DelayLoadMethodImport)other)._localMethod != null)
                return -1;

            return base.CompareToImpl(other, comparer);
        }
    }
}
