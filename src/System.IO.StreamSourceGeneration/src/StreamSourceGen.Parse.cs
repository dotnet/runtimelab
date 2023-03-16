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
    public partial class StreamSourceGen
    {
        internal static List<StreamTypeInfo>? Parse(
            Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax> classes,
            CancellationToken cancellationToken)
        {
            INamedTypeSymbol? streamBoilerplateAttributeSymbol = compilation.GetBestTypeByMetadataName(StreamBoilerplateAttributeFullName);
            INamedTypeSymbol? streamSymbol = compilation.GetBestTypeByMetadataName(StreamFullName);

            if (streamBoilerplateAttributeSymbol == null || streamSymbol == null)
            {
                return null;
            }

            List<StreamTypeInfo>? retVal = null;

            foreach (IGrouping<SyntaxTree, ClassDeclarationSyntax> group in classes.GroupBy(c => c.SyntaxTree))
            {
                SyntaxTree syntaxTree = group.Key;
                SemanticModel compilationSemanticModel = compilation.GetSemanticModel(syntaxTree);

                foreach (ClassDeclarationSyntax classNode in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!DerivesFromStream(classNode, streamSymbol, compilationSemanticModel, cancellationToken))
                    {
                        continue;
                    }

                    INamedTypeSymbol typeSymbol = compilationSemanticModel.GetDeclaredSymbol(classNode, cancellationToken)!;

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

        // TODO: merge this method with DerivesFromJsonSerializerContext.
        // https://github.com/dotnet/runtime/blob/30cc26fae6439707097ebd07145e8600e99a416c/src/libraries/System.Text.Json/gen/JsonSourceGenerator.Parser.cs#L400
        internal static bool DerivesFromStream(
            ClassDeclarationSyntax classDeclarationSyntax,
            INamedTypeSymbol streamSymbol,
            SemanticModel compilationSemanticModel,
            CancellationToken cancellationToken)
        {
            SeparatedSyntaxList<BaseTypeSyntax>? baseTypeSyntaxList = classDeclarationSyntax.BaseList?.Types;
            if (baseTypeSyntaxList == null)
            {
                return false;
            }

            INamedTypeSymbol? match = null;

            foreach (BaseTypeSyntax baseTypeSyntax in baseTypeSyntaxList)
            {
                INamedTypeSymbol? candidate = compilationSemanticModel.GetSymbolInfo(baseTypeSyntax.Type, cancellationToken).Symbol as INamedTypeSymbol;
                if (candidate != null && streamSymbol.Equals(candidate, SymbolEqualityComparer.Default))
                {
                    match = candidate;
                    break;
                }
            }

            return match != null;
        }
    }
}
