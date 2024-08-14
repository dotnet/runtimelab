// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis;

using Internal.JitInterface;
using Internal.JitInterface.LLVMInterop;
using Internal.TypeSystem;

namespace ILCompiler
{
    public sealed unsafe partial class LLVMCodegenCompilation
    {
        internal LLVMContext* LLVMContext { get; } = LLVMContextRef.Create();
        internal LLVMType* LLVMInt32Type { get; private set; }
        internal LLVMType* LLVMInt64Type { get; private set; }
        internal LLVMType* LLVMPtrType { get; private set; }
        internal LLVMType* LLVMFloatType { get; private set; }
        internal LLVMType* LLVMDoubleType { get; private set; }
        internal LLVMType* LLVMVoidType { get; private set; }

        private void InitializeCodeGen()
        {
            LLVMInt32Type = LLVMTypeRef.GetInt(LLVMContext, 32);
            LLVMInt64Type = LLVMTypeRef.GetInt(LLVMContext, 64);
            LLVMPtrType = LLVMTypeRef.GetPointer(LLVMContext);
            LLVMFloatType = LLVMTypeRef.GetFloat(LLVMContext);
            LLVMDoubleType = LLVMTypeRef.GetDouble(LLVMContext);
            LLVMVoidType = LLVMTypeRef.GetVoid(LLVMContext);
        }

        public override ISymbolNode GetExternalMethodAccessor(MethodDesc method, ReadOnlySpan<TargetAbiType> sig)
        {
            Debug.Assert(!sig.IsEmpty);
            string name = PInvokeILProvider.GetDirectCallExternName(method);

            return NodeFactory.ExternSymbolWithAccessor(name, method, sig);
        }

        public override CorInfoLlvmEHModel GetLlvmExceptionHandlingModel() => Options.ExceptionHandlingModel;
    }
}
