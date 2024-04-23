// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    public partial class StringCSharpEmitter : ICSharpEmitter
    {
        /// <summary>
        /// Emits a class declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The class declaration.</param>
        private void EmitClass(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, ClassDecl classDecl)
        {
            writer.WriteLine($"public unsafe class {classDecl.Name} {{");
            writer.Indent++;
            foreach (BaseDecl baseDecl in classDecl.Declarations)
                EmitBaseDecl(writer, moduleDecl, classDecl, baseDecl);
            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}
