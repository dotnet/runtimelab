// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Microsoft.Extensions.Logging;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an string-based C# emitter.
    /// </summary>
    public partial class StringEmitter : IEmitter
    {
        // Private properties
        private readonly string _outputDirectory;
        private readonly ITypeDatabase _typeDatabase;
        private readonly ILogger _logger;
        private readonly Conductor _conductor;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEmitter"/> class.
        /// </summary>
        public StringEmitter(string outputDirectory, ITypeDatabase typeDatabase, ILoggerFactory loggerFactory)
        {
            _outputDirectory = outputDirectory;
            _typeDatabase = typeDatabase;
            _logger = loggerFactory.CreateLogger<StringEmitter>();
            _conductor = new Conductor(loggerFactory);
        }

        /// <summary>
        /// Emits a C# module based on the module declaration.
        /// </summary>
        /// <param name="moduleDecl">The module declaration.</param>
        public void EmitModule(ModuleDecl moduleDecl)
        {
            if (_conductor.TryGetModuleHandler(moduleDecl, out var moduleHandler))
            {
                var csStringWriter = new StringWriter();
                CSharpWriter csWriter = new(csStringWriter);
                var swiftStringWriter = new StringWriter();
                SwiftWriter swiftWriter = new(swiftStringWriter);
                var @namespace = $"Swift.{moduleDecl.Name}";

                var env = moduleHandler.Marshal(moduleDecl, _typeDatabase);
                moduleHandler.Emit(csWriter, swiftWriter, env, _conductor);

                string csOutputPath = Path.Combine(_outputDirectory, $"{@namespace}.cs");
                using (StreamWriter outputFile = new(csOutputPath))
                {
                    outputFile.Write(csStringWriter.ToString());
                }
                string swiftOutputPath = Path.Combine(_outputDirectory, $"{@namespace}.swift");
                using (StreamWriter outputFile = new(swiftOutputPath))
                {
                    outputFile.Write(swiftStringWriter.ToString());
                }
            }
            else
            {
                _logger.LogWarning($"No module handler found for {moduleDecl.Name}");
            }
        }
    }
}
