using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.Interop
{
    internal static class MarshallerHelpers
    {
        public static readonly ExpressionSyntax IsWindows = InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            ParseTypeName("System.OperatingSystem"),
                                                            IdentifierName("IsWindows")));

        public static readonly TypeSyntax InteropServicesMarshalType = ParseTypeName(TypeNames.System_Runtime_InteropServices_Marshal);

        public static readonly TypeSyntax SystemIntPtrType = ParseTypeName("System.IntPtr");

        public static ForStatementSyntax GetForLoop(string collectionIdentifier, string indexerIdentifier)
        {
            // for(int <indexerIdentifier> = 0; <indexerIdentifier> < <collectionIdentifier>.Length; ++<indexerIdentifier>)
            //      ;
            return ForStatement(EmptyStatement())
            .WithDeclaration(
                VariableDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.IntKeyword)))
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(
                            Identifier(indexerIdentifier))
                        .WithInitializer(
                            EqualsValueClause(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(0)))))))
            .WithCondition(
                BinaryExpression(
                    SyntaxKind.LessThanExpression,
                    IdentifierName(indexerIdentifier),
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(collectionIdentifier),
                        IdentifierName("Length"))))
            .WithIncrementors(
                SingletonSeparatedList<ExpressionSyntax>(
                    PrefixUnaryExpression(
                        SyntaxKind.PreIncrementExpression,
                        IdentifierName(indexerIdentifier))));
        }

        public static LocalDeclarationStatementSyntax DeclareWithDefault(TypeSyntax typeSyntax, string identifier)
        {
            // <type> <identifier> = default;
            return LocalDeclarationStatement(
                VariableDeclaration(
                    typeSyntax,
                    SingletonSeparatedList(
                        VariableDeclarator(identifier)
                            .WithInitializer(
                                EqualsValueClause(
                                    LiteralExpression(SyntaxKind.DefaultLiteralExpression))))));
        }

        public static RefKind GetRefKindForByValueContentsKind(this ByValueContentsMarshalKind byValue)
        {
            return byValue switch
            {
                ByValueContentsMarshalKind.Default => RefKind.None,
                ByValueContentsMarshalKind.In => RefKind.In,
                ByValueContentsMarshalKind.InOut => RefKind.Ref,
                ByValueContentsMarshalKind.Out => RefKind.Out,
                _ => throw new System.ArgumentOutOfRangeException(nameof(byValue))
            };
        }

        public static TypeSyntax GetCompatibleGenericTypeParameterSyntax(this TypeSyntax type)
        {
            TypeSyntax spanElementTypeSyntax = type;
            if (spanElementTypeSyntax is PointerTypeSyntax)
            {
                // Pointers cannot be passed to generics, so use IntPtr for this case.
                spanElementTypeSyntax = SystemIntPtrType;
            }
            return spanElementTypeSyntax;
        }

        public static string GetMarshallerIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return context.GetAdditionalIdentifier(info, "marshaller");
        }

        public static string GetNativeSpanIdentifier(TypePositionInfo info, StubCodeContext context)
        {
            return context.GetAdditionalIdentifier(info, "nativeSpan");
        }

        /// <summary>
        /// Generate a topologically sorted collection of elements.
        /// </summary>
        /// <typeparam name="T">The type of element.</typeparam>
        /// <param name="elements">The initial collection of elements.</param>
        /// <param name="indexFn">A function to create an element index value. This value must be non-negative and the maximum value is recommended to be close to <c><paramref name="elements"/>.Count</c> for best performance.</param>
        /// <param name="getDependentIndicesFn">A function to resolve the dependencies of a given item in the <paramref name="elements"/> collection as index values that would be returned by <paramref name="indexFn"/>.</param>
        /// <returns>A topologically sorted collection of the elemens of the <paramref name="elements"/> collection.</returns>
        /// <exception cref="InvalidOperationException">The graph of <paramref name="elements"/> nodes and the edges produced by <paramref name="getDependentIndicesFn"/> has cycles.</exception>
        public static IEnumerable<T> GetTopologicallySortedElements<T>(
            ICollection<T> elements,
            Func<T, int> indexFn,
            Func<T, IEnumerable<int>> getDependentIndicesFn)
            where T : IEquatable<T?>
        {
            int highestManagedIndex = -1;
            foreach (var element in elements)
            {
                highestManagedIndex = Math.Max(indexFn(element), highestManagedIndex);
            }

            T[] elementByElementIndex = new T[highestManagedIndex + 1];
            foreach (var element in elements)
            {
                elementByElementIndex[indexFn(element)] = element;
            }

            // edgeMap contains a map of boolean values denoting if an edge exists
            // If edgeMap[X][Y] is true, that means that there exists an edge Y -> X
            bool[][] edgeMap = new bool[highestManagedIndex + 1][];
            for (int i = 0; i < edgeMap.Length; i++)
            {
                edgeMap[i] = new bool[highestManagedIndex + 1];
            }

            foreach (var element in elements)
            {
                int elementIndex = indexFn(element);
                foreach (var dependentElementIndex in getDependentIndicesFn(element))
                {
                    // Add an edge from dependentElementIndex->elementIndex
                    // This way, elements that have no dependencies have no edges pointing to them.
                    edgeMap[elementIndex][dependentElementIndex] = true;
                }
            }

            // Now that we have initialized our map of edges and we have our list of nodes,
            // we'll use Khan's algorithm to calculate a topological sort of the elements.
            // Algorithm adapted from A. B. Kahn. 1962. Topological sorting of large networks. Commun. ACM 5, 11 (Nov. 1962), 558–562. DOI:https://doi.org/10.1145/368996.369025

            // L is the sorted list
            List<T> L = new List<T>(elements.Count);
            // S is the set of elements with no incoming edges (no dependencies on it)
            List<T> S = new List<T>(elements.Count);

            // Initialize S
            for (int elementIndex = 0; elementIndex <= highestManagedIndex; elementIndex++)
            {
                if (elementByElementIndex[elementIndex].Equals(default))
                {
                    continue;
                }
                bool anyIncomingEdges = Array.IndexOf(edgeMap[elementIndex], true) != -1;
                if (!anyIncomingEdges)
                {
                    S.Add(elementByElementIndex[elementIndex]);
                }
            }

            while (S.Count != 0)
            {
                // Remove element from S
                T element = S[S.Count - 1];
                S.RemoveAt(S.Count - 1);
                // Add element to L
                L.Add(element);
                // For each node m that element points to
                for (int i = 0; i < edgeMap.Length; i++)
                {
                    int elementIndex = indexFn(element);
                    if (!edgeMap[i][elementIndex])
                    {
                        continue;
                    }
                    // Remove the edge from element to m
                    edgeMap[i][elementIndex] = false;
                    // If m does not have any incoming edges, add to S
                    bool anyIncomingEdges = Array.IndexOf(edgeMap[i], true) != -1;
                    if (!anyIncomingEdges)
                    {
                        S.Add(elementByElementIndex[i]);
                    }
                }
            }

            // If we have edges left, then we have a cycle.
            for (int i = 0; i < edgeMap.Length; i++)
            {
                for (int j = 0; j < edgeMap.Length; j++)
                {
                    if (edgeMap[i][j])
                    {
                        throw new InvalidOperationException();
                    }
                }
            }

            // If we make it here, we have a topologically sorted list.
            return L;
        }

        public static IEnumerable<TypePositionInfo> GetDependentElementsOfMarshallingInfo(
            MarshallingInfo elementMarshallingInfo)
        {
            if (elementMarshallingInfo is NativeContiguousCollectionMarshallingInfo nestedCollection)
            {
                if (nestedCollection.ElementCountInfo is CountElementCountInfo { ElementInfo: TypePositionInfo nestedCountElement })
                {
                    yield return nestedCountElement;
                }
                foreach (var nestedElements in GetDependentElementsOfMarshallingInfo(nestedCollection.ElementMarshallingInfo))
                {
                    yield return nestedElements;
                }
            }
        }

        public static class StringMarshaller
        {
            public static ExpressionSyntax AllocationExpression(CharEncoding encoding, string managedIdentifier)
            {
                string methodName = encoding switch
                {
                    CharEncoding.Utf8 => "StringToCoTaskMemUTF8", // Not in .NET Standard 2.0, so we use the hard-coded name 
                    CharEncoding.Utf16 => nameof(System.Runtime.InteropServices.Marshal.StringToCoTaskMemUni),
                    CharEncoding.Ansi => nameof(System.Runtime.InteropServices.Marshal.StringToCoTaskMemAnsi),
                    _ => throw new System.ArgumentOutOfRangeException(nameof(encoding))
                };

                // Marshal.StringToCoTaskMemUTF8(<managed>)
                // or
                // Marshal.StringToCoTaskMemUni(<managed>)
                // or
                // Marshal.StringToCoTaskMemAnsi(<managed>)
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        InteropServicesMarshalType,
                        IdentifierName(methodName)),
                    ArgumentList(
                        SingletonSeparatedList<ArgumentSyntax>(
                            Argument(IdentifierName(managedIdentifier)))));
            }

            public static ExpressionSyntax FreeExpression(string nativeIdentifier)
            {
                // Marshal.FreeCoTaskMem((IntPtr)<nativeIdentifier>)
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        InteropServicesMarshalType,
                        IdentifierName(nameof(System.Runtime.InteropServices.Marshal.FreeCoTaskMem))),
                    ArgumentList(SingletonSeparatedList(
                        Argument(
                            CastExpression(
                                SystemIntPtrType,
                                IdentifierName(nativeIdentifier))))));
            }
        }
    }
}