// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.DotnetRuntime.Extensions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace System.IO.StreamSourceGeneration
{
    public partial class StreamSourceGenerator
    {
        internal static List<StreamTypeInfo>? Parse(
            Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax> classes,
            SourceProductionContext context)
        {
            INamedTypeSymbol? streamBoilerplateAttributeSymbol = compilation.GetBestTypeByMetadataName(StreamBoilerplateAttributeFullName);
            INamedTypeSymbol? streamSymbol = compilation.GetBestTypeByMetadataName(StreamFullName);

            if (streamBoilerplateAttributeSymbol == null || streamSymbol == null)
            {
                return null;
            }

            CancellationToken cancellationToken = context.CancellationToken;
            List<StreamTypeInfo>? retVal = null;

            foreach (IGrouping<SyntaxTree, ClassDeclarationSyntax> group in classes.GroupBy(c => c.SyntaxTree))
            {
                SyntaxTree syntaxTree = group.Key;
                SemanticModel compilationSemanticModel = compilation.GetSemanticModel(syntaxTree);

                foreach (ClassDeclarationSyntax classNode in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    INamedTypeSymbol typeSymbol = compilationSemanticModel.GetDeclaredSymbol(classNode, cancellationToken)!;

                    if (!streamSymbol.IsAssignableFrom(typeSymbol))
                    {
                        context.ReportDiagnostic(CreateDiagnostic(s_boilerplateAttributeOnNonStreamType, typeSymbol));
                        continue;
                    }

                    foreach (AttributeListSyntax attributeListSyntax in classNode.AttributeLists)
                    {
                        AttributeSyntax attributeSyntax = attributeListSyntax.Attributes.First();
                        IMethodSymbol? attributeSymbol = compilationSemanticModel.GetSymbolInfo(attributeSyntax, cancellationToken).Symbol as IMethodSymbol;

                        if (attributeSymbol == null ||
                            !streamBoilerplateAttributeSymbol.Equals(attributeSymbol.ContainingType, SymbolEqualityComparer.Default))
                        {
                            // badly formed attribute definition, or not the right attribute
                            continue;
                        }

                        StreamTypeInfo streamTypeInfo = new(typeSymbol);
                        retVal ??= new List<StreamTypeInfo>();
                        retVal.Add(streamTypeInfo);
                    }
                }
            }

            return retVal;
        }
    }
}
