// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.Wasm;

using Internal.TypeSystem;

namespace ILCompiler
{
    public partial class MetadataManager
    {
        public bool HasStackTraceIpWithPreciseVirtualUnwind(IWasmMethodCodeNode methodNode)
        {
            Debug.Assert(_typeSystemContext.WasmMethodLevelVirtualUnwindModel == WasmMethodLevelVirtualUnwindModel.Precise);
            MethodStackTraceVisibilityFlags visibility = _stackTraceEmissionPolicy.GetMethodVisibility(methodNode.Method);
            if ((visibility & MethodStackTraceVisibilityFlags.HasMetadata) != 0)
            {
                // Unboxing thunks logically represent the target in our logic, so we cannot allow them to appear
                // in stack traces as separate entities.
                Debug.Assert(methodNode is not ISpecialUnboxThunkNode { IsSpecialUnboxingThunk: true });
                return true;
            }

            return false;
        }

        public bool RequiresStackTraceIpWithPreciseVirtualUnwind(NodeFactory factory, IWasmMethodCodeNode methodNode)
        {
            Debug.Assert(methodNode.Marked);
            if (!HasStackTraceIpWithPreciseVirtualUnwind(methodNode))
            {
                return false;
            }

            // If codegen didn't need precise virtual unwind info for this method, it means that the only way to get its
            // IP at runtime is via DiagnosticMethodInfo.Create(new Delegate(Method)).
            if (methodNode.PreciseVirtualUnwindInfo == null && !IsPossibleDelegateTarget(factory, methodNode))
            {
                return false;
            }

            return true;
        }

        public bool IsPossibleDelegateOrReflectionTarget(NodeFactory factory, IWasmMethodCodeNode methodNode)
        {
            // Note that this check depends on us setting up a 'fake' ObjectInterner, just so that the compiler tracks
            // address-taken nodes. We're also depending on the implementation detail of reflection always "exposing"
            // methods (see "ReflectionInvokeMap.AddDependenciesDueToReflectability").
            Debug.Assert(methodNode.Marked && !factory.ObjectInterner.IsNull);
            MethodDesc method = methodNode.Method;
            return factory.AddressTakenMethodEntrypoint(method, unboxingStub: method.OwningType.IsValueType && !method.Signature.IsStatic).Marked;
        }

        public ObjectDataInterner CreateObjectInternerForAddressExposureTracking()
        {
            return _typeSystemContext.WasmMethodLevelVirtualUnwindModel == WasmMethodLevelVirtualUnwindModel.Precise
                ? ObjectDataInterner.NullWithTracking
                : null;
        }

        private bool SkipGeneratingStackTraceMappingForWasmMethod(NodeFactory factory, IMethodBodyNode methodNode)
        {
            Debug.Assert(methodNode.Marked);
            if (methodNode is IWasmMethodCodeNode wasmMethodNode &&
                _typeSystemContext.WasmMethodLevelVirtualUnwindModel == WasmMethodLevelVirtualUnwindModel.Precise)
            {
                return !RequiresStackTraceIpWithPreciseVirtualUnwind(factory, wasmMethodNode);
            }

            return false;
        }

        private static bool IsPossibleDelegateTarget(NodeFactory factory, IWasmMethodCodeNode methodNode)
        {
            // Doing this precisely would require intrusive changes to upstream code; we make do with an approximation.
            Debug.Assert(methodNode.Marked && !factory.ObjectInterner.IsNull);
            MethodDesc method = methodNode.Method;
            return factory.AddressTakenMethodEntrypoint(method, unboxingStub: method.OwningType.IsValueType && !method.Signature.IsStatic).Marked;
        }
    }

    public partial class ObjectDataInterner
    {
        public static ObjectDataInterner NullWithTracking { get; } =
            new ObjectDataInterner() { _symbolRemapping = new() { [new ExternSymbolNode("")] = null } };
    }
}
