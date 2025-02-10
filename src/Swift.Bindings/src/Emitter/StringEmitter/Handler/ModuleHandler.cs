// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Factory class for creating instances of ModuleHandler.
    /// </summary>
    public class ModuleHandlerFactory : IFactory<BaseDecl, IModuleHandler>
    {
        /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is ModuleDecl;
        }

        /// <summary>
        /// Constructs a new instance of ModuleHandler.
        /// </summary>
        public IModuleHandler Construct()
        {
            return new ModuleHandler();
        }
    }

    /// <summary>
    /// Handler class for module declarations.
    /// </summary>
    public class ModuleHandler : BaseHandler, IModuleHandler
    {
        public ModuleHandler()
        {
        }

        /// <inheritdoc/>
        public IEnvironment Marshal(BaseDecl baseDecl, ITypeDatabase typeDatabase)
        {
            if (baseDecl is not ModuleDecl moduleDecl)
            {
                throw new ArgumentException("The provided decl must be a ModuleDecl.", nameof(baseDecl));
            }
            return new ModuleEnvironment(moduleDecl, typeDatabase);
        }

        /// <inheritdoc/>
        public void Emit(CSharpWriter csWriter, SwiftWriter swiftWriter, IEnvironment env, Conductor conductor)
        {
            var moduleEnv = (ModuleEnvironment)env;
            var moduleDecl = moduleEnv.ModuleDecl;

            var generatedNamespace = $"Swift.{moduleDecl.Name}";

            csWriter.WriteLine($"using System;");
            csWriter.WriteLine($"using System.Runtime.CompilerServices;");
            csWriter.WriteLine($"using System.Runtime.InteropServices;");
            csWriter.WriteLine($"using System.Runtime.InteropServices.Swift;");
            csWriter.WriteLine($"using Swift;");
            csWriter.WriteLine($"using Swift.Runtime;");
            csWriter.WriteLine($"using Swift.Runtime.InteropServices;");
            csWriter.WriteLine();
            csWriter.WriteLine($"namespace {generatedNamespace}");
            csWriter.WriteLine("{");
            csWriter.Indent++;

            // Emit top-level methods
            if (moduleDecl.Methods.Any())
            {
                csWriter.WriteLine($"public class {moduleDecl.Name}");
                csWriter.WriteLine("{");
                csWriter.Indent++;
                csWriter.WriteLine();
                foreach (MethodDecl methodDecl in moduleDecl.Methods)
                {
                    if (conductor.TryGetMethodHandler(methodDecl, out var methodHandler))
                    {
                        var methodEnv = methodHandler.Marshal(methodDecl, env.TypeDatabase);
                        methodHandler.Emit(csWriter, swiftWriter, methodEnv, conductor);
                    }
                    else
                    {
                        Console.WriteLine($"No handler found for method {methodDecl.Name}");
                    }
                    // EmitMethod(csWriter, swiftWriter, moduleDecl, moduleDecl, methodDecl);
                    csWriter.WriteLine();
                }
                csWriter.Indent--;
                csWriter.WriteLine("}");
                csWriter.WriteLine();
            }

            // Emit top-level types
            base.HandleBaseDecl(csWriter, swiftWriter, moduleDecl.Types, conductor, env.TypeDatabase);

            csWriter.Indent--;
            csWriter.WriteLine("}");

        }
    }
}
