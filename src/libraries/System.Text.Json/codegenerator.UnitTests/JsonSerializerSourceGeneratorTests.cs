// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using NuGet.Frameworks;
using Xunit.Sdk;

namespace System.Text.Json.CodeGenerator.UnitTests
{
    public class GeneratorTests
    {

        // Helper test classes.
        internal class CallbackGenerator : ISourceGenerator
        {
            private readonly Action<InitializationContext> _onInit;
            private readonly Action<SourceGeneratorContext> _onExecute;

            public CallbackGenerator(Action<InitializationContext> onInit, Action<SourceGeneratorContext> onExecute)
            {
                _onInit = onInit;
                _onExecute = onExecute;
            }

            public void Execute(SourceGeneratorContext context) => _onExecute(context);
            public void Initialize(InitializationContext context) => _onInit(context);
        }

        [Fact]
        public void TypeDiscoveryPrimitivePOCO()
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(@"
            using System;
            using System.Text.Json.Serialization;

              namespace HelloWorld
              {
                [JsonSerializable]
                public class MyType {
                    public int PublicPropertyInt { get; set; }
                    public string PublicPropertyString { get; set; }
                    private int PrivatePropertyInt { get; set; }
                    private string PrivatePropertyString { get; set; }

                    public double PublicDouble;
                    public char PublicChar;
                    private double PrivateDouble;
                    private char PrivateChar;

                    public void MyMethod() { }
                    public void MySecondMethod() { }
                }
              }");

            // Bypass System.Runtime error.
            Assembly systemRuntimeAssembly = Assembly.Load("System.Runtime, Version=5.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            string systemRuntimeAssemblyPath = systemRuntimeAssembly.Location;

            CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            MetadataReference[] references = new MetadataReference[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(JsonSerializableAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(systemRuntimeAssemblyPath),
            };

            Compilation compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees: new[] { tree },
                references: references,
                options: options
            );

            //// Emit the image of the referenced assembly.
            byte[] image = null;
            using (MemoryStream ms = new MemoryStream())
            {
                var emitResult = compilation.Emit(ms);
                if (!emitResult.Success)
                {
                    throw new InvalidOperationException();
                }
                image = ms.ToArray();
            }

            JsonSerializerSourceGenerator generator = new JsonSerializerSourceGenerator();

            GeneratorDriver driver = new CSharpGeneratorDriver(
                new CSharpParseOptions(kind: SourceCodeKind.Regular, documentationMode: DocumentationMode.Parse),
                ImmutableArray.Create<ISourceGenerator>(generator),
                ImmutableArray<AdditionalText>.Empty
                );

            driver.RunFullGeneration(compilation, out _, out _);

            // Check base functionality of found types.
            Assert.Equal(1, generator.foundTypes.Count);
            Assert.Equal("HelloWorld.MyType", generator.foundTypes[0].FullName);

            // Check for received properties in created type.
            string[] expectedPropertyNames = { "PublicPropertyInt", "PublicPropertyString", "PrivatePropertyInt", "PrivatePropertyString" };
            string[] receivedPropertyNames = generator.foundTypes[0].GetProperties().Select(property => property.Name).ToArray();
            Assert.Equal(expectedPropertyNames, receivedPropertyNames);

            // Check for fields in created type.
            string[] expectedFieldNames = { "PublicDouble", "PublicChar", "PrivateDouble", "PrivateChar" };
            string[] receivedFieldNames = generator.foundTypes[0].GetFields().Select(field => field.Name).ToArray();
            Assert.Equal(expectedFieldNames, receivedFieldNames);

            // Check for methods in created type.
            string[] expectedMethodNames = { "get_PublicPropertyInt", "set_PublicPropertyInt", "get_PublicPropertyString", "set_PublicPropertyString", "MyMethod", "MySecondMethod" };
            string[] receivedMethodNames = generator.foundTypes[0].GetMethods().Select(method => method.Name).ToArray();
            Assert.Equal(expectedMethodNames, receivedMethodNames);
        }

        [Fact]
        public void TypeDiscoveryReferencedPrimitivePOCO()
        {
            // Base compilation options.
            CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            // Create a MetadataReference from new code.
            SyntaxTree referenceTree = CSharpSyntaxTree.ParseText(@"
            using System;
              namespace ReferencedAssembly
              {
                public class ReferencedType {
                    public int ReferencedPublicInt;     
                    public double ReferencedPublicDouble;     
                }
            }");

            
            MetadataReference[] referencedReferences = new MetadataReference[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                };

            // Compile the referenced assembly first.
            Compilation referencedCompilation = CSharpCompilation.Create(
                "ReferencedAssembly",
                syntaxTrees: new[] { referenceTree },
                references: referencedReferences,
                options: options
                );

            //// Emit the image of the referenced assembly.
            byte[] referencedImage = null;
            using (MemoryStream ms = new MemoryStream())
            {
                var emitResult = referencedCompilation.Emit(ms);
                if (!emitResult.Success)
                {
                    throw new InvalidOperationException();
                }
                referencedImage = ms.ToArray();
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(@"
            using System;
            using System.Text.Json.Serialization;
            using ReferencedAssembly;

              namespace HelloWorld
              {
                [JsonSerializable]
                public class MyType {
                    public void MyMethod() { }
                    public void MySecondMethod() { }
                }

                [JsonSerializable(typeof(ReferencedType))]
                public static partial class ExternType { }
              }");

            // Bypass System.Runtime error.
            Assembly systemRuntimeAssembly = Assembly.Load("System.Runtime, Version=5.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            string systemRuntimeAssemblyPath = systemRuntimeAssembly.Location;

            MetadataReference[] references = new MetadataReference[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(JsonSerializableAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(systemRuntimeAssemblyPath),
                MetadataReference.CreateFromImage(referencedImage)
            };

            Compilation compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees: new[] { tree },
                references: references,
                options: options
                );

            //// Emit the image of the referenced assembly.
            byte[] image = null;
            using (MemoryStream ms = new MemoryStream())
            {
                var emitResult = compilation.Emit(ms);
                if (!emitResult.Success)
                {
                    IEnumerable<Diagnostic> failures = emitResult.Diagnostics.Where(
                        diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        Assert.Equal("Compilation error", diagnostic.GetMessage());
                    }
                    throw new InvalidOperationException();
                }
                image = ms.ToArray();
            }

            JsonSerializerSourceGenerator generator = new JsonSerializerSourceGenerator();

            GeneratorDriver driver = new CSharpGeneratorDriver(
                new CSharpParseOptions(kind: SourceCodeKind.Regular, documentationMode: DocumentationMode.Parse),
                ImmutableArray.Create<ISourceGenerator>(generator),
                ImmutableArray<AdditionalText>.Empty
                );

            driver.RunFullGeneration(compilation, out _, out _);

            Assert.Equal(2, generator.foundTypes.Count);
        }

        [Fact]
        public void TypeDiscoveryNonExistantPOCO()
        {
        }

        [Fact]
        public void TypeDiscoveryTypeOfSameAssemblyPOCO()
        {
        }

        [Fact]
        public static void SourceGeneratorExecutionFail()
        {
        }
    }
}
