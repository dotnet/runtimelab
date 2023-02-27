// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;

using ILCompiler.DependencyAnalysis;
using LLVMSharp.Interop;

namespace Internal.IL
{
    internal partial class ILImporter
    {
        static LLVMValueRef DebugtrapFunction = default(LLVMValueRef);
        static LLVMValueRef TrapFunction = default(LLVMValueRef);
        static LLVMValueRef DoNothingFunction = default(LLVMValueRef);
        static LLVMValueRef CxaBeginCatchFunction = default(LLVMValueRef);
        static LLVMValueRef CxaEndCatchFunction = default(LLVMValueRef);
        static LLVMValueRef RhpCallCatchFunclet = default(LLVMValueRef);
        static LLVMValueRef LlvmCatchFunclet = default(LLVMValueRef);
        static LLVMValueRef LlvmFilterFunclet = default(LLVMValueRef);
        static LLVMValueRef LlvmFinallyFunclet = default(LLVMValueRef);
        static LLVMValueRef NullRefFunction = default(LLVMValueRef);
        static LLVMValueRef CkFinite32Function = default(LLVMValueRef);
        static LLVMValueRef CkFinite64Function = default(LLVMValueRef);
        public static LLVMValueRef GxxPersonality = default(LLVMValueRef);
        public static LLVMTypeRef GxxPersonalityType = default(LLVMTypeRef);

