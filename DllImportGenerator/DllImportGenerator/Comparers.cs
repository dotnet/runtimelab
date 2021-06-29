using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.Interop
{

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

    internal class GeneratedSyntaxComparer : IEqualityComparer<(MemberDeclarationSyntax, ImmutableArray<Diagnostic>)>
    {
        private static readonly IEqualityComparer<ImmutableArray<Diagnostic>> diagnosticComparer = new ImmutableArraySequenceEqualComparer<Diagnostic>(EqualityComparer<Diagnostic>.Default);
        public bool Equals((MemberDeclarationSyntax, ImmutableArray<Diagnostic>) x, (MemberDeclarationSyntax, ImmutableArray<Diagnostic>) y)
        {
            return x.Item1.IsEquivalentTo(y.Item1)
            && diagnosticComparer.Equals(x.Item2, y.Item2);
        }

        public int GetHashCode((MemberDeclarationSyntax, ImmutableArray<Diagnostic>) obj)
        {
            return (obj.Item1.ToFullString(), diagnosticComparer.GetHashCode(obj.Item2)).GetHashCode();
        }
    }

    internal class SyntaxEquivalentComparer : IEqualityComparer<SyntaxNode>
    {
        private static readonly IEqualityComparer<ImmutableArray<Diagnostic>> diagnosticComparer = new ImmutableArraySequenceEqualComparer<Diagnostic>(EqualityComparer<Diagnostic>.Default);
        public bool Equals(SyntaxNode x, SyntaxNode y)
        {
            return x.IsEquivalentTo(y);
        }

        public int GetHashCode(SyntaxNode obj)
        {
            return obj.ToFullString().GetHashCode();
        }
    }


    internal class GeneratedSourceComparer : IEqualityComparer<(string, ImmutableArray<Diagnostic>)>
    {
        private static readonly IEqualityComparer<ImmutableArray<Diagnostic>> diagnosticComparer = new ImmutableArraySequenceEqualComparer<Diagnostic>(EqualityComparer<Diagnostic>.Default);

        public bool Equals((string, ImmutableArray<Diagnostic>) x, (string, ImmutableArray<Diagnostic>) y)
        {
            return x.Item1 == y.Item1
            && diagnosticComparer.Equals(x.Item2, y.Item2);
        }

        public int GetHashCode((string, ImmutableArray<Diagnostic>) obj)
        {
            return (obj.Item1, diagnosticComparer.GetHashCode(obj.Item2)).GetHashCode();
        }
    }
}
