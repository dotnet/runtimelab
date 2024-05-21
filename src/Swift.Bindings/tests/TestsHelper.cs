// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace BindingsGeneration.Tests
{
    public static class TestsHelper
    {
        private static int uniqueId = 0;

        public static string Compile(string[] filePaths, string[] sourceCodes, string[] dependencies)
        {
            var expandedFilePaths = ExpandFilePaths(filePaths);
            Console.WriteLine($"Expanded file paths: {string.Join(", ", expandedFilePaths)}");
            var fileSourceCodes = expandedFilePaths.Select(File.ReadAllText).ToArray();
            var allSourceCodes = fileSourceCodes.Concat(sourceCodes).ToArray();

            var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true);
            var syntaxTrees = allSourceCodes.Select(code => CSharpSyntaxTree.ParseText(code)).ToArray();

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime.InteropServices").Location),
            };

            foreach (string dependency in dependencies)
            {
                references.Add(MetadataReference.CreateFromFile(Assembly.Load(dependency).Location));
            }

            var compilation = CSharpCompilation.Create($"CompiledAssembly{uniqueId}",
                                                        syntaxTrees: syntaxTrees,
                                                        references: references,
                                                        options: options);

            string assemblyPath = Path.Combine(Path.GetTempPath(), $"CompiledAssembly{uniqueId++}.dll");
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

                return assemblyPath;
            }
        }

        public static object Execute(string assemblyPath, string typeName, string methodName, object[] args)
        {
            Assembly compiledAssembly = Assembly.LoadFile(assemblyPath);
            Type targetType = compiledAssembly.GetType(typeName);
            MethodInfo customMethod = targetType.GetMethod(methodName);
            return customMethod.Invoke(null, args);
        }

        private static IEnumerable<string> ExpandFilePaths(IEnumerable<string> filePaths)
        {
            foreach (var path in filePaths)
            {
                if (path.Contains("*"))
                {
                    var dirPath = Path.GetDirectoryName(path);
                    var searchPattern = Path.GetFileName(path);
                    foreach (var expandedPath in Directory.GetFiles(dirPath, searchPattern))
                    {
                        yield return expandedPath;
                    }
                }
                else
                {
                    yield return path;
                }
            }
        }
    }
}
