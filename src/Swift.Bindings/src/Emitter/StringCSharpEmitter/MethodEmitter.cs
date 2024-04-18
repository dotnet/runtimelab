// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;

namespace BindingsGeneration
{
    public partial class StringCSharpEmitter : ICSharpEmitter
    {
        /// <summary>
        /// Emits the method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="decl">The method declaration.</param>
        private void EmitMethod(IndentedTextWriter writer, ModuleDecl moduleDecl, MethodDecl methodDecl)
        {
            var accessModifier = methodDecl.RequireMarshalling ? "internal" : "public";

            // Write the P/Invoke attribute and method signature
            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"{moduleDecl.Name}.dylib\", EntryPoint = \"{methodDecl.MangledName}\")]");
            writer.Write($"{accessModifier} static extern {methodDecl.Signature.First().TypeIdentifier} {GetMethodName(methodDecl)}(");
            EmitMethodParams(writer, methodDecl.Signature.Skip(1));
            writer.WriteLine($");");

            // Emit the wrapper method if marshalling is required
            if (methodDecl.RequireMarshalling)
            {
                writer.Write($"public {(methodDecl.IsStatic ? "static" : "")} {methodDecl.Signature.First().TypeIdentifier} {methodDecl.Name}(");
                EmitMethodParams(writer, methodDecl.Signature.Skip(1));
                writer.WriteLine($")");
                writer.WriteLine($"{{");
                writer.Indent++;

                string returnPrefix = methodDecl.Signature.First().TypeIdentifier != "void" ? "return " : "";
                writer.WriteLine($"{returnPrefix}{PInvokePrefix}{methodDecl.Name}({GetMethodArguments(methodDecl)});");

                writer.Indent--;
                writer.WriteLine($"}}");
            }
        }

        /// <summary>
        /// Constructs the method name with optional prefix based on PInvoke requirement.
        /// </summary>
        private string GetMethodName(MethodDecl decl)
        {
            return decl.RequireMarshalling ? $"{PInvokePrefix}{decl.Name}" : decl.Name;
        }

        /// <summary>
        /// Formats method arguments for invocation.
        /// </summary>
        private string GetMethodArguments(MethodDecl decl)
        {
            return string.Join(", ", decl.Signature.Skip(1).Select(p => p.Name));
        }

        /// <summary>
        /// Emits parameters for the method.
        /// </summary>
        private void EmitMethodParams(IndentedTextWriter writer, IEnumerable<TypeDecl> parameters)
        {
            writer.Write(string.Join(", ", parameters.Select(p => $"{p.TypeIdentifier} {p.Name}")));
        }
    }
}