        internal static LLVMValueRef MakeFatPointer(LLVMBuilderRef builder, LLVMValueRef targetLlvmFunction, LLVMCodegenCompilation compilation)
        {
            var asInt = builder.BuildPtrToInt(targetLlvmFunction, LLVMTypeRef.Int32, "toInt");
            return builder.BuildBinOp(LLVMOpcode.LLVMOr, asInt, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)compilation.TypeSystemContext.Target.FatFunctionPointerOffset, false), "makeFat");
        }

        private static IList<string> GetParameterNamesForMethod(MethodDesc method)
        {
            // TODO: The uses of this method need revision. The right way to get to this info is from
            //       a MethodIL. For declarations, we don't need names.
            method = method.GetTypicalMethodDefinition();
            var ecmaMethod = method as EcmaMethod;
            if (ecmaMethod != null && ecmaMethod.Module.PdbReader != null)
            {
                List<string> parameterNames = new List<string>(new EcmaMethodDebugInformation(ecmaMethod).GetParameterNames());

                // Return the parameter names only if they match the method signature
                if (parameterNames.Count != 0)
                {
                    var methodSignature = method.Signature;
                    int argCount = methodSignature.Length;
                    if (!methodSignature.IsStatic)
                        argCount++;

                    if (parameterNames.Count == argCount)
                        return parameterNames;
                }
            }

            return null;
        }

        static void BuildCatchFunclet(LLVMModuleRef module, LLVMTypeRef[] funcletArgTypes)
        {
            LlvmCatchFunclet = module.AddFunction("LlvmCatchFunclet", LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, funcletArgTypes, false));
            var block = LlvmCatchFunclet.AppendBasicBlock("Catch");
            LLVMBuilderRef funcletBuilder = Context.CreateBuilder();
            funcletBuilder.PositionAtEnd( block);

            LLVMValueRef leaveToILOffset = funcletBuilder.BuildCall(LlvmCatchFunclet.GetParam(0), new LLVMValueRef[] { LlvmCatchFunclet.GetParam(1) }, "callCatch");
            funcletBuilder.BuildRet(leaveToILOffset);
            funcletBuilder.Dispose();
        }

        static void BuildFilterFunclet(LLVMModuleRef module, LLVMTypeRef[] funcletArgTypes)
        {
            LlvmFilterFunclet = module.AddFunction("LlvmFilterFunclet", LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32,  funcletArgTypes, false));
            var block = LlvmFilterFunclet.AppendBasicBlock("Filter");
            LLVMBuilderRef funcletBuilder = Context.CreateBuilder();
            funcletBuilder.PositionAtEnd(block);

            LLVMValueRef filterResult = funcletBuilder.BuildCall(LlvmFilterFunclet.GetParam(0), new LLVMValueRef[] { LlvmFilterFunclet.GetParam(1) }, "callFilter");
            funcletBuilder.BuildRet(filterResult);
            funcletBuilder.Dispose();
        }

        static void BuildFinallyFunclet(LLVMModuleRef module)
        {
            LlvmFinallyFunclet = module.AddFunction("LlvmFinallyFunclet", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void,
                new LLVMTypeRef[]
                {
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)}, false), 0), // finallyHandler
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), // shadow stack
                }, false));
            var block = LlvmFinallyFunclet.AppendBasicBlock("Finally");
            LLVMBuilderRef funcletBuilder = Context.CreateBuilder();
            funcletBuilder.PositionAtEnd(block);

            var finallyFunclet = LlvmFinallyFunclet.GetParam(0);
            var castShadowStack = LlvmFinallyFunclet.GetParam(1);

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(castShadowStack);

            funcletBuilder.BuildCall(finallyFunclet, llvmArgs.ToArray(), string.Empty);
            funcletBuilder.BuildRetVoid();
            funcletBuilder.Dispose();
        }

        private static LLVMValueRef CreateLLVMFunction(LLVMModuleRef module, string mangledName, MethodSignature signature, bool hasHiddenParameter)
        {
            return module.AddFunction(mangledName, LLVMCodegenCompilation.GetLLVMSignatureForMethod(signature, hasHiddenParameter));
        }

        internal static LLVMValueRef GetOrCreateLLVMFunction(LLVMModuleRef module, string mangledName, MethodSignature signature, bool hasHiddenParam)
        {
            LLVMValueRef llvmFunction = module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == IntPtr.Zero)
            {
                return CreateLLVMFunction(module, mangledName, signature, hasHiddenParam);
            }
            return llvmFunction;
        }

        internal static void GenerateRuntimeExportThunk(LLVMCodegenCompilation compilation, MethodDesc method, LLVMValueRef llvmFunction)
        {
            if (method.IsRuntimeExport || method.IsUnmanagedCallersOnly)
            {
                IMethodNode methodNode = compilation.NodeFactory.MethodEntrypoint(method);
                string name = compilation.NodeFactory.GetSymbolAlternateName(methodNode) ?? method.Name;
                EmitNativeToManagedThunk(compilation, method, name, llvmFunction);
            }
        }

        internal static void EmitNativeToManagedThunk(LLVMCodegenCompilation compilation, MethodDesc method, string nativeName, LLVMValueRef managedFunction)
        {
            if (!_pinvokeMap.ContainsKey(nativeName))
            {
                _pinvokeMap.Add(nativeName, method);
            }

            LLVMTypeRef[] llvmParams = new LLVMTypeRef[method.Signature.Length];
            for (int i = 0; i < llvmParams.Length; i++)
            {
                llvmParams[i] = GetLLVMTypeForTypeDesc(method.Signature[i]);
            }

            LLVMTypeRef thunkSig = LLVMTypeRef.CreateFunction(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), llvmParams, false);
            LLVMValueRef thunkFunc = GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, nativeName, thunkSig);

            using LLVMBuilderRef builder = Context.CreateBuilder();
            LLVMBasicBlockRef block = thunkFunc.AppendBasicBlock("ManagedCallBlock");
            builder.PositionAtEnd(block);

            // Get the shadow stack. If we are wrapping a runtime export, the caller is (by definition) managed, so we must have set up the shadow
            // stack already and can bypass the init check.
            string getShadowStackFuncName = method.IsRuntimeExport ? "RhpGetShadowStackTop" : "RhpGetOrInitShadowStackTop";
            LLVMTypeRef getShadowStackFuncSig = LLVMTypeRef.CreateFunction(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), Array.Empty<LLVMTypeRef>());
            LLVMValueRef getShadowStackFunc = GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, getShadowStackFuncName, getShadowStackFuncSig);
            LLVMValueRef shadowStack = builder.BuildCall(getShadowStackFunc, Array.Empty<LLVMValueRef>());

            LLVMTypeRef reversePInvokeFrameType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            LLVMValueRef reversePInvokeFrame = default(LLVMValueRef);
            LLVMTypeRef reversePInvokeFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false);
            if (method.IsUnmanagedCallersOnly)
            {
                reversePInvokeFrame = builder.BuildAlloca(reversePInvokeFrameType, "ReversePInvokeFrame");
                LLVMValueRef RhpReversePInvoke = GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, "RhpReversePInvoke", reversePInvokeFunctionType);
                builder.BuildCall(RhpReversePInvoke, new LLVMValueRef[] { reversePInvokeFrame }, "");
            }

            bool needsReturnSlot = LLVMCodegenCompilation.NeedsReturnStackSlot(method.Signature);

            int curOffset = 0;
            LLVMValueRef calleeFrame;
            if (needsReturnSlot)
            {
                curOffset = PadNextOffset(method.Signature.ReturnType, curOffset);
                curOffset = PadOffset(compilation.GetWellKnownType(WellKnownType.Object), curOffset); // Align the stack to pointer size.
                ImportCallMemset(shadowStack, 0, curOffset, builder); // clear any uncovered object references for GC.Collect

                calleeFrame = builder.BuildGEP(shadowStack, new LLVMValueRef[] { BuildConstInt32(curOffset) }, "calleeFrame");
            }
            else
            {
                calleeFrame = shadowStack;
            }

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(calleeFrame);

            if (needsReturnSlot)
            {
                // Slot for return value if necessary
                llvmArgs.Add(shadowStack);
            }

            for (int i = 0; i < llvmParams.Length; i++)
            {
                LLVMValueRef argValue = thunkFunc.GetParam((uint)i);

                if (CanStoreTypeOnStack(method.Signature[i]))
                {
                    llvmArgs.Add(argValue);
                }
                else
                {
                    curOffset = PadOffset(method.Signature[i], curOffset);
                    LLVMValueRef argAddr = builder.BuildGEP(shadowStack, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)curOffset, false) }, "arg" + i);
                    builder.BuildStore(argValue, CastIfNecessary(builder, argAddr, LLVMTypeRef.CreatePointer(llvmParams[i], 0), $"parameter{i}_"));
                    curOffset = PadNextOffset(method.Signature[i], curOffset);
                }
            }

            LLVMValueRef llvmReturnValue = builder.BuildCall(managedFunction, llvmArgs.ToArray(), "");

            if (method.IsUnmanagedCallersOnly)
            {
                LLVMValueRef RhpReversePInvokeReturn = GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, "RhpReversePInvokeReturn", reversePInvokeFunctionType);
                builder.BuildCall(RhpReversePInvokeReturn, new LLVMValueRef[] { reversePInvokeFrame }, "");

                // Restore the shadow stack. Note: only needed for UCO methods as otherwise the caller is guaranteed to never use the stale value.
                LLVMTypeRef setShadowStackFuncSig = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) });
                LLVMValueRef setShadowStackFunc = GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, "RhpSetShadowStackTop", setShadowStackFuncSig);
                builder.BuildCall(setShadowStackFunc, new[] { shadowStack });
            }

            if (!method.Signature.ReturnType.IsVoid)
            {
                if (needsReturnSlot)
                {
                    builder.BuildRet(builder.BuildLoad(CastIfNecessary(builder, shadowStack, LLVMTypeRef.CreatePointer(GetLLVMTypeForTypeDesc(method.Signature.ReturnType), 0)), "returnValue"));
                }
                else
                {
                    builder.BuildRet(llvmReturnValue);
                }
            }
            else
            {
                builder.BuildRetVoid();
            }
        }

        internal static LLVMValueRef GetOrCreateLLVMFunction(LLVMModuleRef module, string mangledName, LLVMTypeRef functionType)
        {
            LLVMValueRef llvmFunction = module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == IntPtr.Zero)
            {
                return module.AddFunction(mangledName, functionType);
            }
            return llvmFunction;
        }

        private static void ImportCallMemset(LLVMValueRef targetPointer, byte value, int length, LLVMBuilderRef builder)
        {
            LLVMValueRef objectSizeValue = BuildConstInt32(length);
            ImportCallMemset(targetPointer, value, objectSizeValue, builder);
        }

        private static void ImportCallMemset(LLVMValueRef targetPointer, byte value, LLVMValueRef length, LLVMBuilderRef builder)
        {
            var memsetSignature = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.Int8, LLVMTypeRef.Int32, LLVMTypeRef.Int1 }, false);
            builder.BuildCall(GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, "llvm.memset.p0i8.i32", memsetSignature), new LLVMValueRef[] { targetPointer, BuildConstInt8(value), length, BuildConstInt1(0) }, String.Empty);
        }

        public static int PadNextOffset(TypeDesc type, int atOffset)
        {
            var size = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldSize : type.Context.Target.LayoutPointerSize;
            return PadOffset(type, atOffset) + size.AsInt;
        }

        public static int PadOffset(TypeDesc type, int atOffset)
        {
            var fieldAlignment = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldAlignment : type.Context.Target.LayoutPointerSize;
            var alignment = LayoutInt.Min(fieldAlignment, new LayoutInt(ComputePackingSize(type))).AsInt;
            var padding = (atOffset + (alignment - 1)) & ~(alignment - 1);
            return padding;
        }

        static LLVMValueRef s_shadowStackTop = default(LLVMValueRef);

        static LLVMValueRef ShadowStackTop
        {
            get
            {
                if (s_shadowStackTop.Handle.Equals(IntPtr.Zero))
                {
                    s_shadowStackTop = LLVMCodegenCompilation.Module.AddGlobal(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "t_pShadowStackTop");
                    s_shadowStackTop.ThreadLocalMode = LLVMThreadLocalMode.LLVMLocalDynamicTLSModel;
                }
                return s_shadowStackTop;
            }
        }

        /// <summary>
        /// Returns true if a type is a struct that just wraps a given primitive
        /// or another struct that does so and can thus be treated as that primitive
        /// </summary>
        /// <param name="type">The struct to evaluate</param>
        /// <param name="primitiveType">The primitive to check for</param>
        /// <returns>True if the struct is a wrapper of the primitive</returns>
        public static bool StructIsWrappedPrimitive(TypeDesc type, TypeDesc primitiveType)
        {
            Debug.Assert(type.IsValueType);
            Debug.Assert(primitiveType.IsPrimitive);

            if (type.GetElementSize().AsInt != primitiveType.GetElementSize().AsInt)
            {
                return false;
            }

            FieldDesc[] fields = type.GetFields().ToArray();
            int instanceFieldCount = 0;
            bool foundPrimitive = false;

            foreach (FieldDesc field in fields)
            {
                if (field.IsStatic)
                {
                    continue;
                }

                instanceFieldCount++;

                // If there's more than one field, figuring out whether this is a primitive gets complicated, so assume it's not
                if (instanceFieldCount > 1)
                {
                    break;
                }

                TypeDesc fieldType = field.FieldType;
                if (fieldType == primitiveType)
                {
                    foundPrimitive = true;
                }
                else if (fieldType.IsValueType && !fieldType.IsPrimitive && StructIsWrappedPrimitive(fieldType, primitiveType))
                {
                    foundPrimitive = true;
                }
            }

            if (instanceFieldCount == 1 && foundPrimitive)
            {
                return true;
            }

            return false;
        }
    }
}
