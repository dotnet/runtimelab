// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace System.Text.Json.SourceGeneration
{
    /// <summary>
    /// Base JsonSerializerSourceGenerator. This class will invoke CodeGenerator within Execute
    /// to generate wanted output code for JsonSerializers.
    /// </summary>
    [Generator]
    public class JsonSourceGenerator : ISourceGenerator
    {
        public Dictionary<string, Type>? FoundTypes { get; private set; }

        public void Execute(GeneratorExecutionContext context)
        {
            JsonSerializableSyntaxReceiver receiver = (JsonSerializableSyntaxReceiver)context.SyntaxReceiver;
            MetadataLoadContext metadataLoadContext = new MetadataLoadContext(context.Compilation);

            // Discover serializable types indicated by JsonSerializableAttribute.
            foreach (CompilationUnitSyntax compilationUnit in receiver.CompilationUnits)
            {
                SemanticModel compilationSemanticModel = context.Compilation.GetSemanticModel(compilationUnit.SyntaxTree);

                foreach (AttributeListSyntax attributeListSyntax in compilationUnit.AttributeLists)
                {
                    AttributeSyntax attributeSyntax = attributeListSyntax.Attributes.Single();
                    IMethodSymbol attributeSymbol = compilationSemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;

                    if (attributeSymbol?.ToString().StartsWith("System.Text.Json.Serialization.JsonSerializableAttribute") == true)
                    {
                        // Get JsonSerializableAttribute arguments.
                        AttributeArgumentSyntax attributeArgumentNode = (AttributeArgumentSyntax)attributeSyntax.DescendantNodes().Where(node => node is AttributeArgumentSyntax).SingleOrDefault();

                        // There should be one `Type` parameter in the constructor of the attribute.
                        TypeOfExpressionSyntax typeNode = (TypeOfExpressionSyntax)attributeArgumentNode.ChildNodes().Single();
                        QualifiedNameSyntax typeQualifiedNameSyntax = (QualifiedNameSyntax)typeNode.ChildNodes().Single();

                        INamedTypeSymbol typeSymbol = (INamedTypeSymbol)compilationSemanticModel.GetTypeInfo(typeQualifiedNameSyntax).ConvertedType;
                        Type type = new TypeWrapper(typeSymbol, metadataLoadContext);
                        (FoundTypes ??= new Dictionary<string, Type>())[type.FullName] = type;
                    }
                }
            }

            if (FoundTypes == null)
            {
                return;
            }

            Debug.Assert(FoundTypes.Count >= 1);

            JsonSourceGeneratorHelper codegen = new JsonSourceGeneratorHelper();

            // Add base default instance source.
            context.AddSource("BaseClassInfo.g.cs", SourceText.From(codegen.GenerateHelperContextInfo(), Encoding.UTF8));

            // Run ClassInfo generation for the object graphs of each root type.
            foreach (KeyValuePair<string, Type> entry in FoundTypes)
            {
                codegen.GenerateClassInfo(entry.Value);
            }

            // Add sources for each type to context.
            foreach (KeyValuePair<Type, Tuple<string, string>> entry in codegen.Types)
            {
                context.AddSource($"{entry.Value.Item1}ClassInfo.g.cs", SourceText.From(entry.Value.Item2, Encoding.UTF8));
            }

            // For each diagnostic, report to the user.
            foreach (Diagnostic diagnostic in codegen.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new JsonSerializableSyntaxReceiver());
        }
    }
}
