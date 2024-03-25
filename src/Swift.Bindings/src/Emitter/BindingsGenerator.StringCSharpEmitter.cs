// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an string-based C# emitter.
    /// </summary>
    public class StringCSharpEmitter : ICSharpEmitter
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

            var generatedNamespace = $"{moduleDecl.Name}Bindings";
            foreach (var dependency in moduleDecl.Dependencies)
            {
                writer.WriteLine($"using {dependency};");
            }
            writer.WriteLine();
            writer.WriteLine($"namespace {generatedNamespace}");
            writer.WriteLine($"{{");

            writer.Indent++;
            writer.WriteLine($"public unsafe class {moduleDecl.Name} {{");

            writer.Indent++;
            foreach (MethodDecl methodDecl in moduleDecl.Methods)
            {
                if (_verbose > 0)
                    Console.WriteLine($"Emitting method: {methodDecl.Name}");
                EmitPInvoke(writer, moduleDecl, methodDecl);
                if (methodDecl.RequireMarshalling)
                    EmitMethod(writer, methodDecl);
            }
            writer.Indent--;
            writer.WriteLine($"}}");
            writer.Indent--;
            writer.WriteLine($"}}");

            string csOutputPath = Path.Combine(_outputDirectory, $"{generatedNamespace}.cs");
            using StreamWriter outputFile = new StreamWriter(csOutputPath);
            outputFile.Write(sw.ToString());
        }

        /// <summary>
        /// Emits the P/Invoke declaration for a method.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="methodDecl">The method declaration.</param>
        public void EmitPInvoke(IndentedTextWriter writer, ModuleDecl moduleDecl, MethodDecl methodDecl)
        {
            writer.WriteLine("[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]");
            writer.WriteLine($"[DllImport(\"lib{moduleDecl.Name}.dylib\", EntryPoint = \"{methodDecl.MangledName}\")]");
            writer.Write($"internal static extern {methodDecl.Signature.First().FullyQualifiedName} {(methodDecl.RequireMarshalling ? PInvokePrefix : String.Empty)}{methodDecl.Name}(");
            EmitMethodParams(writer, methodDecl.Signature);
            writer.WriteLine($");");
        }

        /// <summary>
        /// Emits the method declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="decl">The method declaration.</param>
        public void EmitMethod(IndentedTextWriter writer, MethodDecl decl)
        {
            writer.Write($"public static {decl.Signature.First().FullyQualifiedName} {decl.Name}(");
            EmitMethodParams(writer, decl.Signature);
            writer.WriteLine($")");
            writer.WriteLine($"{{");

            writer.Indent++;
            if (decl.Signature.First().FullyQualifiedName != "void")
                writer.Write("return ");
            writer.Write($"{PInvokePrefix}_{decl.Name}(");
            EmitMethodArgs(writer, decl.Signature);
            writer.WriteLine($");");

            writer.Indent--;
            writer.WriteLine($"}}");
        }

        /// <summary>
        /// Emits the parameters of a method.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="signature">The signature of the method.</param>
        public void EmitMethodParams(IndentedTextWriter writer, IEnumerable<TypeDecl> signature) {
            var signatureList = signature.ToList();
            for (int i = 1; i < signatureList.Count; i++)
            {
                var param = signatureList[i];
                writer.Write($"{param.FullyQualifiedName} {param.Name}");
                if (i < signatureList.Count - 1)
                    writer.Write(", ");
            }
        }

        /// <summary>
        /// Emits invocation arguments of a method.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="signature">The signature of the method.</param>
        public void EmitMethodArgs(IndentedTextWriter writer, IEnumerable<TypeDecl> signature) {
            var signatureList = signature.ToList();
            for (int i = 1; i < signatureList.Count; i++)
            {
                var param = signatureList[i];
                writer.Write($"{param.Name}");
                if (i < signatureList.Count - 1)
                    writer.Write(", ");
            }
        }
    }
}
