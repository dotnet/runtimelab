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
    internal class Helpers
    {
        internal static IEnumerable<string> GetOverriddenMembers(ITypeSymbol symbol)
        {
            return symbol.GetMembers().Select(m => GetOverriddenMember(m)?.ToDisplayString()).Where(s => s != null)!;

            static ISymbol? GetOverriddenMember(ISymbol member)
                => member switch
                {
                    IMethodSymbol method => method.OverriddenMethod,
                    IPropertySymbol property => property.OverriddenProperty,
                    _ => null
                };
        }

        /// <summary>
        /// Gets the boilerplate/template for the specific combination of the specified <paramref name="member"/>
        /// and <paramref name="memberToCall"/>
        /// </summary>
        /// <returns>The template to use for the specified combination or <see langword="null" />
        /// if its best to call the base implementation.</returns>
        internal static string? GetMemberToCallForTemplate(StreamMember member, StreamMember memberToCall)
        {
            return member switch
            {
                StreamMember.ReadBytes => memberToCall switch
                {
                    StreamMember.ReadSpan => StreamBoilerplateConstants.ReadBytesCallsToReadSpan,
                    StreamMember.ReadAsyncBytes => StreamBoilerplateConstants.ReadBytesCallsToReadAsyncBytes,
                    StreamMember.ReadAsyncMemory => StreamBoilerplateConstants.ReadBytesCallsToReadAsyncMemory,
                    _ => throw new InvalidOperationException()
                },
                StreamMember.ReadSpan => memberToCall switch
                {
                    StreamMember.ReadBytes => null,
                    StreamMember.ReadAsyncBytes => StreamBoilerplateConstants.ReadSpanCallsToReadAsyncBytes,
                    StreamMember.ReadAsyncMemory => StreamBoilerplateConstants.ReadSpanCallsToReadAsyncMemory,
                    _ => throw new InvalidOperationException()
                },
                StreamMember.ReadAsyncBytes => memberToCall switch
                {
                    StreamMember.ReadBytes => StreamBoilerplateConstants.ReadAsyncBytesCallsToReadBytes,
                    StreamMember.ReadSpan => StreamBoilerplateConstants.ReadAsyncBytesCallsToReadSpan,
                    StreamMember.ReadAsyncMemory => StreamBoilerplateConstants.ReadAsyncBytesCallsToReadAsyncMemory,
                    _ => throw new InvalidOperationException()
                },
                StreamMember.ReadAsyncMemory => memberToCall switch
                {
                    StreamMember.ReadBytes => StreamBoilerplateConstants.ReadAsyncMemoryCallsToReadBytes,
                    StreamMember.ReadSpan => null,
                    StreamMember.ReadAsyncBytes => null,
                    _ => throw new InvalidOperationException()
                },
                StreamMember.WriteBytes => memberToCall switch
                {
                    StreamMember.WriteSpan => StreamBoilerplateConstants.WriteBytesCallsToWriteSpan,
                    StreamMember.WriteAsyncBytes => StreamBoilerplateConstants.WriteBytesCallsToWriteAsyncBytes,
                    StreamMember.WriteAsyncMemory => StreamBoilerplateConstants.WriteBytesCallsToWriteAsyncMemory,
                    _ => throw new InvalidOperationException()
                },
                StreamMember.WriteSpan => memberToCall switch
                {
                    StreamMember.WriteBytes => null,
                    StreamMember.WriteAsyncBytes => StreamBoilerplateConstants.WriteSpanCallsToWriteAsyncBytes,
                    StreamMember.WriteAsyncMemory => StreamBoilerplateConstants.WriteSpanCallsToWriteAsyncMemory,
                    _ => throw new InvalidOperationException()
                },
                StreamMember.WriteAsyncBytes => memberToCall switch
                {
                    StreamMember.WriteBytes => StreamBoilerplateConstants.WriteAsyncBytesCallsToWriteBytes,
                    StreamMember.WriteSpan => StreamBoilerplateConstants.WriteAsyncBytesCallsToWriteSpan,
                    StreamMember.WriteAsyncMemory => StreamBoilerplateConstants.WriteAsyncBytesCallsToWriteAsyncMemory,
                    _ => throw new InvalidOperationException()
                },
                StreamMember.WriteAsyncMemory => memberToCall switch
                {
                    StreamMember.WriteBytes => StreamBoilerplateConstants.WriteAsyncMemoryCallsToWriteBytes,
                    StreamMember.WriteSpan => null,
                    StreamMember.WriteAsyncBytes => null,
                    _ => throw new InvalidOperationException()
                },
                _ => throw new InvalidOperationException()
            };
        }

        internal static List<StreamTypeInfo>? GetClassesWithGenerationOptions(
            Compilation compilation, 
            ImmutableArray<ClassDeclarationSyntax> classes, 
            CancellationToken cancellationToken)
        {
            INamedTypeSymbol? streamBoilerplateAttributeSymbol = compilation.GetBestTypeByMetadataName(StreamSourceGen.StreamBoilerplateAttributeFullName);
            INamedTypeSymbol? streamSymbol = compilation.GetBestTypeByMetadataName(StreamSourceGen.StreamFullName);

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
            Threading.CancellationToken cancellationToken)
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