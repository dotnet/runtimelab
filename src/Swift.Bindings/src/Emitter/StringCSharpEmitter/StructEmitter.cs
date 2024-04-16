// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;

namespace BindingsGeneration
{
    public partial class StringCSharpEmitter : ICSharpEmitter
    {
        /// <summary>
        /// Emits a struct declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="decl">The struct declaration.</param>
        private void EmitStruct(IndentedTextWriter writer, ModuleDecl moduleDecl, StructDecl structDecl)
        {
            writer.WriteLine($"public unsafe struct {structDecl.Name} {{");
            writer.Indent++;
            foreach (BaseDecl baseDecl in structDecl.Declarations)
                EmitBaseDecl(writer, moduleDecl, baseDecl);
            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}
