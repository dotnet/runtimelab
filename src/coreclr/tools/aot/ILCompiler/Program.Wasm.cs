// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.Wasm;

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler
{
    internal partial class Program
    {
        private void InitializeWasmCompilationOptions(CompilerTypeSystemContext context)
        {
            if (!context.Target.IsWasm)
                return;

            context.InitializeWasmCompilationOptions(
                Get(_command.WasmMethodLevelVirtualUnwindModel),
                Get(_command.EmitStackTraceData),
                Get(_command.WasmGlobalBase));
        }

        private static void AddInternalWasmFeatureSwitches(CompilerTypeSystemContext context, Dictionary<string, bool> featureSwitches)
        {
            if (!context.Target.IsWasm)
                return;

            featureSwitches["Internal.Runtime.PreciseVirtualUnwind"] =
                context.WasmMethodLevelVirtualUnwindModel == WasmMethodLevelVirtualUnwindModel.Precise;
        }

        private static void AddInternalWasmSubstitutions(CompilerTypeSystemContext context, ref BodyAndFieldSubstitutions substitutions)
        {
            if (!context.Target.IsWasm)
                return;

            TypeDesc targetType = context.SystemModule.GetType("System.Runtime", "PreciseVirtualUnwindInfo");
            MethodDesc target = targetType.GetKnownMethod("GetUnwindInfoViaAbsoluteValueLimit", null);
            uint value = WasmMethodPreciseVirtualUnwindInfoNode.GetUnwindInfoViaAbsoluteValueLimit(context);
            BodySubstitution substitution = BodySubstitution.Create((int)value);

            Dictionary<MethodDesc, BodySubstitution> addedSubstitutions = new() { [target] = substitution };
            substitutions.AppendFrom(new BodyAndFieldSubstitutions(addedSubstitutions, []));
        }
    }

    internal partial class ILCompilerRootCommand
    {
        public CliOption<WasmMethodLevelVirtualUnwindModel?> WasmMethodLevelVirtualUnwindModel { get; } =
            new("--wasm-method-level-virtual-unwind-model") { Description = "WASM method-level virtual unwind model override" };
        public CliOption<uint> WasmGlobalBase { get; } =
            new("--wasm-global-base") { Description = "WASM global base value" };

        private void InitializeWasmOptions()
        {
            Options.Add(WasmMethodLevelVirtualUnwindModel);
            Options.Add(WasmGlobalBase);
        }
    }
}
