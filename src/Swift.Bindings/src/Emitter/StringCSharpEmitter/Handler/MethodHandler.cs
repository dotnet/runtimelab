// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a method handler factory.
    /// </summary>
    public class MethodHandlerFactory : IFactory<BaseDecl, IMethodHandler>
    {
        /// <summary>
        /// Checks if the factory can handle the declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        /// <returns></returns>
        public bool Handles(BaseDecl decl)
        {
            return decl is MethodDecl;
        }

        /// <summary>
        /// Constructs a handler.
        /// </summary>
        public IMethodHandler Construct ()
        {
            return new MethodHandler();
        }
    }

    /// <summary>
    /// Represents a method handler.
    /// </summary>
    public class MethodHandler : BaseHandler, IMethodHandler
    {
        public MethodHandler ()
        {
        }

        /// <summary>
        /// Marshals the method declaration.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        public IEnvironment Marshal(BaseDecl methodDecl)
        {
            return new MethodEnvironment(methodDecl);
        }

        /// <summary>
        /// Emits the method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor, TypeDatabase typeDatabase)
        {
            var methodEnv = (MethodEnvironment)env;
            var methodDecl = (MethodDecl)methodEnv.MethodDecl;
            
            // Emit PInvoke method
            EmitPInvoke(writer, methodEnv, typeDatabase);

            // Emit wrapper method if marshalling is required
            if (methodDecl.ParentDecl is StructDecl || methodDecl.ParentDecl is ClassDecl)
            {
                if (methodDecl.IsConstructor)
                {
                    // Emit constructor
                    EmitConstructor(writer, methodEnv);
                }
                else
                {
                    // Emit method
                    EmitWrapperMethod(writer, methodEnv);
                }
            }

            writer.WriteLine();
        }

        /// <summary>
        /// Emits the PInvoke method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="typeDatabase">The type database.</param>
        private void EmitPInvoke(IndentedTextWriter writer, MethodEnvironment env, TypeDatabase typeDatabase)
        {
            var methodDecl = (MethodDecl)env.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            var moduleDecl = methodDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));

            string accessModifier = parentDecl == moduleDecl ? "public" : "internal";
            string methodType = methodDecl.IsConstructor ? parentDecl.Name : methodDecl.Signature.First().TypeIdentifier.Name;
            string methodName = parentDecl == moduleDecl ? methodDecl.Name : $"{env.PInvokePrefix}{methodDecl.Name}";
            string libPath = typeDatabase.GetLibraryName(moduleDecl.Name);

            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"{libPath}\", EntryPoint = \"{methodDecl.MangledName}\")]");
            writer.WriteLine($"{accessModifier} static extern {methodType} {methodName}({GetInternalMethodSignature(methodDecl)});");
        }

        /// <summary>
        /// Emits the constructor declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment declaration.</param>
        private void EmitConstructor(IndentedTextWriter writer, MethodEnvironment env)
        {
            var methodDecl = (MethodDecl)env.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            var moduleDecl = methodDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));

            string methodName = $"{env.PInvokePrefix}{methodDecl.Name}";

            writer.WriteLine($"public {parentDecl.Name}({GetPublicMethodSignature(methodDecl)})");
            writer.WriteLine("{");
            writer.Indent++;

            string methodArgs = string.Join(", ", methodDecl.Signature.Skip(1).Select(p => p.Name));
            writer.WriteLine($"this = {methodName}({GetMethodArgs(methodDecl)});");

            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Emits the wrapper method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        private void EmitWrapperMethod(IndentedTextWriter writer, MethodEnvironment env)
        {
            var methodDecl = (MethodDecl)env.MethodDecl;
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            var moduleDecl = methodDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));

            string methodName = $"{env.PInvokePrefix}{methodDecl.Name}";

            writer.WriteLine($"public {(methodDecl.MethodType == MethodType.Static ? "static " : "")}{methodDecl.Signature.First().TypeIdentifier.Name} {methodDecl.Name}({GetPublicMethodSignature(methodDecl)})");
            writer.WriteLine("{");
            writer.Indent++;

            if (methodDecl.MethodType == MethodType.Instance)
            {
                writer.WriteLine($"{parentDecl.Name} self = this;");
            }
            string returnPrefix = methodDecl.Signature.First().TypeIdentifier.Name == "void" ? "" : "return ";
            string methodArgs = string.Join(", ", methodDecl.Signature.Skip(1).Select(p => p.Name));
            writer.WriteLine($"{returnPrefix}{methodName}({GetMethodArgs(methodDecl)});");
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Gets the method parameters.
        /// </summary>
        /// <param name="methodDecl">The method declaration.</param>
        /// <returns>The list of method parameters.</returns>
        private List<ArgumentDecl> GetMethodParams(MethodDecl methodDecl)
        {
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));
            List<ArgumentDecl> tempDecl = new(methodDecl.Signature);

            // If this is a type method, add the marshalling for the self parameter
            if (parentDecl is StructDecl || parentDecl is ClassDecl)
            {
                if (!methodDecl.IsConstructor && methodDecl.MethodType != MethodType.Static)
                {
                    // Add self as the first parameter (after the return type)
                    tempDecl.Insert(1, new ArgumentDecl { 
                        TypeIdentifier = new TypeDecl { Name = parentDecl.Name, MangledName = string.Empty, Fields = new List<FieldDecl>(), Declarations = new List<BaseDecl>(), ParentDecl = parentDecl, ModuleDecl = parentDecl.ModuleDecl},
                        Name = "self",
                        PrivateName = string.Empty,
                        IsInOut = false,
                        ParentDecl = methodDecl,
                        ModuleDecl = methodDecl.ModuleDecl
                    });
                }
            }

            return tempDecl.Skip(1).ToList();
        }

        /// <summary>
        /// Gets the internal method signature.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The internal method signature.</returns>
        private string GetInternalMethodSignature(MethodDecl methodDecl)
        {
            var parentDecl = methodDecl.ParentDecl ?? throw new ArgumentNullException(nameof(methodDecl.ParentDecl));

            List<ArgumentDecl> parameters = GetMethodParams(methodDecl);
            return string.Join(", ", parameters.Select(p => $"{p.TypeIdentifier.Name} {p.Name}").ToList());
        }

        /// <summary>
        /// Gets the public method signature.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The public method signature.</returns>
        private string GetPublicMethodSignature(MethodDecl methodDecl)
        {
            List<ArgumentDecl> parameters = methodDecl.Signature.Skip(1).ToList();
            return string.Join(", ", parameters.Select(p => $"{p.TypeIdentifier.Name} {p.Name}").ToList());
        }

        /// <summary>
        /// Gets the method arguments.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <returns>The public method arguments.</returns>
        private string GetMethodArgs(MethodDecl methodDecl)
        {
            List<ArgumentDecl> parameters = GetMethodParams(methodDecl);
            return string.Join(", ", parameters.Select(p => p.Name).ToList());
        }
    }
}
