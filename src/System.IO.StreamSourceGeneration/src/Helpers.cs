using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.DotnetRuntime.Extensions;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
//using System.Threading;

namespace System.IO.StreamSourceGeneration;

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

    internal static string GetMemberToCallForTemplate(string candidateName, string memberToCall)
    {
        return candidateName switch
        {
            StreamMembersConstants.ReadByte => memberToCall switch
            {
                StreamMembersConstants.ReadSpan => StreamBoilerplateConstants.ReadByteCallsToReadSpan,
                StreamMembersConstants.ReadAsyncByte => StreamBoilerplateConstants.ReadByteCallsToReadAsyncByte,
                StreamMembersConstants.ReadAsyncMemory => StreamBoilerplateConstants.ReadByteCallsToReadAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.ReadSpan => memberToCall switch
            {
                StreamMembersConstants.ReadByte => StreamBoilerplateConstants.ReadSpanCallsToReadByte,
                StreamMembersConstants.ReadAsyncByte => StreamBoilerplateConstants.ReadSpanCallsToReadAsyncByte,
                StreamMembersConstants.ReadAsyncMemory => StreamBoilerplateConstants.ReadSpanCallsToReadAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.ReadAsyncByte => memberToCall switch
            {
                StreamMembersConstants.ReadByte => StreamBoilerplateConstants.ReadAsyncByteCallsToReadByte,
                StreamMembersConstants.ReadSpan => StreamBoilerplateConstants.ReadAsyncByteCallsToReadSpan,
                StreamMembersConstants.ReadAsyncMemory => StreamBoilerplateConstants.ReadAsyncByteCallsToReadAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.ReadAsyncMemory => memberToCall switch
            {
                StreamMembersConstants.ReadByte => StreamBoilerplateConstants.ReadAsyncMemoryCallsToReadByte,
                StreamMembersConstants.ReadSpan => StreamBoilerplateConstants.ReadAsyncMemoryCallsToReadSpan,
                StreamMembersConstants.ReadAsyncByte => StreamBoilerplateConstants.ReadAsyncMemoryCallsToReadAsyncByte,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.WriteByte => memberToCall switch
            {
                StreamMembersConstants.WriteSpan => StreamBoilerplateConstants.WriteByteCallsToWriteSpan,
                StreamMembersConstants.WriteAsyncByte => StreamBoilerplateConstants.WriteByteCallsToWriteAsyncByte,
                StreamMembersConstants.WriteAsyncMemory => StreamBoilerplateConstants.WriteByteCallsToWriteAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.WriteSpan => memberToCall switch
            {
                StreamMembersConstants.WriteByte => StreamBoilerplateConstants.WriteSpanCallsToWriteByte,
                StreamMembersConstants.WriteAsyncByte => StreamBoilerplateConstants.WriteSpanCallsToWriteAsyncByte,
                StreamMembersConstants.WriteAsyncMemory => StreamBoilerplateConstants.WriteSpanCallsToWriteAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.WriteAsyncByte => memberToCall switch
            {
                StreamMembersConstants.WriteByte => StreamBoilerplateConstants.WriteAsyncByteCallsToWriteByte,
                StreamMembersConstants.WriteSpan => StreamBoilerplateConstants.WriteAsyncByteCallsToWriteSpan,
                StreamMembersConstants.WriteAsyncMemory => StreamBoilerplateConstants.WriteAsyncByteCallsToWriteAsyncMemory,
                _ => throw new InvalidOperationException()
            },
            StreamMembersConstants.WriteAsyncMemory => memberToCall switch
            {
                StreamMembersConstants.WriteByte => StreamBoilerplateConstants.WriteAsyncMemoryCallsToWriteByte,
                StreamMembersConstants.WriteSpan => StreamBoilerplateConstants.WriteAsyncMemoryCallsToWriteSpan,
                StreamMembersConstants.WriteAsyncByte => StreamBoilerplateConstants.WriteAsyncMemoryCallsToWriteAsyncByte,
                _ => throw new InvalidOperationException()
            },
            _ => throw new InvalidOperationException()
        };
    }

    internal static List<StreamTypeInfo>? GetClassesWithGenerationOptions(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, Threading.CancellationToken cancellationToken)
    {
        INamedTypeSymbol? streamBoilerplateAttributeSymbol = compilation.GetBestTypeByMetadataName(StreamSourceGen.StreamBoilerplateAttributeFullName);
        INamedTypeSymbol? streamSymbol = compilation.GetBestTypeByMetadataName(StreamSourceGen.StreamFullName);

        if (streamBoilerplateAttributeSymbol == null ||
            streamSymbol == null)
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
