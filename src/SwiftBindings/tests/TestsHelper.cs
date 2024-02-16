// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace SwiftBindings.Tests
{
    public static class TestsHelper
    {
        public static int CompileAndExecuteFromFileAndString(string filePath, string sourceCode)
        {
            string fileSourceCode = File.ReadAllText(filePath);
            var sourceCodes = new[] { fileSourceCode, sourceCode };
            return CompileAndExecute(sourceCodes);
        }

        private static int CompileAndExecute(string[] sourceCodes)
        {
            var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
            var syntaxTrees = sourceCodes.Select(code => CSharpSyntaxTree.ParseText(code)).ToArray();

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            };


            var compilation = CSharpCompilation.Create("CompiledAssembly",
                                                        syntaxTrees: syntaxTrees,
                                                        references: references,
                                                        options: options);

            string assemblyPath = Path.Combine(Path.GetTempPath(), "CompiledAssembly.dll");
            using (var stream = new FileStream(assemblyPath, FileMode.Create))
            {
                EmitResult emitResult = compilation.Emit(stream);

                if (!emitResult.Success)
                {
                    string errorMessage = "Compilation failed:";
                    foreach (var diagnostic in emitResult.Diagnostics)
                    {
                        errorMessage += $"\n{diagnostic}";
                    }
                    throw new InvalidOperationException(errorMessage);
                }
            }

            Assembly compiledAssembly = Assembly.LoadFile(assemblyPath);

            MethodInfo entryPoint = compiledAssembly.EntryPoint;
            object[] args = entryPoint.GetParameters().Length == 0 ? null : new object[] { new string[0] };
            int result = (int)entryPoint.Invoke(null, args);

            return result;
        }
    }
}