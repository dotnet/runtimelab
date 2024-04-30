// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an string-based C# emitter.
    /// </summary>
    public partial class StringCSharpEmitter : ICSharpEmitter
    {
        // Literals in generated source
        private const string PInvokePrefix = "PIfunc_";

        // Private properties
        private readonly string _outputDirectory;
        private readonly TypeDatabase _typeDatabase;
        private readonly int _verbose;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringCSharpEmitter"/> class.
        /// </summary>
        public StringCSharpEmitter(string outputDirectory, TypeDatabase typeDatabase, int verbose = 0)
        {
            _outputDirectory = outputDirectory;
            _typeDatabase = typeDatabase;
            _verbose = verbose;
        }

        /// <summary>
        /// Emits a C# module based on the module declaration.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        public void EmitModule(ModuleDecl moduleDecl)
        {
            var sw = new StringWriter();
            IndentedTextWriter writer = new(sw);
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
                    EmitMethod(writer, moduleDecl, moduleDecl, methodDecl);
                    writer.WriteLine();
                }
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine();
            }

            // Emit top-level types
            foreach (BaseDecl baseDecl in moduleDecl.Declarations)
            {
                EmitBaseDecl(writer, moduleDecl, moduleDecl, baseDecl);
                writer.WriteLine();
            }

            writer.Indent--;
            writer.WriteLine("}");

            string csOutputPath = Path.Combine(_outputDirectory, $"{generatedNamespace}.cs");
            using (StreamWriter outputFile = new StreamWriter(csOutputPath))
            {
                outputFile.Write(sw.ToString());
            }
        }

        /// <summary>
        /// Emits a base declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The base declaration.</param>
        private void EmitBaseDecl(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, BaseDecl baseDecl)
        {
            if (baseDecl is StructDecl structDecl)
                EmitStruct(writer, moduleDecl, parentDecl, structDecl);
            else if (baseDecl is ClassDecl classDecl)
                EmitClass(writer, moduleDecl, parentDecl, classDecl);
            else if (baseDecl is MethodDecl methodDecl)
                EmitMethod(writer, moduleDecl, parentDecl, methodDecl);
            else
                throw new NotImplementedException($"Unsupported declaration type: {baseDecl.GetType().Name}");
        }
    }
}
