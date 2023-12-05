// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Internal.IL.Stubs;
using Internal.TypeSystem;

namespace ILCompiler
{
    internal sealed class WasmConfigurablePInvokePolicy : ConfigurablePInvokePolicy
    {
        public WasmConfigurablePInvokePolicy(TargetDetails target, IReadOnlyList<string> directPInvokes, IReadOnlyList<string> directPInvokeLists)
            : base(target, directPInvokes, directPInvokeLists)
        {
        }

        public override bool GenerateDirectCall(MethodDesc method, out string externName)
        {
            if (IsWasmImport(method, out PInvokeMetadata pInvokeMetadata))
            {
                externName = "__wasm_import_" + pInvokeMetadata.Module + "_" + pInvokeMetadata.Name;
                return true;
            }

            return base.GenerateDirectCall(method, out externName);
        }

        private static bool IsWasmImport(MethodDesc method, out PInvokeMetadata pInvokeMetadata)
        {
            if (method is PInvokeTargetNativeMethod pInvokeTargetNativeMethod)
            {
                method = pInvokeTargetNativeMethod.Target;
            }

            if (method.HasCustomAttribute("System.Runtime.InteropServices", "WasmImportLinkageAttribute"))
            {
                pInvokeMetadata = method.GetPInvokeMethodMetadata();
                return true;
            }

            pInvokeMetadata = default;
            return false;
        }
    }
}
