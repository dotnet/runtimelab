// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;

namespace Internal.IL
{
    public abstract class PInvokeILEmitterConfiguration
    {
        public abstract bool GenerateDirectCall(MethodDesc method, out string externName);

        public virtual bool GenerateWasmImportCall(MethodDesc method, out string externName, out string moduleName) => throw new NotImplementedException();
    }
}
