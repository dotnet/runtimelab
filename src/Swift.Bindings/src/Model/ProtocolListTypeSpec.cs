// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration;

public class ProtocolListTypeSpec : TypeSpec
{

    class SpecComparer : IComparer<TypeSpec>
    {
        public int Compare(TypeSpec? x, TypeSpec? y)
        {
            if (x is null)
                throw new ArgumentNullException(nameof(x));
            if (y is null)
                throw new ArgumentNullException(nameof(y));

            return StringComparer.Ordinal.Compare(x.ToString(), y.ToString());
        }
    }

    class SpecEqComparer : IEqualityComparer<TypeSpec>
    {
        bool partialNameMatch;
        public SpecEqComparer(bool partialNameMatch)
        {
            this.partialNameMatch = partialNameMatch;
        }

        public bool Equals(TypeSpec? x, TypeSpec? y)
        {
            if (x is null)
                throw new ArgumentNullException(nameof(x));
            if (y is null)
                throw new ArgumentNullException(nameof(y));

            if (partialNameMatch)
                return x.EqualsPartialMatch(y);
            return x.Equals(y);
        }

        public int GetHashCode(TypeSpec obj)
        {
            throw new NotImplementedException();
        }
    }

    public ProtocolListTypeSpec()
        : base(TypeSpecKind.ProtocolList)
    {
        Protocols = new SortedList<NamedTypeSpec, bool>(new SpecComparer());
    }

    public ProtocolListTypeSpec(IEnumerable<NamedTypeSpec> protos)
        : this()
    {
        foreach (var proto in protos)
            Protocols.Add(proto, false);
    }

    public SortedList<NamedTypeSpec, bool> Protocols { get; private set; }

    protected override bool LLEquals(TypeSpec? other, bool partialNameMatch)
    {
        var otherProtos = other as ProtocolListTypeSpec;
        if (otherProtos == null)
            return false;
        if (otherProtos.Protocols.Count != Protocols.Count)
            return false;
        var eqComparer = new SpecEqComparer(partialNameMatch);

        return Protocols.Keys.SequenceEqual(otherProtos.Protocols.Keys, eqComparer);
    }

    protected override string LLToString(bool useFullName)
    {
        return string.Join(" & ", Protocols.Keys);
    }

    public override bool HasDynamicSelf => false;
}