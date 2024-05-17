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
        // Private properties
        private readonly string _outputDirectory;
        private readonly TypeDatabase _typeDatabase;
        private readonly int _verbose;
        private readonly Conductor _conductor;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringCSharpEmitter"/> class.
        /// </summary>
        public StringCSharpEmitter(string outputDirectory, TypeDatabase typeDatabase, int verbose = 0)
        {
            _outputDirectory = outputDirectory;
            _typeDatabase = typeDatabase;
            _verbose = verbose;
            _conductor = new Conductor();
        }

        /// <summary>
        /// Emits a C# module based on the module declaration.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        public void EmitModule(ModuleDecl moduleDecl)
        {
            if (_conductor.TryGetModuleHandler(moduleDecl, out var moduleHandler))
            {
                var sw = new StringWriter();
                IndentedTextWriter writer = new(sw);
                var @namespace = $"Swift.{moduleDecl.Name}";

                var env = moduleHandler.Marshal(moduleDecl);
                moduleHandler.Emit(writer, env, _conductor, _typeDatabase);

                string csOutputPath = Path.Combine(_outputDirectory, $"{@namespace}.cs");
                using (StreamWriter outputFile = new(csOutputPath))
                {
                    outputFile.Write(sw.ToString());
                }
            }
            else
            {
                if (_verbose > 0)
                    Console.WriteLine($"No module handler found for {moduleDecl.Name}");
            }
        }
    }
}
