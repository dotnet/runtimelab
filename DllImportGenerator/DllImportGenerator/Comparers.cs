using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Interop
{
    internal static class Comparers
    {
        public static IEqualityComparer<ImmutableArray<(string, ImmutableArray<Diagnostic>)>> GeneratedSourceSet = new ImmutableArraySequenceEqualComparer<(string, ImmutableArray<Diagnostic>)>(new CustomValueTupleElementComparer<string, ImmutableArray<Diagnostic>>(EqualityComparer<string>.Default, new ImmutableArraySequenceEqualComparer<Diagnostic>(EqualityComparer<Diagnostic>.Default)));
        public static IEqualityComparer<(string, ImmutableArray<Diagnostic>)> GeneratedSource = new CustomValueTupleElementComparer<string, ImmutableArray<Diagnostic>>(EqualityComparer<string>.Default, new ImmutableArraySequenceEqualComparer<Diagnostic>(EqualityComparer<Diagnostic>.Default));
        public static IEqualityComparer<(MemberDeclarationSyntax, ImmutableArray<Diagnostic>)> GeneratedSyntax = new CustomValueTupleElementComparer<MemberDeclarationSyntax, ImmutableArray<Diagnostic>>(new SyntaxEquivalentComparer(), new ImmutableArraySequenceEqualComparer<Diagnostic>(EqualityComparer<Diagnostic>.Default));

        public static IEqualityComparer<(MethodDeclarationSyntax, DllImportGenerator.IncrementalStubGenerationContext)> CalculatedContextWithSyntax = new CustomValueTupleElementComparer<MethodDeclarationSyntax, DllImportGenerator.IncrementalStubGenerationContext>(new SyntaxEquivalentComparer(), EqualityComparer<DllImportGenerator.IncrementalStubGenerationContext>.Default);
    }

    internal class ImmutableArraySequenceEqualComparer<T> : IEqualityComparer<ImmutableArray<T>>
    {
        private readonly IEqualityComparer<T> elementComparer;

        public ImmutableArraySequenceEqualComparer(IEqualityComparer<T> elementComparer)
        {
            this.elementComparer = elementComparer;
        }

        public bool Equals(ImmutableArray<T> x, ImmutableArray<T> y)
        {
            return x.SequenceEqual(y, elementComparer);
        }

        public int GetHashCode(ImmutableArray<T> obj)
        {
            return obj.Aggregate(0, (hash, elem) => (hash, elementComparer.GetHashCode(elem)).GetHashCode());
        }
    }

    internal class SyntaxEquivalentComparer : IEqualityComparer<SyntaxNode>
    {
        public bool Equals(SyntaxNode x, SyntaxNode y)
        {
            return x.IsEquivalentTo(y);
        }

        public int GetHashCode(SyntaxNode obj)
        {
            return obj.ToFullString().GetHashCode();
        }
    }

    internal class CustomValueTupleElementComparer<T, U> : IEqualityComparer<(T, U)>
    {
        private readonly IEqualityComparer<T> item1Comparer;
        private readonly IEqualityComparer<U> item2Comparer;

        public CustomValueTupleElementComparer(IEqualityComparer<T> item1Comparer, IEqualityComparer<U> item2Comparer)
        {
            this.item1Comparer = item1Comparer;
            this.item2Comparer = item2Comparer;
        }

        public bool Equals((T, U) x, (T, U) y)
        {
            return item1Comparer.Equals(x.Item1, y.Item1) && item2Comparer.Equals(x.Item2, y.Item2);
        }

        public int GetHashCode((T, U) obj)
        {
            return (item1Comparer.GetHashCode(obj.Item1), item2Comparer.GetHashCode(obj.Item2)).GetHashCode();
        }
    }
}
