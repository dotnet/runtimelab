// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Linq;
using Xunit;

namespace System.IO.StreamSourceGeneration.Tests
{
    public static class CompilationHelper
    {
        public static Compilation CreateCompilation(
            string source,
            MetadataReference[] additionalReferences = null,
            string assemblyName = "TestAssembly")
        {

            string coreLib = Assembly.GetAssembly(typeof(object))!.Location;
            string runtimeDir = Path.GetDirectoryName(coreLib)!;

            List<MetadataReference> references = new()
            {
                MetadataReference.CreateFromFile(coreLib),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(GenerateStreamBoilerplateAttribute).Assembly.Location)
            };

            if (additionalReferences != null)
            {
                references.AddRange(additionalReferences);
            }

            return CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
                references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
        }

        public static ImmutableArray<Diagnostic> RunSourceGenerator(Compilation compilation)
        {
            CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(new StreamSourceGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation newCompilation, out ImmutableArray<Diagnostic> diagnostics);

            newCompilation.GetDiagnostics().AssertMaxSeverity(DiagnosticSeverity.Info);
            diagnostics.AssertMaxSeverity(DiagnosticSeverity.Info);

            return diagnostics;
        }

        internal static void AssertMaxSeverity(this IEnumerable<Diagnostic> diagnostics, DiagnosticSeverity maxSeverity)
        {
            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity > maxSeverity));
        }
    }
}
