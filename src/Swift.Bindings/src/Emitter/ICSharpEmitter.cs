// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an interface for emitting C# source code.
    /// </summary>
    public interface ICSharpEmitter
    {
        /// <summary>
        /// Emits a C# module based on the module declaration.
        /// </summary>
        /// <param name="decl">The module declaration.</param>
        public void EmitModule(ModuleDecl decl);
    }
}