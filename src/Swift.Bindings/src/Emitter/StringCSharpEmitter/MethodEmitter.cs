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
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The method declaration.</param>
        private void EmitMethod(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, MethodDecl methodDecl)
        {
            // Emit PInvoke method
            EmitPInvoke(writer, moduleDecl, parentDecl, methodDecl);

            // Emit wrapper method if marshalling is required
            if (parentDecl is StructDecl || parentDecl is ClassDecl)
            {
                if (methodDecl.IsConstructor)
                {
                    // Emit constructor
                    EmitConstructor(writer, moduleDecl, parentDecl, methodDecl);
                }
                else
                {
                    // Emit method
                    EmitWrapperMethod(writer, moduleDecl, parentDecl, methodDecl);
                }
            }

            writer.WriteLine();
        }

        /// <summary>
        /// Emits the PInvoke method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The method declaration.</param>
        private void EmitPInvoke(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, MethodDecl methodDecl)
        {
            string accessModifier = parentDecl == moduleDecl ? "public" : "internal";
            string methodType = methodDecl.IsConstructor ? parentDecl.Name : methodDecl.Signature.First().TypeIdentifier.Name;
            string methodName = parentDecl == moduleDecl ? methodDecl.Name : $"{PInvokePrefix}{methodDecl.Name}";
            string libPath = _typeDatabase.GetLibraryName(moduleDecl.Name);

            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"{libPath}\", EntryPoint = \"{methodDecl.MangledName}\")]");
            writer.WriteLine($"{accessModifier} static extern {methodType} {methodName}({GetInternalMethodSignature(parentDecl, methodDecl)});");
        }

        /// <summary>
        /// Emits the constructor declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The method declaration.</param>
        private void EmitConstructor(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, MethodDecl methodDecl)
        {
            string methodName = $"{PInvokePrefix}{methodDecl.Name}";

            writer.WriteLine($"public {parentDecl.Name}({GetPublicMethodSignature(parentDecl, methodDecl)})");
            writer.WriteLine("{");
            writer.Indent++;

            string methodArgs = string.Join(", ", methodDecl.Signature.Skip(1).Select(p => p.Name));
            writer.WriteLine($"this = {methodName}({GetMethodArgs(parentDecl, methodDecl)});");

            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Emits the wrapper method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The method declaration.</param>
        private void EmitWrapperMethod(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, MethodDecl methodDecl)
        {
            string methodName = $"{PInvokePrefix}{methodDecl.Name}";

            writer.WriteLine($"public {(methodDecl.MethodType == MethodType.Static ? "static " : "")}{methodDecl.Signature.First().TypeIdentifier.Name} {methodDecl.Name}({GetPublicMethodSignature(parentDecl, methodDecl)})");
            writer.WriteLine("{");
            writer.Indent++;

            if (methodDecl.MethodType == MethodType.Instance)
            {
                writer.WriteLine($"{parentDecl.Name} self = this;");
            }
            string returnPrefix = methodDecl.Signature.First().TypeIdentifier.Name == "void" ? "" : "return ";
            string methodArgs = string.Join(", ", methodDecl.Signature.Skip(1).Select(p => p.Name));
            writer.WriteLine($"{returnPrefix}{methodName}({GetMethodArgs(parentDecl, methodDecl)});");
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Gets the method parameters.
        /// </summary>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The list of method parameters.</returns>
        private List<FieldDecl> GetMethodParams(BaseDecl parentDecl, MethodDecl methodDecl)
        {
            List<FieldDecl> tempDecl = new List<FieldDecl>(methodDecl.Signature);

            // If this is a type method, add the marshalling for the self parameter
            if (parentDecl is StructDecl || parentDecl is ClassDecl)
            {
                if (!methodDecl.IsConstructor && methodDecl.MethodType != MethodType.Static)
                {
                    // Add self as the first parameter (after the return type)
                    tempDecl.Insert(1, new FieldDecl { 
                        TypeIdentifier = new TypeDecl { Name = parentDecl.Name, MangledName = string.Empty, Fields = new List<FieldDecl>(), Declarations = new List<BaseDecl>()},
                        Name = "self",
                        Visibility = Visibility.Public
                    });
                }
            }

            return tempDecl.Skip(1).ToList();
        }

        /// <summary>
        /// Gets the internal method signature.
        /// </summary>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The internal method signature.</returns>
        private string GetInternalMethodSignature(BaseDecl parentDecl, MethodDecl methodDecl)
        {
            List<FieldDecl> parameters = GetMethodParams(parentDecl, methodDecl);
            return string.Join(", ", parameters.Select(p => $"{p.TypeIdentifier.Name} {p.Name}").ToList());
        }

        /// <summary>
        /// Gets the public method signature.
        /// </summary>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The public method signature.</returns>
        private string GetPublicMethodSignature(BaseDecl parentDecl, MethodDecl methodDecl)
        {
            List<FieldDecl> parameters = methodDecl.Signature.Skip(1).ToList();
            return string.Join(", ", parameters.Select(p => $"{p.TypeIdentifier.Name} {p.Name}").ToList());
        }

        /// <summary>
        /// Gets the method arguments.
        /// </summary>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The public method arguments.</returns>
        private string GetMethodArgs(BaseDecl parentDecl, MethodDecl methodDecl)
        {
            List<FieldDecl> parameters = GetMethodParams(parentDecl, methodDecl);
            return string.Join(", ", parameters.Select(p => p.Name).ToList());
        }
    }
}
