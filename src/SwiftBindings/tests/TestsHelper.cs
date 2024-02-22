// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace SwiftBindings.Tests
{
    public static class TestsHelper
    {
        public static object CompileAndExecute(string filePath, string sourceCode, string typeName, string methodName)
        {
            string fileSourceCode = File.ReadAllText(filePath);
            var sourceCodes = new[] { fileSourceCode, sourceCode };
            return CompileAndExecute(sourceCodes, typeName, methodName);
        }

        private static object CompileAndExecute(string[] sourceCodes, string typeName, string methodName)
        {
            var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
            var syntaxTrees = sourceCodes.Select(code => CSharpSyntaxTree.ParseText(code)).ToArray();
            var systemRuntimeAssemblyPath = Assembly.Load("System.Runtime").Location;

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(systemRuntimeAssemblyPath),
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

            AssemblyLoadContext context = new AssemblyLoadContext("", true);
            object result = null;
            try
            {
            Assembly compiledAssembly = context.LoadFromAssemblyPath(assemblyPath);

            Type targetType = compiledAssembly.GetType("Test.MainClass");
            MethodInfo customMethod = targetType.GetMethod(methodName);
            result = customMethod.Invoke(null, new object[] { });
            }
            finally
            {
                context.Unload();
            }
            return result;
        }
    }
}