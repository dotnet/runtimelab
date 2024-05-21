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
        public IModuleHandler Construct ()
        {
            return new ModuleHandler();
        }
    }

    /// <summary>
    /// Handler class for module declarations.
    /// </summary>
    public class ModuleHandler : BaseHandler, IModuleHandler
    {
        public ModuleHandler ()
        {
        }

        /// <summary>
        /// Marshals the module declaration.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        public IEnvironment Marshal(BaseDecl moduleDecl)
        {
            return new ModuleEnvironment(moduleDecl);
        }

        /// <summary>
        /// Emits the code for the specified environment.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor, TypeDatabase typeDatabase)
        {
            var moduleEnv = (ModuleEnvironment)env;
            var moduleDecl = (ModuleDecl)moduleEnv.ModuleDecl;
            
            var generatedNamespace = $"Swift.{moduleDecl.Name}";

            writer.WriteLine($"using System;");
            writer.WriteLine($"using System.Runtime.CompilerServices;");
            writer.WriteLine($"using System.Runtime.InteropServices;");
            writer.WriteLine($"using System.Runtime.InteropServices.Swift;");
            foreach (var dependency in moduleDecl.Dependencies)
                writer.WriteLine(dependency.Contains("Swift.") ? $"using {dependency};" : $"using Swift.{dependency};");


            writer.WriteLine();
            writer.WriteLine($"namespace {generatedNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            // Emit top-level fields and pinvokes
            if (moduleDecl.Methods.Any() || moduleDecl.Fields.Any())
            {
                writer.WriteLine($"public class {moduleDecl.Name}");
                writer.WriteLine("{");
                writer.Indent++;
                foreach(FieldDecl fieldDecl in moduleDecl.Fields)
                {
                    string accessModifier = fieldDecl.Visibility == Visibility.Public ? "public" : "private";
                    writer.WriteLine($"{accessModifier} {fieldDecl.TypeIdentifier.Name} {fieldDecl.Name};");
                }
                writer.WriteLine();
                foreach(MethodDecl methodDecl in moduleDecl.Methods)
                {
                    if (conductor.TryGetMethodHandler(methodDecl, out var methodHandler))
                    {
                        var methodEnv = methodHandler.Marshal(methodDecl);
                        methodHandler.Emit(writer, methodEnv, conductor, typeDatabase);
                    }
                    else
                    {
                        Console.WriteLine($"No handler found for method {methodDecl.Name}");
                    }
                    // EmitMethod(writer, moduleDecl, moduleDecl, methodDecl);
                    writer.WriteLine();
                }
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
            }

            // Emit top-level types
            base.HandleBaseDecl(writer, moduleDecl.Declarations, conductor, typeDatabase);

            writer.Indent--;
            writer.WriteLine("}");

        }
    }
}
