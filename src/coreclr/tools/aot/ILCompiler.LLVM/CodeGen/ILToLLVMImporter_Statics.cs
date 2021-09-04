// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;

using ILCompiler.DependencyAnalysis;
using LLVMSharp.Interop;

namespace Internal.IL
{
    internal partial class ILImporter
    {
        public static void CompileMethod(LLVMCodegenCompilation compilation, LLVMMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            if (compilation.Logger.IsVerbose)
            {
                string methodName = method.ToString();
                compilation.Logger.Writer.WriteLine("Compiling " + methodName);
            }

            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                methodCodeNodeNeedingCode.CompilationCompleted = true;
                //throw new NotImplementedException();
                //CompileExternMethod(methodCodeNodeNeedingCode, ((EcmaMethod)method).GetRuntimeImportName());
                //return;
            }

            if (method.IsRawPInvoke())
            {
                //CompileExternMethod(methodCodeNodeNeedingCode, method.GetPInvokeMethodMetadata().Name ?? method.Name);
                //return;
            }
            var methodIL = compilation.GetMethodIL(method);
            if (methodIL == null)
                return;

            ILImporter ilImporter = null;
            try
            {
                string mangledName;

                // TODO: Better detection of the StartupCodeMain method
                if (methodCodeNodeNeedingCode.Method.Signature.IsStatic && methodCodeNodeNeedingCode.Method.Name == "StartupCodeMain")
                {
                    mangledName = "StartupCodeMain";
                }
                else
                {
                    mangledName = compilation.NameMangler.GetMangledMethodName(methodCodeNodeNeedingCode.Method).ToString();
                }

                ilImporter = new ILImporter(compilation, method, methodIL, mangledName, methodCodeNodeNeedingCode is LlvmUnboxingThunkNode);

                CompilerTypeSystemContext typeSystemContext = compilation.TypeSystemContext;

                //MethodDebugInformation debugInfo = compilation.GetDebugInfo(methodIL);

               /* if (!compilation.Options.HasOption(CppCodegenConfigProvider.NoLineNumbersString))*/
                {
                    //IEnumerable<ILSequencePoint> sequencePoints = debugInfo.GetSequencePoints();
                    /*if (sequencePoints != null)
                        ilImporter.SetSequencePoints(sequencePoints);*/
                }

                //IEnumerable<ILLocalVariable> localVariables = debugInfo.GetLocalVariables();
                /*if (localVariables != null)
                    ilImporter.SetLocalVariables(localVariables);*/

                IEnumerable<string> parameters = GetParameterNamesForMethod(method);
                /*if (parameters != null)
                    ilImporter.SetParameterNames(parameters);*/

                ilImporter.Import();
                ilImporter.CreateEHData(methodCodeNodeNeedingCode);
                methodCodeNodeNeedingCode.CompilationCompleted = true;
            }
            catch (Exception e)
            {
                compilation.Logger.Writer.WriteLine(e.Message + " (" + method + ")");

                methodCodeNodeNeedingCode.CompilationCompleted = true;
//                methodCodeNodeNeedingCode.SetDependencies(ilImporter.GetDependencies());
                //throw new NotImplementedException();
                //methodCodeNodeNeedingCode.SetCode(sb.ToString(), Array.Empty<Object>());
            }

            // Uncomment the block below to get specific method failures when LLVM fails for cryptic reasons
#if false
            LLVMBool result = LLVM.VerifyFunction(ilImporter._llvmFunction, LLVMVerifierFailureAction.LLVMPrintMessageAction);
            if (result.Value != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error compliling {method.OwningType}.{method}");
                Console.ResetColor();
            }
#endif // false

            // Ensure dependencies show up regardless of exceptions to avoid breaking LLVM
            methodCodeNodeNeedingCode.SetDependencies(ilImporter.GetDependencies());
        }

        static LLVMValueRef DebugtrapFunction = default(LLVMValueRef);
        static LLVMValueRef TrapFunction = default(LLVMValueRef);
        static LLVMValueRef DoNothingFunction = default(LLVMValueRef);
        static LLVMValueRef CxaBeginCatchFunction = default(LLVMValueRef);
        static LLVMValueRef CxaEndCatchFunction = default(LLVMValueRef);
        static LLVMValueRef RhpThrowEx = default(LLVMValueRef);
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

