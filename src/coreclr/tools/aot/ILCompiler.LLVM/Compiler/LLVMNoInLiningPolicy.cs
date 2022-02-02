// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;

namespace ILCompiler.Compiler
{
    internal class LLVMNoInLiningPolicy : IInliningPolicy
    {
        // TODO-LLVM: enable the scanner so we get a real in-lining policy, then delete this class
        public bool CanInline(MethodDesc caller, MethodDesc callee) => false;
    }
}