        internal static void GenerateRuntimeExportThunk(LLVMCodegenCompilation compilation, MethodDesc compiledMethod, LLVMValueRef llvmFunction)
        {
            if ((compiledMethod.IsRuntimeExport || compiledMethod.IsUnmanagedCallersOnly) && compiledMethod is EcmaMethod)  // TODO: Reverse delegate invokes probably need something here, but what would be the export name?
            {
                EcmaMethod ecmaMethod = ((EcmaMethod)compiledMethod);
                string exportName = ecmaMethod.IsRuntimeExport ? ecmaMethod.GetRuntimeExportName() : ecmaMethod.GetUnmanagedCallersOnlyExportName();
                if (exportName == null)
                {
                    exportName = ecmaMethod.Name;
                }

                EmitNativeToManagedThunk(compilation, compiledMethod, exportName, llvmFunction);
            }
        }

        internal static void EmitNativeToManagedThunk(LLVMCodegenCompilation compilation, MethodDesc method, string nativeName, LLVMValueRef managedFunction)
        {
            if (_pinvokeMap.TryGetValue(nativeName, out MethodDesc existing))
            {
                // if (existing != method) return;
                    // throw new InvalidProgramException("export and import function were mismatched");
            }
            else
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

            LLVMBasicBlockRef shadowStackSetupBlock = thunkFunc.AppendBasicBlock("ShadowStackSetupBlock");
            LLVMBasicBlockRef allocateShadowStackBlock = thunkFunc.AppendBasicBlock("allocateShadowStackBlock");
            LLVMBasicBlockRef managedCallBlock = thunkFunc.AppendBasicBlock("ManagedCallBlock");

            LLVMBuilderRef builder = Context.CreateBuilder();
            builder.PositionAtEnd(shadowStackSetupBlock);

            // Allocate shadow stack if it's null
            LLVMValueRef shadowStackPtr = builder.BuildAlloca(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "ShadowStackPtr");
            LLVMValueRef savedShadowStack = builder.BuildLoad(ShadowStackTop, "SavedShadowStack");
            builder.BuildStore(savedShadowStack, shadowStackPtr);
            LLVMValueRef shadowStackNull = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, savedShadowStack, LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)), "ShadowStackNull");
            builder.BuildCondBr(shadowStackNull, allocateShadowStackBlock, managedCallBlock);

            builder.PositionAtEnd(allocateShadowStackBlock);

            LLVMValueRef newShadowStack = builder.BuildArrayMalloc(LLVMTypeRef.Int8, BuildConstInt32(1000000), "NewShadowStack");
            builder.BuildStore(newShadowStack, shadowStackPtr);
            builder.BuildBr(managedCallBlock);

            builder.PositionAtEnd(managedCallBlock);
            LLVMTypeRef reversePInvokeFrameType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            LLVMValueRef reversePInvokeFrame = default(LLVMValueRef);
            LLVMTypeRef reversePInvokeFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false);
            if (method.IsUnmanagedCallersOnly)
            {
                reversePInvokeFrame = builder.BuildAlloca(reversePInvokeFrameType, "ReversePInvokeFrame");
                LLVMValueRef RhpReversePInvoke2 = GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, "RhpReversePInvoke2", reversePInvokeFunctionType);
                builder.BuildCall(RhpReversePInvoke2, new LLVMValueRef[] { reversePInvokeFrame }, "");
            }

            LLVMValueRef shadowStack = builder.BuildLoad(shadowStackPtr, "ShadowStack");
            int curOffset = 0;
            curOffset = PadNextOffset(method.Signature.ReturnType, curOffset);
            ImportCallMemset(shadowStack, 0, curOffset, builder); // clear any uncovered object references for GC.Collect
            LLVMValueRef calleeFrame = builder.BuildGEP(shadowStack, new LLVMValueRef[] { BuildConstInt32(curOffset) }, "calleeFrame");

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(calleeFrame);

            bool needsReturnSlot = LLVMCodegenCompilation.NeedsReturnStackSlot(method.Signature);

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
                LLVMValueRef RhpReversePInvokeReturn2 = GetOrCreateLLVMFunction(LLVMCodegenCompilation.Module, "RhpReversePInvokeReturn2", reversePInvokeFunctionType);
                builder.BuildCall(RhpReversePInvokeReturn2, new LLVMValueRef[] { reversePInvokeFrame }, "");
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
                    s_shadowStackTop.Linkage = LLVMLinkage.LLVMExternalLinkage;
                    s_shadowStackTop.Initializer = LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
                    s_shadowStackTop.ThreadLocalMode = LLVMThreadLocalMode.LLVMLocalDynamicTLSModel;
                }
                return s_shadowStackTop;
            }
        }
    }
}
